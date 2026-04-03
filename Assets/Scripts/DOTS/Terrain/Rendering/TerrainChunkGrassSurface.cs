using Unity.Entities;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Marks a terrain chunk entity as a grass-bearing surface and carries per-chunk
    /// grass configuration consumed by <see cref="GrassChunkGenerationSystem"/>.
    ///
    /// TAGGING RULES:
    ///   Only topmost solid-layer chunks should receive this component.
    ///   Underground chunks, cave ceilings, and wall-only faces must never be tagged —
    ///   they waste blade budget and draw calls.
    ///
    /// CURRENT STATUS (Phase 1 / POC):
    ///   Tags added at runtime via "DOTS Terrain > [POC] Tag All Chunks as Grass Surface".
    ///   Production intent: assigned during terrain generation when a chunk column is
    ///   identified as the topmost layer, using biome data to determine surface type.
    ///
    /// GRASS TYPES:
    ///   GrassType == 0 : Standard GPU-instanced blade system (this spec, implemented).
    ///   GrassType == 1 : Sparse clump variant (reserved; not yet implemented).
    ///   Any chunk with GrassType != 0 is skipped until that variant is added.
    /// </summary>
    public struct TerrainChunkGrassSurface : IComponentData
    {
        /// <summary>
        /// Grass density for this chunk. Range 0..1.
        ///   0 = no blades rendered (chunk is bare).
        ///   1 = maximum density (BladesPerSqMeter × surface area, capped at MaxBladesPerChunk).
        /// Driven by biome, altitude, erosion, and player modification in future phases.
        /// </summary>
        public float Density;

        /// <summary>
        /// Index into <c>GrassSystemSettings.Biomes[]</c>.
        /// Controls colour, height range, density multiplier, and wind strength for this chunk.
        /// 0 = default biome.
        /// </summary>
        public int BiomeTypeId;

        /// <summary>
        /// 0 = standard instanced blades (Phase 1).
        /// 1 = sparse clumps (reserved, Phase 4).
        /// </summary>
        public byte GrassType;

        /// <summary>Full-density, default biome, standard blade type.</summary>
        public static TerrainChunkGrassSurface Default => new TerrainChunkGrassSurface
        {
            Density     = 1f,
            BiomeTypeId = 0,
            GrassType   = 0,
        };
    }
}
