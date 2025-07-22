using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

public partial class TerrainModificationSystem : SystemBase
{
    private ComputeShaderManager computeManager;
    private TerrainComputeBufferManager bufferManager;
    
    protected override void OnCreate()
    {
        // Get references to required managers
        computeManager = ComputeShaderManager.Instance;
        bufferManager = Object.FindFirstObjectByType<TerrainComputeBufferManager>();
        
        if (computeManager == null)
        {
            Debug.LogError("[TerrainModificationSystem] ComputeShaderManager not found!");
        }
        
        if (bufferManager == null)
        {
            Debug.LogWarning("[TerrainModificationSystem] TerrainComputeBufferManager not found - terrain modifications will be logged only");
        }
    }
    
    protected override void OnUpdate()
    {
        // Retry finding buffer manager if it wasn't found during OnCreate
        if (bufferManager == null)
        {
            bufferManager = Object.FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager != null)
            {
                Debug.Log("[TerrainModificationSystem] Found TerrainComputeBufferManager on retry");
            }
        }
        
        // Use EntityCommandBuffer for structural changes
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        Entities
            .WithAll<PlayerModificationComponent>()
            .ForEach((Entity entity, in PlayerModificationComponent modification) =>
            {
                // Log the modification request
                Debug.Log($"Glob Removal Request: Pos={modification.position}, Radius={modification.radius}, Type={modification.removalType}, Underground={modification.allowUnderground}");
                
                // Apply terrain modification if managers are available
                if (computeManager != null && bufferManager != null)
                {
                    ApplyTerrainGlobRemoval(modification);
                }
                else
                {
                    Debug.LogWarning($"[TerrainModificationSystem] Skipping modification - ComputeManager: {(computeManager != null ? "✓" : "✗")}, BufferManager: {(bufferManager != null ? "✓" : "✗")}");
                }
                
                // Queue entity destruction
                ecb.DestroyEntity(entity);
            }).WithoutBurst().Run();
        
        // Play back the command buffer
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    private void ApplyTerrainGlobRemoval(PlayerModificationComponent modification)
    {
        // Get the terrain glob removal compute shader
        var globShader = computeManager.GetComputeShader("TerrainGlobRemoval");
        if (globShader == null)
        {
            Debug.LogError("[TerrainModificationSystem] TerrainGlobRemoval compute shader not found!");
            return;
        }
        
        // Find affected terrain chunks (simplified - you'll need to implement chunk detection)
        // For now, we'll assume a single chunk at the modification position
        var terrainChunk = FindTerrainChunkAtPosition(modification.position);
        if (terrainChunk == Entity.Null)
        {
            Debug.LogWarning($"[TerrainModificationSystem] No terrain chunk found at position {modification.position}");
            return;
        }
        
        // Get terrain data
        var terrainData = EntityManager.GetComponentData<TerrainData>(terrainChunk);
        
        // Check if terrain has height data
        if (!terrainData.heightData.IsCreated)
        {
            Debug.LogWarning($"[TerrainModificationSystem] Terrain chunk at {terrainData.chunkPosition} has no height data");
            return;
        }
        
        // Get height data from blob asset
        ref var heightData = ref terrainData.heightData.Value;
        ref var heights = ref heightData.heights;
        ref var terrainTypes = ref heightData.terrainTypes;
        
        // Use buffer manager for height and terrain type buffers
        int bufferSize = terrainData.resolution * terrainData.resolution;
        var heightBuffer = bufferManager.GetHeightBuffer(terrainData.chunkPosition, terrainData.resolution);
        var terrainTypeBuffer = bufferManager.GetTerrainTypeBuffer(terrainData.chunkPosition, terrainData.resolution);
        var removedMaskBuffer = new ComputeBuffer(bufferSize, sizeof(float));
        
        // Copy height data to compute buffer
        var heightArray = new float[bufferSize];
        var terrainTypeArray = new int[bufferSize];
        
        for (int i = 0; i < bufferSize; i++)
        {
            heightArray[i] = heights[i];
            terrainTypeArray[i] = (int)terrainTypes[i];
        }
        
        heightBuffer.SetData(heightArray);
        terrainTypeBuffer.SetData(terrainTypeArray);
        
         // Set compute shader parameters
        globShader.SetBuffer(0, "heights", heightBuffer);
        globShader.SetBuffer(0, "terrainTypes", terrainTypeBuffer);
        globShader.SetBuffer(0, "removedMask", removedMaskBuffer);
        
        // Set glob removal parameters (using individual parameter names)
        globShader.SetVector("globCenter", new Vector4(modification.position.x, modification.position.y, modification.position.z, 0));
        globShader.SetFloat("globRadius", modification.radius);
        globShader.SetFloat("globStrength", modification.strength * modification.toolEfficiency);
        globShader.SetInt("globRemovalType", (int)modification.removalType);
        globShader.SetFloat("globMaxDepth", modification.maxDepth);
        globShader.SetBool("globAllowUnderground", modification.allowUnderground);
        
        // Set terrain chunk parameters (using individual parameter names)
        globShader.SetVector("chunkPosition", new Vector4(terrainData.chunkPosition.x, 0, terrainData.chunkPosition.y, 0));
        globShader.SetInt("chunkResolution", terrainData.resolution);
        globShader.SetFloat("chunkWorldScale", terrainData.worldScale);
        globShader.SetFloat("chunkTime", (float)SystemAPI.Time.ElapsedTime);
        
        // Dispatch the compute shader
        int threadGroups = (terrainData.resolution + 7) / 8;
        globShader.Dispatch(0, threadGroups, threadGroups, 1); // RemoveTerrainGlob kernel
        
        // Read back the modified data
        heightBuffer.GetData(heightArray);
        
        // Update the terrain data (this would need to be done through a proper update system)
        // For now, just log the modification
        Debug.Log($"[TerrainModificationSystem] Applied glob removal at {modification.position} with radius {modification.radius}");
        
        // Clean up only the removedMaskBuffer
        removedMaskBuffer.Release();
    }
    
    private Entity FindTerrainChunkAtPosition(float3 worldPosition)
    {
        // Simplified chunk finding - you'll need to implement proper chunk detection
        // This should find the terrain chunk that contains the given world position
        Entity foundChunk = Entity.Null;
        
        // Use a temporary list to store found entities
        var foundEntities = new NativeList<Entity>(Allocator.Temp);
        
        // Use a different approach to avoid ForEach lambda issues
        var query = GetEntityQuery(typeof(TerrainData));
        var entities = query.ToEntityArray(Allocator.Temp);
        
        foreach (var entity in entities)
        {
            var terrain = EntityManager.GetComponentData<TerrainData>(entity);
            
            // Simple bounds check - replace with proper chunk boundary detection
            var chunkWorldPos = terrain.chunkPosition;
            float chunkSize = terrain.resolution * terrain.worldScale;
            
            if (worldPosition.x >= chunkWorldPos.x && worldPosition.x < chunkWorldPos.x + chunkSize &&
                worldPosition.z >= chunkWorldPos.y && worldPosition.z < chunkWorldPos.y + chunkSize)
            {
                foundChunk = entity;
                break;
            }
        }
        
        entities.Dispose();
        return foundChunk;
    }
}