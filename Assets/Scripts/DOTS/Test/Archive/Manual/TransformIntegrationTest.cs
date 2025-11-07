using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// Test script to verify Unity.Transforms integration with terrain system
/// </summary>
public class TransformIntegrationTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestOnStart = true;
    public int testChunkCount = 3;
    public float testWorldScale = 10f;
    
    [Header("Transform Verification")]
    public bool verifyTransforms = true;
    public bool logTransformUpdates = true;
    
    [Header("World Initialization")]
    public float worldInitDelay = 0.5f; // Delay to wait for DOTS world initialization
    
    private TerrainEntityManager entityManager;
    private Entity[] testEntities;
    
    void Start()
    {
        if (runTestOnStart)
        {
            // Wait for DOTS world to be available
            StartCoroutine(RunTestAfterWorldInit());
        }
    }
    
    private System.Collections.IEnumerator RunTestAfterWorldInit()
    {
        // Wait for DOTS world to be available
        yield return new WaitForSeconds(worldInitDelay);
        
        // Use DOTSWorldSetup to check world availability
        int maxAttempts = 10;
        int attempts = 0;
        
        while (!DOTSWorldSetup.IsWorldReady() && attempts < maxAttempts)
        {
            Debug.Log($"Waiting for DOTS world... Attempt {attempts + 1}/{maxAttempts}");
            yield return new WaitForSeconds(0.1f);
            attempts++;
        }
        
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("DOTS world not available after waiting - test cannot proceed");
            Debug.LogError("Make sure you're in Play Mode and DOTS packages are properly installed");
            yield break;
        }
        
        Debug.Log("DOTS world is available - running transform integration test");
        RunTransformIntegrationTest();
    }
    
    [ContextMenu("Run Transform Integration Test")]
    public void RunTransformIntegrationTest()
    {
        Debug.Log("=== TRANSFORM INTEGRATION TEST ===");
        
        // Check if DOTS world is available
        if (World.DefaultGameObjectInjectionWorld == null)
        {
            Debug.LogError("DOTS world not available - cannot run test. Try running in Play Mode.");
            return;
        }
        
        SetupTestEnvironment();
        CreateTestEntities();
        VerifyTransformComponents();
        MonitorTransformUpdates();
        
        Debug.Log("=== TRANSFORM INTEGRATION TEST COMPLETE ===");
    }
    
    private void SetupTestEnvironment()
    {
        Debug.Log("Setting up test environment...");
        
        // Find or create TerrainEntityManager
        entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            Debug.Log("Creating TerrainEntityManager for transform test...");
            var go = new GameObject("TransformTestEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
        }
        
        Debug.Log("✓ Test environment setup complete");
    }
    
    private void CreateTestEntities()
    {
        Debug.Log($"Creating {testChunkCount} test entities...");
        
        testEntities = new Entity[testChunkCount];
        
        for (int i = 0; i < testChunkCount; i++)
        {
            int2 chunkPosition = new int2(i, 0);
            testEntities[i] = entityManager.CreateTerrainEntity(
                chunkPosition,
                32, // resolution
                testWorldScale,
                BiomeType.Plains
            );
            
            if (testEntities[i] != Entity.Null)
            {
                Debug.Log($"✓ Created test entity {i} at chunk position {chunkPosition}");
            }
            else
            {
                Debug.LogError($"✗ Failed to create test entity {i}");
            }
        }
    }
    
    private void VerifyTransformComponents()
    {
        if (!verifyTransforms) return;
        
        Debug.Log("Verifying transform components...");
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("No DOTS world found for transform verification");
            return;
        }
        
        var entityManager = world.EntityManager;
        
        foreach (var entity in testEntities)
        {
            if (entity == Entity.Null) continue;
            
            // Check if entity has required components
            bool hasTerrainData = entityManager.HasComponent<TerrainData>(entity);
            bool hasLocalTransform = entityManager.HasComponent<LocalTransform>(entity);
            bool hasLocalToWorld = entityManager.HasComponent<LocalToWorld>(entity);
            
            Debug.Log($"Entity {entity.Index}: TerrainData={hasTerrainData}, LocalTransform={hasLocalTransform}, LocalToWorld={hasLocalToWorld}");
            
            if (hasTerrainData && hasLocalTransform && hasLocalToWorld)
            {
                // Get component data
                var terrainData = entityManager.GetComponentData<TerrainData>(entity);
                var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                var localToWorld = entityManager.GetComponentData<LocalToWorld>(entity);
                
                Debug.Log($"  TerrainData.chunkPosition: {terrainData.chunkPosition}");
                Debug.Log($"  LocalTransform.Position: {localTransform.Position}");
                Debug.Log($"  LocalTransform.Rotation: {localTransform.Rotation}");
                Debug.Log($"  LocalTransform.Scale: {localTransform.Scale}");
                Debug.Log($"  LocalToWorld matrix: {localToWorld.Value}");
                
                // Verify positions are calculated correctly from chunk position
                float3 expectedPosition = new float3(terrainData.chunkPosition.x * terrainData.worldScale, 0, terrainData.chunkPosition.y * terrainData.worldScale);
                if (math.all(expectedPosition == localTransform.Position))
                {
                    Debug.Log($"  ✓ Position calculated correctly for entity {entity.Index}");
                }
                else
                {
                    Debug.LogWarning($"  ✗ Position mismatch for entity {entity.Index}: Expected={expectedPosition}, LocalTransform={localTransform.Position}");
                }
            }
            else
            {
                Debug.LogError($"  ✗ Missing required components for entity {entity.Index}");
            }
        }
    }
    
    private void MonitorTransformUpdates()
    {
        if (!logTransformUpdates) return;
        
        Debug.Log("Transform components are now managed directly by TerrainEntityManager");
        Debug.Log("No separate transform synchronization system needed");
    }
    
    [ContextMenu("Check Current Transforms")]
    public void CheckCurrentTransforms()
    {
        Debug.Log("=== CURRENT TRANSFORM STATUS ===");
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("No DOTS world found");
            return;
        }
        
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(TerrainData), typeof(LocalTransform));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        Debug.Log($"Found {entities.Length} entities with TerrainData and LocalTransform");
        
        foreach (var entity in entities)
        {
            var terrainData = entityManager.GetComponentData<TerrainData>(entity);
            var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
            
            Debug.Log($"Entity {entity.Index}: Chunk={terrainData.chunkPosition}, TransformPos={localTransform.Position}, AvgHeight={terrainData.averageHeight}");
        }
        
        entities.Dispose();
    }
    
    [ContextMenu("Force Transform Update")]
    public void ForceTransformUpdate()
    {
        Debug.Log("Forcing transform update...");
        
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null)
        {
            Debug.LogError("No DOTS world found");
            return;
        }
        
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(TerrainData), typeof(LocalTransform));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        foreach (var entity in entities)
        {
            var terrainData = entityManager.GetComponentData<TerrainData>(entity);
            
            // Modify average height to test terrain data changes
            terrainData.averageHeight += 1f;
            entityManager.SetComponentData(entity, terrainData);
            
            Debug.Log($"Modified average height for entity {entity.Index} to {terrainData.averageHeight}");
        }
        
        entities.Dispose();
    }
} 