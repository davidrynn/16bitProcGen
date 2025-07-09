using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Quick script to create terrain entities for testing
/// This will immediately create entities so the HybridTerrainGenerationSystem has something to process
/// </summary>
public class QuickTerrainEntityCreator : MonoBehaviour
{
    [Header("Creation Settings")]
    public bool createOnStart = true;
    public int numberOfEntities = 5;
    public int resolution = 32;
    public float worldScale = 10f;
    
    private TerrainEntityManager entityManager;
    
    private void Start()
    {
        if (createOnStart)
        {
            CreateTerrainEntities();
        }
    }
    
    /// <summary>
    /// Creates terrain entities for testing
    /// </summary>
    [ContextMenu("Create Terrain Entities")]
    public void CreateTerrainEntities()
    {
        Debug.Log("=== QUICK TERRAIN ENTITY CREATOR ===");
        
        // Find or create TerrainEntityManager
        entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            Debug.Log("Creating TerrainEntityManager...");
            var go = new GameObject("TerrainEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
        }
        
        Debug.Log($"Creating {numberOfEntities} terrain entities...");
        
        // Create entities in a grid pattern
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numberOfEntities));
        
        for (int i = 0; i < numberOfEntities; i++)
        {
            int x = i % gridSize;
            int z = i / gridSize;
            var chunkPosition = new int2(x, z);
            
            // Create terrain entity
            var entity = entityManager.CreateTerrainEntity(
                chunkPosition,
                resolution,
                worldScale,
                BiomeType.Plains
            );
            
            if (entity == Entity.Null)
            {
                Debug.LogError($"Failed to create terrain entity at {chunkPosition}");
                continue;
            }
            
            // Mark for generation
            var world = World.DefaultGameObjectInjectionWorld;
            if (world != null)
            {
                var terrainData = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
                terrainData.needsGeneration = true;
                world.EntityManager.SetComponentData(entity, terrainData);
                
                Debug.Log($"✓ Created terrain entity {i} at {chunkPosition} - marked for generation");
            }
        }
        
        Debug.Log($"✓ Created {numberOfEntities} terrain entities successfully");
        Debug.Log("The HybridTerrainGenerationSystem should now process these entities!");
    }
    
    /// <summary>
    /// Gets the current entity count
    /// </summary>
    [ContextMenu("Get Entity Count")]
    public void GetEntityCount()
    {
        if (entityManager != null)
        {
            var count = entityManager.GetTerrainEntityCount();
            Debug.Log($"Current terrain entity count: {count}");
        }
        else
        {
            Debug.LogWarning("TerrainEntityManager not found");
        }
    }
    
    /// <summary>
    /// Destroys all terrain entities
    /// </summary>
    [ContextMenu("Destroy All Entities")]
    public void DestroyAllEntities()
    {
        if (entityManager != null)
        {
            entityManager.DestroyAllTerrainEntities();
            Debug.Log("Destroyed all terrain entities");
        }
        else
        {
            Debug.LogWarning("TerrainEntityManager not found");
        }
    }
} 