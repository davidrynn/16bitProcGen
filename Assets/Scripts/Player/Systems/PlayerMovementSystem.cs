using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Translates player input into physics-friendly velocity updates, handling both ground movement and airborne control.
    /// </summary>
    /// <remarks>
    /// Runs before <see cref="PhysicsSimulationGroup"/> so the updated velocities feed into the current physics step.
    /// </remarks>
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
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (config, input, movementState, transform, velocity) in
                     SystemAPI.Query<RefRO<PlayerMovementConfig>, RefRW<PlayerInputComponent>, RefRW<PlayerMovementState>, RefRO<LocalTransform>, RefRW<PhysicsVelocity>>())
            {
                float2 moveInput = input.ValueRO.Move;
                if (math.lengthsq(moveInput) > 1f)
                {
                    moveInput = math.normalize(moveInput);
                }

                float3 forward = math.normalizesafe(math.mul(transform.ValueRO.Rotation, new float3(0f, 0f, 1f)), new float3(0f, 0f, 1f));
                float3 right = math.normalizesafe(math.mul(transform.ValueRO.Rotation, new float3(1f, 0f, 0f)), new float3(1f, 0f, 0f));
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

                velocity.ValueRW.Linear = currentVelocity;
            }
        }
    }
}
