using Unity.Burst;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

namespace DOTS.Terrain
{
    /// <summary>
    /// Burst-safe collection of signed distance helpers used by the SDF terrain backend.
    /// </summary>
    [BurstCompile]
    public static class SDFMath
    {
        public static float SdSphere(float3 p, float radius)
        {
            var safeRadius = math.max(radius, 1e-5f);
            return math.length(p) - safeRadius;
        }

        public static float SdBox(float3 p, float3 halfExtents)
        {
            var q = math.abs(p) - halfExtents;
            var outside = math.max(q, float3.zero);
            var inside = math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
            return math.length(outside) + inside;
        }

        public static float SdGround(float3 p, float amplitude, float frequency, float baseHeight, float noiseValue)
        {
            // Combine a sine-based undulation with the provided noise sample to form a simple height field.
            var wave = (math.sin(p.x * frequency) + math.sin(p.z * frequency)) * 0.5f;
            var height = baseHeight + amplitude * (wave + noiseValue);
            return p.y - height;
        }

        /// <summary>
        /// Produces a stable world-space float2 offset for a given seed and layer index.
        /// Used to give each noise layer an independent, non-overlapping sample region.
        /// </summary>
        /// <remarks>
        /// Mix seed and layer with distinct primes to produce independent streams.
        /// The upper 24 bits are mapped to a [0, 500) float range.
        /// 500 world units >> any visible terrain region — prevents cross-seed correlation at even
        /// the lowest terrain frequency (0.004 → one feature per 250 units).
        /// See TERRAIN_PLAINS_NOISE_ALGORITHM.md §3 for full rationale.
        /// </remarks>
        internal static float2 SeedLayerOffset(uint seed, uint layer)
        {
            var hx = (seed ^ (layer * 2654435761u)) * 0x9e3779b9u;
            var hz = (seed ^ (layer * 1013904223u)) * 0x6c62272eu;
            // Map upper 24 bits to a [0, 500) float range.
            const float scale = 500f / 16777216f;
            return new float2((hx >> 8) * scale, (hz >> 8) * scale);
        }

        /// <summary>
        /// Deterministic layered-noise ground function. Replaces SdGround for biome-aware terrain.
        /// All sampling is in world space — never restart at chunk origin.
        /// </summary>
        /// <param name="worldPos">World-space sample position.</param>
        /// <param name="settings">Plains (or other biome) terrain field settings.</param>
        /// <param name="seed">World seed from TerrainGenerationContext.</param>
        public static float SdLayeredGround(float3 worldPos, in TerrainFieldSettings settings, uint seed)
        {
            var xz = worldPos.xz;

            // Sample three independent layers using per-layer seed offsets.
            var low  = snoise((xz + SeedLayerOffset(seed, 0u)) * settings.ElevationLowFrequency)
                       * settings.ElevationLowAmplitude;
            var mid  = snoise((xz + SeedLayerOffset(seed, 1u)) * settings.ElevationMidFrequency)
                       * settings.ElevationMidAmplitude;
            var high = snoise((xz + SeedLayerOffset(seed, 2u)) * settings.ElevationHighFrequency)
                       * settings.ElevationHighAmplitude;

            var combined = low + mid + high;

            // Redistribute elevation to shape the biome character.
            // sign(x) * pow(abs(x), exp) applied to a normalized value:
            //   exp > 1  → compresses mid-range values toward zero, widening flat areas (plains)
            //   exp = 1  → no redistribution
            //   exp < 1  → amplifies mid-range, sharpening peaks (mountains — deferred)
            // See TERRAIN_PLAINS_NOISE_ALGORITHM.md §4 for worked examples.
            var maxPossible = settings.ElevationLowAmplitude
                            + settings.ElevationMidAmplitude
                            + settings.ElevationHighAmplitude;
            maxPossible = math.max(maxPossible, 1e-5f);

            var normalized    = combined / maxPossible;                               // → [-1, 1]
            var redistributed = math.sign(normalized)
                              * math.pow(math.abs(normalized), settings.ElevationExponent);
            var elevation     = redistributed * maxPossible;

            var height = settings.BaseHeight + elevation;
            return worldPos.y - height;
        }

        public static float OpUnion(float a, float b) => math.min(a, b);

        /// <summary>
        /// Spec-compliant subtraction that carves interior points but never pushes outside samples farther away.
        /// Exterior points keep their original base distance, while interior points use max(base, -edit).
        /// </summary>
        public static float OpSubtraction(float baseDistance, float editDistance)
        {
            var subtraction = math.max(baseDistance, -editDistance);
            return baseDistance > 0f ? math.min(baseDistance, subtraction) : subtraction;
        }
    }
}
