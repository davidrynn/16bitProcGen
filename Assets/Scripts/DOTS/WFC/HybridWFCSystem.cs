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
        
        // Random number generator for WFC (Burst-compatible)
        private Unity.Mathematics.Random random;
        
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
            
            // Initialize random generator (configurable seed for testing)
            if (DOTS.Terrain.Core.DebugSettings.UseFixedWFCSeed)
            {
                random = new Unity.Mathematics.Random((uint)DOTS.Terrain.Core.DebugSettings.FixedWFCSeed);
                DOTS.Terrain.Core.DebugSettings.LogWFC($"HybridWFCSystem: Random seed initialized to {DOTS.Terrain.Core.DebugSettings.FixedWFCSeed} for deterministic testing");
            }
            else
            {
                random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
                DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Random generator initialized with time-based seed");
            }
            
            DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Initialization complete");
        }
        
        private bool HasAdjacentWall(int2 pos)
        {
            // Check immediate N/E/S/W neighbors for walls
            bool northWall = IsWallAt(new int2(pos.x, pos.y + 1));
            bool southWall = IsWallAt(new int2(pos.x, pos.y - 1));
            bool eastWall  = IsWallAt(new int2(pos.x + 1, pos.y));
            bool westWall  = IsWallAt(new int2(pos.x - 1, pos.y));

            // If any neighbor is wall without an interposed floor, disallow wall selection here
            // This approximates "no wall face adjacency" by forbidding immediate wall-wall faces.
            if (northWall || southWall || eastWall || westWall)
            {
                return true;
            }
            return false;
        }

        private bool IsWallAt(int2 pos)
        {
            // Query WFCCell for this position; for simplicity scan cells (grid sizes are small in tests)
            var cells = cellQuery.ToComponentDataArray<WFCCell>(Allocator.Temp);
            for (int i = 0; i < cells.Length; i++)
            {
                if (cells[i].position.x == pos.x && cells[i].position.y == pos.y && cells[i].collapsed && cells[i].selectedPattern == 1)
                {
                    cells.Dispose();
                    return true;
                }
            }
            cells.Dispose();
            return false;
        }
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            float deltaTime = currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;
            
            // Check if any WFC is still active
            bool hasActiveWFC = false;
            bool hasCompletedWFC = false;
            foreach (var wfc in SystemAPI.Query<RefRO<WFCComponent>>())
            {
                if (wfc.ValueRO.isGenerating || wfc.ValueRO.needsGeneration)
                {
                    hasActiveWFC = true;
                }
                if (wfc.ValueRO.isCollapsed)
                {
                    hasCompletedWFC = true;
                }
            }
            
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
            
            foreach (var (wfc, entity) in SystemAPI.Query<RefRW<WFCComponent>>().WithEntityAccess())
            {
                if (wfc.ValueRO.needsGeneration && !wfc.ValueRO.isGenerating)
                {
                    DebugLog($"Starting WFC generation for entity {entity.Index}");
                    
                    // Start generation
                    wfc.ValueRW.isGenerating = true;
                    wfc.ValueRW.generationProgress = 0.0f;
                    wfc.ValueRW.iterations = 0;
                    
                    // Initialize WFC data if needed
                    if (wfc.ValueRO.patterns == BlobAssetReference<WFCPatternData>.Null)
                    {
                        InitializeWFCData(ref wfc.ValueRW);
                    }
                    
                    wfcEntitiesProcessed++;
                }
                else if (wfc.ValueRO.isGenerating)
                {
                    // Continue generation
                    ContinueWFCGeneration(ref wfc.ValueRW);
                }
            }
        }
        
        /// <summary>
        /// Processes individual WFC cells
        /// </summary>
        private void ProcessWFCCells()
        {
            cellsProcessed = 0;
            
            // Check if WFC is complete - if so, don't process cells
            bool wfcComplete = false;
            foreach (var wfc in SystemAPI.Query<RefRO<WFCComponent>>())
            {
                if (wfc.ValueRO.isCollapsed)
                {
                    wfcComplete = true;
                }
            }
            
            if (wfcComplete)
            {
                DebugLog("WFC is complete - skipping cell processing");
                return;
            }
            
            foreach (var cell in SystemAPI.Query<RefRW<WFCCell>>())
            {
                if (!cell.ValueRO.collapsed)
                {
                    // Always process non-collapsed cells
                    ProcessWFCResults(ref cell.ValueRW);
                    
                    // Add some gradual entropy reduction for cells that don't collapse
                    if (!cell.ValueRO.collapsed && cell.ValueRO.entropy > 1)
                    {
                        cell.ValueRW.entropy = math.max(1, cell.ValueRO.entropy - 0.5f);
                    }
                    
                    cellsProcessed++;
                }
            }
                
            if (cellsProcessed > 0)
            {
                DebugLog($"Processed {cellsProcessed} cells this frame");
            }
        }
        
        /// <summary>
        /// Initializes WFC data with default patterns and constraints
        /// </summary>
        private void InitializeWFCData(ref WFCComponent wfc)
        {
            DebugLog("Initializing WFC data with dungeon macro-tile patterns and constraints");
            
            // Create macro-tile patterns with rotated variants
            var patterns = WFCBuilder.CreateDungeonMacroTilePatterns();
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
                // Allow all macro-tile patterns (up to 32)
                var initWfcComponents = wfcQuery.ToComponentDataArray<WFCComponent>(Allocator.Temp);
                int patternCount = 0;
                if (initWfcComponents.Length > 0 && initWfcComponents[0].patterns != BlobAssetReference<WFCPatternData>.Null)
                {
                    ref var patternDataRef = ref initWfcComponents[0].patterns.Value;
                    patternCount = patternDataRef.patternCount;
                }
                initWfcComponents.Dispose();
                uint mask = (patternCount >= 32) ? 0xFFFFFFFFu : ((1u << patternCount) - 1u);
                cell.possiblePatternsMask = mask;
                cell.patternCount = math.min(32, patternCount);
                DebugLog($"Initialized cell at {cell.position} with {cell.patternCount} macro-tile patterns");
            }
            
            // Prune possibilities using collapsed neighbors and macro-tile edge rules
            PruneWithCollapsedNeighbors(ref cell);
            
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
            else if (possibleCount <= 3 && random.NextFloat() < 0.5f)
            {
                // Random collapse among currently possible patterns
                DebugLog($"Cell at {cell.position} considering random collapse (entropy={possibleCount})");
                
                int[] possiblePatterns = new int[possibleCount];
                int index = 0;
                for (int i = 0; i < 32; i++)
                {
                    if (WFCCellHelpers.IsPatternPossible(ref cell, i))
                    {
                        possiblePatterns[index++] = i;
                    }
                }
                int selectedPattern = possiblePatterns[random.NextInt(0, possibleCount)];
                cell.selectedPattern = selectedPattern;
                cell.collapsed = true;
                DebugLog($"Cell at {cell.position} collapsed to pattern {selectedPattern} (random collapse)");
            }
            else if (random.NextFloat() < 0.1f)
            {
                // Force collapse: choose any possible pattern randomly
                DebugLog($"Cell at {cell.position} forced collapse (entropy={possibleCount})");
                
                int[] possiblePatterns = new int[possibleCount];
                int index = 0;
                for (int i = 0; i < 32; i++)
                {
                    if (WFCCellHelpers.IsPatternPossible(ref cell, i))
                    {
                        possiblePatterns[index++] = i;
                    }
                }
                int selectedPattern = possiblePatterns.Length > 0 ? possiblePatterns[random.NextInt(0, possiblePatterns.Length)] : WFCCellHelpers.GetFirstPossiblePattern(cell.possiblePatternsMask);
                cell.selectedPattern = selectedPattern;
                cell.collapsed = true;
                DebugLog($"Cell at {cell.position} collapsed to pattern {selectedPattern} (forced collapse)");
            }
            else
            {
                // Reduced debug logging - only log occasionally to avoid spam
                if (cell.position.x < 2 && cell.position.y < 2 && random.NextFloat() < 0.01f)
                {
                    DebugLog($"Cell at {cell.position} not collapsing: possibleCount={possibleCount}");
                }
            }
            // Otherwise, don't collapse - let entropy reduction happen naturally
        }

        /// <summary>
        /// Applies neighbor-based constraint pruning: for each collapsed neighbor,
        /// remove any patterns in this cell that are incompatible at that edge.
        /// </summary>
        private void PruneWithCollapsedNeighbors(ref WFCCell cell)
        {
            // Load pattern blob once
            var wfcComponents = wfcQuery.ToComponentDataArray<WFCComponent>(Allocator.Temp);
            if (wfcComponents.Length == 0 || wfcComponents[0].patterns == BlobAssetReference<WFCPatternData>.Null)
            {
                return;
            }
            ref var patternData = ref wfcComponents[0].patterns.Value;
            wfcComponents.Dispose();
            
            // Fetch all cells to inspect neighbors (grids are small in tests)
            var allCells = cellQuery.ToComponentDataArray<WFCCell>(Allocator.Temp);
            
            // Helper to find a collapsed neighbor by position
            bool TryGetCollapsedNeighbor(int2 neighborPos, out WFCCell neighbor)
            {
                for (int i = 0; i < allCells.Length; i++)
                {
                    var c = allCells[i];
                    if (c.position.x == neighborPos.x && c.position.y == neighborPos.y && c.collapsed && c.selectedPattern >= 0)
                    {
                        neighbor = c;
                        return true;
                    }
                }
                neighbor = default;
                return false;
            }
            
            // Copy mask to modify
            uint newMask = cell.possiblePatternsMask;
            
            // For each direction, if neighbor collapsed, keep only patterns compatible with it
            // 0=N, 1=E, 2=S, 3=W
            int2 pos = cell.position;
            // North neighbor
            if (TryGetCollapsedNeighbor(new int2(pos.x, pos.y + 1), out var nCell))
            {
                int neighborIdx = nCell.selectedPattern;
                var neighborPat = patternData.patterns[neighborIdx];
                for (int i = 0; i < math.min(32, patternData.patternCount); i++)
                {
                    if ((newMask & (1u << i)) == 0) continue;
                    var pat = patternData.patterns[i];
                    if (!WFCBuilder.PatternsAreCompatible(pat, neighborPat, 0))
                    {
                        newMask &= ~(1u << i);
                    }
                }
            }
            // East neighbor
            if (TryGetCollapsedNeighbor(new int2(pos.x + 1, pos.y), out var eCell))
            {
                int neighborIdx = eCell.selectedPattern;
                var neighborPat = patternData.patterns[neighborIdx];
                for (int i = 0; i < math.min(32, patternData.patternCount); i++)
                {
                    if ((newMask & (1u << i)) == 0) continue;
                    var pat = patternData.patterns[i];
                    if (!WFCBuilder.PatternsAreCompatible(pat, neighborPat, 1))
                    {
                        newMask &= ~(1u << i);
                    }
                }
            }
            // South neighbor
            if (TryGetCollapsedNeighbor(new int2(pos.x, pos.y - 1), out var sCell))
            {
                int neighborIdx = sCell.selectedPattern;
                var neighborPat = patternData.patterns[neighborIdx];
                for (int i = 0; i < math.min(32, patternData.patternCount); i++)
                {
                    if ((newMask & (1u << i)) == 0) continue;
                    var pat = patternData.patterns[i];
                    if (!WFCBuilder.PatternsAreCompatible(pat, neighborPat, 2))
                    {
                        newMask &= ~(1u << i);
                    }
                }
            }
            // West neighbor
            if (TryGetCollapsedNeighbor(new int2(pos.x - 1, pos.y), out var wCell))
            {
                int neighborIdx = wCell.selectedPattern;
                var neighborPat = patternData.patterns[neighborIdx];
                for (int i = 0; i < math.min(32, patternData.patternCount); i++)
                {
                    if ((newMask & (1u << i)) == 0) continue;
                    var pat = patternData.patterns[i];
                    if (!WFCBuilder.PatternsAreCompatible(pat, neighborPat, 3))
                    {
                        newMask &= ~(1u << i);
                    }
                }
            }
            
            // Apply pruned mask
            cell.possiblePatternsMask = newMask;
            cell.patternCount = WFCCellHelpers.CountPossiblePatterns(newMask);
            
            allCells.Dispose();
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
                    // Fallback simple selection: pick first available pattern
                    cell.selectedPattern = WFCCellHelpers.GetFirstPossiblePattern(cell.possiblePatternsMask);
                    cell.entropy = 0f;
                }
            }
            
            processedCells[index] = cell;
        }
    }
} 