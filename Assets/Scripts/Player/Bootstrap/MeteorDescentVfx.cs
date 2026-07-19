using DOTS.Core;
using DOTS.Player.Components;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// V13 burning-descent VFX (METEOR_ARRIVAL_SEQUENCE_SPEC.md Phase 3).
    /// First-person flame tongues at the screen edges + embers streaming past, rendered as one
    /// procedural screen-space layer (the V14 shell architecture — no particle assets). Ignites
    /// on the readiness gate's release (the V14 break-open signal), burns at full strength
    /// through the upper descent, and fades out by altitude band so it is fully extinguished
    /// before the C3 landing handoff. One-shot by construction: installed only by the arrival
    /// bootstrap and self-destroys after extinguishing, so ordinary later falls never show
    /// meteor effects (spec §9.6).
    /// </summary>
    public static class MeteorDescentVfx
    {
        private const string RootName = "MeteorDescentVfxRoot";

        // Intensity envelope (public for the EditMode timing tests).
        // Fall math from the 400u spawn: the fade band 230→120u burns flames for ~6s of the
        // descent and extinguishes ~1.5s before impact — "fully out below ~150u" per spec §5.2.
        public const float IgniteRampSeconds = 0.35f;
        public const float FadeStartY = 230f;
        public const float FadeEndY = 120f;

        /// <summary>
        /// Pure intensity envelope: fast ramp-in at ignition × altitude-band fade-out.
        /// Altitude-driven (not elapsed-time) per the spec's lean — it tracks what the player
        /// actually sees during the fall regardless of load hitches.
        /// </summary>
        public static float EvaluateIntensity(float playerY, float secondsSinceIgnite)
        {
            float ramp = Mathf.Clamp01(secondsSinceIgnite / IgniteRampSeconds);
            float altitude = Mathf.Clamp01((playerY - FadeEndY) / (FadeStartY - FadeEndY));
            // smoothstep the altitude band so the burn-off eases rather than snapping linearly
            altitude = altitude * altitude * (3f - 2f * altitude);
            return ramp * altitude;
        }

        public static void Install()
        {
            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
            root.AddComponent<MeteorDescentVfxController>();
            DebugSettings.LogPlayer("MeteorDescentVfx: installed (waiting for break-open to ignite).", forceLog: true);
        }

        private sealed class MeteorDescentVfxController : MonoBehaviour
        {
            private enum Phase : byte { WaitingForIgnition, Burning, Done }

            // The gate's 8s timeout guarantees a release; if we never observe one (world torn
            // down, bootstrap disabled) just leave quietly — unlike the shell there is nothing
            // opaque to force open.
            private const float FailSafeSeconds = 30f;

            private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

            private Phase _phase = Phase.WaitingForIgnition;
            private float _installedTime;
            private float _igniteTime;
            private RawImage _image;
            private Material _material;
            private World _queryWorld;
            private EntityQuery _playerQuery;

            private void Awake()
            {
                _installedTime = Time.unscaledTime;
                BuildUi();
            }

            private void OnDestroy()
            {
                DisposeQuery();
                if (_material != null) Destroy(_material);
            }

            private void Update()
            {
                switch (_phase)
                {
                    case Phase.WaitingForIgnition:
                        if (TryGetPlayer(out _, out bool gateReleased) && gateReleased)
                        {
                            _igniteTime = Time.unscaledTime;
                            _phase = Phase.Burning;
                            DebugSettings.LogPlayer(
                                $"MeteorDescentVfx: ignited at t={Time.timeSinceLevelLoad:0.00}s.", forceLog: true);
                        }
                        else if (Time.unscaledTime - _installedTime >= FailSafeSeconds)
                        {
                            _phase = Phase.Done;
                            Destroy(gameObject);
                        }
                        break;

                    case Phase.Burning:
                        UpdateBurn();
                        break;
                }
            }

            private void UpdateBurn()
            {
                float sinceIgnite = Time.unscaledTime - _igniteTime;
                float intensity = 0f;

                if (TryGetPlayer(out float playerY, out _))
                {
                    intensity = EvaluateIntensity(playerY, sinceIgnite);

                    // Grounded = burn is over regardless of terrain height (a timeout release
                    // on a ground-level spawn must not leave the player standing in flames).
                    var em = _queryWorld.EntityManager;
                    using var players = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                    if (players.Length > 0
                        && em.HasComponent<PlayerMovementState>(players[0])
                        && em.GetComponentData<PlayerMovementState>(players[0]).IsGrounded)
                    {
                        intensity = 0f;
                    }
                }

                if (_material != null)
                {
                    _material.SetFloat(IntensityId, intensity);
                }

                // Fully burned off (and past the ramp so we don't self-destroy on frame one).
                if (intensity <= 0f && sinceIgnite > IgniteRampSeconds)
                {
                    DebugSettings.LogPlayer(
                        $"MeteorDescentVfx: extinguished at t={Time.timeSinceLevelLoad:0.00}s.", forceLog: true);
                    _phase = Phase.Done;
                    Destroy(gameObject);
                }
            }

            private bool TryGetPlayer(out float playerY, out bool gateReleased)
            {
                playerY = 0f;
                gateReleased = false;

                var world = World.DefaultGameObjectInjectionWorld;
                if (world == null || !world.IsCreated)
                {
                    DisposeQuery();
                    return false;
                }

                if (_queryWorld != world)
                {
                    DisposeQuery();
                    _playerQuery = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
                    _queryWorld = world;
                }

                var em = world.EntityManager;
                using var players = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (players.Length == 0)
                {
                    return false;
                }

                var player = players[0];
                if (em.HasComponent<LocalTransform>(player))
                {
                    playerY = em.GetComponentData<LocalTransform>(player).Position.y;
                }
                gateReleased = !em.HasComponent<PlayerStartupReadinessGate>(player);
                return true;
            }

            private void DisposeQuery()
            {
                if (_queryWorld != null && _queryWorld.IsCreated)
                {
                    _playerQuery.Dispose();
                }
                _queryWorld = null;
            }

            private void BuildUi()
            {
                var canvasGO = new GameObject("MeteorDescentVfxCanvas");
                canvasGO.transform.SetParent(transform, false);

                var canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Below the V14 shell (4000): the flames ignite behind the still-dissolving
                // plates, so the break-open reveals the burn already going — one beat (§9.4).
                canvas.sortingOrder = 3500;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var imageGO = new GameObject("MeteorDescentFlames");
                imageGO.transform.SetParent(canvasGO.transform, false);

                _image = imageGO.AddComponent<RawImage>();
                _image.raycastTarget = false;

                var rect = _image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var shader = Resources.Load<Shader>("Shaders/MeteorDescentFlames");
                if (shader != null)
                {
                    _material = new Material(shader);
                    _material.SetFloat(IntensityId, 0f);
                    _image.material = _material;
                }
                else
                {
                    // No fallback visual — flames are pure garnish; fail invisible, not ugly.
                    DebugSettings.LogPlayerWarning(
                        "MeteorDescentVfx: Shaders/MeteorDescentFlames not found in Resources — descent VFX disabled.",
                        forceLog: true);
                    _image.color = Color.clear;
                }
            }
        }
    }
}
