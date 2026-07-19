using System.Collections;
using DOTS.Core;
using DOTS.Player.Components;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// V14 meteor-interior loading shell (METEOR_ARRIVAL_SEQUENCE_SPEC.md Phases 1–2).
    /// Full-screen diegetic loading overlay: the player starts inside the meteor (dark rock
    /// vignette + glowing pulsing cracks + rumble jitter) and the shell breaks open when the
    /// V7 startup readiness gate actually releases — never on fake progress. Installed by
    /// <c>DotsSystemBootstrap.Awake</c> when sky-drop + shell are enabled, so the screen is
    /// covered before the first rendered frame.
    /// </summary>
    public static class MeteorShellOverlay
    {
        private const string RootName = "MeteorShellRoot";

        public static void Install()
        {
            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            var root = new GameObject(RootName);
            Object.DontDestroyOnLoad(root);
            root.AddComponent<MeteorShellController>();
            DebugSettings.LogPlayer("MeteorShellOverlay: installed (holding until readiness gate release).");
        }

        private sealed class MeteorShellController : MonoBehaviour
        {
            // The break-open signal is the gate component's removal (the same contract the
            // PlayMode smoke test polls) — no separate bridge component needed. Before the
            // player entity exists the shell stays closed; a player with no gate means released.
            private enum Phase : byte { WaitingForRelease, BreakingOpen, Done }

            // Well past the gate's own 8 s timeout fallback: if we never observe a release
            // (world torn down, player bootstrap disabled mid-session, ...) the shell force-opens
            // rather than trapping the player behind an opaque screen.
            private const float FailSafeSeconds = 20f;

            private const float FlareSeconds = 0.25f;
            private const float DissolveSeconds = 0.65f;
            private const int TextureSize = 512;

            private static readonly int OpenProgressId = Shader.PropertyToID("_OpenProgress");
            private static readonly int FlareId = Shader.PropertyToID("_Flare");

            private Phase _phase = Phase.WaitingForRelease;
            private float _installedTime;
            private Canvas _canvas;
            private RawImage _image;
            private RectTransform _imageRect;
            private Material _material;
            private Texture2D _texture;
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
                if (_texture != null) Destroy(_texture);
            }

            private void Update()
            {
                if (_phase != Phase.WaitingForRelease)
                {
                    return;
                }

                ApplyRumbleJitter();

                if (IsGateReleased())
                {
                    DebugSettings.LogPlayer("MeteorShellOverlay: readiness gate released — breaking open.");
                    StartCoroutine(BreakOpen());
                    return;
                }

                if (Time.unscaledTime - _installedTime >= FailSafeSeconds)
                {
                    DebugSettings.LogPlayerWarning(
                        "MeteorShellOverlay: no gate release observed before fail-safe timeout — force-opening the shell.");
                    StartCoroutine(BreakOpen());
                }
            }

            /// <summary>Released = a player entity exists and no longer carries the readiness gate.</summary>
            private bool IsGateReleased()
            {
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

                using var players = _playerQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                if (players.Length == 0)
                {
                    return false;
                }

                return !world.EntityManager.HasComponent<PlayerStartupReadinessGate>(players[0]);
            }

            private void DisposeQuery()
            {
                if (_queryWorld != null && _queryWorld.IsCreated)
                {
                    _playerQuery.Dispose();
                }
                _queryWorld = null;
            }

            private IEnumerator BreakOpen()
            {
                _phase = Phase.BreakingOpen;

                // Crack flare — the "cracks flare" beat before the shatter.
                for (float t = 0f; t < FlareSeconds; t += Time.unscaledDeltaTime)
                {
                    SetShader(FlareId, t / FlareSeconds);
                    yield return null;
                }
                SetShader(FlareId, 1f);

                // Dissolve outward along the cracks; the view opens onto the plain below while
                // the player is already falling — gravity released the same beat (spec §5.1).
                for (float t = 0f; t < DissolveSeconds; t += Time.unscaledDeltaTime)
                {
                    SetShader(OpenProgressId, t / DissolveSeconds);
                    yield return null;
                }

                _phase = Phase.Done;
                Destroy(gameObject);
            }

            private void SetShader(int id, float value)
            {
                if (_material != null)
                {
                    _material.SetFloat(id, value);
                }
            }

            /// <summary>Small unscaled-time positional jitter sells "inside something falling".</summary>
            private void ApplyRumbleJitter()
            {
                if (_imageRect == null)
                {
                    return;
                }

                float t = Time.unscaledTime;
                float x = (Mathf.PerlinNoise(t * 9f, 0.37f) - 0.5f) * 8f;
                float y = (Mathf.PerlinNoise(0.71f, t * 9f) - 0.5f) * 8f;
                _imageRect.anchoredPosition = new Vector2(x, y);
            }

            private void BuildUi()
            {
                var canvasGO = new GameObject("MeteorShellCanvas");
                canvasGO.transform.SetParent(transform, false);

                _canvas = canvasGO.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                // Above every other overlay (reticle sits at 500) — the shell is the whole screen.
                _canvas.sortingOrder = 4000;

                var scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;

                var imageGO = new GameObject("MeteorShellImage");
                imageGO.transform.SetParent(canvasGO.transform, false);

                _image = imageGO.AddComponent<RawImage>();
                _image.raycastTarget = false;

                _imageRect = _image.rectTransform;
                _imageRect.anchorMin = Vector2.zero;
                _imageRect.anchorMax = Vector2.one;
                _imageRect.offsetMin = Vector2.zero;
                _imageRect.offsetMax = Vector2.zero;
                // Slight overscan so the rumble jitter never exposes screen edges.
                _imageRect.localScale = new Vector3(1.05f, 1.05f, 1f);

                var shader = Resources.Load<Shader>("Shaders/MeteorShellOverlay");
                if (shader != null)
                {
                    _texture = GenerateShellTexture();
                    _material = new Material(shader);
                    _image.texture = _texture;
                    _image.material = _material;
                }
                else
                {
                    // Plain opaque cover — worse-looking but still an honest loading screen.
                    DebugSettings.LogPlayerWarning(
                        "MeteorShellOverlay: Shaders/MeteorShellOverlay not found in Resources — using flat black fallback.");
                    _image.color = new Color(0.05f, 0.04f, 0.035f, 1f);
                }
            }

            /// <summary>
            /// Packs the shell's static fields into one texture the shader animates:
            /// R = rock luminance FBM, G = crack mask (Voronoi edge lines), B = radial distance
            /// from screen center (drives both the interior vignette and the dissolve order).
            /// Deterministic — no per-run variation to eyeball-tune against.
            /// </summary>
            private static Texture2D GenerateShellTexture()
            {
                const int size = TextureSize;
                const int cells = 6; // Voronoi grid resolution — ~coarse plates with long cracks

                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                // Jittered-grid Voronoi feature points (fixed seed).
                var rng = new System.Random(0x5EED);
                var points = new Vector2[cells * cells];
                for (int cy = 0; cy < cells; cy++)
                {
                    for (int cx = 0; cx < cells; cx++)
                    {
                        points[cy * cells + cx] = new Vector2(
                            (cx + (float)rng.NextDouble()) / cells,
                            (cy + (float)rng.NextDouble()) / cells);
                    }
                }

                var pixels = new Color32[size * size];
                var center = new Vector2(0.5f, 0.5f);

                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size;
                        var p = new Vector2(u, v);

                        // F2 - F1 over the 3×3 cell neighbourhood → small near plate borders.
                        int pcx = Mathf.Clamp((int)(u * cells), 0, cells - 1);
                        int pcy = Mathf.Clamp((int)(v * cells), 0, cells - 1);
                        float f1 = float.MaxValue, f2 = float.MaxValue;
                        for (int oy = -1; oy <= 1; oy++)
                        {
                            for (int ox = -1; ox <= 1; ox++)
                            {
                                int nx = pcx + ox, ny = pcy + oy;
                                if (nx < 0 || ny < 0 || nx >= cells || ny >= cells) continue;
                                float d = (points[ny * cells + nx] - p).sqrMagnitude;
                                if (d < f1) { f2 = f1; f1 = d; }
                                else if (d < f2) { f2 = d; }
                            }
                        }
                        float edge = Mathf.Sqrt(f2) - Mathf.Sqrt(f1);
                        float crack = 1f - Mathf.Clamp01(edge / 0.045f);
                        crack = crack * crack; // sharpen the line profile

                        // 4-octave rock FBM.
                        float rock = 0f, amp = 0.5f, freq = 5f;
                        for (int o = 0; o < 4; o++)
                        {
                            rock += amp * Mathf.PerlinNoise(u * freq + 13.7f, v * freq + 41.3f);
                            amp *= 0.5f;
                            freq *= 2.1f;
                        }

                        float radial = Mathf.Clamp01(Vector2.Distance(p, center) / 0.7071f);

                        pixels[y * size + x] = new Color32(
                            (byte)(Mathf.Clamp01(rock) * 255f),
                            (byte)(crack * 255f),
                            (byte)(radial * 255f),
                            255);
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return texture;
            }
        }
    }
}
