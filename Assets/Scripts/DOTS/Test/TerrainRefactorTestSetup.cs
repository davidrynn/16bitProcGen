using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// Simple setup script to create the necessary components for testing the terrain refactor
/// </summary>
public class TerrainRefactorTestSetup : MonoBehaviour
{
    [Header("Test Setup")]
    public bool setupOnStart = true;
    public bool createTerrainEntityManager = true;
    public bool createDOTSWorldSetup = true;
    
    void Start()
    {
        if (setupOnStart)
        {
            SetupTestEnvironment();
        }
    }
    
    [ContextMenu("Setup Test Environment")]
    public void SetupTestEnvironment()
    {
        Debug.Log("[TerrainRefactorTestSetup] Setting up test environment...");
        
        if (createTerrainEntityManager)
        {
            var existingManager = FindFirstObjectByType<TerrainEntityManager>();
            if (existingManager == null)
            {
                var managerGO = new GameObject("TerrainEntityManager");
                managerGO.AddComponent<TerrainEntityManager>();
                Debug.Log("✓ Created TerrainEntityManager");
            }
            else
            {
                Debug.Log("✓ TerrainEntityManager already exists");
            }
        }
        
        if (createDOTSWorldSetup)
        {
            var existingSetup = FindFirstObjectByType<DOTSWorldSetup>();
            if (existingSetup == null)
            {
                var setupGO = new GameObject("DOTSWorldSetup");
                setupGO.AddComponent<DOTSWorldSetup>();
                Debug.Log("✓ Created DOTSWorldSetup");
            }
            else
            {
                Debug.Log("✓ DOTSWorldSetup already exists");
            }
        }
        
        // Add the test script to this GameObject
        if (GetComponent<TerrainRefactorTest>() == null)
        {
            gameObject.AddComponent<TerrainRefactorTest>();
            Debug.Log("✓ Added TerrainRefactorTest component");
        }
        
        Debug.Log("[TerrainRefactorTestSetup] Test environment setup complete!");
        Debug.Log("The TerrainRefactorTest will run automatically, or use the context menu to run it manually.");
    }
    
    [ContextMenu("Run All Tests")]
    public void RunAllTests()
    {
        var refactorTest = GetComponent<TerrainRefactorTest>();
        if (refactorTest != null)
        {
            refactorTest.RunTest();
        }
        
        var integrationTest = FindFirstObjectByType<TransformIntegrationTest>();
        if (integrationTest != null)
        {
            integrationTest.CheckCurrentTransforms();
        }
    }
}
