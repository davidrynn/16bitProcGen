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
        // Fall math from the 400u spawn: the band now OPENS at the spawn altitude, so the burn-off
        // begins the moment the gate releases rather than holding full strength for the first 60u
        // (the live band comes from ProjectFeatureConfig, spec §5.2 leans altitude).
        public const float IgniteRampSeconds = 0.35f;
        // Default burn-off band (world Y). Raised twice: 230→120 originally, then 340→240 to
        // extinguish sooner/higher (2026-07-19), and now the top pinned to the 400u spawn height so
        // the fade starts immediately on ignition instead of after a full-strength stretch (owner
        // call 2026-07-21). That pin is enforced, not just documented — Install() runs the authored
        // value through ResolveFadeStartY against the live spawn height. The live values come from
        // ProjectFeatureConfig via Install(); these consts are the fallback + test baseline.
        public const float FadeStartY = 400f;
        public const float FadeEndY = 240f;

        /// <summary>
        /// Pure intensity envelope: fast ramp-in at ignition × altitude-band fade-out, evaluated
        /// against an explicit burn-off band. Altitude-driven (not elapsed-time) per the spec's lean —
        /// it tracks what the player actually sees during the fall regardless of load hitches.
        /// </summary>
        public static float EvaluateIntensity(float playerY, float secondsSinceIgnite, float fadeStartY, float fadeEndY)
        {
            float ramp = Mathf.Clamp01(secondsSinceIgnite / IgniteRampSeconds);
            // Guard a degenerate band (start <= end) so a mis-set config can't divide by zero.
            float span = Mathf.Max(fadeStartY - fadeEndY, 1e-3f);
            float altitude = Mathf.Clamp01((playerY - fadeEndY) / span);
            // smoothstep the altitude band so the burn-off eases rather than snapping linearly
            altitude = altitude * altitude * (3f - 2f * altitude);
            return ramp * altitude;
        }

        /// <summary>Convenience overload using the default band — the envelope tests call this.</summary>
        public static float EvaluateIntensity(float playerY, float secondsSinceIgnite)
            => EvaluateIntensity(playerY, secondsSinceIgnite, FadeStartY, FadeEndY);

        /// <summary>
        /// Resolves the effective top of the burn-off band. The fade has to begin the moment the
        /// gate releases, which means the band must open at or above the spawn altitude — an
        /// authored value below it silently reintroduces a full-strength plateau for the first
        /// (spawn − authored) units of descent, which is exactly the artefact the 340→400 change
        /// removed. The two config fields are independent, so this rule lives here rather than
        /// relying on them being kept equal by hand. Pure + public so the EditMode tests pin it.
        /// </summary>
        public static float ResolveFadeStartY(float authoredFadeStartY, float spawnHeight)
            => Mathf.Max(authoredFadeStartY, spawnHeight);

        public static void Install(float fadeStartY, float fadeEndY, float spawnHeight)
        {
            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            float resolvedFadeStartY = ResolveFadeStartY(fadeStartY, spawnHeight);

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
            var controller = root.AddComponent<MeteorDescentVfxController>();
            // Seed the burn-off band before the first Update (Awake only builds the UI, not the
            // envelope).
            controller.Configure(resolvedFadeStartY, fadeEndY);
            // Log the RESOLVED band: when the authored value gets clamped up, that's the number
            // that explains what the descent actually looks like.
            DebugSettings.LogPlayer(
                $"MeteorDescentVfx: installed (band {resolvedFadeStartY:0}→{fadeEndY:0}, "
                + "waiting for break-open to ignite).", forceLog: true);
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
            // Burn-off band, seeded by Install() from ProjectFeatureConfig; defaults kept so a
            // controller added without Install() still behaves.
            private float _fadeStartY = FadeStartY;
            private float _fadeEndY = FadeEndY;

            internal void Configure(float fadeStartY, float fadeEndY)
            {
                _fadeStartY = fadeStartY;
                _fadeEndY = fadeEndY;
            }
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
                            // Start drawing only now (see BuildUi): everything before this point
                            // would have been a full-screen pass resolving to zero.
                            if (_image != null) _image.enabled = true;
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
                    intensity = EvaluateIntensity(playerY, sinceIgnite, _fadeStartY, _fadeEndY);

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
                // Stay dark until ignition. The gate holds for 1.75-8s while the world streams in,
                // and this is a full-screen procedural pass (flame FBM + two smoke FBMs + embers)
                // that would resolve to nothing at _Intensity 0 — pure waste at the exact moment
                // the frame budget is tightest. Re-enabled on the break-open signal.
                _image.enabled = false;

                var rect = _image.rectTransform;
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                // Load the tunable material asset (the owner tweaks _EmberDensity/_EmberSize/
                // _EmberSpeed/_EmberJitter/flame colors on it in the inspector) and instantiate a
                // COPY so our per-frame _Intensity writes never dirty the shared asset. Fall back to
                // the bare shader if the asset is missing so the effect still renders.
                var mat = Resources.Load<Material>("Materials/MeteorDescentFlames");
                if (mat != null)
                {
                    _material = new Material(mat);
                }
                else
                {
                    var shader = Resources.Load<Shader>("Shaders/MeteorDescentFlames");
                    if (shader != null) _material = new Material(shader);
                }

                if (_material != null)
                {
                    _material.SetFloat(IntensityId, 0f);
                    _image.material = _material;
                }
                else
                {
                    // No fallback visual — flames are pure garnish; fail invisible, not ugly.
                    DebugSettings.LogPlayerWarning(
                        "MeteorDescentVfx: material/shader not found in Resources — descent VFX disabled.",
                        forceLog: true);
                    _image.color = Color.clear;
                }
            }
        }
    }
}
