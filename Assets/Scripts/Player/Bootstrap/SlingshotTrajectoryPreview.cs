using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Debug trajectory arc rendered with a LineRenderer during slingshot charging.
    /// Reads SlingshotChargeState + SlingshotConfig from ECS each frame and computes
    /// projectile-motion points using the same impulse formula as SlingshotLaunchSystem.
    /// Auto-installs at runtime. Temporary debug aid — remove or replace with VFX later.
    /// </summary>
    public static class SlingshotTrajectoryPreview
    {
        private const string RootName = "SlingshotTrajectoryPreviewRoot";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (GameObject.Find(RootName) != null)
                return;

            var go = new GameObject(RootName);
            Object.DontDestroyOnLoad(go);
            go.AddComponent<TrajectoryRenderer>();
        }

        private sealed class TrajectoryRenderer : MonoBehaviour
        {
            private const int MaxPoints = 40;
            private const float TimeStep = 0.05f;

            private LineRenderer _line;

            private void Awake()
            {
                _line = gameObject.AddComponent<LineRenderer>();
                _line.positionCount = 0;
                _line.startWidth = 0.08f;
                _line.endWidth = 0.03f;
                _line.material = new Material(Shader.Find("Sprites/Default"));
                _line.startColor = new Color(1f, 0.9f, 0.2f, 0.9f);
                _line.endColor = new Color(1f, 0.4f, 0.1f, 0.3f);
                _line.useWorldSpace = true;
            }

            private void LateUpdate()
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    _line.positionCount = 0;
                    return;
                }

                var em = world.EntityManager;

                // Find a player entity with an active charge state
                using var chargeQuery = em.CreateEntityQuery(
                    typeof(SlingshotChargeState),
                    typeof(SlingshotConfig),
                    typeof(LocalTransform));

                if (chargeQuery.IsEmpty)
                {
                    _line.positionCount = 0;
                    return;
                }

                var entity = chargeQuery.GetSingletonEntity();
                var charge = em.GetComponentData<SlingshotChargeState>(entity);
                var config = em.GetComponentData<SlingshotConfig>(entity);
                var transform = em.GetComponentData<LocalTransform>(entity);

                float impulseStrength = config.MaxForce * math.pow(charge.ChargeNormalized, config.CurveExponent);

                // Below launch threshold — show nothing
                if (charge.ChargeNormalized < config.MinLaunchThreshold)
                {
                    _line.positionCount = 0;
                    return;
                }

                float3 velocity = charge.AimDirection * impulseStrength;
                float3 gravity = new float3(0f, -config.CustomGravity, 0f);
                float3 startPos = transform.Position + new float3(0f, 1f, 0f); // offset to roughly head height

                // Step through projectile motion: p(t) = p0 + v*t + 0.5*g*t^2
                int pointCount = MaxPoints;
                var positions = new Vector3[pointCount];
                for (int i = 0; i < pointCount; i++)
                {
                    float t = i * TimeStep;
                    float3 pos = startPos + velocity * t + 0.5f * gravity * t * t;

                    // Stop the arc if it goes below the start height (hit the ground roughly)
                    if (i > 0 && pos.y < startPos.y - 1f)
                    {
                        pointCount = i;
                        break;
                    }

                    positions[i] = pos;
                }

                _line.positionCount = pointCount;
                _line.SetPositions(positions);
            }
        }
    }
}
