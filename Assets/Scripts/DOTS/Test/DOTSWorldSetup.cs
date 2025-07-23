using Unity.Entities;
using UnityEngine;

/// <summary>
/// Simple setup script to ensure DOTS world is properly initialized
/// Can be used by other test scripts to ensure world is ready
/// </summary>
public class DOTSWorldSetup : MonoBehaviour
{
    [Header("Setup Settings")]
    public bool setupOnStart = true;
    public bool logSetupStatus = true;
    
    [Header("World Status")]
    [SerializeField] private string worldStatus = "Not initialized";
    
    private void Start()
    {
        if (setupOnStart)
        {
            SetupDOTSWorld();
        }
    }
    
    [ContextMenu("Setup DOTS World")]
    public void SetupDOTSWorld()
    {
        Debug.Log("=== DOTS WORLD SETUP ===");
        
        // Check if we're in Play Mode
        if (!Application.isPlaying)
        {
            Debug.LogWarning("DOTS World Setup: Not in Play Mode - DOTS world will not be available");
            worldStatus = "Not in Play Mode";
            return;
        }
        
        // Wait a frame for DOTS to initialize
        StartCoroutine(SetupAfterDelay());
    }
    
    private System.Collections.IEnumerator SetupAfterDelay()
    {
        // Wait for DOTS to initialize
        yield return new WaitForSeconds(0.1f);
        
        // Check if world is available
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null)
        {
            worldStatus = $"World ready: {world.Name}";
            
            if (logSetupStatus)
            {
                Debug.Log($"✓ DOTS World is ready: {world.Name}");
                Debug.Log($"✓ EntityManager available: {world.EntityManager != null}");
                
                // List available systems
                var systems = world.Systems;
                Debug.Log($"✓ Available systems: {systems.Count}");
                
                foreach (var system in systems)
                {
                    if (system.GetType().Name.Contains("Terrain"))
                    {
                        Debug.Log($"  - {system.GetType().Name}");
                    }
                }
            }
        }
        else
        {
            worldStatus = "World not available";
            
            if (logSetupStatus)
            {
                Debug.LogError("✗ DOTS World is not available");
                Debug.LogError("Make sure you're in Play Mode and DOTS packages are properly installed");
            }
        }
        
        Debug.Log("=== DOTS WORLD SETUP COMPLETE ===");
    }
    
    /// <summary>
    /// Static method to check if DOTS world is ready
    /// </summary>
    /// <returns>True if world is available</returns>
    public static bool IsWorldReady()
    {
        return World.DefaultGameObjectInjectionWorld != null;
    }
    
    /// <summary>
    /// Static method to get the DOTS world
    /// </summary>
    /// <returns>The default world or null if not available</returns>
    public static World GetWorld()
    {
        return World.DefaultGameObjectInjectionWorld;
    }
    
    /// <summary>
    /// Static method to get the EntityManager
    /// </summary>
    /// <returns>The EntityManager or null if world is not available</returns>
    public static EntityManager? GetEntityManager()
    {
        var world = GetWorld();
        return world?.EntityManager;
    }
} 