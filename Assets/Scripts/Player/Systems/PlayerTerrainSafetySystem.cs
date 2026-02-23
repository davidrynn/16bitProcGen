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
    [UpdateAfter(typeof(BuildPhysicsWorld))]
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

                // Only check for tunneling when the player is airborne and has been
                // falling for a meaningful duration. When grounded, the prev->current
                // ray will clip the terrain surface we're standing on, causing constant
                // false-positive snap-backs (the "bouncing" bug).
                if (movementState.ValueRO.IsGrounded)
                    continue;

                if (movementState.ValueRO.FallTime < MinFallTimeForCheck)
                    continue;

                var displacement = currentPos - previousPos;
                if (math.lengthsq(displacement) < MinDisplacementSq)
                    continue;

                // Only check when moving downward — tunneling through terrain means
                // passing downward through a surface, not walking horizontally.
                if (displacement.y >= 0f)
                    continue;

                if (velocity.ValueRO.Linear.y > MinDownwardVelocity)
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
                    // Snap back to previous known-good position and zero downward velocity.
                    transform.ValueRW.Position = previousPos;
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
