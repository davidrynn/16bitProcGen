#if UNITY_EDITOR
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Core;
using System.Collections.Generic;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Test system for WFC dungeon generation and rendering
    /// This test creates a WFC dungeon and ensures it renders properly
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class WFCDungeonRenderingTest : SystemBase
    {
        private bool testInitialized = false;
        private Entity testWFCEntity;
        private Entity dungeonRequestEntity;
        private float testStartTime;
        private float testTimeout = 15.0f; // 15 seconds timeout for generation + rendering
        private int currentPatternCount = 0;
        
        // Test results
        private bool testCompleted = false;
        private bool testPassed = false;
        private string testResult = "";
        
        // Static flag to control if this specific test can run
        private static bool enableWFCDungeonTest = false;
        
        public static void SetWFCDungeonTestEnabled(bool enabled)
        {
            enableWFCDungeonTest = enabled;
        }
        
        
        
        protected override void OnCreate()
        {
            // Only initialize if test systems are enabled AND WFC dungeon test is specifically enabled
            if (!DebugSettings.EnableTestSystems || !enableWFCDungeonTest)
            {
                return;
            }
            
            DebugSettings.LogTest("WFCDungeonRenderingTest: Initializing...", true);
            testInitialized = false;
            testCompleted = false;
            testPassed = false;
            testResult = "";
        }
        
        protected override void OnUpdate()
        {
            // Only run if test systems are enabled AND WFC dungeon test is specifically enabled
            if (!DebugSettings.EnableTestSystems || !enableWFCDungeonTest)
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
        /// Initializes the WFC dungeon rendering test
        /// </summary>
        private void InitializeTest()
        {
            DebugSettings.LogTest("WFCDungeonRenderingTest: Starting WFC dungeon rendering test...");
            
            // Create test WFC entity
            testWFCEntity = EntityManager.CreateEntity();
            
            // Create and assign the dungeon macro-tile pattern set
            var patternList = WFCBuilder.CreateDungeonMacroTilePatterns();
            currentPatternCount = patternList.Count;
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
                gridSize = new int2(12, 12), // Smaller grid for faster testing
                patternSize = 1,
                cellSize = 3.0f,
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
                maxIterations = 300 // Reduced for faster testing
            };
            EntityManager.AddComponentData(testWFCEntity, wfcComponent);
            
            // Add generation settings
            var settings = new WFCGenerationSettings
            {
                maxIterations = 300,
                constraintStrength = 1.0f,
                entropyThreshold = 0.1f,
                enableBacktracking = true,
                backtrackingLimit = 50,
                generationTimeout = 3.0f
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
            
            // Create dungeon generation request
            dungeonRequestEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<DungeonGenerationRequest>(dungeonRequestEntity);
            EntityManager.SetComponentData(dungeonRequestEntity, new DungeonGenerationRequest
            {
                isActive = true,
                position = new float3(0, 0, 0),
                size = new int2(12, 12),
                cellSize = 1.0f
            });
            
            testStartTime = (float)SystemAPI.Time.ElapsedTime;
            testInitialized = true;
            
            DebugSettings.LogTest("WFCDungeonRenderingTest: Test initialized with 12x12 grid and dungeon generation request");
        }
        
        /// <summary>
        /// Creates test WFC cells
        /// </summary>
        private void CreateTestCells()
        {
            int gridSize = 12;
            
            for (int y = 0; y < gridSize; y++)
            {
                for (int x = 0; x < gridSize; x++)
                {
                    var cellEntity = EntityManager.CreateEntity();
                    
                    var cell = new WFCCell
                    {
                        position = new int2(x, y),
                        collapsed = false,
                        entropy = math.min(32, math.max(1, currentPatternCount)),
                        selectedPattern = -1,
                        needsUpdate = true,
                        patternCount = math.min(32, math.max(1, currentPatternCount)),
                        visualized = false,
                        possiblePatternsMask = (currentPatternCount >= 32) ? 0xFFFFFFFFu : ((1u << currentPatternCount) - 1u)
                    };
                    
                    EntityManager.AddComponentData(cellEntity, cell);
                }
            }
            
            DebugSettings.LogTest($"WFCDungeonRenderingTest: Created {gridSize * gridSize} test cells");
        }
        
        /// <summary>
        /// Checks the progress of the WFC dungeon rendering test
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
            int visualizedCells = 0;
            int totalCells = cells.Length;
            
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].collapsed)
                {
                    collapsedCells++;
                }
                if (cells[i].visualized)
                {
                    visualizedCells++;
                }
            }
            
            cells.Dispose();
            
            // Check visualization via GameObject bridge (testing/editor only)
            var gameObjectVisualizedQuery = GetEntityQuery(ComponentType.ReadOnly<DungeonVisualized>());
            visualizedCells = gameObjectVisualizedQuery.CalculateEntityCount();
            
            // Check if generation completed (all cells collapsed)
            if (collapsedCells == totalCells && totalCells > 0)
            {
                // Check if rendering is also complete
                if (visualizedCells == totalCells)
                {
                    CompleteTest(true, $"WFC dungeon generation and rendering completed successfully in {wfcComponent.iterations} iterations. {collapsedCells}/{totalCells} cells collapsed and visualized.");
                    return;
                }
                else
                {
                    // Generation complete, waiting for rendering
                    float renderTime = (float)SystemAPI.Time.ElapsedTime;
                    if (math.floor(renderTime) > math.floor(renderTime - SystemAPI.Time.DeltaTime))
                    {
                        DebugSettings.LogTest($"WFCDungeonRenderingTest: Generation complete, waiting for rendering - {visualizedCells}/{totalCells} cells visualized");
                    }
                    return;
                }
            }
            
            // Check if generation failed
            if (wfcComponent.iterations >= wfcComponent.maxIterations)
            {
                CompleteTest(false, $"WFC generation failed after {wfcComponent.iterations} iterations. {collapsedCells}/{totalCells} cells collapsed.");
                return;
            }
            
            // Log progress every second
            float progressTime = (float)SystemAPI.Time.ElapsedTime;
            if (math.floor(progressTime) > math.floor(progressTime - SystemAPI.Time.DeltaTime))
            {
                DebugSettings.LogTest($"WFCDungeonRenderingTest: Progress - {collapsedCells}/{totalCells} cells collapsed, {visualizedCells}/{totalCells} visualized, Iterations: {wfcComponent.iterations}");
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
                DebugSettings.LogTest($"WFCDungeonRenderingTest: PASSED - {result}", true);
                // Don't cleanup immediately - let the dungeon remain visible
            }
            else
            {
                DebugSettings.LogError($"WFCDungeonRenderingTest: FAILED - {result}");
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
            
            if (EntityManager.Exists(dungeonRequestEntity))
            {
                EntityManager.DestroyEntity(dungeonRequestEntity);
            }
            
            // Clean up all WFCCell entities
            var cellQuery = GetEntityQuery(ComponentType.ReadOnly<WFCCell>());
            EntityManager.DestroyEntity(cellQuery);
            
            // Clean up all DungeonElementInstance entities
            var elementQuery = GetEntityQuery(ComponentType.ReadOnly<DungeonElementInstance>());
            EntityManager.DestroyEntity(elementQuery);
            
            DebugSettings.LogTest("WFCDungeonRenderingTest: Cleanup completed");
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
            DebugSettings.LogTest("WFCDungeonRenderingTest: Destroyed");
        }
    }
}
#endif 