#if UNITY_EDITOR
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain.WFC;
using System.Collections.Generic;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Test system for WFC functionality
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class WFCSystemTest : SystemBase
    {
        private bool testInitialized = false;
        private Entity testWFCEntity;
        private float testStartTime;
        private float testTimeout = 10.0f; // 10 seconds timeout
        
        // Test results
        private bool testCompleted = false;
        private bool testPassed = false;
        private string testResult = "";
        
        // Static flag to control if this specific test can run
        private static bool enableWFCTest = false;
        
        public static void SetWFCTestEnabled(bool enabled)
        {
            enableWFCTest = enabled;
        }
        
        protected override void OnCreate()
        {
            // Only initialize if test systems are enabled AND WFC test is specifically enabled
            if (!DOTS.Terrain.Core.DebugSettings.EnableTestSystems || !enableWFCTest)
            {
                // Don't log anything - just return silently
                return;
            }
            
            DOTS.Terrain.Core.DebugSettings.LogTest("WFCSystemTest: Initializing...", true);
            testInitialized = false;
            testCompleted = false;
            testPassed = false;
            testResult = "";
        }
        
        protected override void OnUpdate()
        {
            // Only run if test systems are enabled AND WFC test is specifically enabled
            if (!DOTS.Terrain.Core.DebugSettings.EnableTestSystems || !enableWFCTest)
            {
                return;
            }
            
            if (!testInitialized)
            {
                InitializeTest();
                return;
            }
            
            if (testCompleted)
            {
                return;
            }
            
            // Check for test timeout
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            if (currentTime - testStartTime > testTimeout)
            {
                CompleteTest(false, "Test timed out");
                return;
            }
            
            // Check test progress
            CheckTestProgress();
        }
        
        /// <summary>
        /// Initializes the WFC test
        /// </summary>
        private void InitializeTest()
        {
            DOTS.Terrain.Core.DebugSettings.LogTest("WFCSystemTest: Starting WFC test...");
            
            // Create test WFC entity
            testWFCEntity = EntityManager.CreateEntity();
            
            // Create and assign the basic dungeon pattern set
            var patternList = WFCBuilder.CreateBasicDungeonPatterns();
            var constraintList = WFCBuilder.CreateBasicDungeonConstraints();
            
            var patternArray = new NativeArray<WFCPattern>(patternList.Count, Allocator.Temp);
            for (int i = 0; i < patternList.Count; i++)
                patternArray[i] = patternList[i];
                
            var constraintArray = new NativeArray<WFCConstraint>(constraintList.Count, Allocator.Temp);
            for (int i = 0; i < constraintList.Count; i++)
                constraintArray[i] = constraintList[i];
                
            // Create pattern blob
            var patternBuilder = new BlobBuilder(Allocator.Temp);
            ref var patternRoot = ref patternBuilder.ConstructRoot<WFCPatternData>();
            patternRoot.patternCount = patternArray.Length;
            var blobPatterns = patternBuilder.Allocate(ref patternRoot.patterns, patternArray.Length);
            for (int i = 0; i < patternArray.Length; i++)
                blobPatterns[i] = patternArray[i];
            var patternBlob = patternBuilder.CreateBlobAssetReference<WFCPatternData>(Allocator.Persistent);
            patternBuilder.Dispose();
            patternArray.Dispose();
            
            // Create constraint blob
            var constraintBuilder = new BlobBuilder(Allocator.Temp);
            ref var constraintRoot = ref constraintBuilder.ConstructRoot<WFCConstraintData>();
            constraintRoot.constraintCount = constraintArray.Length;
            var blobConstraints = constraintBuilder.Allocate(ref constraintRoot.constraints, constraintArray.Length);
            for (int i = 0; i < constraintArray.Length; i++)
                blobConstraints[i] = constraintArray[i];
            var constraintBlob = constraintBuilder.CreateBlobAssetReference<WFCConstraintData>(Allocator.Persistent);
            constraintBuilder.Dispose();
            constraintArray.Dispose();
            
            // Add WFC component
            var wfcComponent = new WFCComponent
            {
                gridSize = new int2(16, 16),
                patternSize = 1,
                cellSize = 1.0f,
                isCollapsed = false,
                entropy = 1.0f,
                selectedPattern = -1,
                patterns = patternBlob,
                constraints = constraintBlob,
                needsGeneration = true,
                isGenerating = false,
                generationProgress = 0.0f,
                lastUpdateTime = 0.0f,
                iterations = 0,
                maxIterations = 500 // Reduced from 1000
            };
            EntityManager.AddComponentData(testWFCEntity, wfcComponent);
            
            // Add generation settings
            var settings = new WFCGenerationSettings
            {
                maxIterations = 500, // Reduced from 1000
                constraintStrength = 1.0f,
                entropyThreshold = 0.1f,
                enableBacktracking = true,
                backtrackingLimit = 100,
                generationTimeout = 5.0f
            };
            EntityManager.AddComponentData(testWFCEntity, settings);
            
            // Add performance monitoring
            var performanceData = new WFCPerformanceData
            {
                generationTime = 0f,
                cellsProcessed = 0,
                constraintChecks = 0,
                averageEntropy = 0f,
                successfulGenerations = 0,
                failedGenerations = 0
            };
            EntityManager.AddComponentData(testWFCEntity, performanceData);
            
            // Create test cells
            CreateTestCells();
            
            testStartTime = (float)SystemAPI.Time.ElapsedTime;
            testInitialized = true;
            
            DOTS.Terrain.Core.DebugSettings.LogTest("WFCSystemTest: Test initialized with 16x16 grid and basic dungeon patterns");
        }
        
        /// <summary>
        /// Creates test WFC cells
        /// </summary>
        private void CreateTestCells()
        {
            int gridSize = 16;
            
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    var cellEntity = EntityManager.CreateEntity();
                    
                    var cell = new WFCCell
                    {
                        position = new int2(x, y),
                        collapsed = false,
                        entropy = 5.0f, // Start with 5 possible patterns
                        selectedPattern = -1,
                        needsUpdate = true,
                        patternCount = 5,
                        visualized = false,
                        possiblePatternsMask = 0x1F // 5 patterns (0-4) = 11111 in binary
                    };
                    
                    EntityManager.AddComponentData(cellEntity, cell);
                }
            }
            
            DOTS.Terrain.Core.DebugSettings.LogTest($"WFCSystemTest: Created {gridSize * gridSize} test cells");
        }
        
        /// <summary>
        /// Checks the progress of the WFC test
        /// </summary>
        private void CheckTestProgress()
        {
            if (!EntityManager.Exists(testWFCEntity))
            {
                CompleteTest(false, "Test entity was destroyed");
                return;
            }
            
            var wfcComponent = EntityManager.GetComponentData<WFCComponent>(testWFCEntity);
            
            // Check if all cells are collapsed
            var cellQuery = GetEntityQuery(ComponentType.ReadOnly<WFCCell>());
            var cells = cellQuery.ToComponentDataArray<WFCCell>(Allocator.Temp);
            
            int collapsedCells = 0;
            int totalCells = cells.Length;
            
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].collapsed)
                {
                    collapsedCells++;
                }
            }
            
            cells.Dispose();
            
            // Check if generation completed (all cells collapsed)
            if (collapsedCells == totalCells && totalCells > 0)
            {
                CompleteTest(true, $"WFC generation completed successfully in {wfcComponent.iterations} iterations. {collapsedCells}/{totalCells} cells collapsed.");
                return;
            }
            
            // Check if generation failed
            if (wfcComponent.iterations >= wfcComponent.maxIterations)
            {
                CompleteTest(false, $"WFC generation failed after {wfcComponent.iterations} iterations. {collapsedCells}/{totalCells} cells collapsed.");
                return;
            }
            
            // Log progress every second
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            if (math.floor(currentTime) > math.floor(currentTime - SystemAPI.Time.DeltaTime))
            {
                DOTS.Terrain.Core.DebugSettings.LogTest($"WFCSystemTest: Progress - {collapsedCells}/{totalCells} cells collapsed, Iterations: {wfcComponent.iterations}");
            }
        }
        
        /// <summary>
        /// Completes the test with results
        /// </summary>
        private void CompleteTest(bool passed, string result)
        {
            testCompleted = true;
            testPassed = passed;
            testResult = result;
            
            // Signal completion to any listeners
            WFCTestEvents.SignalTestCompleted(passed, result);
            
            if (passed)
            {
                DOTS.Terrain.Core.DebugSettings.LogTest($"WFCSystemTest: PASSED - {result}", true);
                // Don't cleanup immediately - let rendering systems process the results
                // CleanupTest(); // Commented out to allow rendering
            }
            else
            {
                DOTS.Terrain.Core.DebugSettings.LogError($"WFCSystemTest: FAILED - {result}");
                // Clean up immediately on failure
                CleanupTest();
            }
        }
        
        /// <summary>
        /// Cleans up test entities
        /// </summary>
        private void CleanupTest()
        {
            if (EntityManager.Exists(testWFCEntity))
            {
                EntityManager.DestroyEntity(testWFCEntity);
            }
            
            // Clean up all WFCCell entities
            var cellQuery = GetEntityQuery(ComponentType.ReadOnly<WFCCell>());
            EntityManager.DestroyEntity(cellQuery);
            
            DOTS.Terrain.Core.DebugSettings.LogTest("WFCSystemTest: Cleanup completed");
        }
        
        /// <summary>
        /// Gets the test results
        /// </summary>
        public (bool passed, string result) GetTestResults()
        {
            return (testPassed, testResult);
        }
        
        /// <summary>
        /// Checks if the test is complete
        /// </summary>
        public bool IsTestComplete()
        {
            return testCompleted;
        }
        
        protected override void OnDestroy()
        {
            CleanupTest();
            DOTS.Terrain.Core.DebugSettings.LogTest("WFCSystemTest: Destroyed");
        }
    }
}
#endif 