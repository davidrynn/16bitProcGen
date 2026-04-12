using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using DOTS.Player.Components;
using DOTS.Terrain.Core;
using System;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Translates player input into physics-friendly velocity updates, handling both ground movement and airborne control.
    /// </summary>
    /// <remarks>
    /// Runs before <see cref="PhysicsSimulationGroup"/> so the updated velocities feed into the current physics step.
    /// </remarks>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PhysicsSimulationGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        // Use a fast lerp instead of instant snap for ground movement.
        // This preserves physics solver depenetration corrections between frames,
        // preventing the player from being driven through thin wall colliders.
        // At 25/s and 60 fps: ~42% per frame, reaches 93% in 5 frames (~83 ms).
        private const float GroundLerpRate = 25f;
        // Preserve responsive movement across brief grounding flicker windows
        // on steep/edited terrain contacts.
        private const float GroundControlGraceTime = 0.12f;
        /// <summary>
        /// Applies horizontal motion, air steering, and jump impulses based on the player's current input and movement state.
        /// </summary>
        /// <param name="state">The execution context for this system tick.</param>
        private static int _frameCount = 0;
        private static bool _hasLoggedMovementOnce = false;

        // BUG-011 wall probe constants.
        // The probe fires a short ray from capsule center in the horizontal movement
        // direction. If terrain is detected within the probe distance, the into-wall
        // velocity component is removed so the movement system never fights the solver.
        private const float WallProbeDistance = 0.6f;  // capsule radius (0.5) + skin (0.1)
        private const float WallProbeMinSpeed = 0.01f;
        private const uint TerrainLayerBit = 2u;
        private static readonly float3 CapsuleCenterOffset = new float3(0f, 1.0f, 0f);
        private bool _hasLoggedInvalidColliderWallProbeRaycast;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementConfig>();
            _hasLoggedInvalidColliderWallProbeRaycast = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            _frameCount++;

            // Cache physics world for wall probes (BUG-011).
            PhysicsWorldSingleton physicsWorldSingleton = default;
            bool hasPhysicsWorld = SystemAPI.HasSingleton<PhysicsWorldSingleton>();
            if (hasPhysicsWorld)
                physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();

            int entityCount = 0;
            foreach (var (config, input, movementState, view, transform, velocity, entity) in
                     SystemAPI.Query<RefRO<PlayerMovementConfig>, RefRW<PlayerInputComponent>, RefRW<PlayerMovementState>, RefRO<PlayerViewComponent>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>>().WithEntityAccess())
            {
                entityCount++;

                // Startup readiness gate keeps player physics frozen until terrain colliders are ready.
                if (state.EntityManager.HasComponent<PlayerStartupReadinessGate>(entity))
                {
                    velocity.ValueRW.Linear = float3.zero;
                    velocity.ValueRW.Angular = float3.zero;
                    input.ValueRW.Move = float2.zero;
                    input.ValueRW.Look = float2.zero;
                    input.ValueRW.JumpPressed = false;
                    movementState.ValueRW.IsGrounded = false;
                    movementState.ValueRW.FallTime = 0f;
                    movementState.ValueRW.PreviousPosition = transform.ValueRO.Position;
                    continue;
                }

                float2 moveInput = input.ValueRO.Move;
                
                // Debug: Log first movement to see which entity is moving
                if (math.lengthsq(moveInput) > 0.01f && !_hasLoggedMovementOnce)
                {
                    DebugSettings.LogPlayer($"Entity {entity.Index} is moving! Input: {moveInput}, Position: {transform.ValueRO.Position}, Velocity: {velocity.ValueRO.Linear}");
                    _hasLoggedMovementOnce = true;
                }
                if (math.lengthsq(moveInput) > 1f)
                {
                    moveInput = math.normalize(moveInput);
                }

                // Calculate camera-relative movement using yaw from PlayerViewComponent
                // This ensures movement is relative to where the camera is looking, not world axes
                float yawRadians = math.radians(view.ValueRO.YawDegrees);
                float3 forward = new float3(math.sin(yawRadians), 0f, math.cos(yawRadians));
                float3 right = new float3(math.cos(yawRadians), 0f, -math.sin(yawRadians));
                float3 desiredHorizontal = right * moveInput.x + forward * moveInput.y;

                float3 currentVelocity = velocity.ValueRO.Linear;

                var useGroundControl = movementState.ValueRO.IsGrounded ||
                                       movementState.ValueRO.FallTime <= GroundControlGraceTime;
                if (useGroundControl)
                {
                    // Lerp toward desired ground speed instead of snapping.
                    // This allows physics solver wall-depenetration corrections to
                    // persist partially between frames, preventing tunnel-through
                    // on thin vertical terrain (BUG-011).
                    float3 targetHorizontal = desiredHorizontal * config.ValueRO.GroundSpeed;
                    float groundLerp = math.saturate(GroundLerpRate * deltaTime);
                    currentVelocity.x = math.lerp(currentVelocity.x, targetHorizontal.x, groundLerp);
                    currentVelocity.z = math.lerp(currentVelocity.z, targetHorizontal.z, groundLerp);
                }
                else
                {
                    // In the air we gradually steer toward the ground-speed target using the configured air control factor.
                    float lerpFactor = math.saturate(config.ValueRO.AirControl * deltaTime);
                    float3 horizontal = new float3(currentVelocity.x, 0f, currentVelocity.z);
                    float3 target = desiredHorizontal * config.ValueRO.GroundSpeed;
                    horizontal = math.lerp(horizontal, target, lerpFactor);
                    currentVelocity.x = horizontal.x;
                    currentVelocity.z = horizontal.z;
                }

                if (input.ValueRO.JumpPressed)
                {
                    if (movementState.ValueRO.IsGrounded)
                    {
                        // Inject an upward impulse, preserving any existing upward motion if it is already higher.
                        currentVelocity.y = math.max(currentVelocity.y, config.ValueRO.JumpImpulse);
                        // Flag the entity as airborne until grounding detects the next contact.
                        movementState.ValueRW.IsGrounded = false;
                    }
                    // Consume the jump input so the impulse only fires once per press.
                    input.ValueRW.JumpPressed = false;
                }

                // BUG-011 fix: Probe ahead for nearby terrain walls and slide velocity
                // along the surface rather than driving into it.
                //
                // Root cause: this system writes horizontal velocity (X/Z) every frame,
                // which fights the physics solver's depenetration impulse when the player
                // moves into a vertical wall. On horizontal ground there's no conflict
                // because the solver pushes on Y (which this system doesn't write), but
                // on vertical surfaces both the solver and this system act on the same
                // axis. The solver must win every frame; if it loses even once the capsule
                // penetrates the open-shell MeshCollider and gets launched upward or
                // pushed through entirely.
                //
                // The probe casts a short ray in the horizontal movement direction. If
                // terrain is detected within capsule radius + skin, the into-wall velocity
                // component is projected out so the solver never has to fight this system.
                //
                // FUTURE: This probe only covers horizontal (X/Z) velocity because the
                // movement system currently never writes velocity.y (gravity and jump are
                // handled by the physics integrator and one-shot impulse respectively).
                // If a future movement mode (swim, jetpack, zero-G) writes velocity.y
                // directly, a vertical probe must be added here to prevent the same
                // solver-fighting issue on horizontal surfaces (floors/ceilings).
                if (hasPhysicsWorld)
                {
                    float3 horizontalVel = new float3(currentVelocity.x, 0f, currentVelocity.z);
                    float horizontalSpeed = math.length(horizontalVel);
                    if (horizontalSpeed > WallProbeMinSpeed)
                    {
                        float3 moveDir = horizontalVel / horizontalSpeed;
                        float3 probeOrigin = transform.ValueRO.Position + CapsuleCenterOffset;

                        var probeInput = new RaycastInput
                        {
                            Start = probeOrigin,
                            End = probeOrigin + moveDir * WallProbeDistance,
                            Filter = new CollisionFilter
                            {
                                BelongsTo = ~0u,
                                CollidesWith = TerrainLayerBit,
                                GroupIndex = 0
                            }
                        };

                        if (TryCastRaySafe(
                            in physicsWorldSingleton.PhysicsWorld,
                            in probeInput,
                            ref _hasLoggedInvalidColliderWallProbeRaycast,
                            out var probeHit))
                        {
                            float velIntoWall = math.dot(horizontalVel, probeHit.SurfaceNormal);
                            if (velIntoWall < 0f)
                            {
                                var horizontalSpeedBeforeClamp = horizontalSpeed;
                                // Remove the into-wall component; player slides along surface.
                                horizontalVel -= velIntoWall * probeHit.SurfaceNormal;
                                currentVelocity.x = horizontalVel.x;
                                currentVelocity.z = horizontalVel.z;

                                if (DebugSettings.EnableFallThroughDebug && _frameCount % 10 == 0)
                                {
                                    var horizontalSpeedAfterClamp = math.length(horizontalVel);
                                    var targetGroundSpeed = math.length(desiredHorizontal) * config.ValueRO.GroundSpeed;
                                    DebugSettings.LogFallThrough(
                                        $"WallClamp: grounded={movementState.ValueRO.IsGrounded} useGroundControl={useGroundControl} " +
                                        $"fallTime={movementState.ValueRO.FallTime:F3} speedBefore={horizontalSpeedBeforeClamp:F3} " +
                                        $"speedAfter={horizontalSpeedAfterClamp:F3} targetGround={targetGroundSpeed:F3} " +
                                        $"normal={probeHit.SurfaceNormal} pos={transform.ValueRO.Position}");
                                }
                            }
                        }
                    }
                }

// write the updated velocity back to physics
                velocity.ValueRW.Linear = currentVelocity;
                velocity.ValueRW.Angular = float3.zero;
            }
            
            // Debug: Warn if multiple player entities found
            if (entityCount > 1 && _frameCount % 60 == 0) // Log every 60 frames
            {
                DebugSettings.LogPlayerWarning($"Found {entityCount} player entities! This causes conflicts. Only one should exist.", forceLog: true);
            }
        }

        private static bool TryCastRaySafe(
            in PhysicsWorld physicsWorld,
            in RaycastInput input,
            ref bool hasLoggedInvalidColliderRaycast,
            out RaycastHit hitInfo)
        {
            hitInfo = default;
            try
            {
                return physicsWorld.CastRay(input, out hitInfo);
            }
            catch (InvalidOperationException ex)
            {
                if (!hasLoggedInvalidColliderRaycast)
                {
                    DebugSettings.LogFallThroughWarning(
                        $"Wall probe raycast skipped due to invalid collider blob reference during collider rebuild. Details: {ex.Message}");
                    hasLoggedInvalidColliderRaycast = true;
                }

                return false;
            }
        }
    }
}
