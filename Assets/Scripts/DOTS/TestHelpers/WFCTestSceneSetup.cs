#if UNITY_EDITOR
using UnityEngine;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Helper script to set up a scene for WFC testing
    /// </summary>
    public class WFCTestSceneSetup : MonoBehaviour
    {
        [Header("Test Configuration")]
        public TestType testType = TestType.SimpleRendering;
        [Tooltip("Automatically set up test environment on start")]
        public bool autoSetup = false;
        
        [Header("Debug Settings")]
        [Tooltip("Enable test systems to run (WFCSystemTest, SimpleRenderingTest, etc.)")]
        public bool enableTestSystems = true;
        [Tooltip("Enable detailed logging for test systems")]
        public bool enableTestDebug = false;
        [Tooltip("Enable detailed logging for rendering systems")]
        public bool enableRenderingDebug = false;
        [Tooltip("Enable detailed logging for WFC systems")]
        public bool enableWFCDebug = false;
        
        public enum TestType
        {
            SimpleRendering,
            FullWFC,
            DungeonRendering,
            DungeonDOTSRendering
        }
        
        void Start()
        {
            if (autoSetup)
            {
                SetupTestEnvironment();
            }
        }
        
        [ContextMenu("Setup Test Environment")]
        public void SetupTestEnvironment()
        {
            Debug.Log("[WFC Test Setup] Configuring test environment...");
            
            // Configure debug settings
            ConfigureDebugSettings();
            
            // Add test manager based on type
            AddTestManager();
            
            Debug.Log("[WFC Test Setup] Test environment ready!");
        }
        
        private void ConfigureDebugSettings()
        {
            // Find or create DebugController
            var debugController = UnityEngine.Object.FindFirstObjectByType<DebugController>();
            if (debugController == null)
            {
                var debugGO = new GameObject("DebugController");
                debugController = debugGO.AddComponent<DebugController>();
                Debug.Log("[WFC Test Setup] Created DebugController");
            }
            
            // Configure debug settings directly on the controller
            debugController.enableTestSystems = enableTestSystems;
            debugController.enableTestDebug = enableTestDebug;
            debugController.enableRenderingDebug = enableRenderingDebug;
            debugController.enableWFCDebug = enableWFCDebug;
            
            // The DebugController will automatically apply settings in Update()
            // Force an immediate update by calling one of the public methods
            if (enableTestSystems && enableTestDebug)
            {
                debugController.EnableTestSystemsOnly();
            }
            else if (enableTestSystems)
            {
                // Just set the flags - the controller will apply them automatically
            }
            
            Debug.Log($"[WFC Test Setup] Debug settings configured: TestSystems={enableTestSystems}, TestDebug={enableTestDebug}, RenderingDebug={enableRenderingDebug}, WFCDebug={enableWFCDebug}");
        }
        
        private void AddTestManager()
        {
            // Remove any existing test managers
            var existingSimple = UnityEngine.Object.FindFirstObjectByType<SimpleTestManager>();
            var existingWFC = UnityEngine.Object.FindFirstObjectByType<WFCTestSetup>();
            
            if (existingSimple != null)
            {
                Debug.Log("[WFC Test Setup] Removing existing SimpleTestManager");
                DestroyImmediate(existingSimple.gameObject);
            }
            
            if (existingWFC != null)
            {
                Debug.Log("[WFC Test Setup] Removing existing WFCTestSetup");
                DestroyImmediate(existingWFC.gameObject);
            }
            
            // Add new test manager based on type
            switch (testType)
            {
                case TestType.SimpleRendering:
                    var simpleGO = new GameObject("SimpleTestManager");
                    simpleGO.AddComponent<SimpleTestManager>();
                    Debug.Log("[WFC Test Setup] Added SimpleTestManager");
                    break;
                    
                case TestType.FullWFC:
                    var wfcGO = new GameObject("WFCTestSetup");
                    wfcGO.AddComponent<WFCTestSetup>();
                    Debug.Log("[WFC Test Setup] Added WFCTestSetup");
                    break;
                    
                case TestType.DungeonRendering:
                    // Enable the dungeon rendering test specifically
                    WFCDungeonRenderingTest.SetWFCDungeonTestEnabled(true);
                    Debug.Log("[WFC Test Setup] Enabled DungeonRendering test");
                    break;
                    
                case TestType.DungeonDOTSRendering:
                    // DOTS-only renderer was removed; map to standard DungeonRendering test
                    WFCDungeonRenderingTest.SetWFCDungeonTestEnabled(true);
                    Debug.Log("[WFC Test Setup] DungeonDOTSRendering mapped to DungeonRendering (DOTS fallback removed)");
                    break;
            }
        }
        
        [ContextMenu("Enable Simple Rendering Test")]
        public void EnableSimpleRenderingTest()
        {
            testType = TestType.SimpleRendering;
            
            // Disable WFC test specifically when running simple test
            WFCSystemTest.SetWFCTestEnabled(false);
            
            SetupTestEnvironment();
        }
        
        [ContextMenu("Enable Full WFC Test")]
        public void EnableFullWFCTest()
        {
            testType = TestType.FullWFC;
            
            // Enable WFC test specifically when running full WFC test
            WFCSystemTest.SetWFCTestEnabled(true);
            
            SetupTestEnvironment();
        }
        
        [ContextMenu("Enable Dungeon Rendering Test")]
        public void EnableDungeonRenderingTest()
        {
            testType = TestType.DungeonRendering;
            
            // Enable dungeon rendering test specifically
            WFCDungeonRenderingTest.SetWFCDungeonTestEnabled(true);
            
            SetupTestEnvironment();
        }
        
        [ContextMenu("Enable Dungeon DOTS Rendering Test")]
        public void EnableDungeonDOTSRenderingTest()
        {
            testType = TestType.DungeonDOTSRendering;
            
            // Enable dungeon DOTS rendering test specifically
            WFCDungeonRenderingTest.SetWFCDungeonTestEnabled(true);
            
            SetupTestEnvironment();
        }
        

        
        [ContextMenu("Disable All Tests")]
        public void DisableAllTests()
        {
            enableTestSystems = false;
            enableTestDebug = false;
            enableRenderingDebug = false;
            enableWFCDebug = false;
            
            // Disable WFC test specifically
            WFCSystemTest.SetWFCTestEnabled(false);
            WFCDungeonRenderingTest.SetWFCDungeonTestEnabled(false);
            
            SetupTestEnvironment();
        }
        
        [ContextMenu("Disable All Test Components")]
        public void DisableAllTestComponents()
        {
            // Disable all WFCTestSetup components
            var wfcSetups = UnityEngine.Object.FindObjectsByType<WFCTestSetup>(FindObjectsSortMode.None);
            foreach (var setup in wfcSetups)
            {
                setup.runTestOnStart = false;
                setup.StopTest();
                Debug.Log($"[WFC Test Setup] Disabled WFCTestSetup on {setup.gameObject.name}");
            }
            
            // Disable all SimpleTestManager components
            var simpleManagers = UnityEngine.Object.FindObjectsByType<SimpleTestManager>(FindObjectsSortMode.None);
            foreach (var manager in simpleManagers)
            {
                manager.enabled = false;
                Debug.Log($"[WFC Test Setup] Disabled SimpleTestManager on {manager.gameObject.name}");
            }
            
            // Disable all WFCTestSceneSetup components
            var sceneSetups = UnityEngine.Object.FindObjectsByType<WFCTestSceneSetup>(FindObjectsSortMode.None);
            foreach (var setup in sceneSetups)
            {
                setup.enableTestSystems = false;
                setup.enableTestDebug = false;
                setup.enableRenderingDebug = false;
                setup.enableWFCDebug = false;
                Debug.Log($"[WFC Test Setup] Disabled WFCTestSceneSetup on {setup.gameObject.name}");
            }
            
            Debug.Log("[WFC Test Setup] All test components disabled");
        }
    }
}
#endif 