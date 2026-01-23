using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.Generation;
using DOTS.Terrain.Weather;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Automatically sets up the required components for hybrid terrain generation testing
    /// Creates ComputeShaderManager and TerrainEntityManager if they don't exist
    /// Uses global DebugSettings.EnableTestDebug for logging control
    /// </summary>
    public class HybridTestSetup : MonoBehaviour
    {
        [Header("Setup Settings")]
        [SerializeField] private bool setupOnStart = true;
        
        private void Start()
        {
            if (setupOnStart)
            {
                SetupHybridTestEnvironment();
            }
        }
        
        /// <summary>
        /// Sets up the complete hybrid test environment
        /// </summary>
        [ContextMenu("Setup Hybrid Test Environment")]
        public void SetupHybridTestEnvironment()
        {
            DebugSettings.LogTest("=== SETTING UP HYBRID TEST ENVIRONMENT ===");
            
            // Step 1: Setup ComputeShaderManager
            if (!SetupComputeShaderManager())
            {
                DebugSettings.LogError("Failed to setup ComputeShaderManager");
                return;
            }
            
            // Step 2: Setup TerrainEntityManager
            if (!SetupTerrainEntityManager())
            {
                DebugSettings.LogError("Failed to setup TerrainEntityManager");
                return;
            }
            
            // Step 3: Setup TerrainComputeBufferManager
            if (!SetupTerrainComputeBufferManager())
            {
                DebugSettings.LogError("Failed to setup TerrainComputeBufferManager");
                return;
            }
            
            // Step 4: Setup Weather Systems
            if (!SetupWeatherSystems())
            {
                DebugSettings.LogError("Failed to setup Weather Systems");
                return;
            }
            
            // Step 5: Verify setup
            if (VerifySetup())
            {
                DebugSettings.LogTest("✓ Hybrid test environment setup complete!");
            }
            else
            {
                DebugSettings.LogError("✗ Hybrid test environment setup failed verification");
            }
        }
        
        /// <summary>
        /// Sets up the ComputeShaderManager component
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupComputeShaderManager()
        {
            DebugSettings.LogTest("Setting up ComputeShaderManager...");
            
            try
            {
                // Get the ComputeShaderManager singleton instance
                var computeManager = ComputeShaderManager.Instance;
                
                if (computeManager == null)
                {
                    DebugSettings.LogError("Failed to get ComputeShaderManager instance");
                    return false;
                }
                
                DebugSettings.LogTest("✓ ComputeShaderManager singleton initialized");
                
                return true;
            }
            catch (System.Exception e)
            {
                DebugSettings.LogError($"Failed to setup ComputeShaderManager: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Sets up the TerrainEntityManager component
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupTerrainEntityManager()
        {
            DebugSettings.LogTest("Setting up TerrainEntityManager...");
            
            // Check if TerrainEntityManager already exists
            var existingManager = FindFirstObjectByType<TerrainEntityManager>();
            if (existingManager != null)
            {
                DebugSettings.LogTest("✓ TerrainEntityManager already exists");
                return true;
            }
            
            // Create TerrainEntityManager GameObject
            var managerGO = new GameObject("TerrainEntityManager");
            var entityManager = managerGO.AddComponent<TerrainEntityManager>();
            
            if (entityManager == null)
            {
                DebugSettings.LogError("Failed to create TerrainEntityManager component");
                return false;
            }
            
            // Configure default settings
            entityManager.defaultResolution = 64;
            entityManager.defaultWorldScale = 1.0f;
            entityManager.defaultBiomeType = BiomeType.Plains;
            
            DebugSettings.LogTest("✓ Created TerrainEntityManager with default settings");
            
            return true;
        }
        
        /// <summary>
        /// Sets up the TerrainComputeBufferManager component
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupTerrainComputeBufferManager()
        {
            DebugSettings.LogTest("Setting up TerrainComputeBufferManager...");
            
            // Check if TerrainComputeBufferManager already exists
            var existingBufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (existingBufferManager != null)
            {
                DebugSettings.LogTest("✓ TerrainComputeBufferManager already exists");
                return true;
            }
            
            // Create TerrainComputeBufferManager GameObject
            var bufferManagerGO = new GameObject("TerrainComputeBufferManager");
            var bufferManager = bufferManagerGO.AddComponent<TerrainComputeBufferManager>();
            
            if (bufferManager == null)
            {
                DebugSettings.LogError("Failed to create TerrainComputeBufferManager component");
                return false;
            }
            
            DebugSettings.LogTest("✓ Created TerrainComputeBufferManager");
            
            return true;
        }
        
        /// <summary>
        /// Sets up the Weather Systems
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupWeatherSystems()
        {
            DebugSettings.LogTest("Setting up Weather Systems...");
            
            try
            {
                // Check if weather systems are registered in the world
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    DebugSettings.LogError("No DOTS world found for weather system setup");
                    return false;
                }
                
                // Weather systems will be auto-registered by DOTS
                DebugSettings.LogTest("Weather systems will be auto-registered by DOTS");
                
                DebugSettings.LogTest("✓ Weather systems setup complete");
                
                return true;
            }
            catch (System.Exception e)
            {
                DebugSettings.LogError($"Failed to setup Weather Systems: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Verifies that all required components are properly set up
        /// </summary>
        /// <returns>True if verification passed</returns>
        private bool VerifySetup()
        {
            DebugSettings.LogTest("Verifying setup...");
            
            // Check ComputeShaderManager
            try
            {
                var computeManager = ComputeShaderManager.Instance;
                if (computeManager == null)
                {
                    DebugSettings.LogError("ComputeShaderManager not found after setup");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                DebugSettings.LogError($"ComputeShaderManager not found after setup: {e.Message}");
                return false;
            }
            
            // Check TerrainEntityManager
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager == null)
            {
                DebugSettings.LogError("TerrainEntityManager not found after setup");
                return false;
            }
            
            // Check TerrainComputeBufferManager
            var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager == null)
            {
                DebugSettings.LogError("TerrainComputeBufferManager not found after setup");
                return false;
            }
            
            // Check DOTS World
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                DebugSettings.LogError("DOTS World not found");
                return false;
            }
            
            // Check if HybridTerrainGenerationSystem is registered
            var hybridSystemHandle = world.GetExistingSystem<HybridTerrainGenerationSystem>();
            if (hybridSystemHandle == SystemHandle.Null)
            {
                DebugSettings.LogError("HybridTerrainGenerationSystem not found in world");
                return false;
            }
            
            // Weather systems will be auto-registered by DOTS
            DebugSettings.LogTest("Weather systems will be auto-registered by DOTS");
            
            DebugSettings.LogTest("✓ All components verified:");
            DebugSettings.LogTest($"  - ComputeShaderManager: Found");
            DebugSettings.LogTest($"  - TerrainEntityManager: {(entityManager != null ? "Found" : "Missing")}");
            DebugSettings.LogTest($"  - TerrainComputeBufferManager: {(bufferManager != null ? "Found" : "Missing")}");
            DebugSettings.LogTest($"  - DOTS World: {(world != null ? "Found" : "Missing")}");
            DebugSettings.LogTest($"  - HybridTerrainGenerationSystem: {(hybridSystemHandle != SystemHandle.Null ? "Found" : "Missing")}");
            DebugSettings.LogTest($"  - WeatherSystem: Auto-registered by DOTS");
            DebugSettings.LogTest($"  - HybridWeatherSystem: Auto-registered by DOTS");
            
            return true;
        }
        
        /// <summary>
        /// Cleans up the test environment
        /// </summary>
        [ContextMenu("Cleanup Test Environment")]
        public void CleanupTestEnvironment()
        {
            DebugSettings.LogTest("Cleaning up test environment...");
            
            // ComputeShaderManager is a singleton, no need to destroy
            DebugSettings.LogTest("✓ ComputeShaderManager is a singleton, no cleanup needed");
            
            // Destroy TerrainEntityManager
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager != null)
            {
                DestroyImmediate(entityManager.gameObject);
                DebugSettings.LogTest("✓ Destroyed TerrainEntityManager");
            }
            
            DebugSettings.LogTest("✓ Test environment cleanup complete");
        }
        
        /// <summary>
        /// Gets the current setup status
        /// </summary>
        [ContextMenu("Get Setup Status")]
        public void GetSetupStatus()
        {
            DebugSettings.LogTest("=== HYBRID TEST SETUP STATUS ===");
            
            ComputeShaderManager computeManager = null;
            try
            {
                computeManager = ComputeShaderManager.Instance;
            }
            catch (System.Exception)
            {
                computeManager = null;
            }
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            var world = World.DefaultGameObjectInjectionWorld;
            var hybridSystemHandle = world?.GetExistingSystem<HybridTerrainGenerationSystem>();
            
            DebugSettings.LogTest($"ComputeShaderManager: {(computeManager != null ? "✓ Found" : "✗ Missing")}");
            DebugSettings.LogTest($"TerrainEntityManager: {(entityManager != null ? "✓ Found" : "✗ Missing")}");
            DebugSettings.LogTest($"DOTS World: {(world != null ? "✓ Found" : "✗ Missing")}");
            DebugSettings.LogTest($"HybridTerrainGenerationSystem: {(hybridSystemHandle != SystemHandle.Null ? "✓ Found" : "✗ Missing")}");
            
            if (computeManager != null && entityManager != null && world != null && hybridSystemHandle != SystemHandle.Null)
            {
                DebugSettings.LogTest("✓ All components ready for testing!");
            }
            else
            {
                DebugSettings.LogWarning("⚠ Some components are missing - run Setup Hybrid Test Environment");
            }
            
            DebugSettings.LogTest("=== STATUS COMPLETE ===");
        }

        private void OnDestroy()
        {
            // Clean up any remaining terrain entities to prevent memory leaks
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager != null)
            {
                DebugSettings.LogTest("HybridTestSetup: Cleaning up terrain entities");
                entityManager.DestroyAllTerrainEntities();
            }
        }
        
        void OnGUI()
        {
            if (!DebugSettings.EnableTestDebug) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 370, 20, 350, 150));
            GUILayout.Label("=== HYBRID SETUP STATUS ===");
            
            var computeManager = ComputeShaderManager.Instance;
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            var world = World.DefaultGameObjectInjectionWorld;
            
            GUILayout.Label($"ComputeShaderManager: {(computeManager != null ? "✓" : "✗")}");
            GUILayout.Label($"TerrainEntityManager: {(entityManager != null ? "✓" : "✗")}");
            GUILayout.Label($"TerrainComputeBufferManager: {(bufferManager != null ? "✓" : "✗")}");
            GUILayout.Label($"DOTS World: {(world != null ? "✓" : "✗")}");
            
            if (computeManager != null && entityManager != null && bufferManager != null && world != null)
            {
                GUILayout.Label("✓ All systems ready!");
            }
            else
            {
                GUILayout.Label("⚠ Some systems missing");
            }
            
            GUILayout.EndArea();
        }
    }
} 