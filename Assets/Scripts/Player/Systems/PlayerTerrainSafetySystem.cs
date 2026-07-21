using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using DOTS.Player.Components;
using DOTS.Core;
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

        // --- M8 / BUG-021: below-world recovery ---
        // Clearance below the chunk slab before a position counts as "out of the world". The slab
        // is only 15u tall, so this is far outside any legitimate play — including digging, which
        // cannot breach the slab floor.
        private const float BelowWorldMargin = 60f;
        // Lift on re-seat so the player doesn't respawn inside the surface they fell through.
        private const float RecoveryLift = 2f;

        private double _lastTeleportTime;
        private bool _hasLoggedInvalidColliderRaycast;
        private float3 _lastGroundedPosition;
        private bool _hasLastGroundedPosition;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
            state.RequireForUpdate<PhysicsWorldSingleton>();
            _lastTeleportTime = 0.0;
            _hasLoggedInvalidColliderRaycast = false;
            _hasLastGroundedPosition = false;
        }

        /// <summary>
        /// Catches a player who has left the world entirely and re-seats them at the last position
        /// they were known to be standing on.
        /// </summary>
        /// <remarks>
        /// Deliberately runs BEFORE the tunneling cooldown gate. Falling out of the world is
        /// run-ending — observed 2026-07-21 at FallTime 90 s, Y = -36,094, 842 m/s and still
        /// accelerating — so it must never be suppressed by a 0.5 s cooldown that exists for a
        /// different failure mode. Always logs: the reason this bug class went undiagnosed for so
        /// long is that nothing ever complained.
        /// </remarks>
        private void RecoverBelowWorld(ref SystemState state, EntityManager entityManager)
        {
            // Derive the floor rather than hardcoding it — ticket U3 (vertical chunking) moves the
            // world floor, and a literal here would silently stop protecting anything.
            var slabBottom = -7.5f;
            if (SystemAPI.TryGetSingleton<DOTS.Terrain.LOD.TerrainLodSettings>(out var lodPolicy))
            {
                var span = math.max(0, lodPolicy.Lod0Resolution.y - 1) * lodPolicy.Lod0VoxelSize;
                var baseHeight = SystemAPI.TryGetSingleton<DOTS.Terrain.SDFTerrainFieldSettings>(out var f)
                    ? f.BaseHeight
                    : 0f;
                slabBottom = baseHeight - span * 0.5f;
            }
            var floorY = slabBottom - BelowWorldMargin;

            foreach (var (transform, velocity, movementState, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerMovementState>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                // The startup gate owns the player during the sky-drop; it legitimately sits far
                // above the world and must not be re-seated by us.
                if (entityManager.HasComponent<PlayerStartupReadinessGate>(entity))
                    continue;

                var pos = transform.ValueRO.Position;

                if (movementState.ValueRO.IsGrounded)
                {
                    _lastGroundedPosition = pos;
                    _hasLastGroundedPosition = true;
                }

                if (pos.y >= floorY)
                    continue;

                // Prefer the last position we know had a collider under it. Without one, re-seat
                // above the slab at the same XZ and let the (now distance-prioritised) chunk
                // pipeline catch up.
                var recovery = _hasLastGroundedPosition
                    ? _lastGroundedPosition + new float3(0f, RecoveryLift, 0f)
                    : new float3(pos.x, slabBottom + math.max(RecoveryLift, 20f), pos.z);

                DebugSettings.LogWarning(
                    $"[PlayerTerrainSafetySystem] BELOW WORLD at {pos} (floor {floorY:F1}, " +
                    $"fallTime {movementState.ValueRO.FallTime:F1}s) — re-seating to {recovery}. " +
                    $"This means a fall-through already happened; see KNOWN_ISSUES BUG-019.");

                transform.ValueRW.Position = recovery;
                velocity.ValueRW.Linear = float3.zero;
                velocity.ValueRW.Angular = float3.zero;
                movementState.ValueRW.PreviousPosition = recovery;
                movementState.ValueRW.FallTime = 0f;
            }
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            RecoverBelowWorld(ref state, entityManager);

            var elapsed = SystemAPI.Time.ElapsedTime;
            if (elapsed - _lastTeleportTime < CooldownSeconds)
                return;

            var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            foreach (var (transform, velocity, movementState, entity) in
                     SystemAPI.Query<RefRW<LocalTransform>, RefRW<PhysicsVelocity>, RefRW<PlayerMovementState>>()
                         .WithAll<PlayerTag>()
                         .WithEntityAccess())
            {
                if (entityManager.HasComponent<PlayerStartupReadinessGate>(entity))
                {
                    movementState.ValueRW.PreviousPosition = transform.ValueRO.Position;
                    velocity.ValueRW.Linear = float3.zero;
                    velocity.ValueRW.Angular = float3.zero;
                    continue;
                }

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

                // Detect lateral tunneling into steep surfaces (e.g. thin vertical terrain walls).
                // Only check while airborne — grounded lateral wall-clipping is handled by
                // the wall probe in PlayerMovementSystem and doesn't require a snap-back.
                // Firing while grounded causes false positives: the horizontal ray at capsule-
                // center height (Y+1) routinely clips seamless terrain geometry on the ground
                // surface, snapping the player back every 0.5 s and appearing as a freeze.
                var horizontalTunnelCheck =
                    !movementState.ValueRO.IsGrounded &&
                    horizontalDisplacementSq >= MinDisplacementSq;
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
