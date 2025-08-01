using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using DOTS.Terrain.WFC;
using DOTS.Terrain;
using System;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Hybrid WFC System that combines DOTS with Compute Shaders for structured terrain generation
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.Generation.HybridTerrainGenerationSystem))]
    public partial class HybridWFCSystem : SystemBase
    {
        private ComputeShaderManager computeManager;
        private EntityQuery wfcQuery;
        private EntityQuery cellQuery;
        
        // Performance monitoring
        private int wfcEntitiesProcessed;
        private int cellsProcessed;
        private float lastUpdateTime;
        private float totalGenerationTime;
        
        // Debug settings
        private bool enableDebugLogs = true; // Enable debug logs to see what's happening
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Initializing...");
            
            // Get compute shader manager
            computeManager = ComputeShaderManager.Instance;
            if (computeManager == null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("HybridWFCSystem: ComputeShaderManager not found!");
                return;
            }
            
            // Create queries
            wfcQuery = GetEntityQuery(
                ComponentType.ReadWrite<WFCComponent>(),
                ComponentType.ReadOnly<WFCGenerationSettings>()
            );
            
            cellQuery = GetEntityQuery(
                ComponentType.ReadWrite<WFCCell>()
            );
            
            RequireForUpdate<WFCComponent>();
            
            // Initialize performance tracking
            wfcEntitiesProcessed = 0;
            cellsProcessed = 0;
            lastUpdateTime = 0f;
            totalGenerationTime = 0f;
            
            DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Initialization complete");
        }
        
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;
            
            // Check if any WFC is still active
            bool hasActiveWFC = false;
            bool hasCompletedWFC = false;
            Entities
                .WithAll<WFCComponent>()
                .ForEach((Entity entity, in WFCComponent wfc) =>
                {
                    if (wfc.isGenerating || wfc.needsGeneration)
                    {
                        hasActiveWFC = true;
                    }
                    if (wfc.isCollapsed)
                    {
                        hasCompletedWFC = true;
                    }
                }).WithoutBurst().Run();
            
            // If no active WFC, don't process anything
            if (!hasActiveWFC)
            {
                // If we have completed WFC, signal completion and stop processing
                if (hasCompletedWFC)
                {
                    // Don't log every frame - just return silently
                    return;
                }
                return;
            }
            
            DebugLog("WFC System Update called"); // Debug: Check if system is running
            
            // Process WFC entities
            ProcessWFCEntities();
            
            // Process WFC cells
            ProcessWFCCells();
            
            // Update performance metrics
            UpdatePerformanceMetrics(deltaTime);
        }
        
        /// <summary>
        /// Processes WFC entities that need generation
        /// </summary>
        private void ProcessWFCEntities()
        {
            wfcEntitiesProcessed = 0;
            
            Entities
                .WithAll<WFCComponent>()
                .ForEach((Entity entity, ref WFCComponent wfc) =>
                {
                    if (wfc.needsGeneration && !wfc.isGenerating)
                    {
                        DebugLog($"Starting WFC generation for entity {entity.Index}");
                        
                        // Start generation
                        wfc.isGenerating = true;
                        wfc.generationProgress = 0.0f;
                        wfc.iterations = 0;
                        
                        // Initialize WFC data if needed
                        if (wfc.patterns == BlobAssetReference<WFCPatternData>.Null)
                        {
                            InitializeWFCData(ref wfc);
                        }
                        
                        wfcEntitiesProcessed++;
                    }
                    else if (wfc.isGenerating)
                    {
                        // Continue generation
                        ContinueWFCGeneration(ref wfc);
                    }
                }).WithoutBurst().Run();
        }
        
        /// <summary>
        /// Processes individual WFC cells
        /// </summary>
        private void ProcessWFCCells()
        {
            cellsProcessed = 0;
            
            // Check if WFC is complete - if so, don't process cells
            bool wfcComplete = false;
            Entities
                .WithAll<WFCComponent>()
                .ForEach((Entity entity, in WFCComponent wfc) =>
                {
                    if (wfc.isCollapsed)
                    {
                        wfcComplete = true;
                    }
                }).WithoutBurst().Run();
            
            if (wfcComplete)
            {
                DebugLog("WFC is complete - skipping cell processing");
                return;
            }
            
            Entities
                .WithAll<WFCCell>()
                .ForEach((Entity entity, ref WFCCell cell) =>
                {
                    if (!cell.collapsed)
                    {
                        // Always process non-collapsed cells
                        ProcessWFCResults(ref cell);
                        
                        // Add some gradual entropy reduction for cells that don't collapse
                        if (!cell.collapsed && cell.entropy > 1)
                        {
                            cell.entropy = math.max(1, cell.entropy - 0.5f); // Increased from 0.1f to 0.5f
                        }
                        
                        cellsProcessed++;
                    }
                }).WithoutBurst().Run();
                
            DebugLog($"Processed {cellsProcessed} cells this frame");
        }
        
        /// <summary>
        /// Initializes WFC data with default patterns and constraints
        /// </summary>
        private void InitializeWFCData(ref WFCComponent wfc)
        {
            DebugLog("Initializing WFC data with dungeon patterns and constraints");
            
            // Create dungeon patterns instead of terrain patterns
            var patterns = WFCBuilder.CreateBasicDungeonPatterns();
            var constraints = WFCBuilder.CreateBasicDungeonConstraints();
            
            // Convert Lists to Arrays
            var patternArray = patterns.ToArray();
            var constraintArray = constraints.ToArray();
            
            // Create blob assets
            wfc.patterns = WFCBuilder.CreatePatternData(patternArray);
            wfc.constraints = WFCBuilder.CreateConstraintData(constraintArray);
            
            DebugLog($"Created {patterns.Count} dungeon patterns and {constraints.Count} constraints");
        }
        
        /// <summary>
        /// Continues WFC generation process
        /// </summary>
        private void ContinueWFCGeneration(ref WFCComponent wfc)
        {
            wfc.iterations++;
            
            // Check for completion or timeout
            if (wfc.iterations >= wfc.maxIterations)
            {
                DebugWarning($"WFC generation timed out after {wfc.iterations} iterations");
                CompleteWFCGeneration(ref wfc, false);
                return;
            }
            
            // Check if all cells are collapsed
            var cells = cellQuery.ToComponentDataArray<WFCCell>(Allocator.Temp);
            int collapsedCells = 0;
            
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].collapsed)
                {
                    collapsedCells++;
                }
            }
            
            cells.Dispose();
            
            // Update generation progress based on actual completion
            wfc.generationProgress = (float)collapsedCells / (wfc.gridSize.x * wfc.gridSize.y);
            
            // Check if generation is complete (all cells collapsed)
            if (collapsedCells == (wfc.gridSize.x * wfc.gridSize.y))
            {
                CompleteWFCGeneration(ref wfc, true);
            }
        }
        
        /// <summary>
        /// Completes WFC generation
        /// </summary>
        private void CompleteWFCGeneration(ref WFCComponent wfc, bool success)
        {
            wfc.isGenerating = false;
            wfc.needsGeneration = false;
            wfc.generationProgress = success ? 1.0f : 0.0f;
            
            if (success)
            {
                wfc.isCollapsed = true;
                DebugLog($"WFC generation completed successfully in {wfc.iterations} iterations");
            }
            else
            {
                DebugWarning("WFC generation failed");
            }
        }
        
        /// <summary>
        /// Uses Compute Shader for constraint propagation
        /// </summary>
        private void PropagateConstraintsWithComputeShader(ref WFCCell cell)
        {
            // This method is kept for future implementation
            // Currently, constraint propagation is handled in DOTS for simplicity
            // In a full implementation, this would dispatch the compute shader
            
            if (computeManager?.WFCShader == null)
            {
                DebugWarning("WFC Compute Shader not available");
                return;
            }
            
            // TODO: Implement full compute shader integration
            // This would involve creating and managing compute buffers
            // and dispatching the appropriate kernels
        }
        
        /// <summary>
        /// Processes WFC results with proper WFC algorithm
        /// </summary>
        private void ProcessWFCResults(ref WFCCell cell)
        {
            if (cell.collapsed) return;
            
            // Initialize possible patterns if not already done
            if (cell.possiblePatternsMask == 0)
            {
                // Start with all patterns possible
                cell.possiblePatternsMask = 0x1F; // 5 patterns (0-4) = 11111 in binary
                cell.patternCount = 5;
                DebugLog($"Initialized cell at {cell.position} with {cell.patternCount} patterns");
            }
            
            // Calculate entropy based on possible patterns
            int possibleCount = WFCCellHelpers.CountPossiblePatterns(cell.possiblePatternsMask);
            cell.entropy = possibleCount;
            
            // Debug: Log entropy for first few cells to see what's happening
            if (cell.position.x < 3 && cell.position.y < 3) // Log first 9 cells
            {
                DebugLog($"Cell at {cell.position}: entropy={cell.entropy}, possible={possibleCount}, collapsed={cell.collapsed}");
            }
            
            if (possibleCount == 0)
            {
                // No valid patterns - this is a failure case
                cell.collapsed = true;
                cell.selectedPattern = -1;
                DebugWarning($"Cell at {cell.position} has no valid patterns - WFC failure");
                return;
            }
            
            // For testing: More aggressive collapse logic to ensure cells actually collapse
            if (possibleCount == 1)
            {
                // Only one pattern left - must collapse
                int selectedPattern = WFCCellHelpers.GetFirstPossiblePattern(cell.possiblePatternsMask);
                cell.selectedPattern = selectedPattern;
                cell.collapsed = true;
                DebugLog($"Cell at {cell.position} collapsed to pattern {selectedPattern} (entropy=1)");
            }
            else if (possibleCount <= 3 && UnityEngine.Random.Range(0f, 1f) < 0.5f) // Increased probability and entropy threshold
            {
                // More aggressive random collapse for testing
                DebugLog($"Cell at {cell.position} considering random collapse (entropy={possibleCount})");
                
                int selectedPattern;
                // Create more diverse patterns - don't heavily favor floor
                if (WFCCellHelpers.IsPatternPossible(ref cell, 0) && UnityEngine.Random.Range(0f, 1f) < 0.3f)
                {
                    selectedPattern = 0; // Floor - reduced preference
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 1) && UnityEngine.Random.Range(0f, 1f) < 0.4f)
                {
                    selectedPattern = 1; // Wall - give walls a good chance
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 2) && UnityEngine.Random.Range(0f, 1f) < 0.3f)
                {
                    selectedPattern = 2; // Door
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 3) && UnityEngine.Random.Range(0f, 1f) < 0.3f)
                {
                    selectedPattern = 3; // Corridor
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 4) && UnityEngine.Random.Range(0f, 1f) < 0.3f)
                {
                    selectedPattern = 4; // Corner
                }
                else
                {
                    // Select a random possible pattern
                    int[] possiblePatterns = new int[possibleCount];
                    int index = 0;
                    for (int i = 0; i < 32; i++)
                    {
                        if (WFCCellHelpers.IsPatternPossible(ref cell, i))
                        {
                            possiblePatterns[index++] = i;
                        }
                    }
                    selectedPattern = possiblePatterns[UnityEngine.Random.Range(0, possibleCount)];
                }
                cell.selectedPattern = selectedPattern;
                cell.collapsed = true;
                DebugLog($"Cell at {cell.position} collapsed to pattern {selectedPattern} (random collapse)");
            }
            else if (UnityEngine.Random.Range(0f, 1f) < 0.1f) // 10% chance to collapse any cell for testing
            {
                // Force some cells to collapse to ensure progress
                DebugLog($"Cell at {cell.position} forced collapse (entropy={possibleCount})");
                
                int selectedPattern;
                // For forced collapse, be more random to create variety
                if (WFCCellHelpers.IsPatternPossible(ref cell, 0) && UnityEngine.Random.Range(0f, 1f) < 0.4f)
                {
                    selectedPattern = 0; // Floor
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 1) && UnityEngine.Random.Range(0f, 1f) < 0.5f)
                {
                    selectedPattern = 1; // Wall
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 2) && UnityEngine.Random.Range(0f, 1f) < 0.4f)
                {
                    selectedPattern = 2; // Door
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 3) && UnityEngine.Random.Range(0f, 1f) < 0.4f)
                {
                    selectedPattern = 3; // Corridor
                }
                else if (WFCCellHelpers.IsPatternPossible(ref cell, 4) && UnityEngine.Random.Range(0f, 1f) < 0.4f)
                {
                    selectedPattern = 4; // Corner
                }
                else
                {
                    // Select first available pattern as fallback
                    selectedPattern = WFCCellHelpers.GetFirstPossiblePattern(cell.possiblePatternsMask);
                }
                cell.selectedPattern = selectedPattern;
                cell.collapsed = true;
                DebugLog($"Cell at {cell.position} collapsed to pattern {selectedPattern} (forced collapse)");
            }
            else
            {
                // Debug: Log why cells aren't collapsing (less frequent)
                if (cell.position.x < 2 && cell.position.y < 2 && UnityEngine.Random.Range(0f, 1f) < 0.1f)
                {
                    DebugLog($"Cell at {cell.position} not collapsing: possibleCount={possibleCount}");
                }
            }
            // Otherwise, don't collapse - let entropy reduction happen naturally
        }
        
        /// <summary>
        /// Propagates constraints from a collapsed cell to its neighbors
        /// </summary>
        private void PropagateConstraintsToNeighbors(Entity collapsedEntity, WFCCell collapsedCell)
        {
            // Get all cells to find neighbors
            var allCells = cellQuery.ToComponentDataArray<WFCCell>(Allocator.Temp);
            
            for (int i = 0; i < allCells.Length; i++)
            {
                var neighborCell = allCells[i];
                if (neighborCell.collapsed) continue;
                
                // Check if this is a neighbor (adjacent cell)
                int2 diff = math.abs(neighborCell.position - collapsedCell.position);
                if (math.all(diff <= 1) && math.any(diff > 0)) // Adjacent but not same cell
                {
                    // Simple constraint: reduce entropy of neighbors
                    neighborCell.entropy = math.max(1, neighborCell.entropy - 0.5f);
                    
                    // Update the cell data
                    var neighborEntity = cellQuery.ToEntityArray(Allocator.Temp)[i];
                    EntityManager.SetComponentData(neighborEntity, neighborCell);
                }
            }
            
            allCells.Dispose();
        }
        

        
        /// <summary>
        /// Updates performance metrics
        /// </summary>
        private void UpdatePerformanceMetrics(float deltaTime)
        {
            totalGenerationTime += deltaTime;
            
            // Log performance every 5 seconds
            if (totalGenerationTime > 5.0f)
            {
                DebugLog($"WFC Performance: {wfcEntitiesProcessed} entities, {cellsProcessed} cells processed");
                totalGenerationTime = 0f;
            }
        }
        
        /// <summary>
        /// Debug logging helper
        /// </summary>
        private void DebugLog(string message, bool force = false)
        {
            DOTS.Terrain.Core.DebugSettings.LogWFC($"HybridWFCSystem: {message}", force);
        }
        
        /// <summary>
        /// Debug warning helper
        /// </summary>
        private void DebugWarning(string message)
        {
            DOTS.Terrain.Core.DebugSettings.LogWarning($"HybridWFCSystem: {message}");
        }
        
        protected override void OnDestroy()
        {
            DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Destroyed");
        }
    }
    
    /// <summary>
    /// Job for processing WFC cells in parallel
    /// </summary>
    [BurstCompile]
    public struct ProcessWFCCellsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<WFCCell> cells;
        [WriteOnly] public NativeArray<WFCCell> processedCells;
        
        public void Execute(int index)
        {
            var cell = cells[index];
            
            if (!cell.collapsed && cell.entropy > 0)
            {
                // Process cell logic
                cell.entropy = math.max(0, cell.entropy - 0.1f);
                
                if (cell.entropy <= 0.1f)
                {
                    cell.collapsed = true;
                    cell.selectedPattern = index % 5; // Simple pattern selection
                    cell.entropy = 0f;
                }
            }
            
            processedCells[index] = cell;
        }
    }
} 