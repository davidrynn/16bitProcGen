using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

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
        /// <summary>
        /// Ensures movement configuration data exists prior to running the system.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementConfig>();
        }

        /// <summary>
        /// Applies horizontal motion, air steering, and jump impulses based on the player's current input and movement state.
        /// </summary>
        /// <param name="state">The execution context for this system tick.</param>
        private static int _frameCount = 0;
        private static bool _hasLoggedMovementOnce = false;

        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            _frameCount++;

            int entityCount = 0;
            foreach (var (config, input, movementState, view, transform, velocity, entity) in
                     SystemAPI.Query<RefRO<PlayerMovementConfig>, RefRW<PlayerInputComponent>, RefRW<PlayerMovementState>, RefRO<PlayerViewComponent>, RefRW<LocalTransform>, RefRW<PhysicsVelocity>>().WithEntityAccess())
            {
                entityCount++;
                float2 moveInput = input.ValueRO.Move;
                
                // Debug: Log first movement to see which entity is moving
                if (math.lengthsq(moveInput) > 0.01f && !_hasLoggedMovementOnce)
                {
                    Debug.Log($"[PlayerMovement] Entity {entity.Index} is moving! Input: {moveInput}, Position: {transform.ValueRO.Position}, Velocity: {velocity.ValueRO.Linear}");
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

                if (movementState.ValueRO.Mode == PlayerMovementMode.Ground || movementState.ValueRO.IsGrounded)
                {
                    // On the ground we snap directly to the desired ground speed for responsive input.
                    currentVelocity.x = desiredHorizontal.x * config.ValueRO.GroundSpeed;
                    currentVelocity.z = desiredHorizontal.z * config.ValueRO.GroundSpeed;
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
                    if (movementState.ValueRO.IsGrounded || movementState.ValueRO.Mode == PlayerMovementMode.Ground)
                    {
                        // Inject an upward impulse, preserving any existing upward motion if it is already higher.
                        currentVelocity.y = math.max(currentVelocity.y, config.ValueRO.JumpImpulse);
                        // Flag the entity as airborne until grounding detects the next contact.
                        movementState.ValueRW.IsGrounded = false;
                        movementState.ValueRW.Mode = PlayerMovementMode.Ground;
                    }
                    // Consume the jump input so the impulse only fires once per press.
                    input.ValueRW.JumpPressed = false;
                }

// write the updated velocity back to physics
                velocity.ValueRW.Linear = currentVelocity;
                velocity.ValueRW.Angular = float3.zero;
            }
            
            // Debug: Warn if multiple player entities found
            if (entityCount > 1 && _frameCount % 60 == 0) // Log every 60 frames
            {
                Debug.LogWarning($"[PlayerMovement] WARNING: Found {entityCount} player entities! This causes conflicts. Only one should exist.");
            }
        }
    }
}
