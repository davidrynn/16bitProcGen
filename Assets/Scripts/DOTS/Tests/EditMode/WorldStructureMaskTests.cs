using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// H3 guards for the flatten mask <c>M(x,z)</c> and the masked field <c>H = A(r)·ridgedFBM·M</c>
    /// (WORLD_STRUCTURE_SPEC.md §4.1). The load-bearing ones are §5.4 (the vista sightline stays flat
    /// so macro relief can never occlude the spawn→hero view) and §5.3 (near-field |H| fits the ~16u
    /// chunk slab). Mirrors <c>WorldStructure.hlsl:WorldMacroMask</c> (C#-only, per the pair contract).
    /// </summary>
    [TestFixture]
    public class WorldStructureMaskTests
    {
        private static WorldStructureConstants DefaultConstants(uint seed = 12345u) => new WorldStructureConstants
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

        private static NativeArray<WorldStructureMaskRegion> VistaCorridor()
            => new NativeArray<WorldStructureMaskRegion>(
                new[] { WorldStructureMask.DefaultVistaCorridor }, Allocator.Temp);

        [Test]
        public void Mask_OnCorridorSegment_IsFlat()
        {
            using var regions = VistaCorridor();
            // Sampled straight down the sightline (0, z) through and past the hero at (0, 900).
            for (float z = 0f; z <= 1000f; z += 50f)
            {
                float m = WorldStructureMask.Evaluate(new float2(0f, z), regions);
                Assert.AreEqual(0f, m, 1e-4f, $"Mask must be 0 (flat) on the corridor segment at z={z}.");
            }
        }

        [Test]
        public void Mask_FarFromCorridor_IsFullRelief()
        {
            using var regions = VistaCorridor();
            // Well outside radius+feather (330u) of the (0,z) segment → full macro relief.
            foreach (var p in new[] { new float2(2000f, 0f), new float2(-1500f, 500f), new float2(800f, 2000f) })
            {
                Assert.AreEqual(1f, WorldStructureMask.Evaluate(p, regions), 1e-4f,
                    $"Mask must be 1 (full relief) far from the corridor at {p}.");
            }
        }

        [Test]
        public void Mask_IsInUnitRange()
        {
            using var regions = VistaCorridor();
            for (int x = -600; x <= 600; x += 40)
            for (int z = -200; z <= 1200; z += 40)
            {
                float m = WorldStructureMask.Evaluate(new float2(x, z), regions);
                Assert.GreaterOrEqual(m, 0f, $"mask < 0 at ({x},{z})");
                Assert.LessOrEqual(m, 1f + 1e-5f, $"mask > 1 at ({x},{z})");
            }
        }

        [Test]
        public void Corridor_FlattensH_AlongSightline()
        {
            // §5.4: |H| ≤ a few units along the spawn→hero sightline so no macro ridge occludes it.
            // Un-masked H peaks near ~16u out at the hero distance (A(r) ramp), so this is a real guard.
            var c = DefaultConstants();
            using var regions = VistaCorridor();
            const float fewUnits = 4f;

            for (float z = 0f; z <= 1100f; z += 25f)
            {
                float h = math.abs(WorldStructureMask.SampleWithMask(new float2(0f, z), c, regions));
                Assert.LessOrEqual(h, fewUnits, $"Masked |H| along the sightline exceeded {fewUnits}u at z={z}.");
            }
        }

        [Test]
        public void NearField_MaskedH_FitsChunkSlabBudget()
        {
            // §5.3 / Q1: near-field |H| (masked) + the existing ±4u surface noise must fit the ~16u slab.
            const float slab = 16f;
            const float surfaceNoiseAmp = 4f;
            var c = DefaultConstants();
            using var regions = VistaCorridor();

            float maxH = 0f;
            for (int x = -300; x <= 300; x += 11)
            for (int z = -300; z <= 300; z += 11)
            {
                float h = math.abs(WorldStructureMask.SampleWithMask(new float2(x, z), c, regions));
                maxH = math.max(maxH, h);
            }

            Assert.LessOrEqual(maxH + surfaceNoiseAmp, slab,
                $"Near-field masked |H| ({maxH:F2}u) + surface noise ({surfaceNoiseAmp}u) must fit the {slab}u slab.");
        }

        [Test]
        public void SampleWithMask_IsDeterministic()
        {
            var c = DefaultConstants();
            using var regions = VistaCorridor();
            foreach (var p in new[] { new float2(50f, 300f), new float2(400f, 400f), new float2(0f, 900f) })
            {
                float a = WorldStructureMask.SampleWithMask(p, c, regions);
                float b = WorldStructureMask.SampleWithMask(p, c, regions);
                Assert.AreEqual(a, b, 0f, $"Masked H must be a pure function at {p}.");
            }
        }

        [Test]
        public void EmptyRegions_MeanFullRelief()
        {
            using var none = new NativeArray<WorldStructureMaskRegion>(0, Allocator.Temp);
            Assert.AreEqual(1f, WorldStructureMask.Evaluate(new float2(0f, 900f), none), 0f,
                "No regions → mask 1 (unmasked H).");
        }
    }
}
