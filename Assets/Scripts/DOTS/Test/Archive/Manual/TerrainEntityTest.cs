using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Test script to create terrain entities and verify the DOTS terrain generation system
/// </summary>
public class TerrainEntityTest : MonoBehaviour
{
    [Header("Test Settings")]
    public int testResolution = 64;
    public float testWorldScale = 1.0f;
    public int numberOfTestChunks = 3;
    
    private TerrainEntityManager entityManager;
    private Entity[] testEntities;
    
    void Start()
    {
        Debug.Log("=== TERRAIN ENTITY TEST ===");
        
        // Create or find TerrainEntityManager
        entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            Debug.Log("Creating TerrainEntityManager...");
            var go = new GameObject("TerrainEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
        }
        
        // Create test terrain entities
        CreateTestTerrainEntities();
        
        Debug.Log("=== TERRAIN ENTITY TEST COMPLETE ===");
    }
    
    void CreateTestTerrainEntities()
    {
        Debug.Log($"Creating {numberOfTestChunks} test terrain entities...");
        
        testEntities = new Entity[numberOfTestChunks];
        
        for (int i = 0; i < numberOfTestChunks; i++)
        {
            // Create terrain entity at different positions
            var chunkPosition = new int2(i, 0);
            var entity = entityManager.CreateTerrainEntity(chunkPosition, BiomeType.Plains);
            
            if (entity == Entity.Null)
            {
                Debug.LogError($"Failed to create terrain entity {i}");
                continue;
            }
            
            testEntities[i] = entity;
            
            // Verify the entity was created with TerrainData
            var world = World.DefaultGameObjectInjectionWorld;
            var worldEntityManager = world.EntityManager;
            
            if (worldEntityManager.HasComponent<DOTS.Terrain.TerrainData>(entity))
            {
                var terrainData = worldEntityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
                Debug.Log($"✓ Created terrain entity {i}: Position={terrainData.chunkPosition}, Resolution={terrainData.resolution}, NeedsGeneration={terrainData.needsGeneration}");
            }
            else
            {
                Debug.LogError($"Entity {i} missing TerrainData component!");
            }
        }
        
        Debug.Log($"✓ Created {testEntities.Length} terrain entities successfully");
    }
    
    void OnDestroy()
    {
        // Clean up test entities
        if (entityManager != null && testEntities != null)
        {
            foreach (var entity in testEntities)
            {
                if (entity != Entity.Null)
                {
                    entityManager.DestroyTerrainEntity(entity);
                }
            }
        } 
    }
    
    void OnGUI()
    {
        if (entityManager != null)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Terrain Entity Test");
            GUILayout.Label($"Active Entities: {entityManager.GetTerrainEntityCount()}");
            
            if (GUILayout.Button("Create New Entity"))
            {
                var newPos = new int2(UnityEngine.Random.Range(-5, 5), UnityEngine.Random.Range(-5, 5));
                entityManager.CreateTerrainEntity(newPos, BiomeType.Forest);
            }
            
            if (GUILayout.Button("Destroy All Entities"))
            {
                entityManager.DestroyAllTerrainEntities();
            }
            
            GUILayout.EndArea();
        }
    }
}
