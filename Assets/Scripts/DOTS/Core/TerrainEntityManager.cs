using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// [LEGACY] Manages terrain entity creation and destruction for the legacy DOTS.Terrain.TerrainData system.
/// Provides methods for spawning and managing terrain chunks as entities.
/// 
/// ⚠️ LEGACY SYSTEM: This is part of the legacy terrain system that uses DOTS.Terrain.TerrainData component.
/// The current active terrain system uses SDF (Signed Distance Fields) with components in DOTS.Terrain namespace.
/// 
/// For new terrain generation, use TerrainBootstrapAuthoring or create entities with SDF components directly.
/// This system is maintained for backward compatibility with existing tests and legacy code.
/// </summary>
public class TerrainEntityManager : MonoBehaviour
{
    [Header("Terrain Settings")]
    public int defaultResolution = 64;
    public float defaultWorldScale = 1.0f;
    public BiomeType defaultBiomeType = BiomeType.Plains;
    
    private EntityManager entityManager;
    private EntityQuery terrainQuery;
    private bool isInitialized = false;
    
    private void Start()
    {
        InitializeIfNeeded();
    }
    
    /// <summary>
    /// Ensures the EntityManager is initialized
    /// </summary>
    private void InitializeIfNeeded()
    {
        if (isInitialized) return;
        
        // Get the default world's entity manager
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogWarning("TerrainEntityManager: No default world found! This is normal during initialization. Will retry when needed.");
            return;
        }
        
        entityManager = world.EntityManager;
        
        // Create query for terrain entities
        terrainQuery = entityManager.CreateEntityQuery(typeof(DOTS.Terrain.TerrainData));
        
