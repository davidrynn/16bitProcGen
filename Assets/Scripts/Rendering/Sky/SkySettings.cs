using UnityEngine;

namespace DOTS.Rendering.Sky
{
    [System.Serializable]
    public struct SkySettings
    {
        /// <summary>Bottom color of the gradient (horizon).</summary>
        public Color horizonColor;

        /// <summary>Top color of the gradient (zenith / straight up).</summary>
        public Color zenithColor;

        /// <summary>
        /// Controls gradient curve sharpness.
        /// 1.0 = linear. >1.0 = more color near horizon. &lt;1.0 = more color near zenith.
        /// Clamped to [0.01, 10.0] at consumption time.
        /// </summary>
        public float gradientExponent;

        /// <summary>
        /// Vertical offset for the horizon line in normalized view-direction space.
        /// 0.0 = geometric horizon. Positive pushes horizon upward.
        /// Clamped to [-0.5, 0.5] at consumption time.
        /// </summary>
        public float horizonHeight;

        public static SkySettings Default => new SkySettings
        {
            horizonColor = new Color(0.85f, 0.75f, 0.55f, 1.0f),
            zenithColor = new Color(0.30f, 0.50f, 0.80f, 1.0f),
            gradientExponent = 1.0f,
            horizonHeight = 0.0f
        };

        public SkySettings Clamped()
        {
            return new SkySettings
            {
                horizonColor = horizonColor,
                zenithColor = zenithColor,
                gradientExponent = Mathf.Clamp(gradientExponent, 0.01f, 10.0f),
                horizonHeight = Mathf.Clamp(horizonHeight, -0.5f, 0.5f)
            };
        }

        public static SkySettings Lerp(SkySettings a, SkySettings b, float t)
        {
            t = Mathf.Clamp01(t);
            return new SkySettings
            {
                horizonColor = Color.Lerp(a.horizonColor, b.horizonColor, t),
                zenithColor = Color.Lerp(a.zenithColor, b.zenithColor, t),
                gradientExponent = Mathf.Lerp(a.gradientExponent, b.gradientExponent, t),
                horizonHeight = Mathf.Lerp(a.horizonHeight, b.horizonHeight, t)
            };
        }
    }
}
