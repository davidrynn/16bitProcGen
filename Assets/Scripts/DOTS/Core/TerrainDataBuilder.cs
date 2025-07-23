using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace DOTS.Terrain
{
    /// <summary>
    /// Factory class for creating TerrainData and related structures
    /// Provides efficient methods for initializing terrain data with proper memory management
    /// </summary>
    public static class TerrainDataBuilder
    {
        /// <summary>
        /// Creates a complete TerrainData structure with all necessary components
        /// Note: ComputeBuffer creation moved to TerrainComputeBufferManager
        /// </summary>
        /// <param name="chunkPosition">2D position of the terrain chunk</param>
        /// <param name="resolution">Resolution of the terrain grid</param>
        /// <param name="worldScale">Scale of world units</param>
        /// <returns>Fully initialized TerrainData</returns>
        public static TerrainData CreateTerrainData(int2 chunkPosition, int resolution, float worldScale)
        {
            // Calculate world position from chunk position
            float3 worldPosition = new float3(
                chunkPosition.x * worldScale,
                0f, // Y position will be set based on average height after generation
                chunkPosition.y * worldScale
            );
            
            var terrainData = new TerrainData
            {
                chunkPosition = chunkPosition,
                resolution = resolution,
                worldScale = worldScale,
                heightData = CreateHeightData(resolution),
                modifications = CreateModificationData(),
                needsGeneration = true,
                needsModification = false,
                
                // NEW: Initialize transform fields
                worldPosition = worldPosition,
                rotation = quaternion.identity, // Default rotation
                scale = new float3(worldScale, 1f, worldScale) // Scale based on world scale
            };

            return terrainData;
        }

        /// <summary>
        /// Creates height data as a blob asset for efficient memory usage
        /// </summary>
        /// <param name="resolution">Resolution of the terrain grid</param>
        /// <returns>BlobAssetReference to TerrainHeightData</returns>
        public static BlobAssetReference<TerrainHeightData> CreateHeightData(int resolution)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainHeightData>();
            
            // Initialize height array
            var heights = builder.Allocate(ref root.heights, resolution * resolution);
            for (int i = 0; i < heights.Length; i++)
            {
                heights[i] = 0f; // Default height
            }
            
            // Initialize terrain type array
            var terrainTypes = builder.Allocate(ref root.terrainTypes, resolution * resolution);
            for (int i = 0; i < terrainTypes.Length; i++)
            {
                terrainTypes[i] = TerrainType.Grass; // Default terrain type
            }
            
            // Set size
            root.size = new int2(resolution, resolution);
            
            var result = builder.CreateBlobAssetReference<TerrainHeightData>(Allocator.Persistent);
            builder.Dispose();
            
            return result;
        }

        /// <summary>
        /// Creates modification data as a blob asset
        /// </summary>
        /// <returns>BlobAssetReference to TerrainModificationData</returns>
        public static BlobAssetReference<TerrainModificationData> CreateModificationData()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainModificationData>();
            
            // Initialize empty modifications array
            builder.Allocate(ref root.modifications, 0);
            root.lastModificationTime = 0f;
            
            var result = builder.CreateBlobAssetReference<TerrainModificationData>(Allocator.Persistent);
            builder.Dispose();
            
            return result;
        }

        /// <summary>
        /// Updates height data from a native array
        /// </summary>
        /// <param name="heights">Native array of height values</param>
        /// <param name="resolution">Resolution of the terrain grid</param>
        /// <returns>Updated BlobAssetReference to TerrainHeightData</returns>
        public static BlobAssetReference<TerrainHeightData> UpdateHeightData(NativeArray<float> heights, int resolution)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainHeightData>();
            
            // Fix 1: Use manual copy instead of CopyTo for BlobBuilderArray
            var heightArray = builder.Allocate(ref root.heights, heights.Length);
            for (int i = 0; i < heights.Length; i++)
            {
                heightArray[i] = heights[i]; // Manual copy instead of CopyTo
            }
            
            // Initialize terrain types based on heights
            var terrainTypes = builder.Allocate(ref root.terrainTypes, heights.Length);
            for (int i = 0; i < terrainTypes.Length; i++)
            {
                terrainTypes[i] = DetermineTerrainType(heights[i]);
            }
            
            // Set size
            root.size = new int2(resolution, resolution);
            
            var result = builder.CreateBlobAssetReference<TerrainHeightData>(Allocator.Persistent);
            builder.Dispose();
            
            return result;
        }

        /// <summary>
        /// Determines terrain type based on height value
        /// </summary>
        /// <param name="height">Height value</param>
        /// <returns>Appropriate TerrainType</returns>
        private static TerrainType DetermineTerrainType(float height)
        {
            if (height < 0.2f) return TerrainType.Water;
            if (height < 0.4f) return TerrainType.Sand;
            if (height < 0.6f) return TerrainType.Grass;
            if (height < 0.8f) return TerrainType.Rock;
            return TerrainType.Snow;
        }

        /// <summary>
        /// Adds a modification to existing modification data
        /// </summary>
        /// <param name="existingModifications">Existing modification data</param>
        /// <param name="modification">New modification to add</param>
        /// <returns>Updated BlobAssetReference to TerrainModificationData</returns>
        public static BlobAssetReference<TerrainModificationData> AddModification(
            BlobAssetReference<TerrainModificationData> existingModifications, 
            TerrainModification modification)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainModificationData>();
            
            // Fix 2: Use ref to access blob storage properly
            ref var existingArray = ref existingModifications.Value.modifications;
            int newSize = existingArray.Length + 1;
            
            // Allocate new array
            var modifications = builder.Allocate(ref root.modifications, newSize);
            
            // Copy existing modifications
            for (int i = 0; i < existingArray.Length; i++)
            {
                modifications[i] = existingArray[i];
            }
            
            // Add new modification
            modifications[existingArray.Length] = modification;
            
            // Update last modification time
            root.lastModificationTime = modification.modificationTime;
            
            var result = builder.CreateBlobAssetReference<TerrainModificationData>(Allocator.Persistent);
            builder.Dispose();
            
            return result;
        }

        /// <summary>
        /// Cleans up old modifications based on time threshold
        /// </summary>
        /// <param name="existingModifications">Existing modification data</param>
        /// <param name="currentTime">Current game time</param>
        /// <param name="timeThreshold">Time threshold for keeping modifications</param>
        /// <returns>Updated BlobAssetReference to TerrainModificationData</returns>
        public static BlobAssetReference<TerrainModificationData> CleanupOldModifications(
            BlobAssetReference<TerrainModificationData> existingModifications,
            float currentTime,
            float timeThreshold)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainModificationData>();
            
            // Fix 3: Use ref to access blob storage properly
            ref var existingArray = ref existingModifications.Value.modifications;
            int validCount = 0;
            for (int i = 0; i < existingArray.Length; i++)
            {
                if (currentTime - existingArray[i].modificationTime < timeThreshold)
                {
                    validCount++;
                }
            }
            
            // Allocate new array
            var modifications = builder.Allocate(ref root.modifications, validCount);
            
            // Copy valid modifications
            int index = 0;
            for (int i = 0; i < existingArray.Length; i++)
            {
                if (currentTime - existingArray[i].modificationTime < timeThreshold)
                {
                    modifications[index] = existingArray[i];
                    index++;
                }
            }
            
            // Update last modification time
            root.lastModificationTime = validCount > 0 ? modifications[validCount - 1].modificationTime : 0f;
            
            var result = builder.CreateBlobAssetReference<TerrainModificationData>(Allocator.Persistent);
            builder.Dispose();
            
            return result;
        }
    }
}