using NUnit.Framework;
using Unity.Mathematics;
using DOTS.Terrain.LOD;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainLodTests
    {
        private static TerrainLodSettings MakeSettings(
            float lod0Max = 2f, float lod1Max = 4f, float lod2Max = 6f, float hysteresis = 0.5f, bool useStreamingAsCullBoundary = false)
        {
            var s = TerrainLodSettings.Default;
            s.Lod0MaxDist = lod0Max;
            s.Lod1MaxDist = lod1Max;
            s.Lod2MaxDist = lod2Max;
            s.HysteresisChunks = hysteresis;
            s.UseStreamingAsCullBoundary = useStreamingAsCullBoundary;
            return s;
        }

        // --- LOD ring selection ---

        [Test]
        public void LodSelection_Lod0_WhenWithinLod0Radius()
        {
            var settings = MakeSettings();
            Assert.AreEqual(0, TerrainChunkLodSelectionSystem.ComputeTargetLod(1f, settings, 0));
        }

        [Test]
        public void LodSelection_Lod0_AtExactBoundary()
        {
            var settings = MakeSettings();
            Assert.AreEqual(0, TerrainChunkLodSelectionSystem.ComputeTargetLod(2f, settings, 0));
        }

        [Test]
        public void LodSelection_Lod1_BeyondLod0Radius()
        {
            var settings = MakeSettings(hysteresis: 0f);
            // dist=3 > Lod0MaxDist=2, <= Lod1MaxDist=4, currentTarget=0 → demote to 1
            Assert.AreEqual(1, TerrainChunkLodSelectionSystem.ComputeTargetLod(3f, settings, 0));
        }

        [Test]
        public void LodSelection_Lod2_BeyondLod1Radius()
        {
            var settings = MakeSettings(hysteresis: 0f);
            Assert.AreEqual(2, TerrainChunkLodSelectionSystem.ComputeTargetLod(5f, settings, 1));
        }

        [Test]
        public void LodSelection_Lod3_BeyondLod2Radius()
        {
            var settings = MakeSettings(hysteresis: 0f);
            Assert.AreEqual(3, TerrainChunkLodSelectionSystem.ComputeTargetLod(7f, settings, 2));
        }

        [Test]
        public void LodSelection_ClampsToLod2_WhenStreamingCullBoundaryEnabled()
        {
            var settings = MakeSettings(hysteresis: 0f, useStreamingAsCullBoundary: true);
            Assert.AreEqual(2, TerrainChunkLodSelectionSystem.ComputeTargetLod(99f, settings, 2));
        }

        // --- Hysteresis ---

        [Test]
        public void LodHysteresis_NoDemotion_WithinBand()
        {
            var settings = MakeSettings(lod0Max: 2f, hysteresis: 0.5f);
            // dist=2.3 is past Lod0MaxDist but within Lod0MaxDist + hysteresis (2.5)
            // currentTarget=0 → should stay 0
            Assert.AreEqual(0, TerrainChunkLodSelectionSystem.ComputeTargetLod(2.3f, settings, 0));
        }

        [Test]
        public void LodHysteresis_Demotion_BeyondBand()
        {
            var settings = MakeSettings(lod0Max: 2f, hysteresis: 0.5f);
            // dist=2.6 is past Lod0MaxDist + hysteresis (2.5) → demote to 1
            Assert.AreEqual(1, TerrainChunkLodSelectionSystem.ComputeTargetLod(2.6f, settings, 0));
        }

        [Test]
        public void LodHysteresis_NoDemotion_Lod1ToLod2_WithinBand()
        {
            var settings = MakeSettings(lod1Max: 4f, hysteresis: 0.5f);
            // dist=4.3 is past Lod1MaxDist but within Lod1MaxDist + hysteresis (4.5), currentTarget=1
            Assert.AreEqual(1, TerrainChunkLodSelectionSystem.ComputeTargetLod(4.3f, settings, 1));
        }

        // --- Promotion is immediate ---

        [Test]
        public void LodPromotion_Immediate_NoHysteresis()
        {
            var settings = MakeSettings();
            // currentTarget=2, dist=1 (within LOD0 ring) → promote immediately to 0
            Assert.AreEqual(0, TerrainChunkLodSelectionSystem.ComputeTargetLod(1f, settings, 2));
        }

        [Test]
        public void LodPromotion_Lod1ToLod0_Immediate()
        {
            var settings = MakeSettings();
            // currentTarget=1, dist=1.5 (within LOD0) → promote to 0
            Assert.AreEqual(0, TerrainChunkLodSelectionSystem.ComputeTargetLod(1.5f, settings, 1));
        }

        // --- Settings helpers ---

        [Test]
        public void Settings_GetResolution_ReturnsCorrectPerLod()
        {
            var s = TerrainLodSettings.Default;
            Assert.AreEqual(s.Lod0Resolution, s.GetResolution(0));
            Assert.AreEqual(s.Lod1Resolution, s.GetResolution(1));
            Assert.AreEqual(s.Lod2Resolution, s.GetResolution(2));
        }

        [Test]
        public void Settings_GetVoxelSize_ReturnsCorrectPerLod()
        {
            var s = TerrainLodSettings.Default;
            Assert.AreEqual(s.Lod0VoxelSize, s.GetVoxelSize(0), 1e-5f);
            Assert.AreEqual(s.Lod1VoxelSize, s.GetVoxelSize(1), 1e-5f);
            Assert.AreEqual(s.Lod2VoxelSize, s.GetVoxelSize(2), 1e-5f);
        }

        [Test]
        public void Settings_GetResolution_UnknownLod_FallsBackToLod0()
        {
            var s = TerrainLodSettings.Default;
            Assert.AreEqual(s.Lod0Resolution, s.GetResolution(-1));
            Assert.AreEqual(s.Lod0Resolution, s.GetResolution(99));
        }
    }
}
