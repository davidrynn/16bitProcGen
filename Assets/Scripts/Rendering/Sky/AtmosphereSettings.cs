using UnityEngine;

namespace DOTS.Rendering.Sky
{
    /// <summary>
    /// Per-preset atmosphere palette block (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.2/§5.3a).
    /// Art-directed per <see cref="SkyPreset"/> rather than per time-of-day keyframe:
    /// ground/rock hues and haze physics stay constant across the cycle, while the
    /// dynamic horizon/zenith colors (from <see cref="SkySettings"/>) carry the
    /// time-of-day shift the haze converges toward.
    /// </summary>
    [System.Serializable]
    public struct AtmosphereSettings
    {
        /// <summary>Base ground/grass tint broadcast as _AtmoGround (disc, mountains, terrain tint).</summary>
        public Color groundColor;

        /// <summary>Base rock tint broadcast as _AtmoRock (disc, mountains).</summary>
        public Color rockColor;

        /// <summary>Global saturation scalar (1 = full color). Broadcast as _AtmoSaturation.</summary>
        [Range(0f, 1f)]
        public float saturation;

        /// <summary>Ground-level (y=0) haze density for the height-aware aerial term (_AtmoHazeDensity).</summary>
        public float hazeDensity;

        /// <summary>1/scale-height of the haze layer (_AtmoHazeFalloff). E.g. 1/60 = density halves every ~42u of altitude.</summary>
        public float hazeFalloff;

        /// <summary>
        /// Small altitude-independent aerial floor reached at the far fade distance (_AtmoDistanceHaze).
        /// Grades the clear zone at altitude into the far-clip concealer instead of a hard ring.
        /// </summary>
        [Range(0f, 1f)]
        public float distanceHaze;

        /// <summary>
        /// Plains/Cloudbreak-tuned defaults. Ground/rock match the disc's shipped literals so
        /// converting consumers produces no color jump. Haze density is anchored to the terrain's
        /// Exp² 0.0022 baseline at the ~180u disc↔terrain seam (both ≈15-20% veiled there, so the
        /// unconverted fogged terrain and the converted disc still read as one atmosphere), while
        /// the 400u sky-drop looks down through thin high air (&lt;10% veil). Far-clip concealment
        /// is handled separately by AtmoFarClipHaze, not by density.
        /// </summary>
        public static AtmosphereSettings Default => new AtmosphereSettings
        {
            groundColor = new Color(0.40f, 0.46f, 0.26f, 1f),
            rockColor = new Color(0.28f, 0.32f, 0.23f, 1f),
            saturation = 1f,
            hazeDensity = 0.0012f,
            hazeFalloff = 1f / 60f,
            distanceHaze = 0.15f,
        };

        public AtmosphereSettings Clamped()
        {
            return new AtmosphereSettings
            {
                groundColor = groundColor,
                rockColor = rockColor,
                saturation = Mathf.Clamp01(saturation),
                hazeDensity = Mathf.Max(hazeDensity, 0f),
                hazeFalloff = Mathf.Max(hazeFalloff, 1e-5f),
                distanceHaze = Mathf.Clamp01(distanceHaze),
            };
        }

        public static AtmosphereSettings Lerp(AtmosphereSettings a, AtmosphereSettings b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AtmosphereSettings
            {
                groundColor = Color.Lerp(a.groundColor, b.groundColor, t),
                rockColor = Color.Lerp(a.rockColor, b.rockColor, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                hazeDensity = Mathf.Lerp(a.hazeDensity, b.hazeDensity, t),
                hazeFalloff = Mathf.Lerp(a.hazeFalloff, b.hazeFalloff, t),
                distanceHaze = Mathf.Lerp(a.distanceHaze, b.distanceHaze, t),
            };
        }
    }
}
