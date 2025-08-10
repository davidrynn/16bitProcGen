using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Rendering;
using UnityEngine;
using Unity.Entities.Graphics;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// DOTS rendering system for dungeon elements using EntitiesGraphics
    /// This provides proper DOTS rendering without GameObject conversion
    /// Updated for Unity 6 compatibility
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DungeonRenderingSystem))]
    public partial class DungeonDOTSRenderingSystem : SystemBase
    {
        private EntityQuery dungeonElementsQuery;
        private EntityCommandBuffer ecb;
        private int updateCounter = 0;
        private bool renderingComplete = false;
        
        // Simple mesh and material references for DOTS rendering
        private Mesh floorMesh;
        private Mesh wallMesh;
        private Material floorMaterial;
        private Material wallMaterial;
        private Material doorMaterial;
        private Material corridorMaterial;
        private Material cornerMaterial;
        
        // Shared render data for Entities Graphics (Unity 6 pattern)
        private RenderMeshArray renderMeshArray;
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonDOTSRenderingSystem: OnCreate called", true);
            
            // Query for dungeon elements that need rendering
            dungeonElementsQuery = GetEntityQuery(
                ComponentType.ReadOnly<DungeonElementComponent>(),
                ComponentType.Exclude<DungeonDOTSVisualized>(),
                ComponentType.Exclude<Prefab>()
            );
            
            RequireForUpdate<DungeonElementComponent>();
        }
        
        protected override void OnStartRunning()
        {
            // Create simple meshes and materials for DOTS rendering
            CreateDOTSRenderingAssets();
        }
        
        protected override void OnUpdate()
        {
            updateCounter++;
            
            // If rendering is complete, stop updating
            if (renderingComplete)
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
            
            // Only log occasionally to reduce spam
            if (updateCounter % 100 == 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonDOTSRenderingSystem: OnUpdate called (update {updateCounter})");
            }
            
            // Create command buffer for this frame
            ecb = new EntityCommandBuffer(Allocator.TempJob);
            
            try
            {
                int processedCount = 0;
                
                // Process entities that need DOTS visualization
                Entities
                    .WithAll<DungeonElementComponent>()
                    .WithNone<DungeonDOTSVisualized>()
                    .WithNone<Prefab>()
                    .ForEach((Entity entity, in DungeonElementComponent element, in LocalTransform transform) =>
                    {
                        // Log first few entities for debugging
                        if (processedCount < 3)
                        {
                            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonDOTSRenderingSystem: Processing entity {entity.Index} - {element.elementType} at {transform.Position}");
                        }
                        
                        CreateDOTSVisualization(entity, element, transform);
                        processedCount++;
                    }).WithoutBurst().Run();
                
                if (processedCount > 0)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonDOTSRenderingSystem: Processed {processedCount} entities for DOTS visualization");
                }
                
                // Check if all entities are now visualized
                int totalUnvisualized = dungeonElementsQuery.CalculateEntityCount();
                if (totalUnvisualized == 0)
                {
                    DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonDOTSRenderingSystem: All entities visualized - stopping updates");
                    renderingComplete = true;
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
        
        private void CreateDOTSVisualization(Entity entity, DungeonElementComponent element, LocalTransform transform)
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonDOTSRenderingSystem: Creating DOTS visualization for entity {entity.Index} - {element.elementType} at {transform.Position}");
            
            // Add rendering components based on element type
            switch (element.elementType)
            {
                case DungeonElementType.Floor:
                    AddFloorRendering(entity, transform);
                    break;
                case DungeonElementType.Wall:
                    AddWallRendering(entity, transform);
                    break;
                case DungeonElementType.Door:
                    AddDoorRendering(entity, transform);
                    break;
                case DungeonElementType.Corridor:
                    AddCorridorRendering(entity, transform);
                    break;
                case DungeonElementType.Corner:
                    AddCornerRendering(entity, transform);
                    break;
            }
            
            // Mark this entity as visualized
            ecb.AddComponent<DungeonDOTSVisualized>(entity);
        }
        
        private void AddFloorRendering(Entity entity, LocalTransform transform)
        {
            // Add rendering components for floor (quad) using Unity 6 DOTS Entities Graphics
            ecb.AddSharedComponentManaged(entity, renderMeshArray);
            ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0)); // floor material, quad mesh
            ecb.AddComponent(entity, new RenderBounds { Value = new AABB { Center = transform.Position, Extents = new float3(0.5f, 0.01f, 0.5f) } });
            
            // Adjust transform for floor (rotate 90° around X to lie flat)
            var floorTransform = new LocalTransform
            {
                Position = transform.Position,
                Rotation = quaternion.Euler(math.radians(90), 0, 0),
                Scale = 1.0f
            };
            ecb.SetComponent(entity, floorTransform);
        }
        
        private void AddWallRendering(Entity entity, LocalTransform transform)
        {
            // Add rendering components for wall (cube) using Unity 6 DOTS Entities Graphics
            ecb.AddSharedComponentManaged(entity, renderMeshArray);
            ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(1, 1)); // wall material, cube mesh
            ecb.AddComponent(entity, new RenderBounds { Value = new AABB { Center = transform.Position, Extents = new float3(0.5f, 1f, 0.05f) } });
            
            // Adjust transform for wall (thin and tall)
            var wallTransform = new LocalTransform
            {
                Position = transform.Position,
                Rotation = transform.Rotation,
                Scale = 1.0f
            };
            ecb.SetComponent(entity, wallTransform);
        }
        
        private void AddDoorRendering(Entity entity, LocalTransform transform)
        {
            // Add rendering components for door (cube) using Unity 6 DOTS Entities Graphics
            ecb.AddSharedComponentManaged(entity, renderMeshArray);
            ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(2, 1)); // door material, cube mesh
            ecb.AddComponent(entity, new RenderBounds { Value = new AABB { Center = transform.Position, Extents = new float3(0.4f, 1f, 0.05f) } });
            
            // Adjust transform for door
            var doorTransform = new LocalTransform
            {
                Position = transform.Position,
                Rotation = transform.Rotation,
                Scale = 1.0f
            };
            ecb.SetComponent(entity, doorTransform);
        }
        
        private void AddCorridorRendering(Entity entity, LocalTransform transform)
        {
            // Add rendering components for corridor (quad) using Unity 6 DOTS Entities Graphics
            ecb.AddSharedComponentManaged(entity, renderMeshArray);
            ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(3, 0)); // corridor material, quad mesh
            ecb.AddComponent(entity, new RenderBounds { Value = new AABB { Center = transform.Position, Extents = new float3(0.5f, 0.01f, 0.5f) } });
            
            // Adjust transform for corridor (rotate 90° around X to lie flat)
            var corridorTransform = new LocalTransform
            {
                Position = transform.Position,
                Rotation = quaternion.Euler(math.radians(90), 0, 0),
                Scale = 1.0f
            };
            ecb.SetComponent(entity, corridorTransform);
        }
        
        private void AddCornerRendering(Entity entity, LocalTransform transform)
        {
            // Add rendering components for corner (cube) using Unity 6 DOTS Entities Graphics
            ecb.AddSharedComponentManaged(entity, renderMeshArray);
            ecb.AddComponent(entity, MaterialMeshInfo.FromRenderMeshArrayIndices(4, 1)); // corner material, cube mesh
            ecb.AddComponent(entity, new RenderBounds { Value = new AABB { Center = transform.Position, Extents = new float3(0.5f, 0.5f, 0.5f) } });
            
            // Corner uses default transform
            ecb.SetComponent(entity, transform);
        }
        
        private void CreateDOTSRenderingAssets()
        {
            // Create simple meshes
            floorMesh = CreateQuadMesh();
            wallMesh = CreateCubeMesh();
            
            // Create materials with different colors
            floorMaterial = CreateMaterial(Color.gray);
            wallMaterial = CreateMaterial(Color.brown);
            doorMaterial = CreateMaterial(Color.yellow);
            corridorMaterial = CreateMaterial(Color.blue);
            cornerMaterial = CreateMaterial(Color.red);

            // Mark runtime-created assets to avoid saving and editor asset destruction warnings
            if (floorMesh != null) floorMesh.hideFlags = HideFlags.HideAndDontSave;
            if (wallMesh != null) wallMesh.hideFlags = HideFlags.HideAndDontSave;
            if (floorMaterial != null) floorMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (wallMaterial != null) wallMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (doorMaterial != null) doorMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (corridorMaterial != null) corridorMaterial.hideFlags = HideFlags.HideAndDontSave;
            if (cornerMaterial != null) cornerMaterial.hideFlags = HideFlags.HideAndDontSave;

            // Build a single RenderMeshArray shared by all dungeon elements
            renderMeshArray = new RenderMeshArray(
                new Material[] { floorMaterial, wallMaterial, doorMaterial, corridorMaterial, cornerMaterial },
                new Mesh[] { floorMesh, wallMesh }
            );
            
            DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonDOTSRenderingSystem: Created DOTS rendering assets");
        }
        
        private Mesh CreateQuadMesh()
        {
            var mesh = new Mesh();
            mesh.vertices = new Vector3[]
            {
                new Vector3(-0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, -0.5f),
                new Vector3(0.5f, 0, 0.5f),
                new Vector3(-0.5f, 0, 0.5f)
            };
            mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new Vector3[]
            {
                Vector3.up, Vector3.up, Vector3.up, Vector3.up
            };
            mesh.uv = new Vector2[]
            {
                new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
            };
            return mesh;
        }
        
        private Mesh CreateCubeMesh()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            // Instantiate a runtime copy so we are not referencing an editor asset
            var meshCopy = Object.Instantiate(sharedMesh);
            Object.DestroyImmediate(go);
            return meshCopy;
        }
        
        private Material CreateMaterial(Color color)
        {
            // Try to find URP shader, fallback to standard shader
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
                DOTS.Terrain.Core.DebugSettings.LogWarning("DungeonDOTSRenderingSystem: URP shader not found, using Standard shader");
            }
            
            if (shader == null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonDOTSRenderingSystem: No shader found! Using default material");
                return new Material(Shader.Find("Hidden/InternalErrorShader"));
            }
            
            var material = new Material(shader);
            material.color = color;
            return material;
        }
        
        protected override void OnDestroy()
        {
            // Clean up meshes and materials
            #if UNITY_EDITOR
            if (floorMesh != null) Object.DestroyImmediate(floorMesh);
            if (wallMesh != null) Object.DestroyImmediate(wallMesh);
            if (floorMaterial != null) Object.DestroyImmediate(floorMaterial);
            if (wallMaterial != null) Object.DestroyImmediate(wallMaterial);
            if (doorMaterial != null) Object.DestroyImmediate(doorMaterial);
            if (corridorMaterial != null) Object.DestroyImmediate(corridorMaterial);
            if (cornerMaterial != null) Object.DestroyImmediate(cornerMaterial);
            #else
            if (floorMesh != null) Object.Destroy(floorMesh);
            if (wallMesh != null) Object.Destroy(wallMesh);
            if (floorMaterial != null) Object.Destroy(floorMaterial);
            if (wallMaterial != null) Object.Destroy(wallMaterial);
            if (doorMaterial != null) Object.Destroy(doorMaterial);
            if (corridorMaterial != null) Object.Destroy(corridorMaterial);
            if (cornerMaterial != null) Object.Destroy(cornerMaterial);
            #endif
        }
    }
    
    /// <summary>
    /// Component to mark entities as already visualized with DOTS rendering
    /// </summary>
    public struct DungeonDOTSVisualized : IComponentData
    {
    }
} 