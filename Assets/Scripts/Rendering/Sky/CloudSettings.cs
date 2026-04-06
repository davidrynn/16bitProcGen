using UnityEngine;

namespace DOTS.Rendering.Sky
{
    [System.Serializable]
    public struct CloudSettings
    {
        [Tooltip("Base cloud color (lit side).")]
        public Color cloudColor;

        [Tooltip("Shadow/underside cloud tint.")]
        public Color cloudShadowColor;

        [Tooltip("Scroll speed in UV units per second.")]
        public Vector2 scrollSpeed;

        [Tooltip("UV tiling scale for the noise texture.")]
        public float noiseScale;

        [Tooltip("Coverage threshold. Higher = fewer clouds. Range [0,1].")]
        [Range(0f, 1f)]
        public float coverageThreshold;

        [Tooltip("Edge softness. Higher = softer edges.")]
        [Range(0.01f, 1f)]
        public float edgeSoftness;

        [Tooltip("Overall cloud opacity.")]
        [Range(0f, 1f)]
        public float opacity;

        public static CloudSettings Default => new CloudSettings
        {
            cloudColor = new Color(1f, 1f, 1f, 1f),
            cloudShadowColor = new Color(0.6f, 0.6f, 0.7f, 1f),
            scrollSpeed = new Vector2(0.01f, 0.005f),
            noiseScale = 3f,
            coverageThreshold = 0.45f,
            edgeSoftness = 0.15f,
            opacity = 0.6f
        };

        public static CloudSettings Lerp(CloudSettings a, CloudSettings b, float t)
        {
            t = Mathf.Clamp01(t);
            return new CloudSettings
            {
                cloudColor = Color.Lerp(a.cloudColor, b.cloudColor, t),
                cloudShadowColor = Color.Lerp(a.cloudShadowColor, b.cloudShadowColor, t),
                scrollSpeed = Vector2.Lerp(a.scrollSpeed, b.scrollSpeed, t),
                noiseScale = Mathf.Lerp(a.noiseScale, b.noiseScale, t),
                coverageThreshold = Mathf.Lerp(a.coverageThreshold, b.coverageThreshold, t),
                edgeSoftness = Mathf.Lerp(a.edgeSoftness, b.edgeSoftness, t),
                opacity = Mathf.Lerp(a.opacity, b.opacity, t)
            };
        }
    }
}
