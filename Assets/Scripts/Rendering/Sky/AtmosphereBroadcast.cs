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
        private const float DefaultWorldReferenceDistance = 600f;

        private static float _worldReferenceDistance = DefaultWorldReferenceDistance;

        /// <summary>
        /// The world reference distance — where the ordinary world visually ends and aerial
        /// perspective reaches full (LANDMARK_DRAW_DISTANCE_SPEC.md P2). Deliberately NOT the
        /// camera far clip plane: R6 raises the far plane for landmark permanence, and tying
        /// _AtmoFarFade to it would silently stretch the disc→skirt handoff and the distanceHaze
        /// ramp with it. Seeded from ProjectFeatureConfig.DerivedCameraFarClip by
        /// DotsSystemBootstrap — pushed in rather than read here because this assembly (Core)
        /// cannot reference DOTS.Core.Authoring (reverse dependency). Default matches the
        /// config default so edit-mode broadcasts agree with play mode.
        /// </summary>
        public static float WorldReferenceDistance
        {
            get => _worldReferenceDistance;
            set => _worldReferenceDistance = Mathf.Max(value, 1f);
        }

        private static float _landmarkDistance = 2000f;

        /// <summary>
        /// Landmark draw distance (LANDMARK_DRAW_DISTANCE_SPEC.md P1/P3): where hero landmarks
        /// dither-dissolve. Broadcast as _AtmoLandmarkFade = max(world reference, this), so with
        /// the feature disabled (0) the dissolve sits at the world edge and doubles as the hero's
        /// far-clip concealment. Seeded from ProjectFeatureConfig.LandmarkDrawDistance by
        /// DotsSystemBootstrap; default matches the config default.
        /// </summary>
        public static float LandmarkDistance
        {
            get => _landmarkDistance;
            set => _landmarkDistance = Mathf.Max(value, 0f);
        }

        /// <summary>
        /// Broadcasts the palette. <paramref name="farFade"/> is the reference distance at which
        /// aerial perspective reaches full — <see cref="WorldReferenceDistance"/>, not the camera
        /// far clip plane.
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
            Shader.SetGlobalFloat(ShaderIDs.AtmoLandmarkFade,
                Mathf.Max(Mathf.Max(farFade, 1f), LandmarkDistance));
            Shader.SetGlobalFloat(ShaderIDs.AtmoHazeDensity, atmo.hazeDensity);
            Shader.SetGlobalFloat(ShaderIDs.AtmoHazeFalloff, atmo.hazeFalloff);
            Shader.SetGlobalFloat(ShaderIDs.AtmoDistanceHaze, atmo.distanceHaze);
            Shader.SetGlobalFloat(ShaderIDs.AtmoHazeMacroScale, atmo.hazeMacroScale);
            Shader.SetGlobalFloat(ShaderIDs.AtmoHazeMacroStrength, atmo.hazeMacroStrength);
        }

        // Global shader values are zero in a fresh session until someone sets them, which would
        // render consumer shaders black in scenes without a TimeOfDayController (test scenes,
        // editor scene view before play). Seed sane defaults early in both contexts.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PushRuntimeDefaults() =>
            Push(SkySettings.Default, AtmosphereSettings.Default, WorldReferenceDistance);

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void PushEditorDefaults() =>
            Push(SkySettings.Default, AtmosphereSettings.Default, WorldReferenceDistance);
#endif
    }
}
