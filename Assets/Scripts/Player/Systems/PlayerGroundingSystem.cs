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
        private const float SteepRecoveryProbeOffset = 0.35f;
        private const float GroundSupportProbeRadius = 0.35f;
        private const float GroundSupportProbeMaxFallTime = 0.08f;
        private const float EmbeddedRecoveryMinFallTime = 0.2f;
        private const float EmbeddedRecoveryMaxVerticalSpeed = 0.05f;
        private const float EmbeddedRecoveryUpProbeDistanceMultiplier = 2f;
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

            foreach (var (transform, config, movementState, velocity) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<PlayerMovementConfig>, RefRW<PlayerMovementState>, RefRO<PhysicsVelocity>>())
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
                var rayHit = TryCastRaySafe(in physicsWorld, in rayInput, ref _hasLoggedInvalidColliderRaycast, out var closestHit);
                var resolvedHit = closestHit;
                var usedSteepRecovery = false;
                var usedSupportRecovery = false;
                var usedEmbeddedRecovery = false;
                var hit = false;

                if (rayHit)
                {
                    if (IsGroundLike(in closestHit))
                    {
                        hit = true;
                    }
                    else if (TryRecoverGroundFromSteepHit(
                                 in physicsWorld,
                                 origin,
                                 distance,
                                 in closestHit,
                                 ref _hasLoggedInvalidColliderRaycast,
                                 out var recoveredHit))
                    {
                        hit = true;
                        resolvedHit = recoveredHit;
                        usedSteepRecovery = true;
                    }
                }

                if (!hit &&
                    ShouldRunSupportProbes(
                        prevGrounded,
                        movementState.ValueRO.FallTime,
                        rayHit,
                        in closestHit) &&
                    TryRecoverGroundFromSupportProbes(
                        in physicsWorld,
                        origin,
                        distance,
                        ref _hasLoggedInvalidColliderRaycast,
                        out var supportHit))
                {
                    hit = true;
                    resolvedHit = supportHit;
                    usedSupportRecovery = true;
                }

                if (!hit &&
                    !rayHit &&
                    TryRecoverGroundFromEmbeddedNoHit(
                        in physicsWorld,
                        origin,
                        distance,
                        movementState.ValueRO.FallTime,
                        velocity.ValueRO.Linear.y,
                        ref _hasLoggedInvalidColliderRaycast,
                        out var embeddedHit))
                {
                    hit = true;
                    resolvedHit = embeddedHit;
                    usedEmbeddedRecovery = true;
                }

                if (DebugSettings.EnableFallThroughDebug && prevGrounded != hit)
                {
                    if (hit)
                    {
                        var recoveryTag = usedSteepRecovery
                            ? " [steep-recovery]"
                            : usedSupportRecovery
                                ? " [support-recovery]"
                                : usedEmbeddedRecovery
                                    ? " [embedded-recovery]"
                                : string.Empty;
                        DebugSettings.LogFallThrough(
                            $"Grounded{recoveryTag}: pos={origin}, hitPos={resolvedHit.Position}, " +
                            $"normal={resolvedHit.SurfaceNormal}, fraction={resolvedHit.Fraction:F4}, " +
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

        private static bool IsGroundLike(in RaycastHit hit)
        {
            return hit.SurfaceNormal.y >= MinGroundNormalY;
        }

        private static bool TryRecoverGroundFromSteepHit(
            in PhysicsWorld physicsWorld,
            float3 origin,
            float distance,
            in RaycastHit steepHit,
            ref bool hasLoggedInvalidColliderRaycast,
            out RaycastHit recoveredHit)
        {
            recoveredHit = default;

            var horizontalNormal = new float3(steepHit.SurfaceNormal.x, 0f, steepHit.SurfaceNormal.z);
            if (math.lengthsq(horizontalNormal) <= 1e-6f)
            {
                return false;
            }

            var probeOffset = math.normalize(horizontalNormal) * SteepRecoveryProbeOffset;
            if (TryGroundProbe(in physicsWorld, origin + probeOffset, distance, ref hasLoggedInvalidColliderRaycast, out var firstHit) &&
                IsGroundLike(in firstHit))
            {
                recoveredHit = firstHit;
                return true;
            }

            if (TryGroundProbe(in physicsWorld, origin - probeOffset, distance, ref hasLoggedInvalidColliderRaycast, out var secondHit) &&
                IsGroundLike(in secondHit))
            {
                recoveredHit = secondHit;
                return true;
            }

            return false;
        }

        private static bool ShouldRunSupportProbes(bool prevGrounded, float fallTime, bool rayHit, in RaycastHit primaryHit)
        {
            if (prevGrounded || fallTime <= GroundSupportProbeMaxFallTime)
            {
                return true;
            }

            return rayHit && primaryHit.Fraction <= 0.2f;
        }

        private static bool TryRecoverGroundFromSupportProbes(
            in PhysicsWorld physicsWorld,
            float3 origin,
            float distance,
            ref bool hasLoggedInvalidColliderRaycast,
            out RaycastHit recoveredHit)
        {
            recoveredHit = default;
            var found = false;
            var bestFraction = float.MaxValue;

            EvaluateSupportProbe(
                in physicsWorld,
                origin + new float3(GroundSupportProbeRadius, 0f, 0f),
                distance,
                ref hasLoggedInvalidColliderRaycast,
                ref found,
                ref bestFraction,
                ref recoveredHit);

            EvaluateSupportProbe(
                in physicsWorld,
                origin - new float3(GroundSupportProbeRadius, 0f, 0f),
                distance,
                ref hasLoggedInvalidColliderRaycast,
                ref found,
                ref bestFraction,
                ref recoveredHit);

            EvaluateSupportProbe(
                in physicsWorld,
                origin + new float3(0f, 0f, GroundSupportProbeRadius),
                distance,
                ref hasLoggedInvalidColliderRaycast,
                ref found,
                ref bestFraction,
                ref recoveredHit);

            EvaluateSupportProbe(
                in physicsWorld,
                origin - new float3(0f, 0f, GroundSupportProbeRadius),
                distance,
                ref hasLoggedInvalidColliderRaycast,
                ref found,
                ref bestFraction,
                ref recoveredHit);

            return found;
        }

        private static bool TryRecoverGroundFromEmbeddedNoHit(
            in PhysicsWorld physicsWorld,
            float3 origin,
            float distance,
            float fallTime,
            float verticalVelocityY,
            ref bool hasLoggedInvalidColliderRaycast,
            out RaycastHit recoveredHit)
        {
            recoveredHit = default;

            if (fallTime < EmbeddedRecoveryMinFallTime)
            {
                return false;
            }

            if (math.abs(verticalVelocityY) > EmbeddedRecoveryMaxVerticalSpeed)
            {
                return false;
            }

            var probeDistance = math.max(0.1f, distance) * EmbeddedRecoveryUpProbeDistanceMultiplier;
            var upProbe = new RaycastInput
            {
                Start = origin,
                End = origin + math.up() * probeDistance,
                Filter = CollisionFilter.Default
            };

            if (!TryCastRaySafe(in physicsWorld, in upProbe, ref hasLoggedInvalidColliderRaycast, out var upHit))
            {
                return false;
            }

            // Embedded recovery is intended to restore grounding against actual floor support,
            // not ceilings. Reuse the standard ground predicate so downward-facing normals fail.
            if (!IsGroundLike(in upHit))
            {
                return false;
            }

            recoveredHit = upHit;
            return true;
        }

        private static void EvaluateSupportProbe(
            in PhysicsWorld physicsWorld,
            float3 probeOrigin,
            float distance,
            ref bool hasLoggedInvalidColliderRaycast,
            ref bool found,
            ref float bestFraction,
            ref RaycastHit recoveredHit)
        {
            if (!TryGroundProbe(in physicsWorld, probeOrigin, distance, ref hasLoggedInvalidColliderRaycast, out var hit))
            {
                return;
            }

            if (!IsGroundLike(in hit))
            {
                return;
            }

            if (!found || hit.Fraction < bestFraction)
            {
                found = true;
                bestFraction = hit.Fraction;
                recoveredHit = hit;
            }
        }

        private static bool TryGroundProbe(
            in PhysicsWorld physicsWorld,
            float3 origin,
            float distance,
            ref bool hasLoggedInvalidColliderRaycast,
            out RaycastHit hitInfo)
        {
            var rayInput = new RaycastInput
            {
                Start = origin,
                End = origin - math.up() * distance,
                Filter = CollisionFilter.Default
            };

            return TryCastRaySafe(in physicsWorld, in rayInput, ref hasLoggedInvalidColliderRaycast, out hitInfo);
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
