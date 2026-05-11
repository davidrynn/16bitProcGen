using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Reads ScreenEffectState (written each frame by movement feedback systems) and drives
    /// a URP global post-process Volume: LensDistortion, ChromaticAberration, and Vignette.
    ///
    /// SpeedLineIntensity is already available on ScreenEffectState for a future
    /// ScriptableRendererFeature to render actual screen-space speed streaks without changing
    /// any ECS code.
    /// </summary>
    /// <remarks>
    /// Uses SystemBase (class) rather than ISystem (struct) because it must hold managed
    /// references to the Volume and VolumeComponent overrides across frames.
    /// </remarks>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial class ScreenEffectResolverSystem : SystemBase
    {
        private float _smoothedSpeedLine;
        private float _smoothedVignette;

        private Volume _volume;
        private LensDistortion _lensDistortion;
        private ChromaticAberration _chromaticAberration;
        private Vignette _vignette;

        protected override void OnCreate()
        {
            RequireForUpdate<ScreenEffectState>();
            RequireForUpdate<ScreenEffectConfig>();
            InitializeVolume();
        }

        protected override void OnUpdate()
        {
            if (_volume == null) return;
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            foreach (var (screenState, config) in
                     SystemAPI.Query<RefRO<ScreenEffectState>, RefRO<ScreenEffectConfig>>())
            {
                float smooth = math.saturate(config.ValueRO.SmoothingRate * dt);

                _smoothedSpeedLine = math.lerp(_smoothedSpeedLine, screenState.ValueRO.SpeedLineIntensity, smooth);
                _smoothedVignette  = math.lerp(_smoothedVignette,  screenState.ValueRO.VignetteIntensity,  smooth);

                _lensDistortion.intensity.value = math.clamp(
                    (_smoothedSpeedLine + screenState.ValueRO.LensDistortionAdd) * config.ValueRO.LensDistortionScale,
                    -1f, 1f);

                _chromaticAberration.intensity.value = math.saturate(
                    _smoothedSpeedLine * config.ValueRO.ChromaticAberrationScale);

                _vignette.intensity.value = math.saturate(_smoothedVignette);
            }
        }

        protected override void OnDestroy()
        {
            if (_volume != null)
                Object.Destroy(_volume.gameObject);
        }

        private void InitializeVolume()
        {
            var volumeGO = new GameObject("Screen Effect Volume (ECS)");

            _volume          = volumeGO.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;

            var profile    = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = profile;

            _lensDistortion = profile.Add<LensDistortion>();
            _lensDistortion.active = true;
            _lensDistortion.intensity.overrideState = true;
            _lensDistortion.intensity.value         = 0f;

            _chromaticAberration = profile.Add<ChromaticAberration>();
            _chromaticAberration.active = true;
            _chromaticAberration.intensity.overrideState = true;
            _chromaticAberration.intensity.value         = 0f;

            _vignette = profile.Add<Vignette>();
            _vignette.active = true;
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value         = 0f;
        }
    }
}
