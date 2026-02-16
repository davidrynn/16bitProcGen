using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using DOTS.Player.Components;
using DOTS.Terrain.Core;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Physics-based safety net: casts a ray from the player's previous position to their current
    /// position each frame. If the ray hits a collider between the two points, the player tunneled
    /// through a surface and is snapped back to the previous (known-good) position.
    ///
    /// Works for any collidable geometry — terrain surfaces, dungeons, caves, SDF carve-outs —
    /// because it queries the actual physics world rather than an analytical formula.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PlayerGroundingSystem))]
    public partial struct PlayerTerrainSafetySystem : ISystem
    {
        private const float CooldownSeconds = 0.5f;
        // Ignore micro-movements to avoid false positives from floating-point jitter.
        private const float MinDisplacementSq = 0.01f;

        private double _lastTeleportTime;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _lastTeleportTime = 0.0;
        }

        public void OnUpdate(ref SystemState state)
        {
            var elapsed = SystemAPI.Time.ElapsedTime;
            if (elapsed - _lastTeleportTime < CooldownSeconds)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            foreach (var (transform, velocity, movementState) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerMovementState>>()
                         .WithAll<PlayerTag>())
            {
                var currentPos = transform.ValueRO.Position;
                var previousPos = movementState.ValueRO.PreviousPosition;

                // Always update previous position for next frame.
                movementState.ValueRW.PreviousPosition = currentPos;

                var displacement = currentPos - previousPos;
                if (math.lengthsq(displacement) < MinDisplacementSq)
                    continue;

                // Cast from previous position to current position.
                // If a collider is hit between the two, the player passed through it.
                var rayInput = new RaycastInput
                {
                    Start = previousPos,
                    End = currentPos,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    // The ray hit a collider between previous and current — the player tunneled.
                    // Snap back to previous known-good position and zero downward velocity.
                    transform.ValueRW.Position = previousPos;
                    // Also update PreviousPosition so we don't re-trigger next frame.
                    movementState.ValueRW.PreviousPosition = previousPos;

                    var vel = velocity.ValueRO.Linear;
                    if (vel.y < 0f)
                        vel.y = 0f;
                    velocity.ValueRW.Linear = vel;

                    movementState.ValueRW.FallTime = 0f;
                    _lastTeleportTime = elapsed;

                    DebugSettings.LogFallThrough(
                        $"Safety snap-back: tunneled from {previousPos} to {currentPos}, " +
                        $"hit at fraction={hit.Fraction:F4}, restored to {previousPos}");
                }
            }
        }
    }
}
