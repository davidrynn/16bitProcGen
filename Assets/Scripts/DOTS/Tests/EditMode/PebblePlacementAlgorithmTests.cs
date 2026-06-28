using DOTS.Terrain.Pebbles;
using NUnit.Framework;
using Unity.Mathematics;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class PebblePlacementAlgorithmTests
    {
        private const uint Seed = 12345u;

        [Test]
        public void IsInRockyZone_IsDeterministic()
        {
            var p = PebblePlacementParams.Default;
            var pos = new float2(123.4f, -567.8f);

            bool first = PebblePlacementAlgorithm.IsInRockyZone(pos, Seed, p.ZoneNoiseFrequency, p.ZoneThreshold);
            bool second = PebblePlacementAlgorithm.IsInRockyZone(pos, Seed, p.ZoneNoiseFrequency, p.ZoneThreshold);

            Assert.AreEqual(first, second, "Zone membership must be a pure function of position/seed/tuning.");
        }

        [Test]
        public void IsInRockyZone_HigherThreshold_NeverAddsZones()
        {
            // Raising the threshold can only shrink zones: any point inside at threshold T
            // that drops out at T' > T is fine, but no point may newly enter. Sample a grid.
            var p = PebblePlacementParams.Default;
            const float higher = 0.8f;

            for (int x = 0; x < 32; x++)
            for (int z = 0; z < 32; z++)
            {
                var pos = new float2(x * 13.7f, z * 17.3f);
                bool inAtHigher = PebblePlacementAlgorithm.IsInRockyZone(pos, Seed, p.ZoneNoiseFrequency, higher);
                if (inAtHigher)
                {
                    Assert.IsTrue(
                        PebblePlacementAlgorithm.IsInRockyZone(pos, Seed, p.ZoneNoiseFrequency, p.ZoneThreshold),
                        "A point inside a zone at a stricter threshold must also be inside at the looser default.");
                }
            }
        }

        [Test]
        public void IsInRockyZone_DefaultTuning_ProducesPartialCoverage()
        {
            // The biome spec wants rocky zones over ~10-20% of area: assert sampled coverage
            // is neither empty nor dominant so default tuning stays in the intended regime.
            var p = PebblePlacementParams.Default;
            int inside = 0;
            const int samplesPerAxis = 64;

            for (int x = 0; x < samplesPerAxis; x++)
            for (int z = 0; z < samplesPerAxis; z++)
            {
                var pos = new float2(x * 23.1f, z * 19.7f);
                if (PebblePlacementAlgorithm.IsInRockyZone(pos, Seed, p.ZoneNoiseFrequency, p.ZoneThreshold))
                {
                    inside++;
                }
            }

            float coverage = inside / (float)(samplesPerAxis * samplesPerAxis);
            Assert.Greater(coverage, 0.02f, "Default tuning produced almost no rocky zones.");
            Assert.Less(coverage, 0.5f, "Default tuning produced near-uniform rocky zones — clustering intent lost.");
        }

        [Test]
        public void IsInRockyZone_DifferentSeeds_ProduceDifferentZones()
        {
            var p = PebblePlacementParams.Default;
            int differing = 0;

            for (int x = 0; x < 32; x++)
            for (int z = 0; z < 32; z++)
            {
                var pos = new float2(x * 13.7f, z * 17.3f);
                bool a = PebblePlacementAlgorithm.IsInRockyZone(pos, 1u, p.ZoneNoiseFrequency, p.ZoneThreshold);
                bool b = PebblePlacementAlgorithm.IsInRockyZone(pos, 999u, p.ZoneNoiseFrequency, p.ZoneThreshold);
                if (a != b)
                {
                    differing++;
                }
            }

            Assert.Greater(differing, 0, "World seed must shift the rocky-zone layout.");
        }

        [Test]
        public void DefaultParams_AreSelfConsistent()
        {
            var p = PebblePlacementParams.Default;

            Assert.Greater(p.MinSpacing, 0f);
            Assert.Greater(p.ZoneNoiseFrequency, 0f);
            Assert.That(p.InZoneProbability, Is.InRange(0f, 1f));
            Assert.That(p.MinGroundNormalY, Is.InRange(0f, 1f));
            Assert.LessOrEqual(p.MinUniformScale, p.MaxUniformScale);
            Assert.GreaterOrEqual((int)p.VariantCount, 1);
        }
    }
}
