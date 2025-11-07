using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.Generation;
using DOTS.Terrain.Weather;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Automatically sets up the required components for hybrid terrain generation testing
    /// Creates ComputeShaderManager and TerrainEntityManager if they don't exist
    /// </summary>
    public class HybridTestSetup : MonoBehaviour
    {
        [Header("Setup Settings")]
        [SerializeField] private bool setupOnStart = true;
        [SerializeField] private bool logSetupProcess = true;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugLogs = false; // NEW: Debug toggle
        
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
            DebugLog("=== SETTING UP HYBRID TEST ENVIRONMENT ===");
            
            // Step 1: Setup ComputeShaderManager
            if (!SetupComputeShaderManager())
            {
                DebugError("Failed to setup ComputeShaderManager");
                return;
            }
            
            // Step 2: Setup TerrainEntityManager
            if (!SetupTerrainEntityManager())
            {
                DebugError("Failed to setup TerrainEntityManager");
                return;
            }
            
            // Step 3: Setup TerrainComputeBufferManager
            if (!SetupTerrainComputeBufferManager())
            {
                DebugError("Failed to setup TerrainComputeBufferManager");
                return;
            }
            
            // Step 4: Setup Weather Systems
            if (!SetupWeatherSystems())
            {
                DebugError("Failed to setup Weather Systems");
                return;
            }
            
            // Step 4: Verify setup
            if (VerifySetup())
            {
                DebugLog("✓ Hybrid test environment setup complete!");
            }
            else
            {
                DebugError("✗ Hybrid test environment setup failed verification");
            }
        }
        
        /// <summary>
        /// Sets up the ComputeShaderManager component
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupComputeShaderManager()
        {
            if (logSetupProcess)
                Debug.Log("Setting up ComputeShaderManager...");
            
            try
            {
                // Get the ComputeShaderManager singleton instance
                var computeManager = ComputeShaderManager.Instance;
                
                if (computeManager == null)
                {
                    Debug.LogError("Failed to get ComputeShaderManager instance");
                    return false;
                }
                
                if (logSetupProcess)
                    Debug.Log("✓ ComputeShaderManager singleton initialized");
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to setup ComputeShaderManager: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Sets up the TerrainEntityManager component
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupTerrainEntityManager()
        {
            if (logSetupProcess)
                Debug.Log("Setting up TerrainEntityManager...");
            
            // Check if TerrainEntityManager already exists
            var existingManager = FindFirstObjectByType<TerrainEntityManager>();
            if (existingManager != null)
            {
                if (logSetupProcess)
                    Debug.Log("✓ TerrainEntityManager already exists");
                return true;
            }
            
            // Create TerrainEntityManager GameObject
            var managerGO = new GameObject("TerrainEntityManager");
            var entityManager = managerGO.AddComponent<TerrainEntityManager>();
            
            if (entityManager == null)
            {
                Debug.LogError("Failed to create TerrainEntityManager component");
                return false;
            }
            
            // Configure default settings
            entityManager.defaultResolution = 64;
            entityManager.defaultWorldScale = 1.0f;
            entityManager.defaultBiomeType = BiomeType.Plains;
            
            if (logSetupProcess)
                Debug.Log("✓ Created TerrainEntityManager with default settings");
            
            return true;
        }
        
        /// <summary>
        /// Sets up the TerrainComputeBufferManager component
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupTerrainComputeBufferManager()
        {
            if (logSetupProcess)
                Debug.Log("Setting up TerrainComputeBufferManager...");
            
            // Check if TerrainComputeBufferManager already exists
            var existingBufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (existingBufferManager != null)
            {
                if (logSetupProcess)
                    Debug.Log("✓ TerrainComputeBufferManager already exists");
                return true;
            }
            
            // Create TerrainComputeBufferManager GameObject
            var bufferManagerGO = new GameObject("TerrainComputeBufferManager");
            var bufferManager = bufferManagerGO.AddComponent<TerrainComputeBufferManager>();
            
            if (bufferManager == null)
            {
                Debug.LogError("Failed to create TerrainComputeBufferManager component");
                return false;
            }
            
            if (logSetupProcess)
                Debug.Log("✓ Created TerrainComputeBufferManager");
            
            return true;
        }
        
        /// <summary>
        /// Sets up the Weather Systems
        /// </summary>
        /// <returns>True if setup was successful</returns>
        private bool SetupWeatherSystems()
        {
            if (logSetupProcess)
                Debug.Log("Setting up Weather Systems...");
            
            try
            {
                // Check if weather systems are registered in the world
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null)
                {
                    Debug.LogError("No DOTS world found for weather system setup");
                    return false;
                }
                
                // Weather systems will be auto-registered by DOTS
                Debug.Log("Weather systems will be auto-registered by DOTS");
                
                if (logSetupProcess)
                    Debug.Log("✓ Weather systems setup complete");
                
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to setup Weather Systems: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Verifies that all required components are properly set up
        /// </summary>
        /// <returns>True if verification passed</returns>
        private bool VerifySetup()
        {
            if (logSetupProcess)
                Debug.Log("Verifying setup...");
            
            // Check ComputeShaderManager
            try
            {
                var computeManager = ComputeShaderManager.Instance;
                if (computeManager == null)
                {
                    Debug.LogError("ComputeShaderManager not found after setup");
                    return false;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ComputeShaderManager not found after setup: {e.Message}");
                return false;
            }
            
            // Check TerrainEntityManager
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager == null)
            {
                Debug.LogError("TerrainEntityManager not found after setup");
                return false;
            }
            
            // Check TerrainComputeBufferManager
            var bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
            if (bufferManager == null)
            {
                Debug.LogError("TerrainComputeBufferManager not found after setup");
                return false;
            }
            
            // Check DOTS World
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("DOTS World not found");
                return false;
            }
            
            // Check if HybridTerrainGenerationSystem is registered
            var hybridSystemHandle = world.GetExistingSystem<HybridTerrainGenerationSystem>();
            if (hybridSystemHandle == SystemHandle.Null)
            {
                Debug.LogError("HybridTerrainGenerationSystem not found in world");
                return false;
            }
            
            // Weather systems will be auto-registered by DOTS
            Debug.Log("Weather systems will be auto-registered by DOTS");
            
            if (logSetupProcess)
            {
                Debug.Log("✓ All components verified:");
                Debug.Log($"  - ComputeShaderManager: Found");
                Debug.Log($"  - TerrainEntityManager: {(entityManager != null ? "Found" : "Missing")}");
                Debug.Log($"  - TerrainComputeBufferManager: {(bufferManager != null ? "Found" : "Missing")}");
                Debug.Log($"  - DOTS World: {(world != null ? "Found" : "Missing")}");
                Debug.Log($"  - HybridTerrainGenerationSystem: {(hybridSystemHandle != SystemHandle.Null ? "Found" : "Missing")}");
                Debug.Log($"  - WeatherSystem: Auto-registered by DOTS");
                Debug.Log($"  - HybridWeatherSystem: Auto-registered by DOTS");
            }
            
            return true;
        }
        
        /// <summary>
        /// Cleans up the test environment
        /// </summary>
        [ContextMenu("Cleanup Test Environment")]
        public void CleanupTestEnvironment()
        {
            Debug.Log("Cleaning up test environment...");
            
            // ComputeShaderManager is a singleton, no need to destroy
            Debug.Log("✓ ComputeShaderManager is a singleton, no cleanup needed");
            
            // Destroy TerrainEntityManager
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager != null)
            {
                DestroyImmediate(entityManager.gameObject);
                Debug.Log("✓ Destroyed TerrainEntityManager");
            }
            
            Debug.Log("✓ Test environment cleanup complete");
        }
        
        /// <summary>
        /// Gets the current setup status
        /// </summary>
        [ContextMenu("Get Setup Status")]
        public void GetSetupStatus()
        {
            Debug.Log("=== HYBRID TEST SETUP STATUS ===");
            
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
            
            Debug.Log($"ComputeShaderManager: {(computeManager != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"TerrainEntityManager: {(entityManager != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"DOTS World: {(world != null ? "✓ Found" : "✗ Missing")}");
            Debug.Log($"HybridTerrainGenerationSystem: {(hybridSystemHandle != SystemHandle.Null ? "✓ Found" : "✗ Missing")}");
            
            if (computeManager != null && entityManager != null && world != null && hybridSystemHandle != SystemHandle.Null)
            {
                Debug.Log("✓ All components ready for testing!");
            }
            else
            {
                Debug.LogWarning("⚠ Some components are missing - run Setup Hybrid Test Environment");
            }
            
            Debug.Log("=== STATUS COMPLETE ===");
        }

        /// <summary>
        /// Debug log wrapper that respects the debug toggle
        /// </summary>
        private void DebugLog(string message, bool verbose = false)
        {
            if (enableDebugLogs && (!verbose || logSetupProcess))
            {
                Debug.Log($"[HybridSetup] {message}");
            }
        }
        
        /// <summary>
        /// Debug error log wrapper
        /// </summary>
        private void DebugError(string message)
        {
            Debug.LogError($"[HybridSetup] {message}");
        }
        
        private void OnDestroy()
        {
            // Clean up any remaining terrain entities to prevent memory leaks
            var entityManager = FindFirstObjectByType<TerrainEntityManager>();
            if (entityManager != null)
            {
                Debug.Log("HybridTestSetup: Cleaning up terrain entities");
                entityManager.DestroyAllTerrainEntities();
            }
        }
        
        void OnGUI()
        {
            if (!enableDebugLogs) return;
            
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