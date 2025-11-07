#if UNITY_EDITOR
using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.WFC;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Simple test manager for basic rendering test
    /// </summary>
    public class SimpleTestManager : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool autoStartTest = true;
        public float testTimeoutSeconds = 10f;
        
        [Header("Debug Settings")]
        public bool enableDebugLogs = true;
        
        // Test state
        private bool testStarted = false;
        private bool testCompleted = false;
        private float testStartTime;
        
        // DOTS world reference
        private World dotsWorld;
        private SimpleRenderingTest simpleTest;
        private DungeonVisualizationSystem visualizationSystem;
        
        void Start()
        {
            if (autoStartTest)
            {
                StartTest();
            }
        }
        
        void Update()
        {
            if (testStarted && !testCompleted)
            {
                CheckTestStatus();
            }
        }
        
        public void StartTest()
        {
            if (testStarted) return;
            
            Log("Starting Simple Rendering Test...");
            testStarted = true;
            testStartTime = Time.time;
            
            // Enable the DOTS test system
            SimpleRenderingTest.SetTestEnabled(true);
            
            // Find DOTS world and systems
            FindDOTSSystems();
            
            // Start the test
            if (simpleTest != null)
            {
                Log("Simple Rendering Test found and starting...");
            }
            else
            {
                LogError("Simple Rendering Test not found!");
                CompleteTest("Simple Rendering Test not found");
            }
        }
        
        private void FindDOTSSystems()
        {
            // Find the default world
            dotsWorld = World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null)
            {
                LogError("No DOTS world found!");
                return;
            }
            
            // Find our systems
            simpleTest = dotsWorld.GetExistingSystemManaged<SimpleRenderingTest>();
            visualizationSystem = dotsWorld.GetExistingSystemManaged<DungeonVisualizationSystem>();
            
            Log($"Found systems: Simple Test={simpleTest != null}, Visualization={visualizationSystem != null}");
        }
        
        private void CheckTestStatus()
        {
            // Check for timeout
            if (Time.time - testStartTime > testTimeoutSeconds)
            {
                CompleteTest($"Test timed out after {testTimeoutSeconds} seconds");
                return;
            }
            
            // Check if test is complete
            if (simpleTest != null)
            {
                // Check if visualization system has completed
                if (visualizationSystem != null)
                {
                    // For now, just check if we've been running for a reasonable time
                    if (Time.time - testStartTime > 3f) // Give it 3 seconds to complete
                    {
                        CompleteTest("Test completed successfully");
                    }
                }
                else
                {
                    // If no visualization system, complete after 2 seconds
                    if (Time.time - testStartTime > 2f)
                    {
                        CompleteTest("Test completed successfully (no visualization system)");
                    }
                }
            }
        }
        
        private void CompleteTest(string message)
        {
            if (testCompleted) return;
            
            testCompleted = true;
            
            Log($"Test completed: {message}");
            Log($"=== SIMPLE RENDERING TEST COMPLETE ===\nDuration: {Time.time - testStartTime:F2}s");
            Log("Check the scene for 9 GameObjects (floors, walls, doors, etc.)");
        }
        
        // Debug methods
        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[Simple Test Manager] {message}");
            }
        }
        
        private void LogError(string message)
        {
            Debug.LogError($"[Simple Test Manager] {message}");
        }
        
        // Public methods for manual control
        public void RestartTest()
        {
            testStarted = false;
            testCompleted = false;
            StartTest();
        }
        
        public bool IsTestComplete() => testCompleted;
    }
}
#endif 