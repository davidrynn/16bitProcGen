using UnityEngine;

namespace DOTS.Rendering.Sky
{
    public class SkyController : MonoBehaviour
    {
        private const string CloudsKeyword = "_CLOUDS_ON";
        [SerializeField] private bool cloudsEnabled = true;

        [Header("Fog")]
        [Tooltip("Drive RenderSettings.fogColor from the current horizon color each update so distance " +
                 "haze always matches the sky across the day/night cycle and biome. Enable/mode/density " +
                 "remain owned by ProjectFeatureConfig / DotsSystemBootstrap.")]
        [SerializeField] private bool _driveFogColor = true;

        [Header("References")]
        [SerializeField] private Material skyMaterial;

        private SkySettings _currentSettings = SkySettings.Default;
        private CloudSettings _currentCloudSettings = CloudSettings.Default;
        private Material _runtimeMaterial;

        private void Start()
        {
            EnsureMaterial();
            PushSkyUniforms();
            PushCloudUniforms();
            SyncCloudKeyword();
            ApplyToCamera();
        }

        private void OnValidate()
        {
            if (_runtimeMaterial == null)
                return;

            PushSkyUniforms();
            PushCloudUniforms();
            SyncCloudKeyword();
        }

        private void OnDestroy()
        {
            if (_runtimeMaterial != null && _runtimeMaterial != skyMaterial)
                Destroy(_runtimeMaterial);
        }

        private void EnsureMaterial()
        {
            if (skyMaterial != null)
            {
                _runtimeMaterial = new Material(skyMaterial);
            }
            else
            {
                var shader = Shader.Find("ProceduralGradientSky");
                if (shader == null)
                {
                    Debug.LogError("[SkyController] ProceduralGradientSky shader not found.");
                    return;
                }
                _runtimeMaterial = new Material(shader);
                _runtimeMaterial.name = "ProceduralGradientSky (Runtime)";
            }
        }

        private void PushSkyUniforms()
        {
            if (_runtimeMaterial == null)
                return;

            var clamped = _currentSettings.Clamped();

            _runtimeMaterial.SetColor(ShaderIDs.HorizonColor, clamped.horizonColor);
            _runtimeMaterial.SetColor(ShaderIDs.ZenithColor, clamped.zenithColor);
            _runtimeMaterial.SetFloat(ShaderIDs.GradientExponent, clamped.gradientExponent);
            _runtimeMaterial.SetFloat(ShaderIDs.HorizonHeight, clamped.horizonHeight);

            // Aerial perspective: keep distance fog matched to the current horizon so the ground
            // plane, scatter, and ground-plane impostor all dissolve into the same band the sky
            // fades to (Highlands vista goal). Color only — fog enable/mode/density stay owned by
            // ProjectFeatureConfig / DotsSystemBootstrap.ApplyDistanceFog.
            if (_driveFogColor)
                RenderSettings.fogColor = clamped.horizonColor;
        }

        private void PushCloudUniforms()
        {
            if (_runtimeMaterial == null)
                return;

            _runtimeMaterial.SetColor(ShaderIDs.CloudColor, _currentCloudSettings.cloudColor);
            _runtimeMaterial.SetColor(ShaderIDs.CloudShadowColor, _currentCloudSettings.cloudShadowColor);
            _runtimeMaterial.SetVector(ShaderIDs.ScrollSpeed, new Vector4(_currentCloudSettings.scrollSpeed.x, _currentCloudSettings.scrollSpeed.y, 0, 0));
            _runtimeMaterial.SetFloat(ShaderIDs.NoiseScale, _currentCloudSettings.noiseScale);
            _runtimeMaterial.SetFloat(ShaderIDs.CoverageThreshold, _currentCloudSettings.coverageThreshold);
            _runtimeMaterial.SetFloat(ShaderIDs.EdgeSoftness, _currentCloudSettings.edgeSoftness);
            _runtimeMaterial.SetFloat(ShaderIDs.Opacity, _currentCloudSettings.opacity);
        }

        private void SyncCloudKeyword()
        {
            if (_runtimeMaterial == null)
                return;

            if (cloudsEnabled)
                _runtimeMaterial.EnableKeyword(CloudsKeyword);
            else
                _runtimeMaterial.DisableKeyword(CloudsKeyword);
        }

        private void ApplyToCamera()
        {
            if (_runtimeMaterial == null)
                return;

            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[SkyController] No main camera found. Sky material will be applied to RenderSettings.skybox instead.");
            }

            RenderSettings.skybox = _runtimeMaterial;

            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.Skybox;
            }
        }

        internal void ApplySettings(SkySettings newSettings)
        {
            _currentSettings = newSettings;
            PushSkyUniforms();
        }

        internal void ApplyCloudSettings(CloudSettings newCloudSettings)
        {
            _currentCloudSettings = newCloudSettings;
            PushCloudUniforms();
        }

        internal void SetCloudsEnabled(bool enabled)
        {
            cloudsEnabled = enabled;
            SyncCloudKeyword();
        }
    }
}
