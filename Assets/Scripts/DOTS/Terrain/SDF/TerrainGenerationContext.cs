using Unity.Entities;

namespace DOTS.Terrain
{
    /// <summary>
    /// Singleton component providing world-seed and generation versioning to all sampling systems.
    /// WorldSeed flows through to SdLayeredGround and tree placement for deterministic output.
    /// </summary>
    public struct TerrainGenerationContext : IComponentData
    {
        public uint WorldSeed;
        /// <summary>
        /// Increment to signal that the algorithm has changed so downstream systems can invalidate caches.
        /// Start at 1.
        /// </summary>
        public uint GenerationVersion;
        /// <summary>
        /// Constant vertical bias applied to all terrain. Replaces the old NoiseValue semantic.
        /// </summary>
        public float GlobalHeightOffset;
    }
}
