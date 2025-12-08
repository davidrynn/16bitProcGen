using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Shared diagnostics helper for player bootstrap physics expectations.
    /// Provides a single place to read gravity/timestep data so runtime and tests stay in sync.
    /// </summary>
    public static class PlayerBootstrapPhysicsUtility
    {
        public static bool EnableLogging { get; set; }
        public static bool EnableVerboseLogging { get; set; }

        private static PlayerBootstrapPhysicsSnapshot _lastSnapshot;
        private static bool _hasSnapshot;
        private static readonly float3 DefaultGravity = new float3(0f, -9.81f, 0f);

        public static PlayerBootstrapPhysicsSnapshot Capture(World world, string contextLabel = null)
        {
            if (world == null || !world.IsCreated)
            {
                var fallback = new PlayerBootstrapPhysicsSnapshot(DefaultGravity, Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : (1f / 60f), 0d);
                MaybeLog("WorldMissing", contextLabel, fallback, "World was null or not created");
                return fallback;
            }

            var entityManager = world.EntityManager;
            float3 gravity = DefaultGravity;
            using (var physicsStepQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsStep>()))
            {
                if (!physicsStepQuery.IsEmpty)
                {
                    gravity = physicsStepQuery.GetSingleton<PhysicsStep>().Gravity;
                }
            }

            float fixedTimeStep = ResolveFixedTimeStep(world);

            var snapshot = new PlayerBootstrapPhysicsSnapshot(gravity, fixedTimeStep, world.Time.ElapsedTime);
            _lastSnapshot = snapshot;
            _hasSnapshot = true;

            MaybeLog("Capture", contextLabel, snapshot, null);
            return snapshot;
        }

        public static bool TryGetLastSnapshot(out PlayerBootstrapPhysicsSnapshot snapshot)
        {
            snapshot = _lastSnapshot;
            return _hasSnapshot;
        }

        private static float ResolveFixedTimeStep(World world)
        {
            var fixedStepGroup = world.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedStepGroup != null && fixedStepGroup.Timestep > 0f)
            {
                return fixedStepGroup.Timestep;
            }

            float deltaTime = world.Time.DeltaTime;
            if (deltaTime > 0f)
            {
                return deltaTime;
            }

            return Time.fixedDeltaTime > 0f ? Time.fixedDeltaTime : 1f / 60f;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void MaybeLog(string action, string contextLabel, PlayerBootstrapPhysicsSnapshot snapshot, string note)
        {
            if (!EnableLogging)
            {
                return;
            }

            if (!EnableVerboseLogging && action != "Capture")
            {
                return;
            }

            var prefix = string.IsNullOrEmpty(contextLabel)
                ? "[PlayerBootstrapDiagnostics]"
                : $"[PlayerBootstrapDiagnostics::{contextLabel}]";

            var message = $"{prefix} {action} gravity={snapshot.Gravity} timestep={snapshot.FixedTimeStep:F6}s capturedAt={snapshot.CaptureTime:F4}";
            if (!string.IsNullOrEmpty(note))
            {
                message += $" ({note})";
            }

            Debug.Log(message);
        }
    }

    public readonly struct PlayerBootstrapPhysicsSnapshot
    {
        public readonly float3 Gravity;
        public readonly float FixedTimeStep;
        public readonly double CaptureTime;

        public PlayerBootstrapPhysicsSnapshot(float3 gravity, float fixedTimeStep, double captureTime)
        {
            Gravity = gravity;
            FixedTimeStep = fixedTimeStep;
            CaptureTime = captureTime;
        }
    }
}
