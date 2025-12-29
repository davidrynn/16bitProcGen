using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;
using DOTS.Terrain;
using DOTS.Terrain.Modification;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// [LEGACY] System for terrain modifications (glob removal) using the legacy TerrainData component.
/// 
/// ⚠️ LEGACY SYSTEM: This system operates on DOTS.Terrain.TerrainData component for terrain glob removal/physics.
/// The current active terrain system uses SDF (Signed Distance Fields) with edit systems in DOTS.Terrain namespace:
/// - TerrainEditInputSystem (input handling for SDF edits)
/// - TerrainChunkEditUtility (SDF edit utilities)
/// - SDFEdit buffer for additive/subtractive terrain edits
/// 
/// This system is maintained for backward compatibility with existing tests and legacy glob physics code.
/// </summary>
[DisableAutoCreation]
public partial struct TerrainModificationSystem : ISystem
{
    // Note: Managed fields are not allowed in struct systems
    // We'll find these components at runtime instead of storing references
    
    // Data structure for queued glob creation
    private struct GlobCreationData
    {
        public float3 position;
        public float radius;
        public GlobRemovalType removalType;
        public TerrainType terrainType;
    }
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<PlayerModificationComponent>();
    }

    public void OnUpdate(ref SystemState state)
    {
        // Find managers at runtime (no stored references in struct systems)
        var computeManager = ComputeShaderManager.Instance;
        var bufferManager = Object.FindFirstObjectByType<TerrainComputeBufferManager>();
        
        // Process modification requests
        var ecb = new EntityCommandBuffer(Allocator.Temp);
        var entityManager = state.EntityManager;
        float elapsedTime = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (modification, entity) in SystemAPI.Query<RefRO<PlayerModificationComponent>>().WithEntityAccess())
        {
            if (bufferManager != null && computeManager != null)
            {
                ApplyTerrainGlobRemoval(entityManager, elapsedTime, modification.ValueRO, computeManager, bufferManager);
                
                var globCreationData = new GlobCreationData
                {
                    position = modification.ValueRO.position,
                    radius = modification.ValueRO.radius,
                    removalType = modification.ValueRO.removalType,
                    terrainType = DetermineTerrainTypeAtPosition(entityManager, modification.ValueRO.position)
                };

                // Create glob entity immediately instead of queuing
                CreateGlobEntity(entityManager, globCreationData);
            }
            else
            {
                Debug.Log($"[TerrainModificationSystem] Modification Request: Pos={modification.ValueRO.position}, Radius={modification.ValueRO.radius}, Strength={modification.ValueRO.strength}, Res={modification.ValueRO.resolution}");
            }

            ecb.DestroyEntity(entity);
        }
        
        ecb.Playback(entityManager);
        ecb.Dispose();
    }
    
    private void ApplyTerrainGlobRemoval(EntityManager entityManager, float elapsedTime, PlayerModificationComponent modification, ComputeShaderManager computeManager, TerrainComputeBufferManager bufferManager)
    {
        var globShader = computeManager.GetComputeShader("TerrainGlobRemoval");
        if (globShader == null) { Debug.LogError("[TerrainModificationSystem] TerrainGlobRemoval compute shader not found!"); return; }
        var terrainChunk = FindTerrainChunkAtPosition(entityManager, modification.position);
        if (terrainChunk == Entity.Null) { Debug.LogWarning($"[TerrainModificationSystem] No terrain chunk found at position {modification.position}"); return; }
        var terrainData = entityManager.GetComponentData<TerrainData>(terrainChunk);
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
    globShader.SetFloat("chunkTime", elapsedTime);
        
        // Dispatch the compute shader
        int threadGroups = (terrainData.resolution + 7) / 8;
        globShader.Dispatch(0, threadGroups, threadGroups, 1); // RemoveTerrainGlob kernel
        
        // Read back the modified data
        heightBuffer.GetData(heightArray);
        
        // Set needsMeshUpdate flag to true and update the entity
        terrainData.needsMeshUpdate = true;
    entityManager.SetComponentData(terrainChunk, terrainData);
        
        // Update the terrain data (this would need to be done through a proper update system)
        // For now, just log the modification
        Debug.Log($"[TerrainModificationSystem] Applied glob removal at {modification.position} with radius {modification.radius}");
        
        // Clean up only the removedMaskBuffer
        removedMaskBuffer.Release();
    }
    

    
    /// <summary>
    /// Determines the terrain type at a given world position
    /// </summary>
    private TerrainType DetermineTerrainTypeAtPosition(EntityManager entityManager, float3 worldPosition)
    {
        // Find the terrain chunk at this position
        var terrainChunk = FindTerrainChunkAtPosition(entityManager, worldPosition);
        if (terrainChunk == Entity.Null)
        {
            return TerrainType.Grass; // Default fallback
        }

        var terrainData = entityManager.GetComponentData<TerrainData>(terrainChunk);
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
    /// Creates a glob entity directly (no queuing needed in struct systems)
    /// </summary>
    private void CreateGlobEntity(EntityManager entityManager, GlobCreationData globData)
    {
        // Create glob entity with physics component
        var globEntity = entityManager.CreateEntity();
        
        var globComponent = new TerrainGlobComponent
        {
            originalPosition = globData.position,
            currentPosition = globData.position,
            globRadius = globData.radius,
            globType = globData.removalType,
            terrainType = globData.terrainType,
            velocity = float3.zero,
            angularVelocity = float3.zero,
            mass = 1.0f,
            bounciness = 0.7f,
            friction = 0.5f,
            isGrounded = false,
            isCollected = false,
            isDestroyed = false,
            lifetime = 0f,
            collectionRadius = globData.radius * 2f,
            canBeCollected = true,
            resourceValue = 1,
            scale = new float3(globData.radius),
            rotation = quaternion.identity,
            visualAlpha = 1.0f
        };
        
        var physicsComponent = new TerrainGlobPhysicsComponent
        {
            enablePhysics = true,
            gravityScale = 1.0f,
            dragCoefficient = 0.1f,
            maxVelocity = 10.0f,
            maxAngularVelocity = 5.0f
        };
        
        var transform = new LocalTransform
        {
            Position = globData.position,
            Rotation = quaternion.identity,
            Scale = globData.radius
        };
        
        entityManager.AddComponentData(globEntity, globComponent);
        entityManager.AddComponentData(globEntity, physicsComponent);
        entityManager.AddComponentData(globEntity, transform);
        
        Debug.Log($"[TerrainModificationSystem] Created glob entity at {globData.position} with radius {globData.radius}");
    }
    
    
    private Entity FindTerrainChunkAtPosition(EntityManager entityManager, float3 worldPosition)
    {
        // Simplified chunk finding - you'll need to implement proper chunk detection
        // This should find the terrain chunk that contains the given world position
        Entity foundChunk = Entity.Null;
        
        // Use a different approach to avoid ForEach lambda issues
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainData>());
        var entities = query.ToEntityArray(Allocator.Temp);
        
        foreach (var entity in entities)
        {
            var terrain = entityManager.GetComponentData<TerrainData>(entity);
            
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
        query.Dispose();
        return foundChunk;
    }
}
