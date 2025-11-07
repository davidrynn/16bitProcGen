using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain;
using DOTS.Terrain.Modification;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// Test script to verify glob physics system functionality
/// </summary>
public class GlobPhysicsTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTestOnStart = true;
    public int testGlobCount = 5;
    public float testWorldScale = 10f;
    
    [Header("Glob Creation")]
    public float3 testPosition = new float3(10, 5, 20);
    public float testRadius = 2f;
    public GlobRemovalType testGlobType = GlobRemovalType.Medium;
    public TerrainType testTerrainType = TerrainType.Rock;
    
    [Header("Physics Testing")]
    public bool enablePhysicsTests = true;
    public bool logPhysicsStats = true;
    public float physicsTestDuration = 10f;
    
    private Entity[] testGlobs;
    private float testStartTime;
    private bool testRunning = false;
    private World dotsWorld;
    
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
        
        // Check if world is ready
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("DOTS world not available - cannot run glob physics test");
            yield break;
        }
        
        Debug.Log("DOTS world is available - running glob physics test");
        RunGlobPhysicsTest();
    }
    
    [ContextMenu("Run Glob Physics Test")]
    public void RunGlobPhysicsTest()
    {
        Debug.Log("=== GLOB PHYSICS TEST ===");
        
        // Check if DOTS world is available
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("DOTS world not available - cannot run test. Try running in Play Mode.");
            return;
        }
        
        SetupTestEnvironment();
        CreateTestGlobs();
        StartPhysicsTest();
        
        Debug.Log("=== GLOB PHYSICS TEST STARTED ===");
    }
    
    private void SetupTestEnvironment()
    {
        Debug.Log("Setting up glob physics test environment...");
        
        // Get the glob physics system
        dotsWorld = DOTSWorldSetup.GetWorld();

        if (dotsWorld == null)
        {
            Debug.LogError("Failed to get DOTS World for glob physics testing");
            return;
        }

        Debug.Log("✓ DOTS world found");
    }
    
    private void CreateTestGlobs()
    {
        Debug.Log($"Creating {testGlobCount} test globs...");
        
        testGlobs = new Entity[testGlobCount];
        
        for (int i = 0; i < testGlobCount; i++)
        {
            // Vary the position for each glob
            float3 position = testPosition + new float3(i * 3f, 5f + i * 2f, i * 2f);
            
            // Vary the glob type
            GlobRemovalType globType = (GlobRemovalType)(i % 3);
            
            // Vary the terrain type
            TerrainType terrainType = (TerrainType)((i % 5) + 1); // Skip Water (0)
            
            // Create the glob
            testGlobs[i] = TerrainGlobPhysicsSystem.CreateTerrainGlob(
                dotsWorld.EntityManager,
                position,
                testRadius + i * 0.5f,
                globType,
                terrainType);
            
            if (testGlobs[i] != Entity.Null)
            {
                Debug.Log($"✓ Created test glob {i} at {position} with type {globType} and terrain {terrainType}");
            }
            else
            {
                Debug.LogError($"✗ Failed to create test glob {i}");
            }
        }
    }
    
    private void StartPhysicsTest()
    {
        if (!enablePhysicsTests) return;
        
        testStartTime = Time.time;
        testRunning = true;
        
        Debug.Log($"Starting physics test for {physicsTestDuration} seconds...");
        Debug.Log("Globs should fall, bounce, and roll based on physics settings");
    }
    
    void Update()
    {
        if (!testRunning) return;
        
        // Log physics stats periodically
        if (logPhysicsStats && Time.time % 2f < Time.deltaTime)
        {
            LogPhysicsStats();
        }
        
        // Check if test duration has elapsed
        if (Time.time - testStartTime > physicsTestDuration)
        {
            EndPhysicsTest();
        }
    }
    
    private void LogPhysicsStats()
    {
    if (!TryGetGlobPhysicsSystem(out var stats)) return;

    Debug.Log($"[GlobPhysicsTest] Active globs: {stats.activeGlobs}, Grounded: {stats.groundedGlobs}");
    }
    
    private void EndPhysicsTest()
    {
        testRunning = false;
        Debug.Log("=== GLOB PHYSICS TEST COMPLETE ===");
        
        // Log final stats
        if (TryGetGlobPhysicsSystem(out var stats))
        {
            Debug.Log($"Final Stats - Active globs: {stats.activeGlobs}, Grounded: {stats.groundedGlobs}");
        }
        
        // Clean up test globs
        CleanupTestGlobs();
    }
    
    private void CleanupTestGlobs()
    {
        Debug.Log("Cleaning up test globs...");
        
        if (testGlobs == null) return;
        
        var world = DOTSWorldSetup.GetWorld();
        if (world == null) return;
        
        var entityManager = world.EntityManager;
        int cleanedCount = 0;
        
        foreach (var glob in testGlobs)
        {
            if (glob != Entity.Null && entityManager.Exists(glob))
            {
                // Mark glob for destruction
                if (entityManager.HasComponent<TerrainGlobComponent>(glob))
                {
                    var globComponent = entityManager.GetComponentData<TerrainGlobComponent>(glob);
                    globComponent.isDestroyed = true;
                    entityManager.SetComponentData(glob, globComponent);
                    cleanedCount++;
                }
            }
        }
        
        Debug.Log($"✓ Marked {cleanedCount} globs for destruction");
    }
    
    [ContextMenu("Create Single Test Glob")]
    public void CreateSingleTestGlob()
    {
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("DOTS world not available");
            return;
        }
        
        var world = DOTSWorldSetup.GetWorld();
        var globEntity = TerrainGlobPhysicsSystem.CreateTerrainGlob(
            world.EntityManager,
            testPosition,
            testRadius,
            testGlobType,
            testTerrainType);
        
        if (globEntity != Entity.Null)
        {
            Debug.Log($"✓ Created single test glob at {testPosition}");
        }
        else
        {
            Debug.LogError("✗ Failed to create single test glob");
        }
    }
    
    [ContextMenu("Check Glob Components")]
    public void CheckGlobComponents()
    {
        Debug.Log("=== CHECKING GLOB COMPONENTS ===");
        
        var world = DOTSWorldSetup.GetWorld();
        if (world == null)
        {
            Debug.LogError("DOTS world not available");
            return;
        }
        
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(TerrainGlobComponent));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        Debug.Log($"Found {entities.Length} glob entities");
        
        foreach (var entity in entities)
        {
            var globComponent = entityManager.GetComponentData<TerrainGlobComponent>(entity);
            var hasPhysics = entityManager.HasComponent<TerrainGlobPhysicsComponent>(entity);
            var hasRender = entityManager.HasComponent<TerrainGlobRenderComponent>(entity);
            var hasTransform = entityManager.HasComponent<LocalTransform>(entity);
            
            Debug.Log($"Entity {entity.Index}: Pos={globComponent.currentPosition}, Type={globComponent.terrainType}, " +
                     $"Physics={hasPhysics}, Render={hasRender}, Transform={hasTransform}");
        }
        
        entities.Dispose();
    }
    
    [ContextMenu("Force Glob Physics Update")]
    public void ForceGlobPhysicsUpdate()
    {
        Debug.Log("Forcing glob physics update...");
        
        var world = DOTSWorldSetup.GetWorld();
        if (world == null)
        {
            Debug.LogError("DOTS world not available");
            return;
        }
        
        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(TerrainGlobComponent), typeof(TerrainGlobPhysicsComponent));
        var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        
        foreach (var entity in entities)
        {
            var globComponent = entityManager.GetComponentData<TerrainGlobComponent>(entity);
            var physicsComponent = entityManager.GetComponentData<TerrainGlobPhysicsComponent>(entity);
            
            // Add some random velocity to make globs move
            globComponent.velocity += new float3(
                UnityEngine.Random.Range(-2f, 2f),
                0f,
                UnityEngine.Random.Range(-2f, 2f)
            );
            
            entityManager.SetComponentData(entity, globComponent);
        }
        
        entities.Dispose();
        Debug.Log($"Applied random velocity to {entities.Length} globs");
    }

    private bool TryGetGlobPhysicsSystem(out (int activeGlobs, int groundedGlobs, float lastUpdateTime) stats)
    {
        if (!DOTSWorldSetup.IsWorldReady())
        {
            stats = default;
            return false;
        }

        stats = TerrainGlobPhysicsSystem.GetPerformanceStats();
        return true;
    }
} 