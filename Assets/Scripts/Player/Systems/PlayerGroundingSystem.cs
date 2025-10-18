using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Tracks whether each controllable entity is touching the ground by issuing a physics raycast every physics step.
    /// </summary>
    /// <remarks>
    /// Runs ahead of <see cref="PlayerMovementSystem"/> so that movement logic can rely on updated grounded state information.
    /// </remarks>
    [BurstCompile]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerGroundingSystem : ISystem
    {
        /// <summary>
        /// Declares the singleton components that must exist before the system begins updating.
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerMovementState>();
        }

        /// <summary>
        /// Performs a downward raycast for each player entity to determine if it is currently grounded.
        /// </summary>
        /// <param name="state">The current system execution state.</param>
        public void OnUpdate(ref SystemState state)
        {
            var physicsWorldSingleton = SystemAPI.GetSingleton<PhysicsWorldSingleton>();
            var physicsWorld = physicsWorldSingleton.PhysicsWorld;
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, config, movementState) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerMovementConfig>, RefRW<PlayerMovementState>>())
            {
                var origin = transform.ValueRO.Position;
                // Guarantee a minimal probe length so very small configured distances still detect immediate ground contact.
                var distance = math.max(0.1f, config.ValueRO.GroundProbeDistance);
                var rayInput = new RaycastInput
                {
                    Start = origin,
                    End = origin - math.up() * distance,
                    // Use the default collision filter so grounding respects the global physics collision matrix.
                    Filter = CollisionFilter.Default
                };

                bool hit = physicsWorld.CastRay(rayInput);

                movementState.ValueRW.IsGrounded = hit;
                if (hit)
                {
                    movementState.ValueRW.Mode = PlayerMovementMode.Ground;
                    movementState.ValueRW.FallTime = 0f;
                }
                else
                {
                    movementState.ValueRW.FallTime += deltaTime;
                }
            }
        }
    }
}
