using UnityEngine;

namespace DOTS.Rendering.Sky
{
    /// <summary>
    /// Tier-1 atmosphere authority broadcast (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.1/§5.2).
    /// Pushes the evaluated palette to the global _Atmo* shader uniforms so every
    /// distance-facing consumer (sky mountain band, ground disc, hero relic, ...)
    /// samples one authoritative palette per frame. Driven from
    /// <see cref="TimeOfDayController"/> — kept static and stateless so EditMode
    /// tests can exercise the contract without a scene.
    /// </summary>
    public static class AtmosphereBroadcast
    {
        /// <summary>
        /// Broadcasts the palette. <paramref name="farFade"/> is the reference distance at which
        /// aerial perspective reaches full — typically the camera far clip plane.
        /// </summary>
        public static void Push(SkySettings sky, AtmosphereSettings atmosphere, float farFade)
        {
            var skyClamped = sky.Clamped();
            var atmo = atmosphere.Clamped();

            Shader.SetGlobalColor(ShaderIDs.AtmoHorizon, skyClamped.horizonColor);
            Shader.SetGlobalColor(ShaderIDs.AtmoZenith, skyClamped.zenithColor);
            Shader.SetGlobalColor(ShaderIDs.AtmoGround, atmo.groundColor);
            Shader.SetGlobalColor(ShaderIDs.AtmoRock, atmo.rockColor);
            Shader.SetGlobalFloat(ShaderIDs.AtmoSaturation, atmo.saturation);
            Shader.SetGlobalFloat(ShaderIDs.AtmoFarFade, Mathf.Max(farFade, 1f));
            Shader.SetGlobalFloat(ShaderIDs.AtmoHazeDensity, atmo.hazeDensity);
            Shader.SetGlobalFloat(ShaderIDs.AtmoHazeFalloff, atmo.hazeFalloff);
            Shader.SetGlobalFloat(ShaderIDs.AtmoDistanceHaze, atmo.distanceHaze);
        }

        // Global shader values are zero in a fresh session until someone sets them, which would
        // render consumer shaders black in scenes without a TimeOfDayController (test scenes,
        // editor scene view before play). Seed sane defaults early in both contexts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PushRuntimeDefaults() =>
            Push(SkySettings.Default, AtmosphereSettings.Default, 600f);

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void PushEditorDefaults() =>
            Push(SkySettings.Default, AtmosphereSettings.Default, 600f);
#endif
    }
}
