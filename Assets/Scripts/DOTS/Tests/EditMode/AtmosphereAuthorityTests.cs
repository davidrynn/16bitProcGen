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
        }

        [Test]
        public void Push_ClampsDegenerateValues()
        {
            var atmo = AtmosphereSettings.Default;
            atmo.saturation = 2f;
            atmo.hazeDensity = -1f;
            atmo.hazeFalloff = 0f;

            AtmosphereBroadcast.Push(SkySettings.Default, atmo, -50f);

            Assert.AreEqual(1f, Shader.GetGlobalFloat(ShaderIDs.AtmoSaturation), Tolerance);
            // Negative/zero farFade would divide-by-zero the shader's distance normalization.
            Assert.GreaterOrEqual(Shader.GetGlobalFloat(ShaderIDs.AtmoFarFade), 1f);
            Assert.GreaterOrEqual(Shader.GetGlobalFloat(ShaderIDs.AtmoHazeDensity), 0f);
            Assert.Greater(Shader.GetGlobalFloat(ShaderIDs.AtmoHazeFalloff), 0f);
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

            var mid = AtmosphereSettings.Lerp(a, b, 0.5f);

            Assert.AreEqual(0.5f, mid.saturation, Tolerance);
            Assert.AreEqual(0.002f, mid.hazeDensity, Tolerance);
            Assert.AreEqual((a.groundColor.r + 0f) * 0.5f, mid.groundColor.r, Tolerance);
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
