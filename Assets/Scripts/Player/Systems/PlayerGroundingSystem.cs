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
    /// Tracks whether each controllable entity is touching the ground by issuing a physics raycast every physics step.
    /// </summary>
    /// <remarks>
    /// Runs ahead of <see cref="PlayerMovementSystem"/> so that movement logic can rely on updated grounded state information.
    /// </remarks>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct PlayerGroundingSystem : ISystem
    {
        // Treat only reasonably upward-facing surfaces as "ground".
        // This prevents vertical/near-vertical walls from being classified as grounded.
        private const float MinGroundNormalY = 0.5f;
        private bool _hasLoggedInvalidColliderRaycast;

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

                var prevGrounded = movementState.ValueRO.IsGrounded;
                bool rayHit;
                bool hit;
                RaycastHit closestHit;
                if (DebugSettings.EnableFallThroughDebug)
                {
                    rayHit = TryCastRaySafe(in physicsWorld, in rayInput, ref _hasLoggedInvalidColliderRaycast, out closestHit);
                    hit = rayHit && closestHit.SurfaceNormal.y >= MinGroundNormalY;

                    // Log grounding state transitions
                    if (prevGrounded != hit)
                    {
                        if (hit)
                        {
                            DebugSettings.LogFallThrough(
                                $"Grounded: pos={origin}, hitPos={closestHit.Position}, " +
                                $"normal={closestHit.SurfaceNormal}, fraction={closestHit.Fraction:F4}, " +
                                $"fallTime={movementState.ValueRO.FallTime:F3}");
                        }
                        else
                        {
                            var reason = rayHit
                                ? $"steepHit normal={closestHit.SurfaceNormal}"
                                : "noHit";
                            DebugSettings.LogFallThroughWarning(
                                $"Ungrounded: pos={origin}, probeEnd={rayInput.End}, reason={reason}, " +
                                $"fallTime={movementState.ValueRO.FallTime:F3}");
                        }
                    }
                }
                else
                {
                    rayHit = TryCastRaySafe(in physicsWorld, in rayInput, ref _hasLoggedInvalidColliderRaycast, out closestHit);
                    hit = rayHit && closestHit.SurfaceNormal.y >= MinGroundNormalY;
                }

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

        private static bool TryCastRaySafe(in PhysicsWorld physicsWorld, in RaycastInput input, ref bool hasLoggedInvalidColliderRaycast, out RaycastHit hitInfo)
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
                        $"Grounding raycast skipped due to invalid collider blob reference. " +
                        $"Likely terrain collider disposal race during streaming/rebuild. Details: {ex.Message}");
                    hasLoggedInvalidColliderRaycast = true;
                }
                return false;
            }
        }
    }
}
