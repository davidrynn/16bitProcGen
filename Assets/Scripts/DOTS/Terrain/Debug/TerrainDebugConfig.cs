using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Debug-only singleton component to control deterministic terrain streaming and seam validation.
    /// Only applies when Enabled == true. Default runtime behavior unchanged when disabled.
    /// </summary>
    public struct TerrainDebugConfig : IComponentData
    {
        /// <summary>
        /// Master switch for debug behavior.
        /// </summary>
        public bool Enabled;

        /// <summary>
        /// If true, do not spawn/despawn chunks due to player movement.
        /// Use FixedCenterChunk and StreamingRadiusInChunks instead.
        /// </summary>
        public bool FreezeStreaming;

        /// <summary>
        /// When FreezeStreaming is true, use this as the center chunk coordinate.
        /// </summary>
        public int2 FixedCenterChunk;

        /// <summary>
        /// When FreezeStreaming is true, spawn chunks in this radius around FixedCenterChunk.
        /// </summary>
        public int StreamingRadiusInChunks;

        /// <summary>
        /// Maximum allowed density difference at chunk borders before logging a seam mismatch.
        /// </summary>
        public float SeamEpsilon;

        /// <summary>
        /// If true, log seam mismatches detected by TerrainSeamValidatorSystem.
        /// </summary>
        public bool EnableSeamLogging;

        public static TerrainDebugConfig Default => new TerrainDebugConfig
        {
            Enabled = false,
            FreezeStreaming = false,
            FixedCenterChunk = int2.zero,
            StreamingRadiusInChunks = 2,
            SeamEpsilon = 0.001f,
            EnableSeamLogging = true
        };
    }
}
