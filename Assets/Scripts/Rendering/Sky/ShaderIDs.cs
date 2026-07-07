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

        // Atmosphere authority globals (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.2)
        public static readonly int AtmoHorizon = Shader.PropertyToID("_AtmoHorizon");
        public static readonly int AtmoZenith = Shader.PropertyToID("_AtmoZenith");
        public static readonly int AtmoGround = Shader.PropertyToID("_AtmoGround");
        public static readonly int AtmoRock = Shader.PropertyToID("_AtmoRock");
        public static readonly int AtmoSaturation = Shader.PropertyToID("_AtmoSaturation");
        public static readonly int AtmoFarFade = Shader.PropertyToID("_AtmoFarFade");
        public static readonly int AtmoHazeDensity = Shader.PropertyToID("_AtmoHazeDensity");
        public static readonly int AtmoHazeFalloff = Shader.PropertyToID("_AtmoHazeFalloff");
        public static readonly int AtmoDistanceHaze = Shader.PropertyToID("_AtmoDistanceHaze");

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
