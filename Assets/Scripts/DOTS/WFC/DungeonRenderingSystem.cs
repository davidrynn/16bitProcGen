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
        
        // Track whether we've successfully bound to the authoring registry
        private bool usingAuthoringRegistry = false;
        
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

            DOTS.Terrain.Core.DebugSettings.LogRendering(
                $"DungeonRenderingSystem (Macro-only): Prefabs => roomFloor={(prefabs.floorPrefab!=Entity.Null)}, roomEdge={(prefabs.wallPrefab!=Entity.Null)}, corridor={(prefabs.corridorPrefab!=Entity.Null)}, corner={(prefabs.cornerPrefab!=Entity.Null)}, door={(prefabs.doorPrefab!=Entity.Null)}");

            // Ensure prefab tags are present
            TagPrefabsAsPrefab(prefabs);
            usingAuthoringRegistry = true;
        }
        
        protected override void OnUpdate()
        {
            updateCounter++;
            
            // Create command buffer for this frame
            ecb = new EntityCommandBuffer(Allocator.TempJob);

            // Early diagnostics
            bool hasRegistrySingleton = SystemAPI.HasSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>();
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): OnUpdate enter, usingAuthoringRegistry={usingAuthoringRegistry}, hasRegistrySingleton={hasRegistrySingleton}", true);

            // Macro-only: Late-bind registry if not yet bound; require macro fields
            if (!usingAuthoringRegistry)
            {
                if (!SystemAPI.HasSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>())
                {
                    // Keep waiting until registry is available
                    DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem (Macro-only): Registry singleton not found. Waiting.", true);
                    ecb.Dispose();
                    return;
                }
                var registry = SystemAPI.GetSingleton<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>();
                if (registry.roomFloorPrefab == Entity.Null || registry.roomEdgePrefab == Entity.Null)
                {
                    DOTS.Terrain.Core.DebugSettings.LogError("DungeonRenderingSystem (Macro-only): roomFloorPrefab or roomEdgePrefab is not assigned in DungeonPrefabRegistry.");
                    ecb.Dispose();
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
                TagPrefabsAsPrefab(prefabs);
                usingAuthoringRegistry = true;
                DOTS.Terrain.Core.DebugSettings.LogRendering(
                    $"DungeonRenderingSystem (Macro-only): Bound DungeonPrefabRegistry (FBX). Prefabs => roomFloor={(prefabs.floorPrefab!=Entity.Null)}, roomEdge={(prefabs.wallPrefab!=Entity.Null)}, corridor={(prefabs.corridorPrefab!=Entity.Null)}, corner={(prefabs.cornerPrefab!=Entity.Null)}, door={(prefabs.doorPrefab!=Entity.Null)}");
            }
            
            // If rendering is complete, just dispose and return
            if (renderingComplete)
            {
                ecb.Dispose();
                return;
            }
            
            // Check if dungeon generation is requested
            bool dungeonGenerationRequested = false;
            int requestCount = 0;
            Entities
                .WithAll<DungeonGenerationRequest>()
                .ForEach((in DungeonGenerationRequest request) =>
                {
                    requestCount++;
                    if (request.isActive)
                    {
                        dungeonGenerationRequested = true;
                    }
                }).WithoutBurst().Run();
                
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Requests={requestCount}, active={dungeonGenerationRequested}", true);
                
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
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): WFC state isCollapsed={wfcComponent.isCollapsed}, isGenerating={wfcComponent.isGenerating}, iterations={wfcComponent.iterations}", true);
            
            // Only process if WFC is complete
            if (!wfcComponent.isCollapsed)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem (Macro-only): WFC not collapsed yet. Waiting.", true);
                ecb.Dispose();
                return;
            }
            
            // Build a map of collapsed cell patterns for neighbor-aware wall orientation
            cellPatternMap.Clear();
            int collapsedCount = 0;
            Entities
                .WithAll<WFCCell>()
                .ForEach((in WFCCell cell) =>
                {
                    if (cell.collapsed)
                    {
                        cellPatternMap[MakeKey(cell.position)] = cell.selectedPattern;
                        collapsedCount++;
                    }
                }).WithoutBurst().Run();
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Collapsed cells counted={collapsedCount}", true);

            // Process cells that need visualization
            int processedCells = 0;
            int totalCells = 0;
            Entities
                .WithAll<WFCCell>()
                .ForEach((Entity entity, ref WFCCell cell) =>
                {
                    totalCells++;
                    if (cell.collapsed && !cell.visualized)
                    {
                        DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Spawning element for cell ({cell.position.x},{cell.position.y}) pattern={cell.selectedPattern}", true);
                        SpawnDungeonElement(ref cell);
                        cell.visualized = true;
                        processedCells++;
                    }
                }).WithoutBurst().Run();
                
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem (Macro-only): Processed {processedCells}/{totalCells} cells for rendering", true);
            
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

            // Look up selected pattern to derive type and edges
            var wfc = SystemAPI.GetSingleton<WFCComponent>();
            ref var patterns = ref wfc.patterns.Value.patterns;
            var pat = patterns[cell.selectedPattern];

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
            if (cellPatternMap.TryGetValue(MakeKey(pos), out var pattern))
            {
                return pattern == 1; // 1 = Wall
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
    }
} 