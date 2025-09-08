#if UNITY_EDITOR || TESTING_DUNGEON_VIZ
using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain.WFC.Authoring;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Simple system to visualize DOTS dungeon entities as GameObjects
    /// This bridges the gap between DOTS entities and visible GameObjects
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DungeonRenderingSystem))]
    public partial class DungeonVisualizationSystem : SystemBase
    {
        private GameObject visualizationParent;
        private EntityCommandBuffer ecb;
        private int updateCounter = 0;
        private bool visualizationComplete = false;
        
        // Macro-only: Prefer direct reference from the scene's authoring registry
        private DungeonPrefabRegistryAuthoring registryAuthoring;
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonVisualizationSystem: OnCreate called", true);
            
            // Create a parent GameObject for all dungeon visualizations
            visualizationParent = new GameObject("Dungeon Visualization");

            // Find a scene-level authoring registry to use assigned macro prefabs directly
            registryAuthoring = Object.FindFirstObjectByType<DungeonPrefabRegistryAuthoring>();
            if (registryAuthoring != null)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonVisualizationSystem (Macro-only): Found DungeonPrefabRegistryAuthoring in scene - will use assigned roomFloor/roomEdge prefabs");
            }
        }
        
        protected override void OnUpdate()
        {
            // Macro-only: Require registry with macro prefabs
            if (registryAuthoring == null)
            {
                registryAuthoring = Object.FindFirstObjectByType<DungeonPrefabRegistryAuthoring>();
            }
            if (registryAuthoring == null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonVisualizationSystem (Macro-only): Missing DungeonPrefabRegistryAuthoring in scene. Cannot visualize.");
                return;
            }
            if (registryAuthoring.roomFloorPrefab == null || registryAuthoring.roomEdgePrefab == null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonVisualizationSystem (Macro-only): roomFloorPrefab or roomEdgePrefab not assigned on DungeonPrefabRegistryAuthoring.");
                return;
            }

            // If visualization is complete, stop updating
            if (visualizationComplete)
            {
                return;
            }
            
            // Check if dungeon generation is requested
            bool dungeonGenerationRequested = false;
            Entities
                .WithAll<DungeonGenerationRequest>()
                .ForEach((in DungeonGenerationRequest request) =>
                {
                    if (request.isActive)
                    {
                        dungeonGenerationRequested = true;
                    }
                }).WithoutBurst().Run();
                
            if (!dungeonGenerationRequested)
            {
                return;
            }
            
            // Debug: Log that we're running (but only occasionally to reduce spam)
            updateCounter++;
            if (updateCounter % 50 == 0) // Log more frequently for debugging
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem (Macro-only): OnUpdate called (update {updateCounter})");
            }
            
            // Only create command buffer if we need to process entities
            bool needsCommandBuffer = false;
            int processedCount = 0;
            int totalEntities = 0;
            bool hasUnvisualizedEntities = false;
            
            // First pass: count entities and check if we need to process any
            Entities
                .WithAll<DungeonElementComponent>()
                .WithNone<Prefab>()
                .ForEach((Entity entity, in DungeonElementComponent element, in LocalTransform transform) =>
                {
                    totalEntities++;
                    
                    // Check if this entity needs visualization
                    if (!EntityManager.HasComponent<DungeonVisualized>(entity))
                    {
                        hasUnvisualizedEntities = true;
                        needsCommandBuffer = true;
                    }
                }).WithoutBurst().Run();
            
            // Only create command buffer and process if needed
            if (needsCommandBuffer)
            {
                ecb = new EntityCommandBuffer(Allocator.TempJob);
                
                try
                {
                    // Second pass: process entities that need visualization
                    Entities
                        .WithAll<DungeonElementComponent>()
                        .WithNone<DungeonVisualized>()
                        .WithNone<Prefab>()
                        .ForEach((Entity entity, in DungeonElementComponent element, in LocalTransform transform) =>
                        {
                            // Log first few entities for debugging
                            if (processedCount < 3)
                            {
                                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: Processing entity {entity.Index} - {element.elementType} at {transform.Position}");
                            }
                            
                            CreateVisualization(entity, element, transform);
                            processedCount++;
                        }).WithoutBurst().Run();
                
                    if (processedCount > 0)
                    {
                        DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: Processed {processedCount} entities for visualization");
                    }
                    
                    // Play back the command buffer to apply structural changes
                    ecb.Playback(EntityManager);
                }
                finally
                {
                    // Ensure command buffer is always disposed
                    ecb.Dispose();
                }
            }
            
            // If there are no element entities at all but WFC is complete, try a direct WFCCell visualization pass
            if (totalEntities == 0)
            {
                bool wfcComplete = false;
                float cellSize = 1f;
                if (SystemAPI.HasSingleton<WFCComponent>())
                {
                    var wfc = SystemAPI.GetSingleton<WFCComponent>();
                    wfcComplete = wfc.isCollapsed;
                    cellSize = math.max(0.0001f, wfc.cellSize);
                }
                if (wfcComplete)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonVisualizationSystem (Macro-only): No element entities. Visualizing WFCCells directly.", true);
                    int spawned = 0;
                    var wfcSingleton = SystemAPI.GetSingleton<WFCComponent>();
                    ref var patterns = ref wfcSingleton.patterns.Value.patterns; // required ref access per EA0001
                    int patternsLength = patterns.Length;
                    var patternTypes = new NativeArray<int>(patternsLength, Allocator.Temp);
                    for (int i = 0; i < patternsLength; i++)
                    {
                        patternTypes[i] = patterns[i].type;
                    }
                    Entities
                        .WithAll<WFCCell>()
                        .ForEach((in WFCCell cell) =>
                        {
                            if (!cell.collapsed) return;
                            if (cell.selectedPattern < 0 || cell.selectedPattern >= patternsLength) return;
                            int patType = patternTypes[cell.selectedPattern];
                            var elementType = (DungeonElementType)(DungeonPatternType)patType;
                            var pos = new float3(cell.position.x * cellSize, 0, cell.position.y * cellSize);
                            var lt = new LocalTransform { Position = pos, Rotation = quaternion.identity, Scale = 1f };
                            var go = CreateDungeonGameObject(elementType, lt);
                            if (go != null) spawned++;
                        }).WithoutBurst().Run();
                    patternTypes.Dispose();
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem (Macro-only): Spawned {spawned} GameObjects from WFCCells.", true);
                    if (spawned > 0)
                    {
                        visualizationComplete = true;
                    }
                }
            }

            // Handle completion logic (outside the command buffer block)
            if (!hasUnvisualizedEntities && totalEntities > 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: All {totalEntities} entities visualized - stopping updates");
                visualizationComplete = true;
            }
            else if (totalEntities == 0)
            {
                // Check if WFC is complete and we have no more entities to visualize
                bool wfcComplete = false;
                Entities
                    .WithAll<WFCComponent>()
                    .ForEach((in WFCComponent wfc) =>
                    {
                        if (wfc.isCollapsed)
                        {
                            wfcComplete = true;
                        }
                    }).WithoutBurst().Run();
                
                if (wfcComplete)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonVisualizationSystem: WFC complete and no more entities to visualize - stopping updates");
                    visualizationComplete = true;
                }
            }
        }
        
        private void CreateVisualization(Entity entity, DungeonElementComponent element, LocalTransform transform)
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem (Macro-only): Creating visualization for entity {entity.Index} - {element.elementType} at {transform.Position}");
            
            // Create a GameObject for this entity
            var go = CreateDungeonGameObject(element.elementType, transform);
            
            if (go != null)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem (Macro-only): Successfully created GameObject '{go.name}' at {go.transform.position}");
            }
            else
            {
                DOTS.Terrain.Core.DebugSettings.LogError($"DungeonVisualizationSystem (Macro-only): Failed to create GameObject for {element.elementType}");
            }
            
            // Mark this entity as visualized (deferred via command buffer)
            ecb.AddComponent<DungeonVisualized>(entity);
        }
        
        private GameObject CreateDungeonGameObject(DungeonElementType elementType, LocalTransform transform)
        {
            GameObject go = null;
            
            switch (elementType)
            {
                case DungeonElementType.RoomFloor:
                    go = CreateFloorGameObject();
                    break;
                case DungeonElementType.RoomEdge:
                    go = CreateWallGameObject();
                    break;
                case DungeonElementType.CorridorEndDoorway:
                    go = CreateDoorGameObject();
                    break;
                case DungeonElementType.Corridor:
                    go = CreateCorridorGameObject();
                    break;
                case DungeonElementType.Corner:
                    go = CreateCornerGameObject();
                    break;
            }
            
            if (go != null)
            {
                // Set position
                go.transform.position = transform.Position;
                
                // Set rotation
                var unityQuaternion = new Quaternion(transform.Rotation.value.x, transform.Rotation.value.y, transform.Rotation.value.z, transform.Rotation.value.w);
                if (elementType == DungeonElementType.RoomFloor || elementType == DungeonElementType.Corridor)
                {
                    // If using a primitive Quad, rotate 90° around X to lie on XZ; prefabs remain on XZ
                    var meshFilter = go.GetComponent<MeshFilter>();
                    bool isPrimitiveQuad = meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.name == "Quad";
                    if (isPrimitiveQuad)
                    {
                        go.transform.rotation = Quaternion.Euler(90, unityQuaternion.eulerAngles.y, 0);
                    }
                    else
                    {
                        go.transform.rotation = Quaternion.Euler(0, unityQuaternion.eulerAngles.y, 0);
                    }
                }
                else
                {
                    // For walls, doors, and corners, apply the full transform rotation
                    go.transform.rotation = unityQuaternion;
                }
                
                go.transform.localScale = Vector3.one * transform.Scale;
                
                // Parent to visualization container
                go.transform.SetParent(visualizationParent.transform);
                
                // Name the GameObject
                go.name = $"{elementType}_{transform.Position.x}_{transform.Position.z}";
            }
            
            return go;
        }
        
        private GameObject CreateFloorGameObject()
        {
            // Late-bind the authoring registry in case the scene loaded after OnCreate
            // Macro-only: Instantiate roomFloor prefab
            return Object.Instantiate(registryAuthoring.roomFloorPrefab);
        }
        
        private GameObject CreateWallGameObject()
        {
            // Macro-only: Instantiate roomEdge prefab
            return Object.Instantiate(registryAuthoring.roomEdgePrefab);
        }
        
        private GameObject CreateDoorGameObject()
        {
            if (registryAuthoring != null && registryAuthoring.doorPrefab != null)
            {
                return Object.Instantiate(registryAuthoring.doorPrefab);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.localScale = new Vector3(0.8f, 2f, 0.1f);
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.yellow);
            return go;
        }
        
        private GameObject CreateCorridorGameObject()
        {
            if (registryAuthoring != null && registryAuthoring.corridorPrefab != null)
            {
                return Object.Instantiate(registryAuthoring.corridorPrefab);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.blue);
            return go;
        }
        
        private GameObject CreateCornerGameObject()
        {
            if (registryAuthoring != null && registryAuthoring.cornerPrefab != null)
            {
                return Object.Instantiate(registryAuthoring.cornerPrefab);
            }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.red);
            return go;
        }
        
        private Material CreateMaterial(Color color)
        {
            // Try to find URP shader, fallback to standard shader if not found
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                DOTS.Terrain.Core.DebugSettings.LogWarning("DungeonVisualizationSystem: URP shader not found, using Standard shader");
            }
            
            if (shader == null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonVisualizationSystem: No shader found! Using default material");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            
            var material = new Material(shader);
            material.color = color;
            
            // Debug: Log the material creation
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: Created material with color {color}");
            
            return material;
        }
        
        protected override void OnDestroy()
        {
            // Clean up visualization parent
            if (visualizationParent != null)
            {
                Object.DestroyImmediate(visualizationParent);
            }
        }
    }
    
    /// <summary>
    /// Component to mark entities as already visualized
    /// </summary>
    public struct DungeonVisualized : IComponentData
    {
    }
    

} 
// File only included in Editor or when TESTING_DUNGEON_VIZ is defined
#endif