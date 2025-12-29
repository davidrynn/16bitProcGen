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
        /// Marks intersecting chunk entities with the density rebuild tag so the sampling system runs only where needed.
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
            var size = new float3(resolution.x * grid.VoxelSize, resolution.y * grid.VoxelSize, resolution.z * grid.VoxelSize);

            min = bounds.WorldOrigin;
            max = bounds.WorldOrigin + size;
            return true;
        }

        private static bool SphereIntersectsAabb(float3 center, float radiusSq, float3 min, float3 max)
        {
            var clamped = math.clamp(center, min, max);
            var delta = center - clamped;
            return math.lengthsq(delta) <= radiusSq;
        }
    }
}
