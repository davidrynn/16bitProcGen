using UnityEngine;
using Unity.Entities;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// MonoBehaviour to run and monitor the WFC DOTS test system.
    /// Attach this to a GameObject in your scene.
    /// </summary>
    public class WFCTestSetup : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool runTestOnStart = true;
        public float testTimeout = 10.0f;

        private WFCSystemTest testSystem;
        private bool testStarted = false;

        void Start()
        {
            if (runTestOnStart)
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
                        Debug.Log($"[WFC Test] SUCCESS: {result}");
                    else
                        Debug.LogError($"[WFC Test] FAILURE: {result}");

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

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[WFC Test] No DOTS world found");
                return;
            }

            testSystem = world.GetOrCreateSystemManaged<WFCSystemTest>();
            if (testSystem == null)
            {
                Debug.LogError("[WFC Test] Failed to create WFC test system");
                return;
            }

            // Optionally set the timeout if you want to expose it
            testStarted = true;
            Debug.Log("[WFC Test] Test started");
        }

        /// <summary>
        /// Stops the WFC test.
        /// </summary>
        public void StopTest()
        {
            if (!testStarted) return;

            testStarted = false;
            Debug.Log("[WFC Test] Test stopped");
        }
    }
} 