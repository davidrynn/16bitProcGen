using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain.WFC.Authoring;
using System.Collections.Generic;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Control component for dungeon generation requests
    /// Add this to an entity to request dungeon generation
    /// </summary>
    public struct DungeonGenerationRequest : IComponentData
    {
        public bool isActive;
        public float3 position;
        public int2 size;
        public float cellSize;
    }
    
    /// <summary>
    /// System responsible for rendering dungeon elements based on WFC results
    /// Spawns entity prefabs for each collapsed WFC cell
    /// Only runs when dungeon generation is explicitly requested
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(HybridWFCSystem))]
    public partial class DungeonRenderingSystem : SystemBase
    {
        private EntityQuery wfcCellsQuery;
        
        // Cached prefab entities (created in code)
        private DungeonPrefabs prefabs;
        
        // Debug counter to reduce log spam
        private int updateCounter = 0;
        
        // Command buffer for deferred structural changes
        private EntityCommandBuffer ecb;
        
        // Flag to track if rendering is complete
        private bool renderingComplete = false;

        // Map of collapsed cell patterns for neighbor-aware orientation (key = packed int2)
        private Dictionary<long, int> cellPatternMap = new Dictionary<long, int>();
    private Dictionary<long, WFCCell> cellLookup = new Dictionary<long, WFCCell>();
        
        // Registry is required; bind prefabs each update when available
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem: OnCreate called", true);
            
            // Query for WFC cells
            wfcCellsQuery = GetEntityQuery(ComponentType.ReadOnly<WFCCell>());
            
            RequireForUpdate<WFCComponent>();
        }
        
        protected override void OnStartRunning()
        {
            // Macro-only: Require baked FBX-based prefabs from the registry
            if (!SystemAPI.HasSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>())
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem (Macro-only): Waiting for DungeonPrefabRegistry (not baked yet)");
                return;
            }

            var registry = SystemAPI.GetSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>();
            if (registry.roomFloorPrefab == Entity.Null || registry.roomEdgePrefab == Entity.Null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonRenderingSystem (Macro-only): roomFloorPrefab or roomEdgePrefab is not assigned in DungeonPrefabRegistry.");
                return;
            }

            prefabs = new DungeonPrefabs
            {
                floorPrefab = registry.roomFloorPrefab,
                wallPrefab = registry.roomEdgePrefab,
                doorPrefab = registry.doorPrefab,
                corridorPrefab = registry.corridorPrefab,
                cornerPrefab = registry.cornerPrefab
            };

            // Ensure prefab tags are present
            TagPrefabsAsPrefab(prefabs);
        }
        
        protected override void OnUpdate()
        {
            updateCounter++;
            
            // Create command buffer for this frame
            ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Require registry; bind prefabs per update when available
            if (!SystemAPI.HasSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>())
            {
                ecb.Dispose();
                return;
            }
            var registryNow = SystemAPI.GetSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>();
            if (registryNow.roomFloorPrefab == Entity.Null || registryNow.roomEdgePrefab == Entity.Null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonRenderingSystem (Macro-only): roomFloorPrefab or roomEdgePrefab is not assigned in DungeonPrefabRegistry.");
                ecb.Dispose();
                return;
            }
            prefabs = new DungeonPrefabs
            {
                floorPrefab = registryNow.roomFloorPrefab,
                wallPrefab = registryNow.roomEdgePrefab,
                doorPrefab = registryNow.doorPrefab,
                corridorPrefab = registryNow.corridorPrefab,
                cornerPrefab = registryNow.cornerPrefab
            };
            TagPrefabsAsPrefab(prefabs);
            
            // If rendering is complete, just dispose and return
            if (renderingComplete)
            {
                ecb.Dispose();
                return;
            }
            
            // Check if dungeon generation is requested
            bool dungeonGenerationRequested = false;
            int requestCount = 0;
            var requestQuery = GetEntityQuery(ComponentType.ReadOnly<DungeonGenerationRequest>());
            using (var requests = requestQuery.ToComponentDataArray<DungeonGenerationRequest>(Allocator.Temp))
            {
                requestCount = requests.Length;
                for (int i = 0; i < requests.Length && !dungeonGenerationRequested; i++)
                {
                    if (requests[i].isActive)
                    {
                        dungeonGenerationRequested = true;
                    }
                }
            }
                
            if (DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Requests={requestCount}, active={dungeonGenerationRequested}");
            }
                
            if (!dungeonGenerationRequested)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem (Macro-only): No active DungeonGenerationRequest. Skipping.", true);
                ecb.Dispose();
                return;
            }
            
            // Only log every 500 updates to reduce spam
            if (updateCounter % 500 == 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Processing dungeon generation (update {updateCounter})");
            }
            
            if (!SystemAPI.HasSingleton<WFCComponent>())
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem (Macro-only): No WFCComponent found.", true);
                ecb.Dispose();
                return;
            }
                
            var wfcComponent = SystemAPI.GetSingleton<WFCComponent>();
            if (DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): WFC state isCollapsed={wfcComponent.isCollapsed}, isGenerating={wfcComponent.isGenerating}, iterations={wfcComponent.iterations}");
            }
            
            // Only process if WFC is complete
            if (!wfcComponent.isCollapsed)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem (Macro-only): WFC not collapsed yet. Waiting.", true);
                ecb.Dispose();
                return;
            }

            using var cellEntities = wfcCellsQuery.ToEntityArray(Allocator.Temp);
            using var cellComponents = wfcCellsQuery.ToComponentDataArray<WFCCell>(Allocator.Temp);
            
            // Build a map of collapsed cell patterns for neighbor-aware wall orientation
            // Build type-safe map of cell -> pattern TYPE
            cellPatternMap.Clear();
            cellLookup.Clear();
            int collapsedCount = 0;
            ref var blobPatterns = ref wfcComponent.patterns.Value.patterns;
            int patternsLength = blobPatterns.Length;
            var patternTypes = new NativeArray<int>(patternsLength, Allocator.Temp);
            for (int i = 0; i < patternsLength; i++)
            {
                patternTypes[i] = blobPatterns[i].type;
            }
            for (int i = 0; i < cellComponents.Length; i++)
            {
                var cell = cellComponents[i];
                if (!cell.collapsed)
                {
                    continue;
                }

                if (cell.selectedPattern < 0 || cell.selectedPattern >= patternsLength)
                {
                    continue;
                }

                int patType = patternTypes[cell.selectedPattern];
                cellPatternMap[MakeKey(cell.position)] = patType;
                cellLookup[MakeKey(cell.position)] = cell;
                collapsedCount++;
            }
            patternTypes.Dispose();
            if (DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Collapsed cells counted={collapsedCount}");
            }

            // Process cells that need visualization
            int processedCells = 0;
            int totalCells = 0;
            int collapsedCells = 0;
            int visualizedCells = 0;
            int collapsedNotVisualized = 0;
            int sampleCount = 0;
            
            for (int i = 0; i < cellComponents.Length; i++)
            {
                var entity = cellEntities[i];
                var cell = cellComponents[i];

                totalCells++;
                if (cell.collapsed)
                {
                    collapsedCells++;
                }

                if (cell.visualized)
                {
                    visualizedCells++;
                }

                if (sampleCount < 5)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Sample cell at ({cell.position.x},{cell.position.y}) - collapsed: {cell.collapsed}, visualized: {cell.visualized}, pattern: {cell.selectedPattern}");
                    sampleCount++;
                }

                if (cell.collapsed && !cell.visualized)
                {
                    collapsedNotVisualized++;
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Spawning element for cell ({cell.position.x},{cell.position.y}) pattern={cell.selectedPattern}", true);
                    SpawnDungeonElement(ref cell);
                    cell.visualized = true;
                    processedCells++;
                    EntityManager.SetComponentData(entity, cell);
                    cellLookup[MakeKey(cell.position)] = cell;
                }
                else if (cell.visualized)
                {
                    // No changes, skip writeback
                }
            }
                
            if (DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Processed {processedCells}/{totalCells} cells for rendering");
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Cell state breakdown - Total: {totalCells}, Collapsed: {collapsedCells}, Visualized: {visualizedCells}, CollapsedNotVisualized: {collapsedNotVisualized}");
            }
            
            // Check if all cells are now visualized
            if (processedCells == 0 && totalCells > 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: RENDERING COMPLETE! All {totalCells} cells processed.");
                renderingComplete = true;
            }
            
            // If rendering is complete, stop processing
            if (renderingComplete)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem: Rendering complete - stopping updates");
                return;
            }
            
            // Play back the command buffer to apply structural changes
            ecb.Playback(EntityManager);
            ecb.Dispose();
            
            // Force a frame delay to ensure components are applied before visualization system runs
            if (processedCells > 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Applied {processedCells} entities with DungeonElementInstance components");
            }
        }
        
        private void SpawnDungeonElement(ref WFCCell cell)
        {
            Entity prefabToSpawn = Entity.Null;
            quaternion rotation = quaternion.identity;

            // Validate pattern index before array access
            var wfc = SystemAPI.GetSingleton<WFCComponent>();
            ref var blobPatterns = ref wfc.patterns.Value.patterns;
            
            if (cell.selectedPattern < 0 || cell.selectedPattern >= blobPatterns.Length)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Invalid pattern {cell.selectedPattern} for cell at {cell.position}, skipping spawn (valid range: 0-{blobPatterns.Length - 1})");
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Cell state - collapsed: {cell.collapsed}, visualized: {cell.visualized}");
                return; // Skip this cell instead of crashing
            }

            // Look up selected pattern to derive type and edges
            var pat = blobPatterns[cell.selectedPattern];

            // Validate WFC constraints with neighbors
            ValidateWFCConstraints(ref cell, pat, ref blobPatterns);

            // Log pattern and rotation for model alignment testing
            if (DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"MODEL ALIGNMENT TEST: Cell at ({cell.position.x},{cell.position.y}) - Pattern: {pat.type} sockets={GetSocketString(pat)} selectedPattern={cell.selectedPattern}");
            }

            // Select prefab by domain type
            switch ((DungeonPatternType)pat.type)
            {
                case DungeonPatternType.Floor:
                    prefabToSpawn = prefabs.floorPrefab;
                    break;

                case DungeonPatternType.Wall:
                    prefabToSpawn = prefabs.wallPrefab;
                    rotation = DetermineWallRotation(cell.position);
                    break;

                case DungeonPatternType.Door:
                    prefabToSpawn = prefabs.doorPrefab;
                    rotation = DetermineDeadEndRotation(pat);
                    break;

                case DungeonPatternType.Corridor:
                    prefabToSpawn = prefabs.corridorPrefab;
                    rotation = DetermineCorridorRotation(pat);
                    break;

                case DungeonPatternType.Corner:
                    prefabToSpawn = prefabs.cornerPrefab;
                    rotation = DetermineCornerRotation(pat);
                    break;
            }

            // Fallback: if only floor/wall prefabs are provided, map other patterns to floor
            if (prefabToSpawn == Entity.Null)
            {
                if (prefabs.floorPrefab != Entity.Null)
                {
                    prefabToSpawn = prefabs.floorPrefab;
                    rotation = quaternion.identity;
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Pattern {cell.selectedPattern} mapped to RoomFloor as fallback at ({cell.position.x}, {cell.position.y})");
                }
                else
                {
                    DOTS.Terrain.Core.DebugSettings.LogWarning($"DungeonRenderingSystem (Macro-only): No prefab for pattern {cell.selectedPattern} and no RoomFloor fallback available");
                }
            }
            
            if (prefabToSpawn != Entity.Null)
            {
                var instance = EntityManager.Instantiate(prefabToSpawn);
                
                // Set position and rotation using command buffer to ensure it's preserved
                // Place using configured cell size
                float cellSize = SystemAPI.GetSingleton<WFCComponent>().cellSize;
                var transform = new LocalTransform
                {
                    Position = new float3(cell.position.x * cellSize, 0, cell.position.y * cellSize),
                    Rotation = rotation,
                    Scale = 1f
                };
                ecb.SetComponent(instance, transform);
                
                // Ensure the DungeonElementComponent is properly set based on pattern TYPE (not index)
                var elementComponent = new DungeonElementComponent();
                switch ((DungeonPatternType)pat.type)
                {
                    case DungeonPatternType.Floor: elementComponent.elementType = DungeonElementType.RoomFloor; break;
                    case DungeonPatternType.Wall: elementComponent.elementType = DungeonElementType.RoomEdge; break;
                    case DungeonPatternType.Door: elementComponent.elementType = DungeonElementType.CorridorEndDoorway; break;
                    case DungeonPatternType.Corridor: elementComponent.elementType = DungeonElementType.Corridor; break;
                    case DungeonPatternType.Corner: elementComponent.elementType = DungeonElementType.Corner; break;
                }
                ecb.SetComponent(instance, elementComponent);
                
                // Debug: Check if the instance has the required components
                if (EntityManager.HasComponent<DungeonElementComponent>(instance))
                {
                    var elementData = EntityManager.GetComponentData<DungeonElementComponent>(instance);
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Spawned {elementData.elementType} at ({cell.position.x}, {cell.position.y}) with transform {transform.Position}");
                }
                else
                {
                    DOTS.Terrain.Core.DebugSettings.LogWarning($"DungeonRenderingSystem: Spawned entity missing DungeonElementComponent at ({cell.position.x}, {cell.position.y})");
                }
                
                // Add a component to track this as a spawned dungeon element (deferred via command buffer)
                ecb.AddComponent<DungeonElementInstance>(instance);
            }
        }

        // Packs an int2 into a long key (x in high 32 bits, y in low 32 bits)
        private static long MakeKey(int2 pos)
        {
            return ((long)pos.x << 32) ^ (uint)pos.y;
        }

        private bool IsWallAt(int2 pos)
        {
            if (cellPatternMap == null || cellPatternMap.Count == 0) return false;
            if (cellPatternMap.TryGetValue(MakeKey(pos), out var storedType))
            {
                return (DungeonPatternType)storedType == DungeonPatternType.Wall;
            }
            return false;
        }

        private quaternion DetermineWallRotation(int2 pos)
        {
            // Neighbor positions
            var north = new int2(pos.x, pos.y + 1);
            var south = new int2(pos.x, pos.y - 1);
            var east  = new int2(pos.x + 1, pos.y);
            var west  = new int2(pos.x - 1, pos.y);

            bool hasN = IsWallAt(north);
            bool hasS = IsWallAt(south);
            bool hasE = IsWallAt(east);
            bool hasW = IsWallAt(west);

            bool vertical = hasN || hasS;
            bool horizontal = hasE || hasW;

            // Straight segments
            if (horizontal && !vertical)
            {
                // Horizontal corridor: rotate 90° around Y (radians)
                return quaternion.Euler(0, math.radians(90f), 0);
            }
            if (vertical && !horizontal)
            {
                // Vertical corridor: no rotation
                return quaternion.identity;
            }

            // Corner case (both horizontal and vertical neighbors):
            // Prefer 90° so this wall forms the perpendicular turn visually.
            if (horizontal && vertical)
            {
                return quaternion.Euler(0, math.radians(90f), 0);
            }

            // Isolated wall or undefined neighborhood: default to no rotation
            return quaternion.identity;
        }

        private static quaternion DetermineCorridorRotation(WFCPattern pat)
        {
            // Corridor: two opposite edges open ('F'). Rotate so open edges align with world Z (N/S) when possible.
            bool openNS = pat.north == (byte)'F' && pat.south == (byte)'F';
            bool openEW = pat.east == (byte)'F' && pat.west == (byte)'F';
            if (openNS) return quaternion.identity;
            if (openEW) return quaternion.Euler(0, math.radians(90f), 0);
            return quaternion.identity;
        }

        private static quaternion DetermineCornerRotation(WFCPattern pat)
        {
            // Corner: two perpendicular edges open ('F'). Map NE->0, ES->90, SW->180, WN->270
            bool n = pat.north == (byte)'F';
            bool e = pat.east == (byte)'F';
            bool s = pat.south == (byte)'F';
            bool w = pat.west == (byte)'F';
            if (n && e) return quaternion.identity;                 // NE
            if (e && s) return quaternion.Euler(0, math.radians(90f), 0);  // ES
            if (s && w) return quaternion.Euler(0, math.radians(180f), 0); // SW
            if (w && n) return quaternion.Euler(0, math.radians(270f), 0); // WN
            return quaternion.identity;
        }

        private static quaternion DetermineDeadEndRotation(WFCPattern pat)
        {
            // DeadEnd has one open edge ('F'), three closed ('W')
            // Rotate to face the open edge
            if (pat.north == (byte)'F') return quaternion.identity;                 // 0°   - Opens North
            if (pat.east == (byte)'F') return quaternion.Euler(0, math.radians(90f), 0);  // 90°  - Opens East
            if (pat.south == (byte)'F') return quaternion.Euler(0, math.radians(180f), 0); // 180° - Opens South
            if (pat.west == (byte)'F') return quaternion.Euler(0, math.radians(270f), 0);  // 270° - Opens West
            
            // Fallback (should never occur for valid DeadEnd patterns)
            DOTS.Terrain.Core.DebugSettings.LogWarning($"DetermineDeadEndRotation: No open edge found for pattern {pat.patternId}");
            return quaternion.identity;
        }

        private void TagPrefabsAsPrefab(DungeonPrefabs createdPrefabs)
        {
            void EnsurePrefab(Entity e)
            {
                if (e != Entity.Null && !EntityManager.HasComponent<Prefab>(e))
                {
                    EntityManager.AddComponent<Prefab>(e);
                }
            }

            EnsurePrefab(createdPrefabs.floorPrefab);
            EnsurePrefab(createdPrefabs.wallPrefab);
            EnsurePrefab(createdPrefabs.doorPrefab);
            EnsurePrefab(createdPrefabs.corridorPrefab);
            EnsurePrefab(createdPrefabs.cornerPrefab);
        }

        /// <summary>
        /// Validates WFC constraints between a cell and its neighbors
        /// </summary>
        private void ValidateWFCConstraints(ref WFCCell cell, WFCPattern pattern, ref BlobArray<WFCPattern> blobPatterns)
        {
            if (!DOTS.Terrain.Core.DebugSettings.EnableRenderingDebug) return;

            int2 pos = cell.position;

            // Check each neighbor direction
            CheckNeighborConstraint(pos, pattern, ref blobPatterns, new int2(0, 1), "North", pattern.north, "South");
            CheckNeighborConstraint(pos, pattern, ref blobPatterns, new int2(1, 0), "East", pattern.east, "West");
            CheckNeighborConstraint(pos, pattern, ref blobPatterns, new int2(0, -1), "South", pattern.south, "North");
            CheckNeighborConstraint(pos, pattern, ref blobPatterns, new int2(-1, 0), "West", pattern.west, "East");
        }

        /// <summary>
        /// Checks constraint between a cell and one of its neighbors
        /// </summary>
        private void CheckNeighborConstraint(int2 pos, WFCPattern pattern, ref BlobArray<WFCPattern> blobPatterns, int2 offset, string direction, byte thisSocket, string neighborSocketName)
        {
            int2 neighborPos = pos + offset;

            if (!cellLookup.TryGetValue(MakeKey(neighborPos), out var neighbor))
            {
                return;
            }
            if (!neighbor.collapsed || neighbor.selectedPattern < 0 || neighbor.selectedPattern >= blobPatterns.Length)
                return;

            var neighborPattern = blobPatterns[neighbor.selectedPattern];
            byte neighborSocket = GetNeighborSocket(neighborPattern, neighborSocketName);

            // Check constraint violation
            if (thisSocket == (byte)'F' && neighborSocket == (byte)'W')
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"WFC CONSTRAINT VIOLATION: Cell at {pos} has {direction} open (F) but neighbor at {neighborPos} has {neighborSocketName} closed (W)");
                DOTS.Terrain.Core.DebugSettings.LogRendering($"  - This pattern: {pattern.type} sockets={GetSocketString(pattern)}");
                DOTS.Terrain.Core.DebugSettings.LogRendering($"  - Neighbor pattern: {neighborPattern.type} sockets={GetSocketString(neighborPattern)}");
            }
            else if (thisSocket == (byte)'W' && neighborSocket == (byte)'F')
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"WFC CONSTRAINT VIOLATION: Cell at {pos} has {direction} closed (W) but neighbor at {neighborPos} has {neighborSocketName} open (F)");
                DOTS.Terrain.Core.DebugSettings.LogRendering($"  - This pattern: {pattern.type} sockets={GetSocketString(pattern)}");
                DOTS.Terrain.Core.DebugSettings.LogRendering($"  - Neighbor pattern: {neighborPattern.type} sockets={GetSocketString(neighborPattern)}");
            }
        }

        /// <summary>
        /// Gets the socket value for a specific direction from a pattern
        /// </summary>
        private byte GetNeighborSocket(WFCPattern pattern, string socketName)
        {
            switch (socketName)
            {
                case "North": return pattern.north;
                case "East": return pattern.east;
                case "South": return pattern.south;
                case "West": return pattern.west;
                default: return (byte)'?';
            }
        }

        /// <summary>
        /// Gets a readable string representation of pattern sockets
        /// </summary>
        private string GetSocketString(WFCPattern pattern)
        {
            return $"{(char)pattern.north}{(char)pattern.east}{(char)pattern.south}{(char)pattern.west}";
        }
    }
} 