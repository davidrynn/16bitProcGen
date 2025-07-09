using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Test script for the biome system
/// Verifies BiomeComponent creation and BiomeBuilder functionality
/// </summary>
public class BiomeSystemTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestsOnStart = true;
    public BiomeType testBiomeType = BiomeType.Forest;
    
    private TerrainEntityManager entityManager;
    
    private void Start()
    {
        if (runTestsOnStart)
        {
            RunBiomeSystemTests();
        }
    }
    
    /// <summary>
    /// Runs all biome system tests
    /// </summary>
    public void RunBiomeSystemTests()
    {
        Debug.Log("=== Starting Biome System Tests ===");
        
        TestBiomeBuilder();
        TestBiomeComponentCreation();
        TestTerrainEntityWithBiome();
        
        Debug.Log("=== Biome System Tests Complete ===");
    }
    
    /// <summary>
    /// Tests BiomeBuilder functionality
    /// </summary>
    private void TestBiomeBuilder()
    {
        Debug.Log("Testing BiomeBuilder...");
        
        try
        {
            // Test creating biome component
            var biomeComponent = BiomeBuilder.CreateBiomeComponent(testBiomeType);
            
            // Verify component properties
            if (biomeComponent.biomeType != testBiomeType)
            {
                Debug.LogError($"Biome type mismatch: expected {testBiomeType}, got {biomeComponent.biomeType}");
                return;
            }
            
            if (biomeComponent.biomeScale <= 0)
            {
                Debug.LogError($"Invalid biome scale: {biomeComponent.biomeScale}");
                return;
            }
            
            if (biomeComponent.noiseScale <= 0)
            {
                Debug.LogError($"Invalid noise scale: {biomeComponent.noiseScale}");
                return;
            }
            
            if (biomeComponent.heightMultiplier <= 0)
            {
                Debug.LogError($"Invalid height multiplier: {biomeComponent.heightMultiplier}");
                return;
            }
            
            // Test terrain data creation
            if (!biomeComponent.terrainData.IsCreated)
            {
                Debug.LogError("Terrain data blob asset not created");
                return;
            }
            
            ref var terrainData = ref biomeComponent.terrainData.Value;
            if (terrainData.terrainChances.Length == 0)
            {
                Debug.LogError("No terrain chances defined for biome");
                return;
            }
            
            Debug.Log($"✓ BiomeBuilder test passed for {testBiomeType}");
            Debug.Log($"  - Biome scale: {biomeComponent.biomeScale}");
            Debug.Log($"  - Noise scale: {biomeComponent.noiseScale}");
            Debug.Log($"  - Height multiplier: {biomeComponent.heightMultiplier}");
            Debug.Log($"  - Terrain chances: {terrainData.terrainChances.Length}");
            
            // Clean up
            biomeComponent.terrainData.Dispose();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BiomeBuilder test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Tests BiomeComponent creation for all biome types
    /// </summary>
    private void TestBiomeComponentCreation()
    {
        Debug.Log("Testing BiomeComponent creation for all biome types...");
        
        BiomeType[] allBiomeTypes = {
            BiomeType.Plains, BiomeType.Forest, BiomeType.Mountains, BiomeType.Desert,
            BiomeType.Ocean, BiomeType.Arctic, BiomeType.Volcanic, BiomeType.Swamp,
            BiomeType.Crystalline, BiomeType.Alien
        };
        
        var blobAssets = new BlobAssetReference<BiomeTerrainData>[allBiomeTypes.Length];
        
        try
        {
            for (int i = 0; i < allBiomeTypes.Length; i++)
            {
                var biomeType = allBiomeTypes[i];
                var biomeComponent = BiomeBuilder.CreateBiomeComponent(biomeType);
                
                // Store blob asset for cleanup
                blobAssets[i] = biomeComponent.terrainData;
                
                // Verify component
                if (biomeComponent.biomeType != biomeType)
                {
                    Debug.LogError($"Biome type mismatch for {biomeType}: expected {biomeType}, got {biomeComponent.biomeType}");
                    continue;
                }
                
                Debug.Log($"✓ Created BiomeComponent for {biomeType}");
            }
            
            Debug.Log("✓ All BiomeComponent creation tests passed");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"BiomeComponent creation test failed: {e.Message}");
        }
        finally
        {
            // Clean up blob assets
            for (int i = 0; i < blobAssets.Length; i++)
            {
                if (blobAssets[i].IsCreated)
                {
                    blobAssets[i].Dispose();
                }
            }
        }
    }
    
    /// <summary>
    /// Tests creating terrain entities with biome components
    /// </summary>
    private void TestTerrainEntityWithBiome()
    {
        Debug.Log("Testing terrain entity creation with biome...");
        
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
            
            // Create test entity
            var chunkPosition = new int2(0, 0);
            var entity = entityManager.CreateTerrainEntity(chunkPosition, testBiomeType);
            
            if (entity == Entity.Null)
            {
                Debug.LogError("Failed to create terrain entity");
                return;
            }
            
            // Verify entity has both components
            var world = World.DefaultGameObjectInjectionWorld;
            var entityManager2 = world.EntityManager;
            
            if (!entityManager2.HasComponent<DOTS.Terrain.TerrainData>(entity))
            {
                Debug.LogError("Entity missing TerrainData component");
                return;
            }
            
            if (!entityManager2.HasComponent<BiomeComponent>(entity))
            {
                Debug.LogError("Entity missing BiomeComponent");
                return;
            }
            
            // Get component data
            var terrainData = entityManager2.GetComponentData<DOTS.Terrain.TerrainData>(entity);
            var biomeComponent = entityManager2.GetComponentData<BiomeComponent>(entity);
            
            // Verify data
            if (terrainData.chunkPosition.x != chunkPosition.x || terrainData.chunkPosition.y != chunkPosition.y)
            {
                Debug.LogError($"Chunk position mismatch: expected {chunkPosition}, got {terrainData.chunkPosition}");
                return;
            }
            
            if (biomeComponent.biomeType != testBiomeType)
            {
                Debug.LogError($"Biome type mismatch: expected {testBiomeType}, got {biomeComponent.biomeType}");
                return;
            }
            
            Debug.Log($"✓ Terrain entity test passed");
            Debug.Log($"  - Entity: {entity}");
            Debug.Log($"  - Chunk position: {terrainData.chunkPosition}");
            Debug.Log($"  - Biome type: {biomeComponent.biomeType}");
            Debug.Log($"  - Resolution: {terrainData.resolution}");
            
            // Clean up
            entityManager.DestroyTerrainEntity(entity);
            
            // Clean up test manager if we created it
            if (entityManager.gameObject.name == "TestTerrainEntityManager")
            {
                DestroyImmediate(entityManager.gameObject);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Terrain entity test failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Manual test trigger for editor
    /// </summary>
    [ContextMenu("Run Biome System Tests")]
    private void RunTests()
    {
        RunBiomeSystemTests();
    }
} 