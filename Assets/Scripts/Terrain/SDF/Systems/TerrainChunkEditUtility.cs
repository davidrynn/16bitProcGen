using Unity.Collections;
using Unity.Entities;

namespace DOTS.Terrain.SDF
{
    /// <summary>
    /// Helper utilities for terrain chunk editing workflows.
    /// </summary>
    public static class TerrainChunkEditUtility
    {
        /// <summary>
        /// Ensures every provided chunk entity has the density rebuild tag so the sampling system runs.
        /// </summary>
        public static void MarkChunksDirty(EntityManager entityManager, NativeArray<Entity> chunkEntities)
        {
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var chunk = chunkEntities[i];
                if (!entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunk))
                {
                    entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunk);
                }
            }
        }
    }
}
