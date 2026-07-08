using UnityEngine;

namespace DOTS.Rendering.Sky
{
    public class TimeOfDayController : MonoBehaviour
    {
        [Header("Time")]
        [Tooltip("Current normalized time of day. 0=dawn, 0.25=noon, 0.5=dusk, 0.75=night")]
        [Range(0f, 1f)]
        [SerializeField] private float normalizedTime;

        [Tooltip("Full day-night cycle duration in seconds. 0 = paused.")]
        [SerializeField] private float cycleDurationSeconds = 600f;

        [Header("Dev Pin")]
        [Tooltip("Hold the cycle at Pinned Normalized Time while enabled (overrides the running cycle). " +
                 "Dev determinism pin for debugging and screenshot validation — see the dev-pin convention in CLAUDE.md.")]
        [SerializeField] private bool pinTimeOfDay;

        [Tooltip("Raw cycle time to hold while pinned. 0.08 = midday under the default remap " +
                 "(raw 0 = dawn, 0.08 = noon, 0.58 = dusk) and matches the scene's default start time.")]
        [Range(0f, 1f)]
        [SerializeField] private float pinnedNormalizedTime = 0.08f;

        [Tooltip("Dev pin: broadcast zero atmosphere haze (_AtmoHazeDensity/_AtmoDistanceHaze) so the " +
                 "V9-converted surfaces (ground disc, skybox skirt, hero relic) render clean for visual " +
                 "debugging — the skirt's material-side _MountainHazeFloor is gated on the broadcast " +
                 "density, so it drops with the pin too. Live — takes effect next frame in Play Mode. " +
                 "Note: ProjectFeatureConfig's EnableDistanceFog only governs the built-in RenderSettings " +
                 "fog on unconverted terrain/scatter and is applied once at bootstrap — toggling it " +
                 "requires restarting Play Mode.")]
        [SerializeField] private bool disableAtmosphereHaze;

        [Tooltip("Maps raw cycle time (X 0-1) to apparent sky time (Y 0-1). " +
                 "Steep slope = fast transition (dawn/dusk). Shallow slope = long phase (day/night). " +
                 "Leave empty to use the built-in default (dawn/dusk ~8%, day/night ~42% of cycle).")]
        [SerializeField] private AnimationCurve _timeRemapCurve;

        [Header("References")]
        [SerializeField] private SkyController skyController;
        [SerializeField] private SkyPreset activePreset;
        [SerializeField] private BiomeSkyMapping biomeSkyMapping;
        [SerializeField] private Light directionalLight;

        [Header("Clouds")]
        [SerializeField] private CloudSettings defaultCloudSettings = CloudSettings.Default;
        [SerializeField] private bool cloudsEnabled = true;

        [Header("Biome Transitions")]
        [SerializeField] private float biomeTransitionDuration = 2f;

        [Header("Sun")]
        [Tooltip("Rotate the directional light to match time of day.")]
        [SerializeField] private bool rotateSun = true;

        [Tooltip("Sun elevation at noon (degrees above horizon).")]
        [SerializeField] private float sunMaxElevation = 70f;

        private SkyPreset _transitionFrom;
        private SkyPreset _transitionTo;
        private CloudSettings _activeCloudSettings = CloudSettings.Default;
        private CloudSettings _transitionFromClouds;
        private CloudSettings _transitionToClouds;
        private float _transitionProgress = 1f;
        private float _transitionDuration;

        public float NormalizedTime
        {
            get => normalizedTime;
            set => normalizedTime = Mathf.Repeat(value, 1f);
        }

        public SkyPreset ActivePreset
        {
            get => activePreset;
            set => activePreset = value;
        }

        /// <summary>
        /// Dev determinism pin: while true, the cycle holds at <see cref="PinnedNormalizedTime"/>
        /// instead of advancing. Exposed for runtime toggling from debug tooling.
        /// </summary>
        public bool PinTimeOfDay
        {
            get => pinTimeOfDay;
            set => pinTimeOfDay = value;
        }

        /// <summary>Raw (pre-remap) cycle time held while <see cref="PinTimeOfDay"/> is enabled.</summary>
        public float PinnedNormalizedTime
        {
            get => pinnedNormalizedTime;
            set => pinnedNormalizedTime = Mathf.Repeat(value, 1f);
        }

