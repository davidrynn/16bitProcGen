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
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainModificationSystem))]
    public partial class TerrainGlobPhysicsSystem : SystemBase
    {
        // Physics constants
        private const float GRAVITY = -9.81f;
        private const float GROUND_Y_THRESHOLD = 0.1f;
        private const float GROUND_FRICTION = 0.8f;
        private const float AIR_FRICTION = 0.02f;
        
        // Performance monitoring
        private int activeGlobs = 0;
        private int groundedGlobs = 0;
        private float lastUpdateTime = 0f;
        
        protected override void OnCreate()
        {
            Debug.Log("[DOTS] TerrainGlobPhysicsSystem: Initializing...");
            RequireForUpdate<TerrainGlobComponent>();
            RequireForUpdate<TerrainGlobPhysicsComponent>();
        }
        
        protected override void OnUpdate()
        {
            float deltaTime = (float)SystemAPI.Time.DeltaTime;
            lastUpdateTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Update glob physics
            UpdateGlobPhysics(deltaTime);
            
            // Handle glob destruction
            HandleGlobDestruction();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        /// <summary>
        /// Updates physics for all terrain globs
        /// </summary>
        private void UpdateGlobPhysics(float deltaTime)
        {
            activeGlobs = 0;
            groundedGlobs = 0;
            
            Entities
                .WithAll<TerrainGlobComponent, TerrainGlobPhysicsComponent>()
                .ForEach((Entity entity, ref TerrainGlobComponent glob, ref TerrainGlobPhysicsComponent physics, ref LocalTransform transform) =>
                {
                    if (!physics.enablePhysics || glob.isDestroyed)
                        return;
                    
                    activeGlobs++;
                    
                    // Apply gravity
                    if (physics.gravityScale > 0f)
                    {
                        glob.velocity.y += GRAVITY * physics.gravityScale * deltaTime;
                    }
                    
                    // Apply air resistance
                    float drag = physics.dragCoefficient * AIR_FRICTION;
                    glob.velocity *= (1f - drag * deltaTime);
                    
                    // Apply angular velocity
                    glob.angularVelocity *= (1f - drag * deltaTime);
                    
                    // Clamp velocities
                    glob.velocity = math.clamp(glob.velocity, -physics.maxVelocity, physics.maxVelocity);
                    glob.angularVelocity = math.clamp(glob.angularVelocity, -physics.maxAngularVelocity, physics.maxAngularVelocity);
                    
                    // Update position
                    glob.currentPosition += glob.velocity * deltaTime;
                    
                    // Update rotation
                    glob.rotation = math.mul(glob.rotation, quaternion.Euler(glob.angularVelocity * deltaTime));
                    
                    // Check for ground collision
                    bool wasGrounded = glob.isGrounded;
                    glob.isGrounded = glob.currentPosition.y <= GROUND_Y_THRESHOLD;
                    
                    if (glob.isGrounded)
                    {
                        groundedGlobs++;
                        
                        // Ground collision response
                        if (!wasGrounded && glob.velocity.y < 0f)
                        {
                            // Bounce off ground
                            glob.velocity.y = -glob.velocity.y * glob.bounciness;
                            
                            // Apply ground friction
                            glob.velocity.xz *= (1f - GROUND_FRICTION * deltaTime);
                            glob.angularVelocity *= (1f - GROUND_FRICTION * deltaTime);
                        }
                        
                        // Keep glob on ground
                        if (glob.currentPosition.y < GROUND_Y_THRESHOLD)
                        {
                            glob.currentPosition.y = GROUND_Y_THRESHOLD;
                        }
                    }
                    
                    // Update transform
                    transform.Position = glob.currentPosition;
                    transform.Rotation = glob.rotation;
                    transform.Scale = glob.scale.x;
                    
                    // Update lifetime
                    glob.lifetime += deltaTime;
                    
                }).WithoutBurst().Run();
        }
        
        /// <summary>
        /// Handles destruction of globs that should be destroyed
        /// </summary>
        private void HandleGlobDestruction()
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            
            Entities
                .WithAll<TerrainGlobComponent>()
                .ForEach((Entity entity, in TerrainGlobComponent glob) =>
                {
                    if (glob.isDestroyed)
                    {
                        ecb.DestroyEntity(entity);
                    }
                }).WithoutBurst().Run();
            
            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
        
        /// <summary>
        /// Updates performance monitoring metrics
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            // Log performance info periodically
            if (lastUpdateTime % 5f < (float)SystemAPI.Time.DeltaTime) // Every 5 seconds
            {
                Debug.Log($"[TerrainGlobPhysicsSystem] Active globs: {activeGlobs}, Grounded: {groundedGlobs}");
            }
        }
        
        /// <summary>
        /// Creates a new terrain glob entity with physics
        /// </summary>
        public Entity CreateTerrainGlob(float3 position, float radius, GlobRemovalType globType, TerrainType terrainType)
        {
            var entity = EntityManager.CreateEntity();
            
            // Create glob component
            var globComponent = new TerrainGlobComponent
            {
                originalPosition = position,
                currentPosition = position,
                globRadius = radius,
                globType = globType,
                terrainType = terrainType,
                velocity = float3.zero,
                angularVelocity = float3.zero,
                mass = radius * 2f, // Mass based on size
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
            
            // Create physics component
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
            
            // Create render component
            var renderComponent = new TerrainGlobRenderComponent
            {
                enableRendering = true,
                meshScale = new float3(radius),
                meshVariant = 0,
                color = GetTerrainColor(terrainType),
                useTerrainColor = true
            };
            
            // Create transform component
            var transformComponent = new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = radius
            };
            
            // Add all components
            EntityManager.AddComponentData(entity, globComponent);
            EntityManager.AddComponentData(entity, physicsComponent);
            EntityManager.AddComponentData(entity, renderComponent);
            EntityManager.AddComponentData(entity, transformComponent);
            
            Debug.Log($"[TerrainGlobPhysicsSystem] Created glob at {position} with radius {radius}");
            
            return entity;
        }
        
        /// <summary>
        /// Calculates resource value based on terrain type and glob size
        /// </summary>
        private int CalculateResourceValue(TerrainType terrainType, GlobRemovalType globType)
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
        private float4 GetTerrainColor(TerrainType terrainType)
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
        /// Gets performance statistics
        /// </summary>
        public (int activeGlobs, int groundedGlobs, float lastUpdateTime) GetPerformanceStats()
        {
            return (activeGlobs, groundedGlobs, lastUpdateTime);
        }
    }
} 