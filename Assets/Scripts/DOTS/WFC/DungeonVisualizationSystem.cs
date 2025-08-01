using Unity.Entities;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

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
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonVisualizationSystem: OnCreate called", true);
            
            // Create a parent GameObject for all dungeon visualizations
            visualizationParent = new GameObject("Dungeon Visualization");
        }
        
        protected override void OnUpdate()
        {
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
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: OnUpdate called (update {updateCounter})");
            }
            
            // Create command buffer for this frame
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            try
            {
                // Single consolidated loop to process entities
                int processedCount = 0;
                int totalEntities = 0;
                bool hasUnvisualizedEntities = false;
                
                Entities
                    .WithAll<DungeonElementInstance>()
                    .ForEach((Entity entity, in DungeonElementComponent element, in LocalTransform transform) =>
                    {
                        totalEntities++;
                        
                        // Check if this entity needs visualization
                        if (!EntityManager.HasComponent<DungeonVisualized>(entity))
                        {
                            hasUnvisualizedEntities = true;
                            
                            // Log first few entities for debugging
                            if (processedCount < 3)
                            {
                                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: Processing entity {entity.Index} - {element.elementType} at {transform.Position}");
                            }
                            
                            CreateVisualization(entity, element, transform);
                            processedCount++;
                        }
                    }).WithoutBurst().Run();
                
                // Handle completion logic
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
        
        private void CreateVisualization(Entity entity, DungeonElementComponent element, LocalTransform transform)
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: Creating visualization for entity {entity.Index} - {element.elementType} at {transform.Position}");
            
            // Create a GameObject for this entity
            var go = CreateDungeonGameObject(element.elementType, transform);
            
            if (go != null)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonVisualizationSystem: Successfully created GameObject '{go.name}' at {go.transform.position}");
            }
            else
            {
                DOTS.Terrain.Core.DebugSettings.LogError($"DungeonVisualizationSystem: Failed to create GameObject for {element.elementType}");
            }
            
            // Mark this entity as visualized (deferred via command buffer)
            ecb.AddComponent<DungeonVisualized>(entity);
        }
        
        private GameObject CreateDungeonGameObject(DungeonElementType elementType, LocalTransform transform)
        {
            GameObject go = null;
            
            switch (elementType)
            {
                case DungeonElementType.Floor:
                    go = CreateFloorGameObject();
                    break;
                case DungeonElementType.Wall:
                    go = CreateWallGameObject();
                    break;
                case DungeonElementType.Door:
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
                // Set position and rotation
                go.transform.position = transform.Position;
                go.transform.rotation = transform.Rotation;
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.rotation = Quaternion.Euler(90, 0, 0); // Rotate 90° around X to face up in XZ plane
            
            // Set material
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.gray);
            
            return go;
        }
        
        private GameObject CreateWallGameObject()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            // Scale to make it thin and tall
            go.transform.localScale = new Vector3(1f, 2f, 0.1f);
            
            // Set material
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.brown);
            
            return go;
        }
        
        private GameObject CreateDoorGameObject()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            // Scale to make it door-sized
            go.transform.localScale = new Vector3(0.8f, 2f, 0.1f);
            
            // Set material
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.yellow);
            
            return go;
        }
        
        private GameObject CreateCorridorGameObject()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.transform.rotation = Quaternion.Euler(90, 0, 0); // Rotate 90° around X to face up in XZ plane
            
            // Set material
            var renderer = go.GetComponent<Renderer>();
            renderer.material = CreateMaterial(Color.blue);
            
            return go;
        }
        
        private GameObject CreateCornerGameObject()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            // Set material
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