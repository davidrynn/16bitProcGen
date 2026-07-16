using NUnit.Framework;
using UnityEngine;
using DOTS.Rendering.Sky;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// V9 P1 contract tests (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §5.1/§5.2): the
    /// broadcast writes the evaluated palette into the global _Atmo* uniforms, and
    /// the per-preset atmosphere block clamps/lerps sanely.
    /// </summary>
    [TestFixture]
    public class AtmosphereAuthorityTests
    {
        private const float Tolerance = 0.001f;

        [Test]
        public void Push_WritesPaletteToGlobalUniforms()
        {
            var sky = SkySettings.Default;
            sky.horizonColor = new Color(0.1f, 0.2f, 0.3f, 1f);
            sky.zenithColor = new Color(0.4f, 0.5f, 0.6f, 1f);

            var atmo = AtmosphereSettings.Default;
            atmo.groundColor = new Color(0.7f, 0.8f, 0.1f, 1f);
            atmo.rockColor = new Color(0.2f, 0.3f, 0.4f, 1f);
            atmo.saturation = 0.75f;
            atmo.hazeDensity = 0.002f;
            atmo.hazeFalloff = 0.02f;
            atmo.distanceHaze = 0.25f;
            atmo.hazeMacroScale = 0.003f;
            atmo.hazeMacroStrength = 0.4f;

            AtmosphereBroadcast.Push(sky, atmo, 600f);

            Assert.AreEqual(sky.horizonColor, Shader.GetGlobalColor(ShaderIDs.AtmoHorizon));
            Assert.AreEqual(sky.zenithColor, Shader.GetGlobalColor(ShaderIDs.AtmoZenith));
            Assert.AreEqual(atmo.groundColor, Shader.GetGlobalColor(ShaderIDs.AtmoGround));
            Assert.AreEqual(atmo.rockColor, Shader.GetGlobalColor(ShaderIDs.AtmoRock));
            Assert.AreEqual(0.75f, Shader.GetGlobalFloat(ShaderIDs.AtmoSaturation), Tolerance);
            Assert.AreEqual(600f, Shader.GetGlobalFloat(ShaderIDs.AtmoFarFade), Tolerance);
            Assert.AreEqual(0.002f, Shader.GetGlobalFloat(ShaderIDs.AtmoHazeDensity), Tolerance);
            Assert.AreEqual(0.02f, Shader.GetGlobalFloat(ShaderIDs.AtmoHazeFalloff), Tolerance);
            Assert.AreEqual(0.25f, Shader.GetGlobalFloat(ShaderIDs.AtmoDistanceHaze), Tolerance);
            Assert.AreEqual(0.003f, Shader.GetGlobalFloat(ShaderIDs.AtmoHazeMacroScale), Tolerance);
            Assert.AreEqual(0.4f, Shader.GetGlobalFloat(ShaderIDs.AtmoHazeMacroStrength), Tolerance);
        }

        [Test]
        public void Push_ClampsDegenerateValues()
        {
            var atmo = AtmosphereSettings.Default;
            atmo.saturation = 2f;
            atmo.hazeDensity = -1f;
            atmo.hazeFalloff = 0f;
            atmo.hazeMacroScale = -0.5f;
            atmo.hazeMacroStrength = 3f;

            AtmosphereBroadcast.Push(SkySettings.Default, atmo, -50f);

            Assert.AreEqual(1f, Shader.GetGlobalFloat(ShaderIDs.AtmoSaturation), Tolerance);
            // Negative/zero farFade would divide-by-zero the shader's distance normalization.
            Assert.GreaterOrEqual(Shader.GetGlobalFloat(ShaderIDs.AtmoFarFade), 1f);
            Assert.GreaterOrEqual(Shader.GetGlobalFloat(ShaderIDs.AtmoHazeDensity), 0f);
            Assert.Greater(Shader.GetGlobalFloat(ShaderIDs.AtmoHazeFalloff), 0f);
            Assert.GreaterOrEqual(Shader.GetGlobalFloat(ShaderIDs.AtmoHazeMacroScale), 0f);
            // Strength > 1 would let a thin patch invert the haze amount negative before saturate.
            Assert.LessOrEqual(Shader.GetGlobalFloat(ShaderIDs.AtmoHazeMacroStrength), 1f);
        }

        [Test]
        public void Push_BroadcastsLandmarkFade_AsMaxOfWorldReferenceAndLandmarkDistance()
        {
            float saved = AtmosphereBroadcast.LandmarkDistance;
            try
            {
                AtmosphereBroadcast.LandmarkDistance = 2000f;
                AtmosphereBroadcast.Push(SkySettings.Default, AtmosphereSettings.Default, 600f);
                Assert.AreEqual(2000f, Shader.GetGlobalFloat(ShaderIDs.AtmoLandmarkFade), Tolerance);

                // Disabled (0) collapses to the world edge so the hero dither fade doubles as
                // the far-clip concealment.
                AtmosphereBroadcast.LandmarkDistance = 0f;
                AtmosphereBroadcast.Push(SkySettings.Default, AtmosphereSettings.Default, 600f);
                Assert.AreEqual(600f, Shader.GetGlobalFloat(ShaderIDs.AtmoLandmarkFade), Tolerance);
            }
            finally
            {
                AtmosphereBroadcast.LandmarkDistance = saved;
            }
        }

        [Test]
        public void WorldReferenceDistance_ClampsToMinimum()
        {
            float saved = AtmosphereBroadcast.WorldReferenceDistance;
            try
            {
                AtmosphereBroadcast.WorldReferenceDistance = -50f;
                // Negative/zero would divide-by-zero the shader's distance normalization.
                Assert.GreaterOrEqual(AtmosphereBroadcast.WorldReferenceDistance, 1f);
            }
            finally
            {
                AtmosphereBroadcast.WorldReferenceDistance = saved;
            }
        }

        /// <summary>
        /// R6 P2 contract (LANDMARK_DRAW_DISTANCE_SPEC.md): the broadcast farFade sources from the
        /// config-seeded world reference distance, NOT Camera.main.farClipPlane — raising the far
        /// plane for landmark permanence must not stretch the disc→skirt handoff.
        /// </summary>
        [Test]
        public void PushAtmosphere_UsesWorldReferenceDistance_NotCameraFarPlane()
        {
            float saved = AtmosphereBroadcast.WorldReferenceDistance;
            var cameraGO = new GameObject("Test Main Camera") { tag = "MainCamera" };
            var controllerGO = new GameObject("Test TimeOfDayController");
            var preset = ScriptableObject.CreateInstance<SkyPreset>();
            try
            {
                cameraGO.AddComponent<Camera>().farClipPlane = 2000f;

                var controller = controllerGO.AddComponent<TimeOfDayController>();
                controller.ActivePreset = preset;

                AtmosphereBroadcast.WorldReferenceDistance = 555f;
                // OnValidate is the controller's edit-mode re-broadcast path — same PushAtmosphere
                // code the Play Mode update drives. Invoked via reflection: SendMessage on a
                // non-ExecuteAlways behaviour asserts ShouldRunBehaviour() in EditMode.
                typeof(TimeOfDayController)
                    .GetMethod("OnValidate",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(controller, null);

                Assert.AreEqual(555f, Shader.GetGlobalFloat(ShaderIDs.AtmoFarFade), Tolerance,
                    "_AtmoFarFade must follow the world reference distance, not the camera far plane.");
            }
            finally
            {
                AtmosphereBroadcast.WorldReferenceDistance = saved;
                Object.DestroyImmediate(preset);
                Object.DestroyImmediate(controllerGO);
                Object.DestroyImmediate(cameraGO);
            }
        }

        [Test]
        public void AtmosphereSettings_Default_HasExpectedValues()
        {
            var a = AtmosphereSettings.Default;

            Assert.AreEqual(new Color(0.40f, 0.46f, 0.26f, 1f), a.groundColor);
            Assert.AreEqual(new Color(0.28f, 0.32f, 0.23f, 1f), a.rockColor);
            Assert.AreEqual(1f, a.saturation, Tolerance);
            Assert.Greater(a.hazeDensity, 0f);
            Assert.Greater(a.hazeFalloff, 0f);
            // V17 P4: patchy haze ships enabled — a zero default would silently disable the
            // one variation mechanism that survives eye-level viewing (spec §5.3b).
            Assert.Greater(a.hazeMacroStrength, 0f);
            Assert.Greater(a.hazeMacroScale, 0f);
        }

        [Test]
        public void AtmosphereSettings_Lerp_Midpoint_BlendsAllFields()
        {
            var a = AtmosphereSettings.Default;
            a.saturation = 0f;
            a.hazeDensity = 0f;
            var b = AtmosphereSettings.Default;
            b.saturation = 1f;
            b.hazeDensity = 0.004f;
            b.groundColor = Color.black;
            a.hazeMacroStrength = 0f;
            b.hazeMacroStrength = 0.5f;

            var mid = AtmosphereSettings.Lerp(a, b, 0.5f);

            Assert.AreEqual(0.5f, mid.saturation, Tolerance);
            Assert.AreEqual(0.002f, mid.hazeDensity, Tolerance);
            Assert.AreEqual((a.groundColor.r + 0f) * 0.5f, mid.groundColor.r, Tolerance);
            Assert.AreEqual(0.25f, mid.hazeMacroStrength, Tolerance);
        }

        [Test]
        public void SkyPreset_NewInstance_CarriesDefaultAtmosphereBlock()
        {
            var preset = ScriptableObject.CreateInstance<SkyPreset>();
            try
            {
                // Guards the serialization-safety assumption: presets saved before the
                // atmosphere field existed must deserialize with these initializer values,
                // not zeroed colors (which would broadcast a black palette).
                Assert.AreEqual(AtmosphereSettings.Default.groundColor, preset.atmosphere.groundColor);
                Assert.Greater(preset.atmosphere.hazeFalloff, 0f);
            }
            finally
            {
                Object.DestroyImmediate(preset);
            }
        }
    }
}
