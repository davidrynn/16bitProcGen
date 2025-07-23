using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Terrain.Core
{
    /// <summary>
    /// System that synchronizes Unity.Transforms components with TerrainData
    /// Ensures that LocalTransform and LocalToWorld components stay updated
    /// when terrain data changes (generation, modification, etc.)
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainSystem))]
    public partial class TerrainTransformSystem : SystemBase
    {
        protected override void OnCreate()
        {
            Debug.Log("[DOTS] TerrainTransformSystem: Initializing...");
            RequireForUpdate<TerrainData>();
            RequireForUpdate<LocalTransform>();
        }
        
        protected override void OnUpdate()
        {
            // Update transforms for entities that have terrain data changes
            Entities
                .WithAll<TerrainData, LocalTransform>()
                .ForEach((Entity entity, ref LocalTransform localTransform, ref LocalToWorld localToWorld, in TerrainData terrainData) =>
                {
                    // Update position based on terrain data
                    float3 newPosition = terrainData.worldPosition;
                    
                    // Adjust Y position based on average height if terrain has been generated
                    if (terrainData.heightData.IsCreated && !terrainData.needsGeneration)
                    {
                        newPosition.y = terrainData.averageHeight;
                    }
                    
                    // Update rotation and scale from terrain data
                    quaternion newRotation = terrainData.rotation;
                    float newScale = terrainData.scale.x; // LocalTransform uses single scale value
                    
                    // Only update if values have changed
                    if (!math.all(localTransform.Position == newPosition) ||
                        !math.all(localTransform.Rotation.value == newRotation.value) ||
                        localTransform.Scale != newScale)
                    {
                        // Update LocalTransform
                        localTransform.Position = newPosition;
                        localTransform.Rotation = newRotation;
                        localTransform.Scale = newScale;
                        
                        // Update LocalToWorld matrix
                        localToWorld.Value = float4x4.TRS(
                            localTransform.Position,
                            localTransform.Rotation,
                            new float3(localTransform.Scale)
                        );
                        
                        Debug.Log($"[TerrainTransformSystem] Updated transform for entity {entity.Index} at position {newPosition}");
                    }
                }).WithoutBurst().Run();
        }
    }
} 