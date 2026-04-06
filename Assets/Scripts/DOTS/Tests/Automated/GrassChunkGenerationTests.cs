using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DOTS.Terrain.Rendering;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// EditMode tests for <see cref="GrassBladeScatter"/> — the pure blade-placement logic
    /// used by <see cref="GrassChunkGenerationSystem"/>.
    ///
    /// All tests create their own NativeArrays and dispose them in TearDown so Unity's
    /// leak detection does not report false positives.
    /// </summary>
    [TestFixture]
    public class GrassChunkGenerationTests
    {
        // Single triangle in XZ plane (y=0), area = 0.5. Winding must give +Y normal so
        // GrassBladeScatter's upward-facing filter (normal.y >= 0.4) accepts it — order
        // (0,1,2) yields -Y and zero blades in Scatter.
        private static readonly float3[] TriVerts  = { float3.zero, new float3(1,0,0), new float3(0,0,1) };
        private static readonly int[]    TriIdx    = { 0, 2, 1 };

        private NativeArray<float3>        _verts;
        private NativeArray<int>           _idx;
        private NativeList<GrassBladeData> _output;

        private static GrassBiomeParams DefaultBiome => new GrassBiomeParams
        {
            BaseColor          = new float3(0.3f, 0.6f, 0.2f),
            DensityMultiplier  = 1f,
            MinBladeHeight     = 0.15f,
            MaxBladeHeight     = 0.45f,
            ColorNoiseScale    = 0.1f,
        };

        [SetUp]
        public void SetUp()
        {
            _verts  = new NativeArray<float3>(TriVerts, Allocator.Temp);
            _idx    = new NativeArray<int>(TriIdx, Allocator.Temp);
            _output = new NativeList<GrassBladeData>(64, Allocator.Temp);
        }

        [TearDown]
        public void TearDown()
        {
            if (_verts.IsCreated)  _verts.Dispose();
            if (_idx.IsCreated)    _idx.Dispose();
            if (_output.IsCreated) _output.Dispose();
        }

        // ── ComputeBladeCount ──────────────────────────────────────────────────────────

        [Test]
        public void ComputeBladeCount_RespectsMaxBladesPerChunk()
        {
            int count = GrassBladeScatter.ComputeBladeCount(
                surfaceArea: 1000f, bladesPerSqMeter: 100f,
                density: 1f, biomeDensityMultiplier: 1f, maxBlades: 512);

            Assert.LessOrEqual(count, 512);
        }

        [Test]
        public void ComputeBladeCount_ScalesWithDensity()
        {
            int full = GrassBladeScatter.ComputeBladeCount(100f, 4f, 1.0f, 1f, 10000);
            int half = GrassBladeScatter.ComputeBladeCount(100f, 4f, 0.5f, 1f, 10000);

            // Allow ±10% for integer rounding
            Assert.AreEqual(full, half * 2, full * 0.1f + 1f,
                "Half density should produce approximately half as many blades.");
        }

        [Test]
        public void ComputeBladeCount_ZeroAtDensityZero()
        {
            int count = GrassBladeScatter.ComputeBladeCount(500f, 8f, 0f, 1f, 4096);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void ComputeBladeCount_BiomeDensityMultiplierApplied()
        {
            int full   = GrassBladeScatter.ComputeBladeCount(100f, 4f, 1f, 1.0f, 10000);
            int sparse = GrassBladeScatter.ComputeBladeCount(100f, 4f, 1f, 0.5f, 10000);

            Assert.AreEqual(full, sparse * 2, full * 0.1f + 1f,
                "0.5 DensityMultiplier should halve the blade count.");
        }

        // ── Scatter ───────────────────────────────────────────────────────────────────

        [Test]
        public void Scatter_ProducesRequestedBladeCount()
        {
            GrassBladeScatter.Scatter(_verts, _idx, 20, DefaultBiome, seed: 42, _output);
            // Allow ±2 for rounding across triangles
            Assert.AreEqual(20, _output.Length, 2f, "Scatter should produce approximately the requested blade count.");
        }

        [Test]
        public void Scatter_ZeroCount_ProducesNoOutput()
        {
            GrassBladeScatter.Scatter(_verts, _idx, 0, DefaultBiome, seed: 1, _output);
            Assert.AreEqual(0, _output.Length);
        }

        [Test]
        public void Scatter_BladePositions_WithinTriangleBounds()
        {
            GrassBladeScatter.Scatter(_verts, _idx, 50, DefaultBiome, seed: 99, _output);

            // Triangle verts: (0,0,0), (1,0,0), (0,0,1) — AABB: x[0..1], y[0..0], z[0..1]
            foreach (var blade in _output)
            {
                Assert.GreaterOrEqual(blade.WorldPosition.x, -0.001f, "X below triangle min");
                Assert.LessOrEqual(blade.WorldPosition.x, 1.001f, "X above triangle max");
                Assert.GreaterOrEqual(blade.WorldPosition.z, -0.001f, "Z below triangle min");
                Assert.LessOrEqual(blade.WorldPosition.z, 1.001f, "Z above triangle max");
                // x + z <= 1 for this specific triangle (half-space check)
                Assert.LessOrEqual(blade.WorldPosition.x + blade.WorldPosition.z, 1.01f,
                    "Position outside triangle hypotenuse");
            }
        }

        [Test]
        public void Scatter_IsDeterministic()
        {
            var output2 = new NativeList<GrassBladeData>(64, Allocator.Temp);
            try
            {
                GrassBladeScatter.Scatter(_verts, _idx, 30, DefaultBiome, seed: 77, _output);
                GrassBladeScatter.Scatter(_verts, _idx, 30, DefaultBiome, seed: 77, output2);

                Assert.AreEqual(_output.Length, output2.Length, "Same seed must produce same count.");
                for (int i = 0; i < _output.Length; i++)
                {
                    Assert.AreEqual(_output[i].WorldPosition.x, output2[i].WorldPosition.x, 1e-5f,
                        $"Position mismatch at blade {i}");
                    Assert.AreEqual(_output[i].Height, output2[i].Height, 1e-5f,
                        $"Height mismatch at blade {i}");
                }
            }
            finally
            {
                output2.Dispose();
            }
        }

        [Test]
        public void Scatter_BladeHeights_WithinBiomeRange()
        {
            GrassBladeScatter.Scatter(_verts, _idx, 40, DefaultBiome, seed: 5, _output);
            foreach (var blade in _output)
            {
                Assert.GreaterOrEqual(blade.Height, DefaultBiome.MinBladeHeight - 1e-5f,
                    "Height below MinBladeHeight");
                Assert.LessOrEqual(blade.Height, DefaultBiome.MaxBladeHeight + 1e-5f,
                    "Height above MaxBladeHeight");
            }
        }

        [Test]
        public void Scatter_ColorTint_WithinBiomeNoiseRange()
        {
            GrassBladeScatter.Scatter(_verts, _idx, 40, DefaultBiome, seed: 13, _output);
            float margin = DefaultBiome.ColorNoiseScale + 1e-4f;
            foreach (var blade in _output)
            {
                for (int ch = 0; ch < 3; ch++)
                {
                    float tintCh = ch == 0 ? blade.ColorTint.x
                                 : ch == 1 ? blade.ColorTint.y
                                 :           blade.ColorTint.z;
                    float baseCh = ch == 0 ? DefaultBiome.BaseColor.x
                                 : ch == 1 ? DefaultBiome.BaseColor.y
                                 :           DefaultBiome.BaseColor.z;
                    float lo = math.clamp(baseCh - DefaultBiome.ColorNoiseScale, 0f, 1f);
                    float hi = math.clamp(baseCh + DefaultBiome.ColorNoiseScale, 0f, 1f);
                    Assert.GreaterOrEqual(tintCh, lo - 1e-4f, $"Channel {ch} below noise floor");
                    Assert.LessOrEqual(tintCh, hi + 1e-4f, $"Channel {ch} above noise ceiling");
                }
            }
        }

        [Test]
        public void Scatter_DifferentSeeds_ProduceDifferentResults()
        {
            var output2 = new NativeList<GrassBladeData>(64, Allocator.Temp);
            try
            {
                GrassBladeScatter.Scatter(_verts, _idx, 20, DefaultBiome, seed: 1,  _output);
                GrassBladeScatter.Scatter(_verts, _idx, 20, DefaultBiome, seed: 99, output2);

                bool anyDifferent = false;
                int  compareCount = math.min(_output.Length, output2.Length);
                for (int i = 0; i < compareCount && !anyDifferent; i++)
                    anyDifferent = math.any(_output[i].WorldPosition != output2[i].WorldPosition);

                Assert.IsTrue(anyDifferent, "Different seeds should produce different blade positions.");
            }
            finally
            {
                output2.Dispose();
            }
        }
    }
}
