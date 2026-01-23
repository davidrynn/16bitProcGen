using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// MonoBehaviour to run and monitor the WFC DOTS test system.
    /// Attach this to a GameObject in your scene.
    /// </summary>
    public class WFCTestSetup : MonoBehaviour
    {
        [Header("Test Settings")]
        [Tooltip("Automatically start test when component starts")]
        public bool runTestOnStart = false;
        [Tooltip("Timeout for test completion in seconds")]
        public float testTimeout = 10.0f;

        private WFCSystemTest testSystem;
        private bool testStarted = false;

        void Start()
        {
            // Only run if test systems are enabled
            if (runTestOnStart && DOTS.Terrain.Core.DebugSettings.EnableTestSystems)
                StartTest();
        }

        void Update()
        {
            if (testStarted && testSystem != null)
            {
                if (testSystem.IsTestComplete())
                {
                    var (passed, result) = testSystem.GetTestResults();

                    if (passed)
                        DebugSettings.Log($"[WFC Test] SUCCESS: {result}");
                    else
                        DebugSettings.LogError($"[WFC Test] FAILURE: {result}");

                    testStarted = false;
                }
            }
        }

        /// <summary>
        /// Starts the WFC test.
        /// </summary>
        public void StartTest()
        {
            if (testStarted) return;
            
            // Only run if test systems are enabled
            if (!DOTS.Terrain.Core.DebugSettings.EnableTestSystems)
            {
                DebugSettings.LogWarning("[WFC Test] Test systems are disabled - not starting test");
                return;
            }

            // Enable the WFC test specifically
            WFCSystemTest.SetWFCTestEnabled(true);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                DebugSettings.LogError("[WFC Test] No DOTS world found");
                return;
            }

            testSystem = world.GetOrCreateSystemManaged<WFCSystemTest>();
            if (testSystem == null)
            {
                DebugSettings.LogError("[WFC Test] Failed to create WFC test system");
                return;
            }

            // Optionally set the timeout if you want to expose it
            testStarted = true;
            DebugSettings.Log("[WFC Test] Test started");
        }

        /// <summary>
        /// Stops the WFC test.
        /// </summary>
        public void StopTest()
        {
            if (!testStarted) return;

            testStarted = false;
            
            // Disable the WFC test specifically
            WFCSystemTest.SetWFCTestEnabled(false);
            
            DebugSettings.Log("[WFC Test] Test stopped");
        }
        
        [ContextMenu("Disable Auto-Start")]
        public void DisableAutoStart()
        {
            runTestOnStart = false;
            DebugSettings.Log("[WFC Test] Auto-start disabled");
        }
        
        [ContextMenu("Enable Auto-Start")]
        public void EnableAutoStart()
        {
            runTestOnStart = true;
            DebugSettings.Log("[WFC Test] Auto-start enabled");
        }
    }
} 