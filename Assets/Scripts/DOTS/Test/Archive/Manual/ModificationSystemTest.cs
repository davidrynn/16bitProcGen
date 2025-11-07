using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class ModificationSystemTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestOnStart = true;
    public int numberOfTestEntities = 3;
    
    [Header("Modification Parameters")]
    public float3 testPosition = new float3(10, 0, 20);
    public float testRadius = 5f;
    public float testStrength = 1f;
    public int testResolution = 32;

    void Start()
    {
        if (runTestOnStart)
        {
            RunModificationTest();
        }
    }

    [ContextMenu("Run Modification Test")]
    public void RunModificationTest()
    {
        Debug.Log("=== Starting Modification System Test ===");
        
        // Setup required managers first
        SetupRequiredManagers();
        
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype archetype = entityManager.CreateArchetype(typeof(PlayerModificationComponent));
        
        // Create multiple test entities
        for (int i = 0; i < numberOfTestEntities; i++)
        {
            Entity entity = entityManager.CreateEntity(archetype);
            
            // Vary the position for each entity
            float3 position = testPosition + new float3(i * 5, 0, i * 3);
            
                    entityManager.SetComponentData(entity, new PlayerModificationComponent
        {
            position = position,
            radius = testRadius + i * 0.5f,
            strength = testStrength + i * 0.1f,
            resolution = testResolution,
            removalType = (GlobRemovalType)(i % 3), // Cycle through Small, Medium, Large
            maxDepth = -50.0f, // Allow digging 50 units underground
            allowUnderground = true,
            toolEfficiency = 0.8f + (i * 0.1f), // Vary tool efficiency
            isMiningTool = true
        });
            
            Debug.Log($"Created test entity {i + 1}: Pos={position}, Radius={testRadius + i * 0.5f}, Type={(GlobRemovalType)(i % 3)}, Underground=true");
        }
        
        Debug.Log($"=== Created {numberOfTestEntities} test entities ===");
        Debug.Log("Check the console for TerrainModificationSystem logs...");
    }
    
    /// <summary>
    /// Sets up required managers for the modification system test
    /// </summary>
    private void SetupRequiredManagers()
    {
        Debug.Log("Setting up required managers...");
        
        // Setup TerrainComputeBufferManager
        var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
        if (bufferManager == null)
        {
            var go = new GameObject("TerrainComputeBufferManager");
            bufferManager = go.AddComponent<TerrainComputeBufferManager>();
            Debug.Log("✓ Created TerrainComputeBufferManager");
        }
        else
        {
            Debug.Log("✓ TerrainComputeBufferManager already exists");
        }
        
        // Setup ComputeShaderManager
        try
        {
            var computeManager = ComputeShaderManager.Instance;
            if (computeManager != null)
            {
                Debug.Log("✓ ComputeShaderManager already exists");
            }
            else
            {
                Debug.LogError("✗ ComputeShaderManager not found - terrain modifications will be logged only");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"✗ ComputeShaderManager setup failed: {e.Message}");
        }
        
        // Setup terrain entities for testing
        SetupTerrainEntities();
    }
    
    /// <summary>
    /// Sets up terrain entities for the modification test
    /// </summary>
    private void SetupTerrainEntities()
    {
        Debug.Log("Setting up terrain entities for modification testing...");
        
        // Find or create TerrainEntityManager
        var entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            Debug.Log("Creating TerrainEntityManager...");
            var go = new GameObject("TerrainEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
        }
        
        // Create terrain entities around the test position
        // Convert world position to chunk coordinates
        int chunkSize = testResolution;
        int2 testChunkPos = new int2(
            Mathf.FloorToInt(testPosition.x / chunkSize),
            Mathf.FloorToInt(testPosition.z / chunkSize)
        );
        
        // Create a 3x3 grid of terrain chunks around the test position
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                int2 chunkPos = testChunkPos + new int2(x, z);
                
                // Create terrain entity
                var entity = entityManager.CreateTerrainEntity(
                    chunkPos,
                    testResolution,
                    10f, // worldScale
                    BiomeType.Plains
                );
                
                if (entity != Entity.Null)
                {
                    // Mark for generation
                    var world = World.DefaultGameObjectInjectionWorld;
                    if (world != null)
                    {
                        var terrainData = world.EntityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
                        terrainData.needsGeneration = true;
                        world.EntityManager.SetComponentData(entity, terrainData);
                        
                        Debug.Log($"✓ Created terrain entity at chunk {chunkPos} (world pos: {chunkPos * chunkSize})");
                    }
                }
            }
        }
        
        Debug.Log("✓ Created 9 terrain entities around test position");
        Debug.Log("The HybridTerrainGenerationSystem should now process these entities!");
    }

    [ContextMenu("Clear All Entities")]
    public void ClearAllEntities()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // Destroy all entities with PlayerModificationComponent
        var query = entityManager.CreateEntityQuery(typeof(PlayerModificationComponent));
        int count = query.CalculateEntityCount();
        entityManager.DestroyEntity(query);
        
        Debug.Log($"Cleared {count} entities with PlayerModificationComponent");
    }

    void OnValidate()
    {
        // Ensure reasonable values
        testRadius = Mathf.Max(0.1f, testRadius);
        testStrength = Mathf.Clamp(testStrength, 0.1f, 10f);
        testResolution = Mathf.Clamp(testResolution, 8, 256);
        numberOfTestEntities = Mathf.Clamp(numberOfTestEntities, 1, 10);
    }
} 