using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

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
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem: OnCreate called", true);
            
            // Query for WFC cells
            wfcCellsQuery = GetEntityQuery(ComponentType.ReadOnly<WFCCell>());
            
            RequireForUpdate<WFCComponent>();
        }
        
        protected override void OnStartRunning()
        {
            // Create prefab entities using pure code approach
            prefabs = DungeonPrefabCreator.CreatePrefabs(EntityManager);
            // Ensure prefabs are tagged so they are excluded by systems that should only process instances
            TagPrefabsAsPrefab(prefabs);
        }
        
        protected override void OnUpdate()
        {
            updateCounter++;
            
            // Create command buffer for this frame
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            
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
                
            if (updateCounter % 100 == 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Found {requestCount} DungeonGenerationRequest entities, active={dungeonGenerationRequested}");
            }
                
            if (!dungeonGenerationRequested)
            {
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
                if (updateCounter % 100 == 0)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonRenderingSystem: No WFCComponent found");
                }
                ecb.Dispose();
                return;
            }
                
            var wfcComponent = SystemAPI.GetSingleton<WFCComponent>();
            
            if (updateCounter % 100 == 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: WFC state - isCollapsed={wfcComponent.isCollapsed}, isGenerating={wfcComponent.isGenerating}, iterations={wfcComponent.iterations}");
            }
            
            // Only process if WFC is complete
            if (!wfcComponent.isCollapsed)
            {
                if (updateCounter % 100 == 0)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: WFC not collapsed yet (isCollapsed={wfcComponent.isCollapsed}, isGenerating={wfcComponent.isGenerating})");
                }
                ecb.Dispose();
                return;
            }
            
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
                        SpawnDungeonElement(ref cell);
                        cell.visualized = true;
                        processedCells++;
                    }
                }).WithoutBurst().Run();
                
            if (processedCells > 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Processed {processedCells} cells for rendering");
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
            
            // Select prefab and rotation based on pattern
            switch (cell.selectedPattern)
            {
                case 0: // Floor
                    prefabToSpawn = prefabs.floorPrefab;
                    break;
                    
                case 1: // Wall
                    prefabToSpawn = prefabs.wallPrefab;
                    // Choose wall orientation based on position for variety
                    rotation = (cell.position.x % 2 == 0) 
                        ? quaternion.identity      // XY wall
                        : quaternion.Euler(0, 90, 0); // ZY wall
                    break;
                    
                case 2: // Door
                    prefabToSpawn = prefabs.doorPrefab;
                    break;
                    
                case 3: // Corridor
                    prefabToSpawn = prefabs.corridorPrefab;
                    break;
                    
                case 4: // Corner
                    prefabToSpawn = prefabs.cornerPrefab;
                    break;
            }
            
            if (prefabToSpawn != Entity.Null)
            {
                var instance = EntityManager.Instantiate(prefabToSpawn);
                
                // Set position and rotation using command buffer to ensure it's preserved
                var transform = new LocalTransform
                {
                    Position = new float3(cell.position.x, 0, cell.position.y),
                    Rotation = rotation,
                    Scale = 1f
                };
                ecb.SetComponent(instance, transform);
                
                // Ensure the DungeonElementComponent is properly set (in case it wasn't copied from prefab)
                var elementComponent = new DungeonElementComponent();
                switch (cell.selectedPattern)
                {
                    case 0: elementComponent.elementType = DungeonElementType.Floor; break;
                    case 1: elementComponent.elementType = DungeonElementType.Wall; break;
                    case 2: elementComponent.elementType = DungeonElementType.Door; break;
                    case 3: elementComponent.elementType = DungeonElementType.Corridor; break;
                    case 4: elementComponent.elementType = DungeonElementType.Corner; break;
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