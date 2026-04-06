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

        private void Start()
        {
            if (skyController == null)
                skyController = GetComponent<SkyController>();

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
            if (cycleDurationSeconds > 0f)
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

            if (_transitionProgress < 1f && _transitionFrom != null && _transitionTo != null)
            {
                var fromSettings = _transitionFrom.Evaluate(normalizedTime);
                var toSettings = _transitionTo.Evaluate(normalizedTime);
                evaluated = SkySettings.Lerp(fromSettings, toSettings, _transitionProgress);
                evaluatedClouds = CloudSettings.Lerp(_transitionFromClouds, _transitionToClouds, _transitionProgress);
            }
            else
            {
                evaluated = activePreset.Evaluate(normalizedTime);
                evaluatedClouds = _activeCloudSettings;
            }

            skyController.ApplySettings(evaluated);
            skyController.ApplyCloudSettings(evaluatedClouds);

            if (rotateSun && directionalLight != null)
            {
                UpdateSunRotation();
            }
        }

        private void UpdateSunRotation()
        {
            // Sun arc: rises at dawn (0.0), peaks at noon (0.25), sets at dusk (0.5), below horizon at night
            // Map normalizedTime to sun angle: 0→sunrise, 0.25→peak, 0.5→sunset
            float sunPhase = normalizedTime * 360f;
            float elevation = Mathf.Sin(normalizedTime * Mathf.PI * 2f) * sunMaxElevation;

            directionalLight.transform.rotation = Quaternion.Euler(elevation, sunPhase, 0f);

            // Dim the light at night
            float intensity = Mathf.Clamp01(Mathf.Sin(normalizedTime * Mathf.PI * 2f) + 0.1f);
            directionalLight.intensity = Mathf.Max(intensity, 0.05f);
        }
    }
}
