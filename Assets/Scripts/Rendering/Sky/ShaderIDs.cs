using UnityEngine;

namespace DOTS.Rendering.Sky
{
    public static class ShaderIDs
    {
        // Gradient sky
        public static readonly int HorizonColor = Shader.PropertyToID("_HorizonColor");
        public static readonly int ZenithColor = Shader.PropertyToID("_ZenithColor");
        public static readonly int GradientExponent = Shader.PropertyToID("_GradientExponent");
        public static readonly int HorizonHeight = Shader.PropertyToID("_HorizonHeight");

        // Clouds
        public static readonly int CloudColor = Shader.PropertyToID("_CloudColor");
        public static readonly int CloudShadowColor = Shader.PropertyToID("_CloudShadowColor");
        public static readonly int ScrollSpeed = Shader.PropertyToID("_ScrollSpeed");
        public static readonly int NoiseScale = Shader.PropertyToID("_NoiseScale");
        public static readonly int CoverageThreshold = Shader.PropertyToID("_CoverageThreshold");
        public static readonly int EdgeSoftness = Shader.PropertyToID("_EdgeSoftness");
        public static readonly int Opacity = Shader.PropertyToID("_Opacity");
    }
}
