using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// Simple test script to validate our DOTS terrain data structures
/// </summary>
public class TerrainDataTest : MonoBehaviour
{
    [Header("Test Settings")]
    public int testResolution = 64;
    public float testWorldScale = 1.0f;
    
    [Header("Test Results")]
    public bool testPassed = false;
    public string testMessage = "";
    
    private EntityManager entityManager;
    private Entity testEntity;
    private DOTS.Terrain.TerrainData testTerrainData; // Store for disposal
    
    void Start()
    {
        // Get the default world's entity manager
        var world = World.DefaultGameObjectInjectionWorld;
        entityManager = world.EntityManager;
        
        // Run the test
        RunTerrainDataTest();
    }
    
    void RunTerrainDataTest()
    {
        try
        {
            Debug.Log("=== Starting TerrainData Test ===");
            
            // Test 1: Create TerrainData
            Debug.Log("Test 1: Creating TerrainData...");
            var chunkPosition = new int2(0, 0);
            testTerrainData = TerrainDataBuilder.CreateTerrainData(chunkPosition, testResolution, testWorldScale);
            
            // Test 2: Create Entity with TerrainData
            Debug.Log("Test 2: Creating Entity with TerrainData...");
            testEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(testEntity, testTerrainData);
            
            // Test 3: Verify Entity has TerrainData
            Debug.Log("Test 3: Verifying Entity data...");
            if (entityManager.HasComponent<DOTS.Terrain.TerrainData>(testEntity))
            {
                var retrievedData = entityManager.GetComponentData<DOTS.Terrain.TerrainData>(testEntity);
                Debug.Log($"✓ Entity has TerrainData: Position={retrievedData.chunkPosition}, Resolution={retrievedData.resolution}");
            }
            else
            {
                throw new System.Exception("Entity does not have TerrainData component");
            }
            
            // Test 4: Test ComputeBuffer Manager
            Debug.Log("Test 4: Testing ComputeBuffer Manager...");
            var bufferManager = Object.FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager == null)
            {
                // Create buffer manager if it doesn't exist
                var go = new GameObject("TerrainComputeBufferManager");
                bufferManager = go.AddComponent<TerrainComputeBufferManager>();
            }
            
            var heightBuffer = bufferManager.GetHeightBuffer(chunkPosition, testResolution);
            var biomeBuffer = bufferManager.GetBiomeBuffer(chunkPosition, testResolution);
            
            Debug.Log($"✓ ComputeBuffers created: Height={heightBuffer.count}, Biome={biomeBuffer.count}");
            
            // Test 5: Test Modification System
            Debug.Log("Test 5: Testing Modification System...");
            var modification = new TerrainModification
            {
                position = new int2(10, 10),
                originalHeight = 0.5f,
                newHeight = 0.7f,
                modificationTime = Time.time,
                type = ModificationType.PlayerDig
            };
            
            var updatedModifications = TerrainDataBuilder.AddModification(
                testTerrainData.modifications, 
                modification
            );
            
            Debug.Log($"✓ Modification added successfully");
            
            // Test 6: Test Height Data Update
            Debug.Log("Test 6: Testing Height Data Update...");
            var testHeights = new NativeArray<float>(testResolution * testResolution, Allocator.TempJob);
            for (int i = 0; i < testHeights.Length; i++)
            {
                testHeights[i] = UnityEngine.Random.Range(0f, 1f);
            }
            
            var updatedHeightData = TerrainDataBuilder.UpdateHeightData(testHeights, testResolution);
            Debug.Log($"✓ Height data updated: {updatedHeightData.Value.heights.Length} values");
            
            // Cleanup
            testHeights.Dispose();
            
            // All tests passed
            testPassed = true;
            testMessage = "All tests passed successfully!";
            Debug.Log("=== TerrainData Test PASSED ===");
            
        }
        catch (System.Exception e)
        {
            testPassed = false;
            testMessage = $"Test failed: {e.Message}";
            Debug.LogError($"=== TerrainData Test FAILED: {e.Message} ===");
        }
    }
    
    void OnDestroy()
    {
        // Dispose blob assets to prevent leaks
        if (testTerrainData.heightData.IsCreated)
        {
            testTerrainData.heightData.Dispose();
        }
        if (testTerrainData.modifications.IsCreated)
        {
            testTerrainData.modifications.Dispose();
        }
    }
    
    void OnGUI()
    {
        // Display test results in scene view
        if (Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"TerrainData Test: {(testPassed ? "PASSED" : "FAILED")}");
            GUILayout.Label(testMessage);
            GUILayout.EndArea();
        }
    }
} 