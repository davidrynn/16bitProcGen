using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using UnityEngine;

namespace DOTS.Terrain.Modification
{
    /// <summary>
    /// System that handles physics behavior for terrain globs
    /// Manages gravity, bouncing, rolling, and collision detection
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainModificationSystem))]
    public partial struct TerrainGlobPhysicsSystem : ISystem
    {
        // Physics constants
        private const float GRAVITY = -9.81f;
        private const float GROUND_Y_THRESHOLD = 0.1f;
        private const float GROUND_FRICTION = 0.8f;
        private const float AIR_FRICTION = 0.02f;
        
        // Performance monitoring
        private int activeGlobs;
        private int groundedGlobs;
        private float lastUpdateTime;

        private static int latestActiveGlobs;
        private static int latestGroundedGlobs;
        private static float latestUpdateTime;
        
        public void OnCreate(ref SystemState state)
        {
            Debug.Log("[DOTS] TerrainGlobPhysicsSystem: Initializing...");
            state.RequireForUpdate<TerrainGlobComponent>();
            state.RequireForUpdate<TerrainGlobPhysicsComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;
            lastUpdateTime = (float)SystemAPI.Time.ElapsedTime;

            UpdateGlobPhysics(ref state, deltaTime);
            HandleGlobDestruction(ref state);
            UpdatePerformanceMetrics(deltaTime);
        }
        
        /// <summary>
        /// Updates physics for all terrain globs
        /// </summary>
        private void UpdateGlobPhysics(ref SystemState state, float deltaTime)
        {
            activeGlobs = 0;
            groundedGlobs = 0;
            
            foreach (var (glob, physics, transform) in SystemAPI
                         .Query<RefRW<TerrainGlobComponent>, RefRW<TerrainGlobPhysicsComponent>, RefRW<LocalTransform>>())
            {
                ref var globData = ref glob.ValueRW;
                ref var physicsData = ref physics.ValueRW;
                ref var transformData = ref transform.ValueRW;

                if (!physicsData.enablePhysics || globData.isDestroyed)
                {
                    continue;
                }

                activeGlobs++;

                if (physicsData.gravityScale > 0f)
                {
                    globData.velocity.y += GRAVITY * physicsData.gravityScale * deltaTime;
                }

                float drag = physicsData.dragCoefficient * AIR_FRICTION;
                globData.velocity *= (1f - drag * deltaTime);
                globData.angularVelocity *= (1f - drag * deltaTime);

                globData.velocity = math.clamp(globData.velocity, -physicsData.maxVelocity, physicsData.maxVelocity);
                globData.angularVelocity = math.clamp(globData.angularVelocity, -physicsData.maxAngularVelocity, physicsData.maxAngularVelocity);

                globData.currentPosition += globData.velocity * deltaTime;
                globData.rotation = math.mul(globData.rotation, quaternion.Euler(globData.angularVelocity * deltaTime));

                bool wasGrounded = globData.isGrounded;
                globData.isGrounded = globData.currentPosition.y <= GROUND_Y_THRESHOLD;

                if (globData.isGrounded)
                {
                    groundedGlobs++;

                    if (!wasGrounded && globData.velocity.y < 0f)
                    {
                        globData.velocity.y = -globData.velocity.y * globData.bounciness;
                        globData.velocity.xz *= (1f - GROUND_FRICTION * deltaTime);
                        globData.angularVelocity *= (1f - GROUND_FRICTION * deltaTime);
                    }

                    if (globData.currentPosition.y < GROUND_Y_THRESHOLD)
                    {
                        globData.currentPosition.y = GROUND_Y_THRESHOLD;
                    }
                }

                transformData.Position = globData.currentPosition;
                transformData.Rotation = globData.rotation;
                transformData.Scale = globData.scale.x;

                globData.lifetime += deltaTime;
            }
        }
        
        /// <summary>
        /// Handles destruction of globs that should be destroyed
        /// </summary>
        private void HandleGlobDestruction(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (glob, entity) in SystemAPI.Query<RefRO<TerrainGlobComponent>>().WithEntityAccess())
            {
                if (glob.ValueRO.isDestroyed)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Updates performance monitoring metrics
        /// </summary>
        private void UpdatePerformanceMetrics(float deltaTime)
        {
            latestActiveGlobs = activeGlobs;
            latestGroundedGlobs = groundedGlobs;
            latestUpdateTime = lastUpdateTime;

            if (lastUpdateTime % 5f < deltaTime)
            {
                Debug.Log($"[TerrainGlobPhysicsSystem] Active globs: {activeGlobs}, Grounded: {groundedGlobs}");
            }
        }
        
        /// <summary>
        /// Creates a new terrain glob entity with physics
        /// </summary>
        public static Entity CreateTerrainGlob(EntityManager entityManager, float3 position, float radius, GlobRemovalType globType, TerrainType terrainType)
        {
            var entity = entityManager.CreateEntity();

            var globComponent = new TerrainGlobComponent
            {
                originalPosition = position,
                currentPosition = position,
                globRadius = radius,
                globType = globType,
                terrainType = terrainType,
                velocity = float3.zero,
                angularVelocity = float3.zero,
                mass = radius * 2f,
                bounciness = 0.3f,
                friction = 0.5f,
                isGrounded = false,
                isCollected = false,
                isDestroyed = false,
                lifetime = 0f,
                collectionRadius = radius * 1.5f,
                canBeCollected = true,
                resourceValue = CalculateResourceValue(terrainType, globType),
                scale = new float3(radius),
                rotation = quaternion.identity,
                visualAlpha = 1f
            };

            var physicsComponent = new TerrainGlobPhysicsComponent
            {
                enablePhysics = true,
                gravityScale = 1f,
                dragCoefficient = 0.1f,
                maxVelocity = 10f,
                maxAngularVelocity = 5f,
                collisionRadius = radius,
                collideWithTerrain = true,
                collideWithOtherGlobs = true,
                collideWithPlayer = false
            };

            var renderComponent = new TerrainGlobRenderComponent
            {
                enableRendering = true,
                meshScale = new float3(radius),
                meshVariant = 0,
                color = GetTerrainColor(terrainType),
                useTerrainColor = true
            };

            var transformComponent = new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = radius
            };

            entityManager.AddComponentData(entity, globComponent);
            entityManager.AddComponentData(entity, physicsComponent);
            entityManager.AddComponentData(entity, renderComponent);
            entityManager.AddComponentData(entity, transformComponent);

            Debug.Log($"[TerrainGlobPhysicsSystem] Created glob at {position} with radius {radius}");

            return entity;
        }

        /// <summary>
        /// Calculates resource value based on terrain type and glob size
        /// </summary>
    private static int CalculateResourceValue(TerrainType terrainType, GlobRemovalType globType)
        {
            int baseValue = terrainType switch
            {
                TerrainType.Grass => 1,
                TerrainType.Sand => 2,
                TerrainType.Rock => 3,
                TerrainType.Snow => 4,
                TerrainType.Water => 1,
                _ => 1
            };
            
            int sizeMultiplier = globType switch
            {
                GlobRemovalType.Small => 1,
                GlobRemovalType.Medium => 2,
                GlobRemovalType.Large => 3,
                _ => 1
            };
            
            return baseValue * sizeMultiplier;
        }
        
        /// <summary>
        /// Gets the color for a terrain type
        /// </summary>
    private static float4 GetTerrainColor(TerrainType terrainType)
        {
            return terrainType switch
            {
                TerrainType.Grass => new float4(0.2f, 0.8f, 0.2f, 1f),
                TerrainType.Sand => new float4(0.9f, 0.8f, 0.6f, 1f),
                TerrainType.Rock => new float4(0.5f, 0.5f, 0.5f, 1f),
                TerrainType.Snow => new float4(0.9f, 0.9f, 0.9f, 1f),
                TerrainType.Water => new float4(0.2f, 0.4f, 0.8f, 1f),
                _ => new float4(0.5f, 0.5f, 0.5f, 1f)
            };
        }
        
        /// <summary>
        public static (int activeGlobs, int groundedGlobs, float lastUpdateTime) GetPerformanceStats()
        {
            return (latestActiveGlobs, latestGroundedGlobs, latestUpdateTime);
        }
    }
} 
