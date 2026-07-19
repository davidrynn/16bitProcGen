using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// H1 parity/property tests for the <c>H</c> authority (WORLD_STRUCTURE_SPEC.md §4).
    ///
    /// <para>NUnit can only execute the C# side, so — like <c>TerrainChunkMaterialContractTests</c>
    /// for the <c>GroundNoise</c> seam — these do not run the HLSL on a GPU. The C#↔HLSL parity
    /// guarantee is the line-for-line structural mirror between <c>WorldStructure.cs</c> and
    /// <c>WorldStructure.hlsl</c> (enforced by review + the mirror-contract comments in both files);
    /// what these tests pin is the C# side: determinism (spec §3), the boundedness the amplitude ramp
    /// relies on, and the near-field slab budget (§5.3) that Phase C depends on.</para>
    /// </summary>
    [TestFixture]
    public class WorldStructureParityTests
    {
        // A spread of world-XZ probe points: origin, near-field, past the world edge, off-axis.
        private static readonly float2[] Probes =
        {
            new float2(0f, 0f),
            new float2(37f, -12f),
            new float2(150f, 150f),
            new float2(-280f, 90f),
            new float2(0f, 900f),      // the vista corridor line (H3 will clamp this)
            new float2(1200f, -400f),
            new float2(-2100f, 2400f),
        };

        private static WorldStructureConstants DefaultConstants(uint seed = 12345u)
        {
            // Mirrors WorldStructureSettings defaults; kept inline so the test does not depend on a
            // Resources asset existing (the asset is authored in-editor).
            return new WorldStructureConstants
            {
                MacroFreq = 0.0004f,
                SeedOffset = WorldStructure.SeedOffset(seed),
                Octaves = 4,
                Lacunarity = 2.0f,
                Gain = 0.5f,
                ANear = 3.0f,
                AFar = 200.0f,
                RampStart = 600.0f,
                RampEnd = 2500.0f,
            };
        }

        [Test]
        public void Sample_IsDeterministic()
        {
            var c = DefaultConstants();
            foreach (var p in Probes)
            {
                float a = WorldStructure.Sample(p, c);
                float b = WorldStructure.Sample(p, c);
                Assert.AreEqual(a, b, 0f,
                    $"H must be a pure function of (seed, settings) — identical inputs gave " +
                    $"different outputs at {p} (spec §3).");
            }
        }

        [Test]
        public void RidgedFBM_IsNormalizedToUnitRange()
        {
            var c = DefaultConstants();
            // Sweep a grid in noise space; the normalized ridged FBM must stay within [0,1] so the
            // amplitude ramp reads as literal peak units.
            for (int x = -50; x <= 50; x += 5)
            for (int z = -50; z <= 50; z += 5)
            {
                float2 p = new float2(x, z) * 0.13f + c.SeedOffset;
                float n = WorldStructure.RidgedFBM(p, c.Octaves, c.Lacunarity, c.Gain);
                Assert.GreaterOrEqual(n, 0.0f, $"RidgedFBM below 0 at {p}");
                Assert.LessOrEqual(n, 1.0f + 1e-5f, $"RidgedFBM above 1 at {p}");
            }
        }

        [Test]
        public void AmplitudeRamp_MatchesEndpointsAndIsMonotonic()
        {
            var c = DefaultConstants();

            Assert.AreEqual(c.ANear, WorldStructure.AmplitudeRamp(0f, c.ANear, c.AFar, c.RampStart, c.RampEnd), 1e-4f,
                "At the origin the ramp must equal ANear.");
            Assert.AreEqual(c.ANear, WorldStructure.AmplitudeRamp(c.RampStart, c.ANear, c.AFar, c.RampStart, c.RampEnd), 1e-4f,
                "At rampStart the ramp must still be ANear (smoothstep floor).");
            Assert.AreEqual(c.AFar, WorldStructure.AmplitudeRamp(c.RampEnd, c.ANear, c.AFar, c.RampStart, c.RampEnd), 1e-4f,
                "At rampEnd the ramp must equal AFar.");
            Assert.AreEqual(c.AFar, WorldStructure.AmplitudeRamp(c.RampEnd + 5000f, c.ANear, c.AFar, c.RampStart, c.RampEnd), 1e-4f,
                "Past rampEnd the ramp must saturate at AFar.");

            float prev = float.NegativeInfinity;
            for (float r = 0f; r <= 3000f; r += 25f)
            {
                float a = WorldStructure.AmplitudeRamp(r, c.ANear, c.AFar, c.RampStart, c.RampEnd);
                Assert.GreaterOrEqual(a, prev - 1e-4f, $"Ramp must be non-decreasing (broke at r={r}).");
                prev = a;
            }
        }

        [Test]
        public void Sample_NeverExceedsAmplitudeEnvelope()
        {
            var c = DefaultConstants();
            // |H| ≤ A(r) everywhere because the mask is 1 and ridged ∈ [0,1]. Consumers rely on this
            // envelope to reason about vertical budgets without evaluating the noise.
            for (int x = -3000; x <= 3000; x += 137)
            for (int z = -3000; z <= 3000; z += 137)
            {
                float2 p = new float2(x, z);
                float h = WorldStructure.Sample(p, c);
                float envelope = WorldStructure.AmplitudeRamp(math.length(p), c.ANear, c.AFar, c.RampStart, c.RampEnd);
                Assert.GreaterOrEqual(h, -1e-4f, $"H should be non-negative (ridged ≥ 0) at {p}");
                Assert.LessOrEqual(h, envelope + 1e-3f, $"H exceeded its amplitude envelope at {p}");
            }
        }

        [Test]
        public void NearField_FitsChunkSlabBudget()
        {
            // Spec §5.3 / Q1: the ~16u single-layer chunk slab already carries ±4u of surface noise,
            // so near-field |H| + that noise must fit. This is a light preview of the H3 CI guard;
            // it locks the default ANear against a regression that would push columns out of the slab.
            const float slab = 16f;
            const float surfaceNoiseAmp = 4f;
            var c = DefaultConstants();

            float maxH = 0f;
            for (int x = -300; x <= 300; x += 11)
            for (int z = -300; z <= 300; z += 11)
            {
                float h = math.abs(WorldStructure.Sample(new float2(x, z), c));
                maxH = math.max(maxH, h);
            }

            Assert.LessOrEqual(maxH + surfaceNoiseAmp, slab,
                $"Near-field |H| ({maxH:F2}u) + surface noise ({surfaceNoiseAmp}u) must fit the " +
                $"{slab}u chunk slab until vertical chunking lands (spec §5.3).");
        }

        [Test]
        public void SeedOffset_IsBoundedAndSeedDependent()
        {
            float2 a = WorldStructure.SeedOffset(12345u);
            float2 b = WorldStructure.SeedOffset(12345u);
            float2 diff = WorldStructure.SeedOffset(12346u);

            Assert.AreEqual(a.x, b.x, 0f, "Same seed must give the same offset.");
            Assert.AreEqual(a.y, b.y, 0f, "Same seed must give the same offset.");
            Assert.IsTrue(math.any(a != diff), "Adjacent seeds must decorrelate to different offsets.");

            foreach (uint s in new uint[] { 0u, 1u, 12345u, uint.MaxValue })
            {
                float2 o = WorldStructure.SeedOffset(s);
                Assert.GreaterOrEqual(o.x, 0f);
                Assert.GreaterOrEqual(o.y, 0f);
                Assert.LessOrEqual(o.x, WorldStructure.MaxSeedOffset);
                Assert.LessOrEqual(o.y, WorldStructure.MaxSeedOffset);
            }
        }

        [Test]
        public void ConfigHash_IsStableAndFieldSensitive()
        {
            var settings = ScriptableObject.CreateInstance<WorldStructureSettings>();
            try
            {
                uint h1 = settings.ComputeConfigHash(12345u);
                uint h2 = settings.ComputeConfigHash(12345u);
                Assert.AreEqual(h1, h2, "Same fields + seed must hash identically (spec §5.1).");

                Assert.AreNotEqual(h1, settings.ComputeConfigHash(12346u),
                    "Changing the world seed must change the save-config hash.");

                settings.aFar += 1f;
                Assert.AreNotEqual(h1, settings.ComputeConfigHash(12345u),
                    "Changing a tunable must change the save-config hash — else saved edits replay " +
                    "against a silently relocated base field (spec §5.1).");
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void Settings_ToConstants_MirrorsFields()
        {
            var settings = ScriptableObject.CreateInstance<WorldStructureSettings>();
            try
            {
                var c = settings.ToConstants(9999u);
                Assert.AreEqual(settings.macroFreq, c.MacroFreq);
                Assert.AreEqual(settings.octaves, c.Octaves);
                Assert.AreEqual(settings.lacunarity, c.Lacunarity);
                Assert.AreEqual(settings.gain, c.Gain);
                Assert.AreEqual(settings.aNear, c.ANear);
                Assert.AreEqual(settings.aFar, c.AFar);
                Assert.AreEqual(settings.rampStart, c.RampStart);
                Assert.AreEqual(settings.rampEnd, c.RampEnd);

                float2 expected = WorldStructure.SeedOffset(9999u);
                Assert.AreEqual(expected.x, c.SeedOffset.x, 0f);
                Assert.AreEqual(expected.y, c.SeedOffset.y, 0f);
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
