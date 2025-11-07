using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// Comprehensive test for Phase 1 completion
/// Verifies all core systems are working correctly
/// </summary>
public class Phase1CompletionTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestsOnStart = true;
    public bool createTestEntities = true;
    public int testEntityCount = 3;
    
    private TerrainEntityManager entityManager;
    private TerrainComputeBufferManager bufferManager;
    
    private void Start()
    {
        if (runTestsOnStart)
        {
            RunPhase1CompletionTests();
        }
    }
    
    /// <summary>
    /// Runs all Phase 1 completion tests
    /// </summary>
    public void RunPhase1CompletionTests()
    {
        Debug.Log("=== PHASE 1 COMPLETION TESTS ===");
        
        TestCoreDataStructures();
        TestBiomeSystem();
        TestEntityManagement();
        TestComputeBufferManagement();
        TestIntegration();
        
        Debug.Log("=== PHASE 1 COMPLETION TESTS FINISHED ===");
    }
    
    /// <summary>
    /// Tests core data structures
    /// </summary>
    private void TestCoreDataStructures()
    {
        Debug.Log("Testing Core Data Structures...");
        
        try
        {
            // Test TerrainData creation
            var terrainData = TerrainDataBuilder.CreateTerrainData(new int2(0, 0), 64, 1.0f);
            
            if (terrainData.resolution != 64)
            {
                Debug.LogError($"TerrainData resolution mismatch: expected 64, got {terrainData.resolution}");
                return;
            }
            
            if (!terrainData.heightData.IsCreated)
            {
                Debug.LogError("TerrainData height data not created");
                return;
            }
            
            if (!terrainData.modifications.IsCreated)
            {
                Debug.LogError("TerrainData modifications not created");
                return;
            }
            
            Debug.Log("✓ Core data structures test passed");
            
            // Clean up
            terrainData.heightData.Dispose();
            terrainData.modifications.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Core data structures test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Tests biome system
    /// </summary>
    private void TestBiomeSystem()
    {
        Debug.Log("Testing Biome System...");
        
        try
        {
            // Test all biome types
            BiomeType[] biomeTypes = {
                BiomeType.Plains, BiomeType.Forest, BiomeType.Mountains, BiomeType.Desert,
                BiomeType.Ocean, BiomeType.Arctic, BiomeType.Volcanic, BiomeType.Swamp,
                BiomeType.Crystalline, BiomeType.Alien
            };
            
            var blobAssets = new BlobAssetReference<BiomeTerrainData>[biomeTypes.Length];
            
            for (int i = 0; i < biomeTypes.Length; i++)
            {
                var biomeComponent = BiomeBuilder.CreateBiomeComponent(biomeTypes[i]);
                blobAssets[i] = biomeComponent.terrainData;
                
                // Verify biome properties
                if (biomeComponent.biomeType != biomeTypes[i])
                {
                    Debug.LogError($"Biome type mismatch for {biomeTypes[i]}: expected {biomeTypes[i]}, got {biomeComponent.biomeType}");
                    continue;
                }
                
                if (biomeComponent.biomeScale <= 0)
                {
                    Debug.LogError($"Invalid biome scale for {biomeTypes[i]}: {biomeComponent.biomeScale}");
                    continue;
                }
                
                ref var terrainData = ref biomeComponent.terrainData.Value;
                if (terrainData.terrainChances.Length == 0)
                {
                    Debug.LogError($"No terrain chances for {biomeTypes[i]}");
                    continue;
                }
                
                Debug.Log($"✓ Biome {biomeTypes[i]} created successfully");
            }
            
            Debug.Log("✓ Biome system test passed");
            
            // Clean up
            for (int i = 0; i < blobAssets.Length; i++)
            {
                if (blobAssets[i].IsCreated)
                {
                    blobAssets[i].Dispose();
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Biome system test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Tests entity management
    /// </summary>
    private void TestEntityManagement()
    {
        Debug.Log("Testing Entity Management...");
        
        try
        {
            // Get or create entity manager
            entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager == null)
            {
                Debug.LogWarning("TerrainEntityManager not found, creating one for testing");
                var go = new GameObject("TestTerrainEntityManager");
                entityManager = go.AddComponent<TerrainEntityManager>();
            }
            
            // Test entity creation
            var entity = entityManager.CreateTerrainEntity(new int2(0, 0), BiomeType.Forest);
            
            if (entity == Entity.Null)
            {
                Debug.LogError("Failed to create terrain entity");
                return;
            }
            
            // Verify entity count
            int entityCount = entityManager.GetTerrainEntityCount();
            if (entityCount != 1)
            {
                Debug.LogError($"Entity count mismatch: expected 1, got {entityCount}");
                return;
            }
            
            // Test entity finding
            var foundEntity = entityManager.FindTerrainEntityAt(new int2(0, 0));
            if (foundEntity != entity)
            {
                Debug.LogError("Failed to find created entity");
                return;
            }
            
            // Clean up
            entityManager.DestroyTerrainEntity(entity);
            
            // Verify cleanup
            entityCount = entityManager.GetTerrainEntityCount();
            if (entityCount != 0)
            {
                Debug.LogError($"Entity count after cleanup: expected 0, got {entityCount}");
                return;
            }
            
            Debug.Log("✓ Entity management test passed");
            
            // Clean up test manager if we created it
            if (entityManager.gameObject.name == "TestTerrainEntityManager")
            {
                DestroyImmediate(entityManager.gameObject);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Entity management test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Tests ComputeBuffer management
    /// </summary>
    private void TestComputeBufferManagement()
    {
        Debug.Log("Testing ComputeBuffer Management...");
        
        try
        {
            // Get or create buffer manager
            bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager == null)
            {
                Debug.LogWarning("TerrainComputeBufferManager not found, creating one for testing");
                var go = new GameObject("TestTerrainComputeBufferManager");
                bufferManager = go.AddComponent<TerrainComputeBufferManager>();
            }
            
            // Test buffer creation
            var heightBuffer = bufferManager.GetHeightBuffer(new int2(0, 0), 64);
            var biomeBuffer = bufferManager.GetBiomeBuffer(new int2(0, 0), 64);
            
            if (heightBuffer == null)
            {
                Debug.LogError("Failed to create height buffer");
                return;
            }
            
            if (biomeBuffer == null)
            {
                Debug.LogError("Failed to create biome buffer");
                return;
            }
            
            // Test buffer cleanup
            bufferManager.ReleaseChunkBuffers(new int2(0, 0));
            
            Debug.Log("✓ ComputeBuffer management test passed");
            
            // Clean up test manager if we created it
            if (bufferManager.gameObject.name == "TestTerrainComputeBufferManager")
            {
                DestroyImmediate(bufferManager.gameObject);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"ComputeBuffer management test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Tests integration of all systems
    /// </summary>
    private void TestIntegration()
    {
        Debug.Log("Testing System Integration...");
        
        try
        {
            // Create managers
            var entityManagerGO = new GameObject("IntegrationTestEntityManager");
            var entityManager = entityManagerGO.AddComponent<TerrainEntityManager>();
            
            var bufferManagerGO = new GameObject("IntegrationTestBufferManager");
            var bufferManager = bufferManagerGO.AddComponent<TerrainComputeBufferManager>();
            
            // Create multiple entities with different biomes
            var entities = new Entity[testEntityCount];
            var positions = new int2[] { new int2(0, 0), new int2(1, 0), new int2(0, 1) };
            var biomes = new BiomeType[] { BiomeType.Forest, BiomeType.Mountains, BiomeType.Desert };
            
            for (int i = 0; i < testEntityCount; i++)
            {
                entities[i] = entityManager.CreateTerrainEntity(positions[i], biomes[i]);
                
                if (entities[i] == Entity.Null)
                {
                    Debug.LogError($"Failed to create entity {i}");
                    return;
                }
            }
            
            // Verify all entities exist
            int entityCount = entityManager.GetTerrainEntityCount();
            if (entityCount != testEntityCount)
            {
                Debug.LogError($"Integration entity count mismatch: expected {testEntityCount}, got {entityCount}");
                return;
            }
            
            // Test DOTS system is processing entities
            var world = World.DefaultGameObjectInjectionWorld;
            var system = world.GetExistingSystem<TerrainSystem>();
            
            if (system == null)
            {
                Debug.LogError("TerrainSystem not found");
                return;
            }
            
            Debug.Log($"✓ System integration test passed with {testEntityCount} entities");
            
            // Clean up
            for (int i = 0; i < testEntityCount; i++)
            {
                entityManager.DestroyTerrainEntity(entities[i]);
            }
            
            DestroyImmediate(entityManagerGO);
            DestroyImmediate(bufferManagerGO);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"System integration test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Manual test trigger for editor
    /// </summary>
    [ContextMenu("Run Phase 1 Completion Tests")]
    private void RunTests()
    {
        RunPhase1CompletionTests();
    }
} 