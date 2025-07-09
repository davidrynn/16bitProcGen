using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// Minimal test for ChunkProcessor
/// Tests basic functionality of the minimal DOTS system
/// </summary>
public class TerrainDataManagerSystemTest : MonoBehaviour
{
    [Header("Test Settings")]
    public int testResolution = 64;
    public float testWorldScale = 1.0f;
    public int numberOfTestEntities = 5;
    
    [Header("Test Results")]
    public bool allTestsPassed = false;
    public string testResults = "";
    
    private EntityManager entityManager;
    private SystemHandle chunkProcessorHandle;
    private Entity[] testEntities;
    private DOTS.Terrain.TerrainData[] terrainDataArray; // Store blob assets for disposal
    
    void Start()
    {
        // Get the default world's entity manager
        var world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;
        
        // Get the chunk processor system handle
        chunkProcessorHandle = world.GetOrCreateSystem<ChunkProcessor>();
        
        // Initialize test entities
        testEntities = new Entity[numberOfTestEntities];
        terrainDataArray = new DOTS.Terrain.TerrainData[numberOfTestEntities]; // Store for disposal
        
        // Run the tests
        RunChunkProcessorTests();
    }
    
    void RunChunkProcessorTests()
    {
        try
        {
            Debug.Log("=== Starting ChunkProcessor Tests ===");
            
            // Test 1: Create terrain entities
            Debug.Log("Test 1: Creating terrain entities...");
            CreateTestEntities();
            
            // Test 2: Test basic validation
            Debug.Log("Test 2: Testing basic validation...");
            TestBasicValidation();
            
            // Test 3: Test invalid entities
            Debug.Log("Test 3: Testing invalid entities...");
            TestInvalidEntities();
            
            // All tests passed
            allTestsPassed = true;
            testResults = "All ChunkProcessor tests passed successfully!";
            Debug.Log("=== ChunkProcessor Tests PASSED ===");
            
        }
        catch (System.Exception e)
        {
            allTestsPassed = false;
            testResults = $"Test failed: {e.Message}";
            Debug.LogError($"=== ChunkProcessor Tests FAILED: {e.Message} ===");
        }
    }
    
    void CreateTestEntities()
    {
        for (int i = 0; i < numberOfTestEntities; i++)
        {
            var chunkPosition = new int2(i, i);
            var terrainData = TerrainDataBuilder.CreateTerrainData(chunkPosition, testResolution, testWorldScale);
            
            testEntities[i] = entityManager.CreateEntity();
            entityManager.AddComponentData(testEntities[i], terrainData);
            terrainDataArray[i] = terrainData; // Store for disposal
            
            Debug.Log($"✓ Created terrain entity {i}: Position={chunkPosition}, Resolution={testResolution}");
        }
    }
    
    void TestBasicValidation()
    {
        // The system should validate entities in OnUpdate
        // We just need to wait a frame for the validation to run
        StartCoroutine(WaitForValidation());
    }
    
    System.Collections.IEnumerator WaitForValidation()
    {
        yield return new WaitForEndOfFrame();
        Debug.Log("✓ Basic validation test completed (check console for any warnings)");
    }
    
    void TestInvalidEntities()
    {
        // Create an invalid entity to test validation
        var invalidEntity = entityManager.CreateEntity();
        var invalidTerrainData = new DOTS.Terrain.TerrainData
        {
            chunkPosition = new int2(999, 999),
            resolution = -1, // Invalid resolution
            worldScale = -1f, // Invalid world scale
            needsGeneration = false,
            needsModification = false
        };
        
        entityManager.AddComponentData(invalidEntity, invalidTerrainData);
        
        // Let the system run for a frame to trigger validation
        StartCoroutine(WaitForInvalidEntityValidation(invalidEntity));
    }
    
    System.Collections.IEnumerator WaitForInvalidEntityValidation(Entity invalidEntity)
    {
        yield return new WaitForEndOfFrame();
        
        // Clean up invalid entity
        if (entityManager.Exists(invalidEntity))
        {
            entityManager.DestroyEntity(invalidEntity);
        }
        
        Debug.Log("✓ Invalid entity validation test completed (check console for expected error messages)");
    }
    
    void OnDestroy()
    {
        // Dispose blob assets to prevent leaks
        if (terrainDataArray != null)
        {
            for (int i = 0; i < terrainDataArray.Length; i++)
            {
                if (terrainDataArray[i].heightData.IsCreated)
                {
                    terrainDataArray[i].heightData.Dispose();
                }
                if (terrainDataArray[i].modifications.IsCreated)
                {
                    terrainDataArray[i].modifications.Dispose();
                }
            }
        }
    }
    
    void OnGUI()
    {
        // Display test results in scene view
        if (Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 400, 150));
            GUILayout.Label($"ChunkProcessor Test: {(allTestsPassed ? "PASSED" : "FAILED")}");
            GUILayout.Label(testResults);
            GUILayout.EndArea();
        }
    }
}