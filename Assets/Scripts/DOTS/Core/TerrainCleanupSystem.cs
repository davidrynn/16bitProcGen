using Unity.Entities;
using Unity.Collections;
using UnityEngine;

namespace DOTS.Terrain
{
    /// <summary>
    /// System that automatically cleans up BlobAssetReferences when terrain entities are destroyed
    /// Prevents memory leaks from undisposed blob assets
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainSystem))]
    public partial class TerrainCleanupSystem : SystemBase
    {
        private EntityQuery terrainQuery;
        private EntityQuery biomeQuery;
        
        protected override void OnCreate()
        {
            // Create queries for entities that might have blob assets
            terrainQuery = GetEntityQuery(typeof(TerrainData));
            biomeQuery = GetEntityQuery(typeof(BiomeComponent));
            
            DOTS.Terrain.Core.DebugSettings.LogTerrain("TerrainCleanupSystem: Initialized");
        }
        
        protected override void OnUpdate()
        {
            // This system doesn't need to run every frame
            // It's here to handle cleanup when entities are destroyed by other systems
        }
        
        protected override void OnDestroy()
        {
            // Clean up any remaining blob assets when the system is destroyed
            CleanupAllBlobAssets();
        }
        
        /// <summary>
        /// Cleans up all blob assets for terrain entities
        /// </summary>
        private void CleanupAllBlobAssets()
        {
            try
            {
                // Clean up terrain data blob assets
                var terrainEntities = terrainQuery.ToEntityArray(Allocator.Temp);
                foreach (var entity in terrainEntities)
                {
                    if (EntityManager.Exists(entity) && EntityManager.HasComponent<TerrainData>(entity))
                    {
                        var terrainData = EntityManager.GetComponentData<TerrainData>(entity);
                        CleanupTerrainData(terrainData);
                    }
                }
                terrainEntities.Dispose();
                
                // Clean up biome component blob assets
                var biomeEntities = biomeQuery.ToEntityArray(Allocator.Temp);
                foreach (var entity in biomeEntities)
                {
                    if (EntityManager.Exists(entity) && EntityManager.HasComponent<BiomeComponent>(entity))
                    {
                        var biomeComponent = EntityManager.GetComponentData<BiomeComponent>(entity);
                        CleanupBiomeComponent(biomeComponent);
                    }
                }
                biomeEntities.Dispose();
                
                DOTS.Terrain.Core.DebugSettings.LogTerrain("TerrainCleanupSystem: Cleaned up all blob assets");
            }
            catch (System.Exception e)
            {
                DOTS.Terrain.Core.DebugSettings.LogWarning($"TerrainCleanupSystem: Error during cleanup: {e.Message}");
            }
        }
        
        /// <summary>
        /// Cleans up terrain data blob assets
        /// </summary>
        private void CleanupTerrainData(TerrainData terrainData)
        {
            if (terrainData.heightData.IsCreated)
            {
                terrainData.heightData.Dispose();
            }
            
            if (terrainData.modifications.IsCreated)
            {
                terrainData.modifications.Dispose();
            }
        }
        
        /// <summary>
        /// Cleans up biome component blob assets
        /// </summary>
        private void CleanupBiomeComponent(BiomeComponent biomeComponent)
        {
            if (biomeComponent.terrainData.IsCreated)
            {
                biomeComponent.terrainData.Dispose();
            }
        }
    }
} 