        /// <summary>
        /// Dev pin: while true, the authority broadcasts zero haze so all V9-converted surfaces
        /// render clean. Exposed for runtime toggling from debug tooling.
        /// </summary>
        public bool DisableAtmosphereHaze
        {
            get => disableAtmosphereHaze;
            set => disableAtmosphereHaze = value;
        }

        private void Start()
        {
            if (skyController == null)
                skyController = GetComponent<SkyController>();

            if (_timeRemapCurve == null || _timeRemapCurve.length == 0)
                _timeRemapCurve = BuildDefaultTimeRemap();

            _activeCloudSettings = defaultCloudSettings;

            if (directionalLight == null)
            {
                var lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude);
                foreach (var light in lights)
                {
                    if (light.type == LightType.Directional)
                    {
                        directionalLight = light;
                        break;
                    }
                }
            }

            skyController?.SetCloudsEnabled(cloudsEnabled);

            if (activePreset == null && biomeSkyMapping != null)
            {
                activePreset = biomeSkyMapping.FallbackPreset;
            }

            ApplyTime();
        }

        private void Update()
        {
            // The pin wins over the running cycle: with a 240s scene day, lighting drifts from
            // midday to night inside a single debugging/screenshot session, which invalidates any
            // visual comparison. Pinning holds one deterministic lighting state for the whole run.
            if (pinTimeOfDay)
            {
                normalizedTime = Mathf.Repeat(pinnedNormalizedTime, 1f);
            }
            else if (cycleDurationSeconds > 0f)
            {
                normalizedTime += Time.deltaTime / cycleDurationSeconds;
                normalizedTime = Mathf.Repeat(normalizedTime, 1f);
            }

            if (_transitionProgress < 1f)
            {
                _transitionProgress += Time.deltaTime / _transitionDuration;
                _transitionProgress = Mathf.Clamp01(_transitionProgress);

                if (_transitionProgress >= 1f)
                {
                    activePreset = _transitionTo;
                    _transitionFrom = null;
                    _transitionTo = null;
                }
            }

            ApplyTime();
        }

        /// <summary>
        /// Smoothly transition to a new SkyPreset over the given duration.
        /// Used when the player moves into a new biome.
        /// </summary>
        public void TransitionToPreset(SkyPreset newPreset, float durationSeconds)
        {
            if (newPreset == activePreset)
                return;

            _transitionFrom = activePreset;
            _transitionTo = newPreset;
            _transitionDuration = Mathf.Max(durationSeconds, 0.01f);
            _transitionProgress = 0f;
        }

        /// <summary>
        /// Applies biome sky data using the current mapping.
        /// Intended integration seam for DOTS biome events/signals.
        /// </summary>
        public void ApplyBiome(BiomeType biome, float durationSeconds = -1f)
        {
            if (biomeSkyMapping == null)
                return;

            biomeSkyMapping.TryGetPreset(biome, out var preset, out var cloudOverride);
            if (preset == null)
                return;

            var targetCloudSettings = cloudOverride ?? defaultCloudSettings;
            var resolvedDuration = durationSeconds >= 0f ? durationSeconds : biomeTransitionDuration;

            if (activePreset == null || resolvedDuration <= 0f)
            {
                activePreset = preset;
                _transitionFrom = null;
                _transitionTo = null;
                _transitionProgress = 1f;
                _activeCloudSettings = targetCloudSettings;
                ApplyTime();
                return;
            }

            _transitionFrom = activePreset;
            _transitionTo = preset;
            _transitionFromClouds = _activeCloudSettings;
            _transitionToClouds = targetCloudSettings;
            _transitionDuration = Mathf.Max(resolvedDuration, 0.01f);
            _transitionProgress = 0f;
        }

