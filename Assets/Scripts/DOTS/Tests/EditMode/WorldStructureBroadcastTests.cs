using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// H2 contract for <see cref="WorldStructureBroadcast"/>: the C# constants reach the
    /// <c>_WorldMacro*</c> shader globals under the exact names the HLSL wrapper reads
    /// (<c>WorldStructure.hlsl:SampleWorldMacroHeightGlobal</c>). Globals are readable back via
    /// <c>Shader.GetGlobal*</c>, so this asserts the wiring without a GPU pass — a rename on either
    /// side breaks it. Also pins the code-default fallback against the settings field defaults.
    /// </summary>
    [TestFixture]
    public class WorldStructureBroadcastTests
    {
        [Test]
        public void Push_SetsEveryGlobal_UnderTheExpectedNames()
        {
            var c = new WorldStructureConstants
            {
                MacroFreq = 0.0004f,
                SeedOffset = new float2(11f, 22f),
                Octaves = 4,
                Lacunarity = 2f,
                Gain = 0.5f,
                ANear = 3f,
                AFar = 200f,
                RampStart = 600f,
                RampEnd = 2500f,
            };

            WorldStructureBroadcast.Push(c);

            Assert.AreEqual(0.0004f, Shader.GetGlobalFloat("_WorldMacroFreq"), 1e-6f);
            var off = Shader.GetGlobalVector("_WorldMacroSeedOffset");
            Assert.AreEqual(11f, off.x, 1e-4f, "_WorldMacroSeedOffset.x");
            Assert.AreEqual(22f, off.y, 1e-4f, "_WorldMacroSeedOffset.y");
            Assert.AreEqual(4, Shader.GetGlobalInteger("_WorldMacroOctaves"), "_WorldMacroOctaves");
            Assert.AreEqual(2f, Shader.GetGlobalFloat("_WorldMacroLacunarity"), 1e-6f);
            Assert.AreEqual(0.5f, Shader.GetGlobalFloat("_WorldMacroGain"), 1e-6f);
            Assert.AreEqual(3f, Shader.GetGlobalFloat("_WorldMacroANear"), 1e-4f);
            Assert.AreEqual(200f, Shader.GetGlobalFloat("_WorldMacroAFar"), 1e-3f);
            Assert.AreEqual(600f, Shader.GetGlobalFloat("_WorldMacroRampStart"), 1e-3f);
            Assert.AreEqual(2500f, Shader.GetGlobalFloat("_WorldMacroRampEnd"), 1e-3f);
        }

        [Test]
        public void DefaultConstants_MatchFieldDefaults()
        {
            // The fallback (no asset) must equal a fresh settings instance's constants, or shaders
            // would seed different values depending on whether the asset exists.
            var settings = ScriptableObject.CreateInstance<WorldStructureSettings>();
            try
            {
                var d = WorldStructureSettings.DefaultConstants;
                var c = settings.ToConstants(); // uses the defaultWorldSeed field default

                Assert.AreEqual(c.MacroFreq, d.MacroFreq, 1e-9f);
                Assert.AreEqual(c.Octaves, d.Octaves);
                Assert.AreEqual(c.Lacunarity, d.Lacunarity, 1e-9f);
                Assert.AreEqual(c.Gain, d.Gain, 1e-9f);
                Assert.AreEqual(c.ANear, d.ANear, 1e-9f);
                Assert.AreEqual(c.AFar, d.AFar, 1e-9f);
                Assert.AreEqual(c.RampStart, d.RampStart, 1e-9f);
                Assert.AreEqual(c.RampEnd, d.RampEnd, 1e-9f);
                Assert.AreEqual(c.SeedOffset.x, d.SeedOffset.x, 0f, "seed offset must match (same default seed)");
                Assert.AreEqual(c.SeedOffset.y, d.SeedOffset.y, 0f);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void PushFromSettings_DoesNotThrow_AndLeavesGlobalsNonDegenerate()
        {
            // Whether or not the asset exists, this must seed a usable field (non-zero octaves and a
            // positive ramp span so a consumer never reads H≡0 or hits the span guard).
            Assert.DoesNotThrow(WorldStructureBroadcast.PushFromSettings);
            Assert.Greater(Shader.GetGlobalInteger("_WorldMacroOctaves"), 0);
            Assert.Greater(Shader.GetGlobalFloat("_WorldMacroRampEnd"),
                           Shader.GetGlobalFloat("_WorldMacroRampStart"));
        }
    }
}
