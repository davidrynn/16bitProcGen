using UnityEngine;

namespace DOTS.Rendering.Sky
{
    [CreateAssetMenu(menuName = "Rendering/Sky Preset", fileName = "SkyPreset")]
    public class SkyPreset : ScriptableObject
    {
        [Header("Time-of-Day Keyframes")]
        [Tooltip("Dawn (time = 0.0)")]
        public SkySettings dawn = new SkySettings
        {
            horizonColor = new Color(0.95f, 0.60f, 0.35f, 1.0f),
            zenithColor = new Color(0.30f, 0.35f, 0.60f, 1.0f),
            gradientExponent = 0.4f,
            horizonHeight = -0.05f
        };

        [Tooltip("Noon (time = 0.25)")]
        public SkySettings noon = new SkySettings
        {
            horizonColor = new Color(0.85f, 0.75f, 0.55f, 1.0f),
            zenithColor = new Color(0.30f, 0.50f, 0.80f, 1.0f),
            gradientExponent = 0.4f,
            horizonHeight = 0.0f
        };

        [Tooltip("Dusk (time = 0.5)")]
        public SkySettings dusk = new SkySettings
        {
            horizonColor = new Color(0.90f, 0.45f, 0.25f, 1.0f),
            zenithColor = new Color(0.20f, 0.15f, 0.40f, 1.0f),
            gradientExponent = 0.5f,
            horizonHeight = -0.05f
        };

        [Tooltip("Night (time = 0.75)")]
        public SkySettings night = new SkySettings
        {
            horizonColor = new Color(0.08f, 0.08f, 0.15f, 1.0f),
            zenithColor = new Color(0.02f, 0.02f, 0.08f, 1.0f),
            gradientExponent = 0.35f,
            horizonHeight = 0.0f
        };

        /// <summary>
        /// Evaluate SkySettings at a normalized time-of-day value.
        /// 0.0 = dawn, 0.25 = noon, 0.5 = dusk, 0.75 = night, 1.0 = dawn (wraps).
        /// </summary>
        public SkySettings Evaluate(float normalizedTime)
        {
            normalizedTime = Mathf.Repeat(normalizedTime, 1.0f);

            if (normalizedTime < 0.25f)
            {
                float t = normalizedTime / 0.25f;
                return SkySettings.Lerp(dawn, noon, t);
            }
            if (normalizedTime < 0.5f)
            {
                float t = (normalizedTime - 0.25f) / 0.25f;
                return SkySettings.Lerp(noon, dusk, t);
            }
            if (normalizedTime < 0.75f)
            {
                float t = (normalizedTime - 0.5f) / 0.25f;
                return SkySettings.Lerp(dusk, night, t);
            }
            {
                float t = (normalizedTime - 0.75f) / 0.25f;
                return SkySettings.Lerp(night, dawn, t);
            }
        }
    }
}
