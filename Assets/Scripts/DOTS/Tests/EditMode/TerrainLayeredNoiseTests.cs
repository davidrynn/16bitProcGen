using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainLayeredNoiseTests
    {
        // ── Test 1: Determinism ────────────────────────────────────────────────

        [Test]
        public void SdLayeredGround_Determinism_SameSeedAndPositionReturnsSameResult()
        {
            var settings = PlainSettings();
            var pos = new float3(37.5f, 2f, 52.3f);
            uint seed = 12345u;

            var r1 = SDFMath.SdLayeredGround(pos, settings, seed);
            var r2 = SDFMath.SdLayeredGround(pos, settings, seed);

            Assert.AreEqual(r1, r2, 1e-5f, "Identical inputs must produce identical output on every call.");
        }

        [Test]
        public void SdLayeredGround_Determinism_DifferentSeedsDifferentResults()
        {
            var settings = PlainSettings();
            var pos = new float3(37.5f, 2f, 52.3f);

            var r1 = SDFMath.SdLayeredGround(pos, settings, 12345u);
            var r2 = SDFMath.SdLayeredGround(pos, settings, 99999u);

            Assert.AreNotEqual(r1, r2, "Different seeds should produce different density values.");
        }

        // ── Test 2: Seam continuity ───────────────────────────────────────────

        [Test]
        public void SdLayeredGround_SeamContinuity_PureWorldSpaceProducesSameValueFromAnyChunkContext()
        {
            // The function takes only worldPos — no chunk-origin parameter.
            // Validate seam continuity by constructing equal world points from two different
            // chunk-origin/local-coordinate contexts and asserting identical density.
            var settings = PlainSettings();
            uint seed = 12345u;
            const float chunkStride = 15f;

            // (originA, localA, originB, localB) pairs that resolve to the same world point
            var seamCases = new (float3 originA, float3 localA, float3 originB, float3 localB)[]
            {
                (new float3(0f, 0f, 0f), new float3(chunkStride, 0.25f, 7f), new float3(chunkStride, 0f, 0f), new float3(0f, 0.25f, 7f)),
                (new float3(0f, 0f, 0f), new float3(7f, 0.5f, chunkStride), new float3(0f, 0f, chunkStride), new float3(7f, 0.5f, 0f)),
                (new float3(0f, 0f, 0f), new float3(chunkStride, 0.75f, chunkStride), new float3(chunkStride, 0f, chunkStride), new float3(0f, 0.75f, 0f)),
                (new float3(chunkStride, 0f, 0f), new float3(chunkStride, 1.0f, chunkStride), new float3(2f * chunkStride, 0f, chunkStride), new float3(0f, 1.0f, 0f)),
            };

            foreach (var seamCase in seamCases)
            {
                var worldA = seamCase.originA + seamCase.localA;
                var worldB = seamCase.originB + seamCase.localB;

                Assert.AreEqual(worldA.x, worldB.x, 1e-6f);
                Assert.AreEqual(worldA.y, worldB.y, 1e-6f);
                Assert.AreEqual(worldA.z, worldB.z, 1e-6f);

                var s1 = SDFMath.SdLayeredGround(worldA, settings, seed);
                var s2 = SDFMath.SdLayeredGround(worldB, settings, seed);
                Assert.AreEqual(s1, s2, 1e-4f,
                    $"Seam world position {worldA} must return identical values across chunk contexts.");
            }
        }

        // ── Test 3: Plains flatness ───────────────────────────────────────────

        [Test]
        public void SdLayeredGround_PlainsFlatness_HeightStdDevUnderBound()
        {
            // Sample 100 evenly-spaced heights across a 45×45 world-unit area (3 chunks wide).
            // Standard deviation of heights must be < 3.5f for plains character.
            var settings = PlainSettings();
            uint seed = 12345u;
            const int gridN = 10;           // 10×10 = 100 samples
            const float areaSize = 45f;
            const float maxStdDev = 3.5f;

            var heights = new float[gridN * gridN];
            float sum = 0f;
            for (int zi = 0; zi < gridN; zi++)
            for (int xi = 0; xi < gridN; xi++)
            {
                float wx = xi * (areaSize / (gridN - 1));
                float wz = zi * (areaSize / (gridN - 1));
                heights[zi * gridN + xi] = FindSurfaceHeight(wx, wz, settings, seed);
                sum += heights[zi * gridN + xi];
            }

            float mean = sum / heights.Length;
            float variance = 0f;
            foreach (var h in heights)
                variance += (h - mean) * (h - mean);
            variance /= heights.Length;
            float stdDev = math.sqrt(variance);

            Assert.Less(stdDev, maxStdDev,
                $"Plains height std dev {stdDev:F3} exceeds allowed maximum {maxStdDev}. " +
                "Reduce ElevationLowAmplitude and re-run the A7 diagnostic before tuning.");
        }

        // ── Test 4: Legacy fallback ───────────────────────────────────────────

        [Test]
        public void SDFTerrainField_LegacyFallback_MatchesDirectSdGroundCall()
        {
            const float amp = 4f, freq = 0.1f, baseH = 2f, noiseV = 0.3f;
            var field = new SDFTerrainField
            {
                BaseHeight      = baseH,
                Amplitude       = amp,
                Frequency       = freq,
                NoiseValue      = noiseV,
                UseLayeredNoise = false,
            };

            var testPositions = new float3[]
            {
                new float3(0f, 0f, 0f),
                new float3(5f, 3f, 10f),
                new float3(-7f, 1.5f, 20f),
            };

            using var emptyEdits = new NativeArray<SDFEdit>(0, Allocator.Temp);
            foreach (var pos in testPositions)
            {
                var fromField  = field.Sample(pos, emptyEdits);
                var fromDirect = SDFMath.SdGround(pos, amp, freq, baseH, noiseV);
                Assert.AreEqual(fromDirect, fromField, 1e-5f,
                    $"Legacy fallback at {pos} must match SDFMath.SdGround directly.");
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static TerrainFieldSettings PlainSettings() => new TerrainFieldSettings
        {
            BaseHeight             = 0f,
            ElevationLowFrequency  = 0.004f,
            ElevationLowAmplitude  = 5.0f,
            ElevationMidFrequency  = 0.018f,
            ElevationMidAmplitude  = 1.2f,
            ElevationHighFrequency = 0.07f,
            ElevationHighAmplitude = 0.25f,
            ElevationExponent      = 1.6f,
        };

        /// <summary>
        /// Binary-searches Y for the world-space surface height at (worldX, worldZ).
        /// Converges to 16-step precision across a ±20 unit window around BaseHeight.
        /// </summary>
        private static float FindSurfaceHeight(float worldX, float worldZ, TerrainFieldSettings s, uint seed)
        {
            float yLow = s.BaseHeight - 20f, yHigh = s.BaseHeight + 20f;
            for (int i = 0; i < 16; i++)
            {
                float yMid = (yLow + yHigh) * 0.5f;
                // SdLayeredGround < 0 → inside terrain (below surface) → increase lower bound
                if (SDFMath.SdLayeredGround(new float3(worldX, yMid, worldZ), s, seed) < 0f)
                    yLow = yMid;
                else
                    yHigh = yMid;
            }
            return (yLow + yHigh) * 0.5f;
        }
    }
}
