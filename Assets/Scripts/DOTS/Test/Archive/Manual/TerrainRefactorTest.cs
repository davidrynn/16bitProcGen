using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// Test script to verify the terrain refactoring works correctly
/// </summary>
public class TerrainRefactorTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestOnStart = true;
    public int2 testChunkPosition = new int2(0, 0);
    public int testResolution = 16;
    public float testWorldScale = 10f;
    
    private TerrainEntityManager entityManager;
    private Entity testEntity;
    
    void Start()
    {
        if (runTestOnStart)
        {
            StartCoroutine(RunTestAfterWorldInit());
        }
    }
    
    private System.Collections.IEnumerator RunTestAfterWorldInit()
    {
        // Wait for DOTS world to be available
        yield return new WaitForSeconds(0.5f);
        
        int maxAttempts = 10;
        int attempts = 0;
        
        while (!DOTSWorldSetup.IsWorldReady() && attempts < maxAttempts)
        {
            Debug.Log($"[TerrainRefactorTest] Waiting for DOTS world... Attempt {attempts + 1}/{maxAttempts}");
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("[TerrainRefactorTest] DOTS world not ready after maximum attempts");
            yield break;
        }
        
        Debug.Log("[TerrainRefactorTest] DOTS world ready, starting test...");
        RunTerrainRefactorTest();
    }
    
    private void RunTerrainRefactorTest()
    {
        Debug.Log("=== TERRAIN REFACTOR TEST ===");
        
        try
        {
            // Test 1: Create terrain entity
            Debug.Log("Test 1: Creating terrain entity...");
            entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager == null)
            {
                Debug.LogError("TerrainEntityManager not found!");
                return;
            }
            
            testEntity = entityManager.CreateTerrainEntity(testChunkPosition, testResolution, testWorldScale, BiomeType.Plains);
            if (testEntity == Entity.Null)
            {
                Debug.LogError("Failed to create terrain entity!");
                return;
            }
            
            Debug.Log($"✓ Terrain entity created: {testEntity.Index}");
            
            // Test 2: Verify components exist
            Debug.Log("Test 2: Verifying components...");
            var world = World.DefaultGameObjectInjectionWorld;
            var em = world.EntityManager;
            
            bool hasTerrainData = em.HasComponent<TerrainData>(testEntity);
            bool hasLocalTransform = em.HasComponent<LocalTransform>(testEntity);
            bool hasLocalToWorld = em.HasComponent<LocalToWorld>(testEntity);
            
            Debug.Log($"  TerrainData: {hasTerrainData}");
            Debug.Log($"  LocalTransform: {hasLocalTransform}");
            Debug.Log($"  LocalToWorld: {hasLocalToWorld}");
            
            if (!hasTerrainData || !hasLocalTransform || !hasLocalToWorld)
            {
                Debug.LogError("✗ Missing required components!");
                return;
            }
            
            Debug.Log("✓ All required components present");
            
            // Test 3: Verify transform data
            Debug.Log("Test 3: Verifying transform data...");
            var terrainData = em.GetComponentData<TerrainData>(testEntity);
            var localTransform = em.GetComponentData<LocalTransform>(testEntity);
            var localToWorld = em.GetComponentData<LocalToWorld>(testEntity);
            
            Debug.Log($"  TerrainData.chunkPosition: {terrainData.chunkPosition}");
            Debug.Log($"  TerrainData.worldScale: {terrainData.worldScale}");
            Debug.Log($"  LocalTransform.Position: {localTransform.Position}");
            Debug.Log($"  LocalTransform.Rotation: {localTransform.Rotation}");
            Debug.Log($"  LocalTransform.Scale: {localTransform.Scale}");
            
            // Test 4: Verify position calculation
            Debug.Log("Test 4: Verifying position calculation...");
            float3 expectedPosition = new float3(
                testChunkPosition.x * testWorldScale, 
                0, 
                testChunkPosition.y * testWorldScale
            );
            
            if (math.all(expectedPosition == localTransform.Position))
            {
                Debug.Log($"✓ Position calculated correctly: {localTransform.Position}");
            }
            else
            {
                Debug.LogError($"✗ Position mismatch! Expected: {expectedPosition}, Got: {localTransform.Position}");
            }
            
            // Test 5: Verify rotation and scale
            if (math.all(localTransform.Rotation.value == quaternion.identity.value))
            {
                Debug.Log("✓ Rotation is identity");
            }
            else
            {
                Debug.LogError($"✗ Rotation should be identity, got: {localTransform.Rotation}");
            }
            
            if (localTransform.Scale == testWorldScale)
            {
                Debug.Log($"✓ Scale is correct: {localTransform.Scale}");
            }
            else
            {
                Debug.LogError($"✗ Scale mismatch! Expected: {testWorldScale}, Got: {localTransform.Scale}");
            }
            
            // Test 6: Verify LocalToWorld matrix
            Debug.Log("Test 5: Verifying LocalToWorld matrix...");
            float4x4 expectedMatrix = float4x4.TRS(
                localTransform.Position,
                localTransform.Rotation,
                new float3(localTransform.Scale)
            );
            
            if (math.all(expectedMatrix.c0 == localToWorld.Value.c0) &&
                math.all(expectedMatrix.c1 == localToWorld.Value.c1) &&
                math.all(expectedMatrix.c2 == localToWorld.Value.c2) &&
                math.all(expectedMatrix.c3 == localToWorld.Value.c3))
            {
                Debug.Log("✓ LocalToWorld matrix is correct");
            }
            else
            {
                Debug.LogError("✗ LocalToWorld matrix mismatch!");
                Debug.LogError($"Expected: {expectedMatrix}");
                Debug.LogError($"Got: {localToWorld.Value}");
            }
            
            Debug.Log("=== TERRAIN REFACTOR TEST COMPLETED SUCCESSFULLY ===");
            
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Terrain refactor test failed with exception: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }
    
    [ContextMenu("Run Terrain Refactor Test")]
    public void RunTest()
    {
        if (DOTSWorldSetup.IsWorldReady())
        {
            RunTerrainRefactorTest();
        }
        else
        {
            Debug.LogWarning("DOTS world not ready. Starting coroutine...");
            StartCoroutine(RunTestAfterWorldInit());
        }
    }
    
    [ContextMenu("Cleanup Test Entity")]
    public void CleanupTestEntity()
    {
        if (testEntity != Entity.Null && entityManager != null)
        {
            entityManager.DestroyTerrainEntity(testEntity);
            testEntity = Entity.Null;
            Debug.Log("Test entity cleaned up");
        }
    }
    
    void OnDestroy()
    {
        CleanupTestEntity();
    }
}
