using Unity.Entities;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Data-driven placement tuning for the pebble-cluster family, registered as a
    /// singleton by <see cref="PebbleVisualBootstrap"/>. This is the per-family
    /// frequency/distribution knob set the rock/tree families lack (their constants
    /// are compile-time — see TICKETS R2, which ports this pattern back to them).
    /// Changing values re-tunes the deterministic noise/hash pipeline, so edits
    /// take effect on chunk (re)generation — not as a per-frame input.
    /// </summary>
    public struct PebblePlacementParams : IComponentData
    {
        /// <summary>World-space frequency of the rocky-zone mask noise. Lower = larger zones.</summary>
        public float ZoneNoiseFrequency;

        /// <summary>snoise value in [-1,1] above which a candidate is inside a rocky zone.
        /// Higher = rarer zones. Default targets ~15-20% area coverage per the biome spec.</summary>
        public float ZoneThreshold;

        /// <summary>Accept probability for candidates inside a rocky zone. Zero outside zones —
        /// that asymmetry is what makes the distribution "clustered, not uniform".</summary>
        public float InZoneProbability;

        /// <summary>Minimum distance between accepted cluster origins; also sets candidate cell size.</summary>
        public float MinSpacing;

        /// <summary>Reject slopes: minimum surface-normal Y (matches rock family default 0.70).</summary>
        public float MinGroundNormalY;

        public float MinUniformScale;
        public float MaxUniformScale;

        /// <summary>Number of mesh variants placement distributes across (render config may carry fewer; render mods by actual count).</summary>
        public byte VariantCount;

        /// <summary>
        /// Defaults targeting the Windswept Colossus Plains spec: 10-30 clusters/ha
        /// concentrated inside rocky zones covering ~15-20% of area.
        /// </summary>
        public static PebblePlacementParams Default => new PebblePlacementParams
        {
            ZoneNoiseFrequency = 0.012f,
            ZoneThreshold      = 0.55f,
            InZoneProbability  = 0.15f,
            MinSpacing         = 3.0f,
            MinGroundNormalY   = 0.70f,
            MinUniformScale    = 0.8f,
            MaxUniformScale    = 1.3f,
            VariantCount       = 3,
        };
    }
}
