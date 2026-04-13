using System;
using NUnit.Framework;
using Unity.Mathematics;
using DOTS.Terrain.Rocks;
using DOTS.Terrain.Trees;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SurfaceScatterJitterRegressionTests
    {
        private const uint SampleSeed = 12345u;

        [Test]
        public void RockCandidateJitter_ZAxis_UsesSignedRangeAndHealthySpan()
        {
            MeasureJitterExtents(
                (chunkCoord, cellX, cellZ) => RockPlacementAlgorithm.CandidateJitter(SampleSeed, chunkCoord, cellX, cellZ),
                RockPlacementAlgorithm.CandidateGridSize,
                out float xMin,
                out float xMax,
                out float zMin,
                out float zMax);

            AssertHealthySignedRange("Rock", RockPlacementAlgorithm.CellJitterRadius, xMin, xMax, zMin, zMax);
        }

        [Test]
        public void TreeCandidateJitter_ZAxis_UsesSignedRangeAndHealthySpan()
        {
            MeasureJitterExtents(
                (chunkCoord, cellX, cellZ) => TreePlacementAlgorithm.CandidateJitter(SampleSeed, chunkCoord, cellX, cellZ),
                TreePlacementAlgorithm.CandidateGridSize,
                out float xMin,
                out float xMax,
                out float zMin,
                out float zMax);

            AssertHealthySignedRange("Tree", TreePlacementAlgorithm.CellJitterRadius, xMin, xMax, zMin, zMax);
        }

        private static void MeasureJitterExtents(
            Func<int3, int, int, float2> sampleJitter,
            int candidateGridSize,
            out float xMin,
            out float xMax,
            out float zMin,
            out float zMax)
        {
            xMin = float.MaxValue;
            xMax = float.MinValue;
            zMin = float.MaxValue;
            zMax = float.MinValue;

            // Sample across many chunk/cell combinations to reveal distribution pathologies.
            for (int chunkZ = 0; chunkZ < 16; chunkZ++)
            for (int chunkX = 0; chunkX < 16; chunkX++)
            {
                var chunkCoord = new int3(chunkX, 0, chunkZ);
                for (int cellZ = 0; cellZ < candidateGridSize; cellZ++)
                for (int cellX = 0; cellX < candidateGridSize; cellX++)
                {
                    var jitter = sampleJitter(chunkCoord, cellX, cellZ);
                    xMin = math.min(xMin, jitter.x);
                    xMax = math.max(xMax, jitter.x);
                    zMin = math.min(zMin, jitter.y);
                    zMax = math.max(zMax, jitter.y);
                }
            }
        }

        private static void AssertHealthySignedRange(
            string family,
            float radius,
            float xMin,
            float xMax,
            float zMin,
            float zMax)
        {
            float xSpan = xMax - xMin;
            float zSpan = zMax - zMin;

            Assert.Less(
                xMin,
                -0.25f * radius,
                $"{family} X jitter should include meaningful negative values. min={xMin:F4} radius={radius:F3}");
            Assert.Greater(
                xMax,
                0.25f * radius,
                $"{family} X jitter should include meaningful positive values. max={xMax:F4} radius={radius:F3}");

            Assert.Less(
                zMin,
                -0.25f * radius,
                $"{family} Z jitter should include meaningful negative values. min={zMin:F4} radius={radius:F3}");
            Assert.Greater(
                zMax,
                0.25f * radius,
                $"{family} Z jitter should include meaningful positive values. max={zMax:F4} radius={radius:F3}");

            Assert.Greater(
                zSpan,
                xSpan * 0.5f,
                $"{family} Z jitter span should be comparable to X span. xSpan={xSpan:F4} zSpan={zSpan:F4}");
        }
    }
}
