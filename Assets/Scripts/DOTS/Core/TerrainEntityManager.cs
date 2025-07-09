using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// Manages terrain entity creation and destruction
/// Provides methods for spawning and managing terrain chunks as entities
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
            Debug.LogError("TerrainEntityManager: No default world found!");
            return;
        }
        
        entityManager = world.EntityManager;
        
        // Create query for terrain entities
        terrainQuery = entityManager.CreateEntityQuery(typeof(DOTS.Terrain.TerrainData));
        
        isInitialized = true;
        Debug.Log("TerrainEntityManager: Initialized successfully");
    }
    
    /// <summary>
    /// Creates a terrain entity at the specified chunk position
    /// </summary>
    /// <param name="chunkPosition">2D position of the terrain chunk</param>
    /// <param name="biomeType">Type of biome for this chunk</param>
    /// <returns>Created entity</returns>
    public Entity CreateTerrainEntity(int2 chunkPosition, BiomeType biomeType = BiomeType.Plains)
    {
        InitializeIfNeeded();
        
        if (!isInitialized)
        {
            Debug.LogError("TerrainEntityManager: Failed to initialize - cannot create entity");
            return Entity.Null;
        }
        
        // Create entity with terrain data
        var terrainData = TerrainDataBuilder.CreateTerrainData(chunkPosition, defaultResolution, defaultWorldScale);
        var biomeComponent = BiomeBuilder.CreateBiomeComponent(biomeType);
        
        // Create entity
        var entity = entityManager.CreateEntity();
        
        // Add components
        entityManager.AddComponentData(entity, terrainData);
        entityManager.AddComponentData(entity, biomeComponent);
        
        Debug.Log($"Created terrain entity at {chunkPosition} with biome {biomeType}");
        
        return entity;
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
        InitializeIfNeeded();
        
        if (!isInitialized)
        {
            Debug.LogError("TerrainEntityManager: Failed to initialize - cannot create entity");
            return Entity.Null;
        }
        
        // Create entity with custom terrain data
        var terrainData = TerrainDataBuilder.CreateTerrainData(chunkPosition, resolution, worldScale);
        var biomeComponent = BiomeBuilder.CreateBiomeComponent(biomeType);
        
        // Create entity
        var entity = entityManager.CreateEntity();
        
        // Add components
        entityManager.AddComponentData(entity, terrainData);
        entityManager.AddComponentData(entity, biomeComponent);
        
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
            // Get terrain data to clean up ComputeBuffers
            if (entityManager.HasComponent<DOTS.Terrain.TerrainData>(entity))
            {
                var terrainData = entityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
                CleanupTerrainData(terrainData);
            }
            
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
    /// <param name="terrainData">Terrain data to clean up</param>
    private void CleanupTerrainData(DOTS.Terrain.TerrainData terrainData)
    {
        // Dispose blob assets
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
                Debug.LogWarning($"TerrainEntityManager: Error disposing query during cleanup: {e.Message}");
            }
        }
        
        // Reset initialization flag
        isInitialized = false;
    }
} 