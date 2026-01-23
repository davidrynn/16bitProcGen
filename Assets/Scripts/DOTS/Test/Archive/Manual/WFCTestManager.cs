using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.Test;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// MonoBehaviour-based test manager for WFC systems
    /// Provides coordination, debugging, and completion tracking
    /// </summary>
    public class WFCTestManager : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool autoStartTest = true;
        public float testTimeoutSeconds = 30f;
        
        [Header("Debug Settings")]
        public bool enableDebugLogs = true;
        public bool showCompletionStatus = true;
        
        // Test state
        private bool testStarted = false;
        private bool testCompleted = false;
        private float testStartTime;
        private TestResult testResult = TestResult.NotStarted;
        
        // DOTS world reference
        private World dotsWorld;
        private WFCSystemTest wfcTestSystem;
        private HybridWFCSystem wfcSystem;
        private DungeonRenderingSystem renderingSystem;
        private DungeonVisualizationSystem visualizationSystem;
        
        public enum TestResult
        {
            NotStarted,
            Running,
            Success,
            Failed,
            Timeout
        }
        
        void Start()
        {
            // Subscribe to test events
            WFCTestEvents.OnTestCompleted += OnTestCompleted;
            WFCTestEvents.OnProgressUpdated += OnProgressUpdated;
            WFCTestEvents.OnDebugMessage += OnDebugMessage;
            
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
            
            Log("Starting WFC Test...");
            testStarted = true;
            testStartTime = Time.time;
            testResult = TestResult.Running;
            
            // Find DOTS world and systems
            FindDOTSSystems();
            
            // Start the test
            if (wfcTestSystem != null)
            {
                Log("WFC Test System found and starting...");
            }
            else
            {
                LogError("WFC Test System not found!");
                CompleteTest(TestResult.Failed, "WFC Test System not found");
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
            wfcTestSystem = dotsWorld.GetExistingSystemManaged<WFCSystemTest>();
            wfcSystem = dotsWorld.GetExistingSystemManaged<HybridWFCSystem>();
            renderingSystem = dotsWorld.GetExistingSystemManaged<DungeonRenderingSystem>();
            visualizationSystem = dotsWorld.GetExistingSystemManaged<DungeonVisualizationSystem>();
            
            Log($"Found systems: WFC Test={wfcTestSystem != null}, WFC={wfcSystem != null}, Rendering={renderingSystem != null}, Visualization={visualizationSystem != null}");
        }
        
        private void CheckTestStatus()
        {
            // Check for timeout
            if (Time.time - testStartTime > testTimeoutSeconds)
            {
                CompleteTest(TestResult.Timeout, $"Test timed out after {testTimeoutSeconds} seconds");
                return;
            }
            
            // Check if all systems are complete
            if (testStarted && !testCompleted)
            {
                // Check WFC completion
                if (wfcSystem != null)
                {
                    // This would need to be checked through the system's state
                    // For now, we'll rely on the event system
                }
                
                // Check if we should stop the test
                if (testResult == TestResult.Success || testResult == TestResult.Failed)
                {
                    CompleteTest(testResult, "Test completed");
                }
            }
        }
        
        private void CompleteTest(TestResult result, string message)
        {
            if (testCompleted) return;
            
            testCompleted = true;
            testResult = result;
            
            Log($"Test {result}: {message}");
            
            if (showCompletionStatus)
            {
                ShowCompletionStatus();
            }
        }
        
        private void ShowCompletionStatus()
        {
            string status = testResult switch
            {
                TestResult.Success => "✅ SUCCESS",
                TestResult.Failed => "❌ FAILED", 
                TestResult.Timeout => "⏰ TIMEOUT",
                TestResult.Running => "🔄 RUNNING",
                _ => "❓ UNKNOWN"
            };
            
            Log($"=== WFC TEST COMPLETE ===\nStatus: {status}\nDuration: {Time.time - testStartTime:F2}s");
        }
        
        // Debug methods
        private void Log(string message)
        {
            if (enableDebugLogs)
            {
                DebugSettings.Log($"[WFC Test Manager] {message}");
            }
        }
        
        private void LogError(string message)
        {
            DebugSettings.LogError($"[WFC Test Manager] {message}");
        }
        
        // Public methods for manual control
        public void RestartTest()
        {
            testStarted = false;
            testCompleted = false;
            testResult = TestResult.NotStarted;
            StartTest();
        }
        
        public TestResult GetTestResult() => testResult;
        public bool IsTestComplete() => testCompleted;
        
        // Event handlers
        private void OnTestCompleted(bool success, string message)
        {
            var result = success ? TestResult.Success : TestResult.Failed;
            CompleteTest(result, message);
        }
        
        private void OnProgressUpdated(int current, int total)
        {
            DebugSettings.Log($"Progress: {current}/{total} cells collapsed");
        }
        
        private void OnDebugMessage(string message)
        {
            DebugSettings.Log($"Debug: {message}");
        }
        
        void OnDestroy()
        {
            // Unsubscribe from events
            WFCTestEvents.OnTestCompleted -= OnTestCompleted;
            WFCTestEvents.OnProgressUpdated -= OnProgressUpdated;
            WFCTestEvents.OnDebugMessage -= OnDebugMessage;
        }
    }
} 