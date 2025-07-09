using UnityEngine;
using Unity.Entities;

/// <summary>
/// Automatically sets up the test environment for terrain generation
/// This script will create all necessary components for testing
/// </summary>
public class AutoTestSetup : MonoBehaviour
{
    [Header("Auto Setup Settings")]
    public bool setupOnStart = true;
    public bool createTestEntities = true;
    public int numberOfTestEntities = 5;
    
    private void Start()
    {
        if (setupOnStart)
        {
            SetupTestEnvironment();
        }
    }
    
    /// <summary>
    /// Sets up the complete test environment
    /// </summary>
    [ContextMenu("Setup Test Environment")]
    public void SetupTestEnvironment()
    {
        Debug.Log("=== AUTO TEST SETUP ===");
        
        // Step 1: Ensure ComputeShaderManager is initialized
        SetupComputeShaderManager();
        
        // Step 2: Ensure TerrainEntityManager exists
        SetupTerrainEntityManager();
        
        // Step 3: Create test entities if requested
        if (createTestEntities)
        {
            SetupTestEntities();
        }
        
        Debug.Log("=== AUTO TEST SETUP COMPLETE ===");
    }
    
    /// <summary>
    /// Sets up the ComputeShaderManager
    /// </summary>
    private void SetupComputeShaderManager()
    {
        Debug.Log("Setting up ComputeShaderManager...");
        
        try
        {
            var computeManager = ComputeShaderManager.Instance;
            if (computeManager != null)
            {
                Debug.Log("✓ ComputeShaderManager initialized successfully");
            }
            else
            {
                Debug.LogError("✗ Failed to initialize ComputeShaderManager");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"✗ ComputeShaderManager setup failed: {e.Message}");
        }
    }
    
    /// <summary>
    /// Sets up the TerrainEntityManager
    /// </summary>
    private void SetupTerrainEntityManager()
    {
        Debug.Log("Setting up TerrainEntityManager...");
        
        var entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            var go = new GameObject("TerrainEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
            Debug.Log("✓ Created TerrainEntityManager");
        }
        else
        {
            Debug.Log("✓ TerrainEntityManager already exists");
        }
    }
    
    /// <summary>
    /// Sets up test terrain entities
    /// </summary>
    private void SetupTestEntities()
    {
        Debug.Log("Setting up test entities...");
        
        var entityCreator = FindFirstObjectByType<QuickTerrainEntityCreator>();
        if (entityCreator == null)
        {
            var go = new GameObject("QuickTerrainEntityCreator");
            entityCreator = go.AddComponent<QuickTerrainEntityCreator>();
            entityCreator.numberOfEntities = numberOfTestEntities;
            Debug.Log("✓ Created QuickTerrainEntityCreator");
        }
        else
        {
            Debug.Log("✓ QuickTerrainEntityCreator already exists");
        }
        
        // Trigger entity creation
        entityCreator.CreateTerrainEntities();
    }
    
    /// <summary>
    /// Gets the current test status
    /// </summary>
    [ContextMenu("Get Test Status")]
    public void GetTestStatus()
    {
        Debug.Log("=== TEST STATUS ===");
        
        // Check ComputeShaderManager
        ComputeShaderManager computeManager = null;
        try
        {
            computeManager = ComputeShaderManager.Instance;
        }
        catch (System.Exception) { }
        
        Debug.Log($"ComputeShaderManager: {(computeManager != null ? "✓ Ready" : "✗ Missing")}");
        
        // Check TerrainEntityManager
        var entityManager = FindFirstObjectByType<TerrainEntityManager>();
        Debug.Log($"TerrainEntityManager: {(entityManager != null ? "✓ Ready" : "✗ Missing")}");
        
        // Check entity count
        if (entityManager != null)
        {
            var entityCount = entityManager.GetTerrainEntityCount();
            Debug.Log($"Terrain Entities: {entityCount}");
        }
        
        // Check DOTS World
        var world = World.DefaultGameObjectInjectionWorld;
        Debug.Log($"DOTS World: {(world != null ? "✓ Ready" : "✗ Missing")}");
        
        Debug.Log("=== STATUS COMPLETE ===");
    }
} 