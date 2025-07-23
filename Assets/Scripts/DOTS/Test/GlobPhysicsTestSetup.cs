using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;
using DOTS.Terrain.Modification;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// Setup script to configure the test environment for glob physics testing
/// </summary>
public class GlobPhysicsTestSetup : MonoBehaviour
{
    [Header("Setup Settings")]
    public bool runSetupOnStart = true;
    public bool createTerrainEntities = true;
    public bool createManagers = true;
    
    [Header("Terrain Setup")]
    public int terrainGridSize = 3;
    public int terrainResolution = 32;
    public float terrainWorldScale = 10f;
    public float3 terrainCenter = new float3(0, 0, 0);
    
    [Header("Manager Setup")]
    public bool createComputeShaderManager = true;
    public bool createBufferManager = true;
    
    void Start()
    {
        if (runSetupOnStart)
        {
            StartCoroutine(SetupAfterWorldInit());
        }
    }
    
    private System.Collections.IEnumerator SetupAfterWorldInit()
    {
        // Wait for DOTS world to be available
        yield return new WaitForSeconds(0.5f);
        
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("DOTS world not available - cannot run setup");
            yield break;
        }
        
        Debug.Log("DOTS world is available - running glob physics test setup");
        RunSetup();
    }
    
    [ContextMenu("Run Glob Physics Test Setup")]
    public void RunSetup()
    {
        Debug.Log("=== GLOB PHYSICS TEST SETUP ===");
        
        if (!DOTSWorldSetup.IsWorldReady())
        {
            Debug.LogError("DOTS world not available - cannot run setup. Try running in Play Mode.");
            return;
        }
        
        if (createManagers)
        {
            SetupManagers();
        }
        
        if (createTerrainEntities)
        {
            SetupTerrainEntities();
        }
        
        Debug.Log("=== GLOB PHYSICS TEST SETUP COMPLETE ===");
    }
    
    private void SetupManagers()
    {
        Debug.Log("Setting up managers...");
        
        // Create ComputeShaderManager if needed
        if (createComputeShaderManager)
        {
            try
            {
                var computeManager = ComputeShaderManager.Instance;
                if (computeManager != null)
                {
                    Debug.Log("✓ ComputeShaderManager already exists");
                }
                else
                {
                    Debug.LogWarning("ComputeShaderManager.Instance returned null - may need initialization");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"ComputeShaderManager not available: {e.Message}");
                Debug.Log("Note: ComputeShaderManager is a DOTS system that initializes automatically");
            }
        }
        
        // Create TerrainComputeBufferManager if needed
        if (createBufferManager)
        {
            var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager == null)
            {
                var bufferManagerGO = new GameObject("TerrainComputeBufferManager");
                bufferManager = bufferManagerGO.AddComponent<TerrainComputeBufferManager>();
                Debug.Log("✓ Created TerrainComputeBufferManager");
            }
            else
            {
                Debug.Log("✓ TerrainComputeBufferManager already exists");
            }
        }
    }
    
    private void SetupTerrainEntities()
    {
        Debug.Log($"Creating {terrainGridSize}x{terrainGridSize} terrain grid...");
        
        var world = DOTSWorldSetup.GetWorld();
        if (world == null)
        {
            Debug.LogError("Cannot get DOTS world for terrain setup");
            return;
        }
        
        var entityManager = world.EntityManager;
        var terrainManager = FindFirstObjectByType<TerrainEntityManager>();
        
        if (terrainManager == null)
        {
            Debug.LogError("TerrainEntityManager not found in scene - creating one");
            var terrainManagerGO = new GameObject("TerrainEntityManager");
            terrainManager = terrainManagerGO.AddComponent<TerrainEntityManager>();
        }
        
        int entitiesCreated = 0;
        
        // Create a grid of terrain entities
        for (int x = -terrainGridSize/2; x <= terrainGridSize/2; x++)
        {
            for (int z = -terrainGridSize/2; z <= terrainGridSize/2; z++)
            {
                var chunkPosition = new int2(x, z);
                var worldPos = terrainCenter + new float3(x * terrainWorldScale, 0, z * terrainWorldScale);
                
                var entity = terrainManager.CreateTerrainEntity(
                    chunkPosition,
                    terrainResolution,
                    terrainWorldScale,
                    BiomeType.Plains // Default biome for testing
                );
                
                if (entity != Entity.Null)
                {
                    entitiesCreated++;
                    Debug.Log($"✓ Created terrain entity at chunk {chunkPosition} (world pos: {worldPos})");
                }
                else
                {
                    Debug.LogError($"✗ Failed to create terrain entity at chunk {chunkPosition}");
                }
            }
        }
        
        Debug.Log($"✓ Created {entitiesCreated} terrain entities");
    }
    
    [ContextMenu("Check Setup Status")]
    public void CheckSetupStatus()
    {
        Debug.Log("=== CHECKING SETUP STATUS ===");
        
        // Check managers
        bool computeManagerExists = false;
        try
        {
            var computeManager = ComputeShaderManager.Instance;
            computeManagerExists = computeManager != null;
        }
        catch
        {
            computeManagerExists = false;
        }
        
        var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
        
        Debug.Log($"ComputeShaderManager: {(computeManagerExists ? "✓" : "✗")}");
        Debug.Log($"TerrainComputeBufferManager: {(bufferManager != null ? "✓" : "✗")}");
        
        // Check DOTS world
        if (DOTSWorldSetup.IsWorldReady())
        {
            var world = DOTSWorldSetup.GetWorld();
            var entityManager = world.EntityManager;
            
            // Count terrain entities
            var terrainQuery = entityManager.CreateEntityQuery(typeof(TerrainData));
            var terrainCount = terrainQuery.CalculateEntityCount();
            
            // Count glob entities
            var globQuery = entityManager.CreateEntityQuery(typeof(TerrainGlobComponent));
            var globCount = globQuery.CalculateEntityCount();
            
            Debug.Log($"DOTS World: ✓");
            Debug.Log($"Terrain Entities: {terrainCount}");
            Debug.Log($"Glob Entities: {globCount}");
            
            // Check systems
            var terrainSystem = world.GetOrCreateSystemManaged<TerrainSystem>();
            var globPhysicsSystem = world.GetOrCreateSystemManaged<TerrainGlobPhysicsSystem>();
            var modificationSystem = world.GetOrCreateSystemManaged<TerrainModificationSystem>();
            
            Debug.Log($"TerrainSystem: {(terrainSystem != null ? "✓" : "✗")}");
            Debug.Log($"TerrainGlobPhysicsSystem: {(globPhysicsSystem != null ? "✓" : "✗")}");
            Debug.Log($"TerrainModificationSystem: {(modificationSystem != null ? "✓" : "✗")}");
        }
        else
        {
            Debug.LogError("DOTS World: ✗ (not available)");
        }
    }
    
    [ContextMenu("Clean Up Test Environment")]
    public void CleanUpTestEnvironment()
    {
        Debug.Log("=== CLEANING UP TEST ENVIRONMENT ===");
        
        // Clean up glob entities
        if (DOTSWorldSetup.IsWorldReady())
        {
            var world = DOTSWorldSetup.GetWorld();
            var entityManager = world.EntityManager;
            
            var globQuery = entityManager.CreateEntityQuery(typeof(TerrainGlobComponent));
            var globEntities = globQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            foreach (var entity in globEntities)
            {
                if (entityManager.HasComponent<TerrainGlobComponent>(entity))
                {
                    var globComponent = entityManager.GetComponentData<TerrainGlobComponent>(entity);
                    globComponent.isDestroyed = true;
                    entityManager.SetComponentData(entity, globComponent);
                }
            }
            
            globEntities.Dispose();
            Debug.Log($"✓ Marked {globEntities.Length} glob entities for destruction");
        }
        
        // Clean up managers (optional)
        // Note: ComputeShaderManager is a DOTS system, not a MonoBehaviour, so it can't be destroyed this way
        Debug.Log("Note: ComputeShaderManager is a DOTS system that manages itself");
        
        var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
        if (bufferManager != null)
        {
            DestroyImmediate(bufferManager.gameObject);
            Debug.Log("✓ Destroyed TerrainComputeBufferManager");
        }
        
        Debug.Log("=== CLEANUP COMPLETE ===");
    }
} 