using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;
using DOTS.Terrain;
using DOTS.Terrain.Modification;
using TerrainData = DOTS.Terrain.TerrainData;

public partial class TerrainModificationSystem : SystemBase
{
    private ComputeShaderManager computeManager;
    private TerrainComputeBufferManager bufferManager;
    private TerrainGlobPhysicsSystem globPhysicsSystem;
    
    // Queue for glob creations to avoid structural change issues
    private List<GlobCreationData> queuedGlobCreations = new List<GlobCreationData>();
    
    // Data structure for queued glob creation
    private struct GlobCreationData
    {
        public float3 position;
        public float radius;
        public GlobRemovalType removalType;
        public TerrainType terrainType;
    }
    
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
            // Silently skip - this is expected when testing WFC without terrain systems
        }
        
        // Get reference to glob physics system
        globPhysicsSystem = World.GetOrCreateSystemManaged<TerrainGlobPhysicsSystem>();
        
        if (globPhysicsSystem == null)
        {
            Debug.LogWarning("[TerrainModificationSystem] TerrainGlobPhysicsSystem not found - globs will not be created");
        }
    }
    
    protected override void OnUpdate()
    {
        // Retry finding buffer manager if it was null in OnCreate
        if (bufferManager == null)
        {
            bufferManager = Object.FindFirstObjectByType<TerrainComputeBufferManager>();
        }
        
        // Process modification requests
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        
        Entities
            .WithAll<PlayerModificationComponent>()
            .ForEach((Entity entity, in PlayerModificationComponent modification) =>
            {
                if (bufferManager != null && computeManager != null)
                {
                    ApplyTerrainGlobRemoval(modification);
                    
                    // Queue glob creation for after the ForEach loop
                    if (globPhysicsSystem != null)
                    {
                        // Store modification data for later processing
                        var globCreationData = new GlobCreationData
                        {
                            position = modification.position,
                            radius = modification.radius,
                            removalType = modification.removalType,
                            terrainType = DetermineTerrainTypeAtPosition(modification.position)
                        };
                        
                        // We'll process this after the ForEach loop
                        QueueGlobCreation(globCreationData);
                    }
                }
                else
                {
                    Debug.Log($"[TerrainModificationSystem] Modification Request: Pos={modification.position}, Radius={modification.radius}, Strength={modification.strength}, Res={modification.resolution}");
                }
                
                // Destroy the modification request entity
                ecb.DestroyEntity(entity);
            }).WithoutBurst().Run();
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
        
        // Process queued glob creations after structural changes are complete
        ProcessQueuedGlobCreations();
    }
    
    private void ApplyTerrainGlobRemoval(PlayerModificationComponent modification)
    {
        var globShader = computeManager.GetComputeShader("TerrainGlobRemoval");
        if (globShader == null) { Debug.LogError("[TerrainModificationSystem] TerrainGlobRemoval compute shader not found!"); return; }
        var terrainChunk = FindTerrainChunkAtPosition(modification.position);
        if (terrainChunk == Entity.Null) { Debug.LogWarning($"[TerrainModificationSystem] No terrain chunk found at position {modification.position}"); return; }
        var terrainData = EntityManager.GetComponentData<TerrainData>(terrainChunk);
        if (!terrainData.heightData.IsCreated) { Debug.LogWarning($"[TerrainModificationSystem] Terrain chunk at {terrainData.chunkPosition} has no height data"); return; }
        
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
        
        // Set needsMeshUpdate flag to true and update the entity
        terrainData.needsMeshUpdate = true;
        EntityManager.SetComponentData(terrainChunk, terrainData);
        
        // Update the terrain data (this would need to be done through a proper update system)
        // For now, just log the modification
        Debug.Log($"[TerrainModificationSystem] Applied glob removal at {modification.position} with radius {modification.radius}");
        
        // Clean up only the removedMaskBuffer
        removedMaskBuffer.Release();
    }
    

    
    /// <summary>
    /// Determines the terrain type at a given world position
    /// </summary>
    private TerrainType DetermineTerrainTypeAtPosition(float3 worldPosition)
    {
        // Find the terrain chunk at this position
        var terrainChunk = FindTerrainChunkAtPosition(worldPosition);
        if (terrainChunk == Entity.Null)
        {
            return TerrainType.Grass; // Default fallback
        }
        
        var terrainData = EntityManager.GetComponentData<TerrainData>(terrainChunk);
        if (!terrainData.heightData.IsCreated)
        {
            return TerrainType.Grass; // Default fallback
        }
        
        // Get the terrain type from the height data
        ref var heightData = ref terrainData.heightData.Value;
        ref var terrainTypes = ref heightData.terrainTypes;
        
        // Calculate the index in the terrain data
        var chunkWorldPos = terrainData.chunkPosition;
        var localPos = worldPosition - new float3(chunkWorldPos.x * terrainData.worldScale, 0, chunkWorldPos.y * terrainData.worldScale);
        
        int x = (int)(localPos.x / terrainData.worldScale * (terrainData.resolution - 1));
        int z = (int)(localPos.z / terrainData.worldScale * (terrainData.resolution - 1));
        
        // Clamp to valid range
        x = math.clamp(x, 0, terrainData.resolution - 1);
        z = math.clamp(z, 0, terrainData.resolution - 1);
        
        int index = z * terrainData.resolution + x;
        if (index >= 0 && index < terrainTypes.Length)
        {
            return terrainTypes[index];
        }
        
        return TerrainType.Grass; // Default fallback
    }
    
    /// <summary>
    /// Queues a glob creation for processing after structural changes
    /// </summary>
    private void QueueGlobCreation(GlobCreationData globData)
    {
        queuedGlobCreations.Add(globData);
    }
    
    /// <summary>
    /// Processes all queued glob creations
    /// </summary>
    private void ProcessQueuedGlobCreations()
    {
        if (queuedGlobCreations.Count == 0) return;
        
        Debug.Log($"[TerrainModificationSystem] Processing {queuedGlobCreations.Count} queued glob creations");
        
        foreach (var globData in queuedGlobCreations)
        {
            // Calculate glob radius based on removal type
            float globRadius = globData.removalType switch
            {
                GlobRemovalType.Small => 1.0f,
                GlobRemovalType.Medium => 2.0f,
                GlobRemovalType.Large => 3.0f,
                _ => globData.radius
            };
            
            // Create the glob entity
            var globEntity = globPhysicsSystem.CreateTerrainGlob(
                globData.position,
                globRadius,
                globData.removalType,
                globData.terrainType
            );
            
            if (globEntity != Entity.Null)
            {
                Debug.Log($"[TerrainModificationSystem] Created glob entity {globEntity.Index} at {globData.position}");
            }
            else
            {
                Debug.LogWarning($"[TerrainModificationSystem] Failed to create glob entity at {globData.position}");
            }
        }
        
        // Clear the queue
        queuedGlobCreations.Clear();
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