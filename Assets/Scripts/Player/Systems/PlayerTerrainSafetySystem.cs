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
    /// Physics-based safety net that detects when the player tunnels through terrain
    /// and snaps them back to the last known-good position.
    ///
    /// Only activates when the player is NOT grounded and falling — this avoids false
    /// positives from the prev→current ray clipping terrain the player is walking on.
    /// The ray is also offset to capsule center height for additional clearance.
    ///
    /// Works for any collidable geometry — terrain surfaces, dungeons, caves, SDF carve-outs.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateBefore(typeof(PlayerGroundingSystem))]
    public partial struct PlayerTerrainSafetySystem : ISystem
    {
        private const float CooldownSeconds = 0.5f;
        // Ignore micro-movements to avoid false positives from floating-point jitter.
        private const float MinDisplacementSq = 0.01f;
        // Require meaningful downward velocity to avoid triggering during minor grounding jitter.
        private const float MinDownwardVelocity = -0.5f;
        // Player must have been falling for this long before we consider tunneling.
        // Avoids false triggers from single-frame grounding flickers on bumpy terrain.
        private const float MinFallTimeForCheck = 0.15f;
        // Hits very close to ray end are usually normal landing contacts, not tunneling.
        private const float MaxHitFractionForTunnel = 0.9f;
        // Player layer bit used in PlayerEntityBootstrap collider setup.
        private const uint PlayerLayerBit = 1u;
        // Push recovered position slightly away from wall to prevent immediate re-penetration.
        private const float WallPushOutDistance = 0.05f;
        // Offset ray to capsule center so it doesn't scrape the terrain surface.
        // Capsule: Vertex0=(0,0.5,0), Vertex1=(0,1.5,0) -> center at Y+1.0 from entity origin.
        private static readonly float3 CapsuleCenterOffset = new float3(0f, 1.0f, 0f);

        private double _lastTeleportTime;
        private bool _hasLoggedInvalidColliderRaycast;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _lastTeleportTime = 0.0;
            _hasLoggedInvalidColliderRaycast = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            var elapsed = SystemAPI.Time.ElapsedTime;
            if (elapsed - _lastTeleportTime < CooldownSeconds)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            foreach (var (transform, velocity, movementState, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerMovementState>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                var currentPos = transform.ValueRO.Position;
                var previousPos = movementState.ValueRO.PreviousPosition;

                // Always update previous position for next frame.
                movementState.ValueRW.PreviousPosition = currentPos;

                var displacement = currentPos - previousPos;
                if (math.lengthsq(displacement) < MinDisplacementSq)
                    continue;

                var horizontalDisplacementSq = displacement.x * displacement.x + displacement.z * displacement.z;
                var fallingTunnelCheck =
                    !movementState.ValueRO.IsGrounded &&
                    movementState.ValueRO.FallTime >= MinFallTimeForCheck &&
                    displacement.y < 0f &&
                    velocity.ValueRO.Linear.y <= MinDownwardVelocity;

                // Also detect lateral tunneling into steep surfaces (e.g. thin vertical terrain walls).
                var horizontalTunnelCheck = horizontalDisplacementSq >= MinDisplacementSq;
                if (!fallingTunnelCheck && !horizontalTunnelCheck)
                    continue;

                // Cast from previous to current, raised to capsule center height.
                var rayInput = new RaycastInput
                {
                    Start = previousPos + CapsuleCenterOffset,
                    End = currentPos + CapsuleCenterOffset,
                    // Ignore player-layer bodies (including self) to prevent constant false hits.
                    Filter = new CollisionFilter
                    {
                        BelongsTo = uint.MaxValue,
                        CollidesWith = ~PlayerLayerBit,
                        GroupIndex = 0
                    }
                };

                if (TryCastRaySafe(in physicsWorld, rayInput, ref _hasLoggedInvalidColliderRaycast, out var hit))
                {
                    // Ignore near-end contacts that represent expected landing.
                    if (hit.Fraction >= MaxHitFractionForTunnel || hit.Entity == entity)
                        continue;

                    // The ray hit a collider between previous and current — the player tunneled.
                    // Snap back to previous known-good position, pushed slightly away from
                    // the wall surface to prevent immediate re-penetration (BUG-011).
                    float3 safePos = previousPos + hit.SurfaceNormal * WallPushOutDistance;
                    transform.ValueRW.Position = safePos;
                    movementState.ValueRW.PreviousPosition = safePos;

                    // Remove the velocity component driving into the wall so the player
                    // doesn't immediately re-penetrate on the next frame.
                    var vel = velocity.ValueRO.Linear;
                    float velIntoWall = math.dot(vel, hit.SurfaceNormal);
                    if (velIntoWall < 0f)
                        vel -= velIntoWall * hit.SurfaceNormal;
                    // Also zero any residual downward velocity for falling tunnel cases.
                    if (vel.y < 0f)
                        vel.y = 0f;
                    velocity.ValueRW.Linear = vel;

                    movementState.ValueRW.FallTime = 0f;
                    _lastTeleportTime = elapsed;

                    DebugSettings.LogFallThrough(
                        $"Safety snap-back: tunneled from {previousPos} to {currentPos}, " +
                        $"hit at fraction={hit.Fraction:F4}, normal={hit.SurfaceNormal}, " +
                        $"restored to {safePos}");
                }
            }
        }

        private static bool TryCastRaySafe(in PhysicsWorld physicsWorld, in RaycastInput input, ref bool hasLoggedInvalidColliderRaycast, out RaycastHit hit)
        {
            hit = default;
            try
            {
                return physicsWorld.CastRay(input, out hit);
            }
            catch (InvalidOperationException ex)
            {
                if (!hasLoggedInvalidColliderRaycast)
                {
                    DebugSettings.LogFallThroughWarning(
                        $"Safety raycast skipped due to invalid collider blob reference. " +
                        $"Likely terrain collider disposal race during streaming/rebuild. Details: {ex.Message}");
                    hasLoggedInvalidColliderRaycast = true;
                }
                return false;
            }
        }
    }
}
