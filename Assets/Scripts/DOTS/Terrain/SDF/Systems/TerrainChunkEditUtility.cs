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
        /// Marks all chunk entities whose density sampling region intersects the edit sphere with
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
            if (!chunkEntities.IsCreated || chunkEntities.Length == 0)
            {
                return;
            }

            var shouldFilter = editRadius > 0f;
            var radiusSq = editRadius * editRadius;

            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var chunk = chunkEntities[i];

                if (shouldFilter && TryGetChunkAabb(entityManager, chunk, out var min, out var max))
                {
                    if (!SphereIntersectsAabb(editCenter, radiusSq, min, max))
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
    }
}
