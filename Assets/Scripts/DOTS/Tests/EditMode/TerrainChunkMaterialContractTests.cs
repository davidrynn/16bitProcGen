using NUnit.Framework;
using UnityEngine;
using DOTS.Terrain.Rendering;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// V9 P3 asset-contract tests (ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §6a): the terrain
    /// chunk material must be the project-owned palette-consumer shader, not a texture-lit
    /// material. The visual behavior lives in TerrainLit.shader (not NUnit-testable); these
    /// guard the asset wiring that a scene swap or fallback could silently regress.
    /// </summary>
    [TestFixture]
    public class TerrainChunkMaterialContractTests
    {
        // GetOrLoad caches and other fixtures inject OverrideSettings — reset on both sides so
        // this fixture reads the real Resources asset and leaves no state behind.
        [SetUp]
        public void SetUp() => TerrainChunkRenderSettingsProvider.ResetCache();

        [TearDown]
        public void TearDown() => TerrainChunkRenderSettingsProvider.ResetCache();

        [Test]
        public void ChunkMaterial_UsesTerrainLitPaletteShader()
        {
            var settings = TerrainChunkRenderSettingsProvider.GetOrLoad();

            Assert.IsNotNull(settings, "TerrainChunkRenderSettings missing from Resources/Terrain.");
            Assert.IsNotNull(settings.ChunkMaterial, "Terrain chunk material not assigned.");
            Assert.AreEqual("Terrain/TerrainLit", settings.ChunkMaterial.shader.name,
                "Terrain chunks must render the palette-consumer shader (V9 P3). A texture-lit " +
                "material here is dead weight: Surface Nets meshes have no UV channel, so texture " +
                "samples read a single texel — see ATMOSPHERE_COLOR_AUTHORITY_SPEC.md §6a.");
        }

        [Test]
        public void ChunkMaterial_NoiseDialsMatchGroundDiscDefaults()
        {
            var settings = TerrainChunkRenderSettingsProvider.GetOrLoad();
            var material = settings.ChunkMaterial;

            // The disc↔terrain seam only disappears if both surfaces mix the shared world-space
            // noise with the same scale/threshold (GroundNoise.hlsl contract). These mirror the
            // GroundPlaneImpostor.shader property defaults.
            Assert.AreEqual(0.004f, material.GetFloat("_NoiseScale"), 1e-5f,
                "Terrain _NoiseScale must match the ground disc or seam patches misalign.");
            Assert.AreEqual(0.6f, material.GetFloat("_RockThreshold"), 1e-5f,
                "Terrain _RockThreshold must match the ground disc or seam patches misalign.");

            // V17 P1 macro luminance dials (GROUND_PLANE_IMPOSTOR_SPEC.md §12.2): same parity
            // rule — the macro octave multiplies both surfaces' post-mix color, so a scale or
            // strength drift shows up as a luminance step at the ~180u seam.
            Assert.AreEqual(0.0007f, material.GetFloat("_MacroNoiseScale"), 1e-6f,
                "Terrain _MacroNoiseScale must match the ground disc or macro luminance " +
                "patches step at the seam (V17 P1).");
            Assert.AreEqual(0.08f, material.GetFloat("_MacroStrength"), 1e-5f,
                "Terrain _MacroStrength must match the ground disc or macro luminance " +
                "patches step at the seam (V17 P1).");
        }
    }
}