        private void ApplyTime()
        {
            if (skyController == null)
                return;

            if (activePreset == null && biomeSkyMapping != null)
            {
                activePreset = biomeSkyMapping.FallbackPreset;
            }

            if (activePreset == null)
                return;

            SkySettings evaluated;
            CloudSettings evaluatedClouds;

            float remappedTime = RemapTime(normalizedTime);

            if (_transitionProgress < 1f && _transitionFrom != null && _transitionTo != null)
            {
                var fromSettings = _transitionFrom.Evaluate(remappedTime);
                var toSettings = _transitionTo.Evaluate(remappedTime);
                evaluated = SkySettings.Lerp(fromSettings, toSettings, _transitionProgress);
                evaluatedClouds = CloudSettings.Lerp(_transitionFromClouds, _transitionToClouds, _transitionProgress);
            }
            else
            {
                evaluated = activePreset.Evaluate(remappedTime);
                evaluatedClouds = _activeCloudSettings;
            }

            skyController.ApplySettings(evaluated);
            skyController.ApplyCloudSettings(evaluatedClouds);

            // Atmosphere authority broadcast (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.1): this
            // controller already evaluates the palette per frame, so it owns pushing the _Atmo*
            // globals — no rival singleton.
            PushAtmosphere(evaluated);

            if (rotateSun && directionalLight != null)
            {
                UpdateSunRotation();
            }
        }

        private void UpdateSunRotation()
        {
            // Sun arc: rises at dawn (0.0), peaks at noon (0.25), sets at dusk (0.5), below horizon at night.
            // Remapped time keeps sun position in sync with the sky preset evaluation.
            float t = RemapTime(normalizedTime);
            float sunPhase = t * 360f;
            float elevation = Mathf.Sin(t * Mathf.PI * 2f) * sunMaxElevation;

            directionalLight.transform.rotation = Quaternion.Euler(elevation, sunPhase, 0f);

            float intensity = Mathf.Clamp01(Mathf.Sin(t * Mathf.PI * 2f) + 0.1f);
            directionalLight.intensity = Mathf.Max(intensity, 0.05f);
        }

        /// <summary>
        /// Resolves the per-preset atmosphere block (transition-blended with the same progress as
        /// the sky settings so consumers never see a palette snap) and broadcasts the _Atmo* globals.
        /// </summary>
        private void PushAtmosphere(SkySettings evaluated)
        {
            var atmosphere = (_transitionProgress < 1f && _transitionFrom != null && _transitionTo != null)
                ? AtmosphereSettings.Lerp(_transitionFrom.atmosphere, _transitionTo.atmosphere, _transitionProgress)
                : activePreset.atmosphere;

            // Dev pin: zero the haze terms on the local copy (struct) — the preset asset is untouched.
            if (disableAtmosphereHaze)
            {
                atmosphere.hazeDensity = 0f;
                atmosphere.distanceHaze = 0f;
            }

            // farFade = world reference distance, NOT Camera.main.farClipPlane — R6 raises the
            // far plane past the world's visual edge (landmark permanence), and the disc→skirt
            // handoff / distanceHaze ramp must stay pinned at the world edge regardless.
            AtmosphereBroadcast.Push(
                evaluated,
                atmosphere,
                AtmosphereBroadcast.WorldReferenceDistance);
        }

        // The controller only updates in Play Mode, so without this an inspector toggle of the
        // haze pin (or a preset swap) would not change the Scene/Game view until play — the
        // globals would keep whatever was last broadcast. Re-broadcast on inspector edits so
        // dev pins are judgeable in edit mode. Broadcast only — sun rotation and sky-material
        // writes stay Play Mode concerns.
        private void OnValidate()
        {
            if (activePreset == null)
                return;

            PushAtmosphere(activePreset.Evaluate(RemapTime(normalizedTime)));
        }

        private float RemapTime(float t) =>
            _timeRemapCurve != null && _timeRemapCurve.length > 0
                ? _timeRemapCurve.Evaluate(t)
                : t;

        /// <summary>
        /// Default remap: dawn and dusk each occupy ~8% of real cycle time;
        /// day and night each occupy ~42%. Smooth tangents for natural transitions.
        /// </summary>
        private static AnimationCurve BuildDefaultTimeRemap()
        {
            var keys = new[]
            {
                new Keyframe(0.00f, 0.00f),  // dawn start
                new Keyframe(0.08f, 0.25f),  // dawn end / day start  (steep — 8% real time)
                new Keyframe(0.50f, 0.50f),  // noon                  (shallow — 42% real time)
                new Keyframe(0.58f, 0.75f),  // dusk end / night start (steep — 8% real time)
                new Keyframe(1.00f, 1.00f),  // night end              (shallow — 42% real time)
            };
            var curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++)
                curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
