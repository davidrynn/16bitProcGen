using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    /// <summary>
    /// Helper utilities for terrain chunk editing workflows.
    /// </summary>
    public static class TerrainChunkEditUtility
    {
        /// <summary>
        /// Marks all chunk entities whose density sampling region intersects the edit volume with
        /// <see cref="TerrainChunkNeedsDensityRebuild"/> so <c>TerrainChunkDensitySamplingSystem</c>
        /// only re-samples where the SDF field has actually changed.
        ///
        /// Critically, this must mark ADJACENT chunks dirty too — not just the chunk the edit
        /// sphere is centered in. Adjacent chunks share a one-voxel-wide boundary density row
        /// with their neighbor (Surface Nets needs the +1 overlap to stitch seams). If only the
        /// directly-hit chunk rebuilds, the neighbor's stored density at the shared boundary
        /// diverges from the rebuilt chunk's value, producing T-junctions and missing interior
        /// walls at the chunk edge. The AABB used for intersection is therefore expanded to the
        /// full density sampling extent, not just the mesh extent (see <see cref="TryGetChunkAabb"/>).
        /// </summary>
        public static void MarkChunksDirty(EntityManager entityManager, NativeArray<Entity> chunkEntities, float3 editCenter, float editRadius)
        {
            MarkChunksDirty(entityManager, chunkEntities, SDFEdit.Create(editCenter, editRadius, SDFEditOperation.Add));
        }

        /// <summary>
        /// Marks all chunks whose density region intersects the edit volume represented by <paramref name="edit"/>.
        /// </summary>
        public static void MarkChunksDirty(EntityManager entityManager, NativeArray<Entity> chunkEntities, in SDFEdit edit)
        {
            if (!chunkEntities.IsCreated || chunkEntities.Length == 0)
            {
                return;
            }

            var hasValidFilter = TryCreateEditFilter(in edit, out var filter);

            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var chunk = chunkEntities[i];

                if (hasValidFilter && TryGetChunkAabb(entityManager, chunk, out var min, out var max))
                {
                    if (!EditIntersectsAabb(in filter, min, max))
                    {
                        continue;
                    }
                }

                if (!entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunk))
                {
                    entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunk);
                }
            }
        }

        public static float ComputeChunkStride(in TerrainChunkGridInfo grid)
        {
            var voxelSize = math.max(1e-5f, grid.VoxelSize);
            var strideCells = math.max(0, grid.Resolution.x - 1);
            return strideCells * voxelSize;
        }

        public static float ComputeQuantizedCellSize(in TerrainChunkGridInfo grid, float editCellFraction)
        {
            var voxelSize = math.max(1e-5f, grid.VoxelSize);
            var chunkStride = ComputeChunkStride(in grid);
            if (chunkStride <= 0f)
            {
                return voxelSize;
            }

            var clampedFraction = math.clamp(editCellFraction, 0.25f, 1f);
            var rawCellSize = chunkStride * clampedFraction;
            var quantizedSteps = math.max(1f, math.round(rawCellSize / voxelSize));
            return quantizedSteps * voxelSize;
        }

        public static float3 SnapToGlobalLattice(float3 worldPoint, float3 anchor, float cellSize)
        {
            var safeCellSize = math.max(1e-5f, cellSize);
            return anchor + math.round((worldPoint - anchor) / safeCellSize) * safeCellSize;
        }

        public static float3 SnapToChunkLocalLattice(float3 worldPoint, float3 chunkOrigin, float cellSize)
        {
            var safeCellSize = math.max(1e-5f, cellSize);
            var localPoint = worldPoint - chunkOrigin;
            var cell = math.floor(localPoint / safeCellSize);
            return chunkOrigin + (cell + 0.5f) * safeCellSize;
        }

        /// <summary>
        /// Finds the chunk that contains <paramref name="worldPoint"/> using half-open bounds.
        /// </summary>
        public static bool TryFindOwningChunk(
            EntityManager entityManager,
            NativeArray<Entity> chunkEntities,
            float3 worldPoint,
            out Entity chunkEntity,
            out TerrainChunk chunk,
            out TerrainChunkBounds bounds,
            out TerrainChunkGridInfo grid)
        {
            chunkEntity = Entity.Null;
            chunk = default;
            bounds = default;
            grid = default;

            if (!chunkEntities.IsCreated || chunkEntities.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var candidate = chunkEntities[i];
                if (!entityManager.HasComponent<TerrainChunk>(candidate) ||
                    !entityManager.HasComponent<TerrainChunkBounds>(candidate) ||
                    !entityManager.HasComponent<TerrainChunkGridInfo>(candidate))
                {
                    continue;
                }

                var candidateChunk = entityManager.GetComponentData<TerrainChunk>(candidate);
                var candidateBounds = entityManager.GetComponentData<TerrainChunkBounds>(candidate);
                var candidateGrid = entityManager.GetComponentData<TerrainChunkGridInfo>(candidate);
                if (!TryGetChunkLookupAabb(candidateBounds, candidateGrid, out var min, out var max))
                {
                    continue;
                }

                if (!PointInHalfOpenAabb(worldPoint, min, max))
                {
                    continue;
                }

                chunkEntity = candidate;
                chunk = candidateChunk;
                bounds = candidateBounds;
                grid = candidateGrid;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the world-space AABB that covers the chunk's full density sampling extent.
        ///
        /// WHY THIS IS NOT THE MESH EXTENT:
        /// <c>TerrainChunkDensitySamplingSystem</c> samples <c>resolution + 1</c> points on every
        /// axis (i.e. <c>resolution * voxelSize</c> world units) so that Surface Nets can read the
        /// one-voxel overlap needed to stitch seams with the adjacent chunk. The mesh itself only
        /// spans <c>(resolution - 1) * voxelSize</c>. If we used the mesh extent here, an edit
        /// sphere that touches the shared boundary row (the +1 samples) would not mark the
        /// neighboring chunk dirty. That neighbor's stored density at the boundary would then be
        /// stale — a different value than the rebuilt chunk computed for the same world position —
        /// causing surface positions to diverge at the seam and leaving holes in the mesh (BUG-007).
        /// Using <c>resolution * voxelSize</c> closes that gap: any edit that can affect a boundary
        /// sample will mark both the owning chunk AND its neighbor dirty.
        /// </summary>
        private static bool TryGetChunkAabb(EntityManager entityManager, Entity chunk, out float3 min, out float3 max)
        {
            min = float3.zero;
            max = float3.zero;

            if (!entityManager.HasComponent<TerrainChunkBounds>(chunk) || !entityManager.HasComponent<TerrainChunkGridInfo>(chunk))
            {
                return false;
            }

            var bounds = entityManager.GetComponentData<TerrainChunkBounds>(chunk);
            var grid = entityManager.GetComponentData<TerrainChunkGridInfo>(chunk);
            var resolution = grid.Resolution;

            // resolution * voxelSize — matches the density sampling extent (resolution + 1 samples,
            // so resolution cells, so resolution * voxelSize world units). One voxel wider than the
            // mesh on every axis, which is exactly the shared boundary row with the adjacent chunk.
            var size = new float3(
                math.max(0, resolution.x) * grid.VoxelSize,
                math.max(0, resolution.y) * grid.VoxelSize,
                math.max(0, resolution.z) * grid.VoxelSize);

            min = bounds.WorldOrigin;
            max = bounds.WorldOrigin + size;
            return true;
        }

        private static bool TryGetChunkLookupAabb(in TerrainChunkBounds bounds, in TerrainChunkGridInfo grid, out float3 min, out float3 max)
        {
            min = bounds.WorldOrigin;
            max = bounds.WorldOrigin;

            var voxelSize = math.max(1e-5f, grid.VoxelSize);
            var meshExtent = new float3(
                math.max(0, grid.Resolution.x - 1) * voxelSize,
                math.max(0, grid.Resolution.y - 1) * voxelSize,
                math.max(0, grid.Resolution.z - 1) * voxelSize);

            if (meshExtent.x <= 0f || meshExtent.y <= 0f || meshExtent.z <= 0f)
            {
                return false;
            }

            max = min + meshExtent;
            return true;
        }

        private static bool PointInHalfOpenAabb(float3 point, float3 min, float3 max)
        {
            return point.x >= min.x && point.x < max.x &&
                   point.y >= min.y && point.y < max.y &&
                   point.z >= min.z && point.z < max.z;
        }

        /// <summary>
        /// Returns true if the sphere (defined by squared radius) overlaps the axis-aligned box.
        /// Finds the closest point on the box to the sphere center; the sphere overlaps if that
        /// distance is within the radius.
        /// </summary>
        private static bool SphereIntersectsAabb(float3 center, float radiusSq, float3 min, float3 max)
        {
            var clamped = math.clamp(center, min, max);
            var delta = center - clamped;
            return math.lengthsq(delta) <= radiusSq;
        }

        private static bool BoxIntersectsAabb(float3 center, float3 halfExtents, float3 min, float3 max)
        {
            var boxMin = center - halfExtents;
            var boxMax = center + halfExtents;
            return boxMin.x <= max.x && boxMax.x >= min.x &&
                   boxMin.y <= max.y && boxMax.y >= min.y &&
                   boxMin.z <= max.z && boxMax.z >= min.z;
        }

        private static bool EditIntersectsAabb(in EditFilter filter, float3 min, float3 max)
        {
            if (filter.Shape == SDFEditShape.Box)
            {
                return BoxIntersectsAabb(filter.Center, filter.HalfExtents, min, max);
            }

            return SphereIntersectsAabb(filter.Center, filter.RadiusSq, min, max);
        }

        private static bool TryCreateEditFilter(in SDFEdit edit, out EditFilter filter)
        {
            filter = default;
            if (!IsFinite(edit.Center))
            {
                return false;
            }

            if (edit.Shape == SDFEditShape.Box)
            {
                var halfExtents = math.max(float3.zero, edit.HalfExtents);
                if (!IsFinite(halfExtents) || math.cmax(halfExtents) <= 0f)
                {
                    return false;
                }

                filter = new EditFilter
                {
                    Shape = SDFEditShape.Box,
                    Center = edit.Center,
                    HalfExtents = halfExtents
                };
                return true;
            }

            if (!math.isfinite(edit.Radius) || edit.Radius <= 0f)
            {
                return false;
            }

            filter = new EditFilter
            {
                Shape = SDFEditShape.Sphere,
                Center = edit.Center,
                RadiusSq = edit.Radius * edit.Radius
            };
            return true;
        }

        private static bool IsFinite(float3 value)
        {
            return math.all(math.isfinite(value));
        }

        private struct EditFilter
        {
            public SDFEditShape Shape;
            public float3 Center;
            public float RadiusSq;
            public float3 HalfExtents;
        }
    }
}