        isInitialized = true;
        Debug.Log("TerrainEntityManager: Initialized successfully");
    }
    
    /// <summary>
    /// Ensures the EntityManager is initialized with retry logic
    /// </summary>
    private bool EnsureInitialized()
    {
        if (isInitialized) return true;
        
        // Try to initialize
        InitializeIfNeeded();
        
        // If still not initialized, the world might not be ready yet
        if (!isInitialized)
        {
            Debug.LogWarning("TerrainEntityManager: World not ready yet - initialization will be retried when world becomes available");
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Creates a terrain entity at the specified chunk position
    /// </summary>
    /// <param name="chunkPosition">2D position of the terrain chunk</param>
    /// <param name="biomeType">Type of biome for this chunk</param>
    /// <returns>Created entity</returns>
    public Entity CreateTerrainEntity(int2 chunkPosition, BiomeType biomeType = BiomeType.Plains)
    {
        return CreateTerrainEntity(chunkPosition, defaultResolution, defaultWorldScale, biomeType);
    }
    
    /// <summary>
    /// Creates a terrain entity with custom settings
    /// </summary>
    /// <param name="chunkPosition">2D position of the terrain chunk</param>
    /// <param name="resolution">Resolution of the terrain grid</param>
    /// <param name="worldScale">Scale of world units</param>
    /// <param name="biomeType">Type of biome for this chunk</param>
    /// <returns>Created entity</returns>
    public Entity CreateTerrainEntity(int2 chunkPosition, int resolution, float worldScale, BiomeType biomeType)
    {
        if (!EnsureInitialized())
        {
            Debug.LogError("TerrainEntityManager: Failed to initialize - cannot create entity. Make sure you're in Play Mode and DOTS world is available.");
            return Entity.Null;
        }
        
        // Create entity with custom terrain data
        var terrainData = TerrainDataBuilder.CreateTerrainData(chunkPosition, resolution, worldScale);
        var biomeComponent = BiomeBuilder.CreateBiomeComponent(biomeType);
        
        // Create entity
        var entity = entityManager.CreateEntity();
        
        // Add terrain and biome components
        entityManager.AddComponentData(entity, terrainData);
        entityManager.AddComponentData(entity, biomeComponent);
        
        // Add Unity.Transforms components
        var localTransform = new LocalTransform
        {
            Position = new float3(chunkPosition.x * worldScale, 0, chunkPosition.y * worldScale),
            Rotation = quaternion.identity,
            Scale = worldScale
        };
        entityManager.AddComponentData(entity, localTransform);
        
        // Add LocalToWorld component for rendering
        var localToWorld = new LocalToWorld
        {
            Value = float4x4.TRS(
                localTransform.Position,
                localTransform.Rotation,
                new float3(localTransform.Scale)
            )
        };
        entityManager.AddComponentData(entity, localToWorld);
        
        Debug.Log($"Created terrain entity at {chunkPosition} with resolution {resolution}, scale {worldScale}, biome {biomeType}");
        
        return entity;
    }
    
    /// <summary>
    /// Destroys a terrain entity
    /// </summary>
    /// <param name="entity">Entity to destroy</param>
    public void DestroyTerrainEntity(Entity entity)
    {
        if (entityManager.Exists(entity))
        {
            // Clean up blob assets before destroying entity
            CleanupTerrainData(entity);
            
            entityManager.DestroyEntity(entity);
            Debug.Log($"Destroyed terrain entity {entity}");
        }
    }
    
    /// <summary>
    /// Destroys all terrain entities
    /// </summary>
    public void DestroyAllTerrainEntities()
    {
        var entities = terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        foreach (var entity in entities)
        {
            DestroyTerrainEntity(entity);
        }
        
        entities.Dispose();
        Debug.Log("Destroyed all terrain entities");
    }
    
    /// <summary>
    /// Gets the number of terrain entities
    /// </summary>
    /// <returns>Number of terrain entities</returns>
    public int GetTerrainEntityCount()
    {
        return terrainQuery.CalculateEntityCount();
    }
    
    /// <summary>
    /// Gets all terrain entities
    /// </summary>
    /// <returns>Array of terrain entities</returns>
    public Entity[] GetAllTerrainEntities()
    {
        return terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp).ToArray();
    }
    
    /// <summary>
    /// Finds a terrain entity at the specified chunk position
    /// </summary>
    /// <param name="chunkPosition">2D position to search for</param>
    /// <returns>Entity at position, or Entity.Null if not found</returns>
    public Entity FindTerrainEntityAt(int2 chunkPosition)
    {
        var entities = terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        foreach (var entity in entities)
        {
            var terrainData = entityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
            if (terrainData.chunkPosition.Equals(chunkPosition))
            {
                entities.Dispose();
                return entity;
            }
        }
        
        entities.Dispose();
        return Entity.Null;
    }
    
    /// <summary>
    /// Cleans up terrain data when destroying entities
    /// </summary>
    /// <param name="entity">Entity to clean up</param>
    private void CleanupTerrainData(Entity entity)
    {
        // Clean up terrain data blob assets
        if (entityManager.HasComponent<DOTS.Terrain.TerrainData>(entity))
        {
            var terrainData = entityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
            
            if (terrainData.heightData.IsCreated)
            {
                terrainData.heightData.Dispose();
            }
            
            if (terrainData.modifications.IsCreated)
            {
                terrainData.modifications.Dispose();
            }
            
            // Note: ComputeBuffer cleanup is handled by TerrainComputeBufferManager
            var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager != null)
            {
                bufferManager.ReleaseChunkBuffers(terrainData.chunkPosition);
            }
        }
        
        // Clean up biome component blob assets
        if (entityManager.HasComponent<BiomeComponent>(entity))
        {
            var biomeComponent = entityManager.GetComponentData<BiomeComponent>(entity);
            
            if (biomeComponent.terrainData.IsCreated)
            {
                biomeComponent.terrainData.Dispose();
            }
        }
    }
    
    private void OnDestroy()
    {
        // Clean up query safely
        if (terrainQuery != null)
        {
            try
            {
                terrainQuery.Dispose();
            }
            catch (System.Exception e)
            {
                // Ignore disposal errors when world is already destroyed
                Debug.LogWarning($"TerrainEntityManager: Error disposing TerrainData query during cleanup. " +
                    $"Exception Type: {e.GetType().Name}, " +
                    $"Message: {e.Message}" +
                    (e.InnerException != null ? $", Inner Exception: {e.InnerException.GetType().Name} - {e.InnerException.Message}" : "") +
                    $"\nStack Trace: {e.StackTrace}");
            }
        }
        
        // Reset initialization flag
        isInitialized = false;
    }
} 