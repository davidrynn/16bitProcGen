using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Per-frame blackboard for screen-space visual effects. Reset to zero by
    /// MovementStateBookkeepingSystem each frame; feedback systems write additively.
    /// ScreenEffectResolverSystem reads and applies to the URP post-process Volume.
    /// </summary>
    public struct ScreenEffectState : IComponentData
    {
        /// <summary>0–1 normalised speed signal. Drives lens distortion and chromatic aberration.</summary>
        public float SpeedLineIntensity;

        /// <summary>0–1 additional lens distortion from non-speed sources (e.g. impact flash).</summary>
        public float LensDistortionAdd;

        /// <summary>0–1 vignette intensity (e.g. landing impact, damage).</summary>
        public float VignetteIntensity;
    }
}
