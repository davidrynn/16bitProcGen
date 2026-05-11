using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants that map ScreenEffectState 0–1 signals to URP post-process values.
    /// Set once at bootstrap; all scaling lives here so feedback systems stay unit-normalised.
    /// </summary>
    public struct ScreenEffectConfig : IComponentData
    {
        /// <summary>
        /// SpeedLineIntensity × this → LensDistortion.intensity.
        /// Negative = barrel (outward bulge), which reads as rushing speed. URP range: [-1, 1].
        /// </summary>
        public float LensDistortionScale;       // -0.4

        /// <summary>
        /// SpeedLineIntensity × this → ChromaticAberration.intensity.
        /// URP range: [0, 1].
        /// </summary>
        public float ChromaticAberrationScale;  // 0.6

        /// <summary>Lerp rate for all smoothed screen effects. Higher = snappier response.</summary>
        public float SmoothingRate;             // 8

        public static ScreenEffectConfig Default => new ScreenEffectConfig
        {
            LensDistortionScale     = -0.4f,
            ChromaticAberrationScale = 0.6f,
            SmoothingRate           = 8f,
        };
    }
}
