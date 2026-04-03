using Unity.Collections;
using Unity.Mathematics;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Burst-compatible parameters derived from <c>GrassBiomeSettings</c> ScriptableObject.
    /// Passed into <see cref="GrassBladeScatter"/> so generation logic can run without
    /// managed object access (enabling future Burst job migration).
    /// </summary>
    public struct GrassBiomeParams
    {
        public float3 BaseColor;
        public float  DensityMultiplier;
        public float  MinBladeHeight;
        public float  MaxBladeHeight;
        public float  ColorNoiseScale;
    }

    /// <summary>
    /// Stateless utility for scattering grass blade instances across a triangle mesh.
    ///
    /// All methods are static and operate on <c>NativeArray</c> / <c>NativeList</c> so
    /// they are ready for a Burst job in a future phase without structural changes.
    ///
    /// Tested directly by <c>GrassChunkGenerationTests</c> in EditMode.
    /// </summary>
    public static class GrassBladeScatter
    {
        /// <summary>
        /// Computes how many blades to generate for a chunk surface.
        /// Result is always within [0, <paramref name="maxBlades"/>].
        /// </summary>
        public static int ComputeBladeCount(
            float surfaceArea,
            float bladesPerSqMeter,
            float density,
            float biomeDensityMultiplier,
            int   maxBlades)
        {
            int raw = (int)(surfaceArea * bladesPerSqMeter * density * biomeDensityMultiplier);
            return math.clamp(raw, 0, maxBlades);
        }

        /// <summary>
        /// Scatters <paramref name="bladeCount"/> grass blades across the triangle soup defined
        /// by <paramref name="vertices"/> and <paramref name="indices"/>.
        ///
        /// Blades are distributed proportionally to triangle area so dense regions of the mesh
        /// receive more blades than sparse ones. Within each triangle positions are chosen via
        /// uniform barycentric sampling. Seed is derived from chunk world position to ensure
        /// identical output across multiple calls (deterministic, no temporal drift).
        /// </summary>
        /// <param name="vertices">World-space vertex positions (Surface Nets outputs world space).</param>
        /// <param name="indices">Triangle index list; length must be a multiple of 3.</param>
        /// <param name="bladeCount">Total blades to scatter across all triangles.</param>
        /// <param name="biome">Per-biome appearance parameters.</param>
        /// <param name="seed">Deterministic seed. Pass 0 to use fallback seed 1.</param>
        /// <param name="output">Populated list; caller owns disposal.</param>
        public static void Scatter(
            NativeArray<float3>     vertices,
            NativeArray<int>        indices,
            int                     bladeCount,
            GrassBiomeParams        biome,
            uint                    seed,
            NativeList<GrassBladeData> output)
        {
            if (bladeCount <= 0 || indices.Length < 3) return;

            int triCount = indices.Length / 3;

            // Per-triangle areas for weighted distribution.
            var triAreas  = new NativeArray<float>(triCount, Allocator.Temp);
            float totalArea = 0f;

            // Only scatter on upward-facing triangles (normal.y > threshold).
            // This excludes vertical cliff faces and downward-facing surfaces so
            // blades only grow on terrain "tops", not walls or ceilings.
            const float UpwardThreshold = 0.4f;

            for (int i = 0; i < triCount; i++)
            {
                float3 a = vertices[indices[i * 3]];
                float3 b = vertices[indices[i * 3 + 1]];
                float3 c = vertices[indices[i * 3 + 2]];
                float3 normal = math.cross(b - a, c - a); // un-normalised, direction only
                float area = math.length(normal) * 0.5f;
                // Reject triangles pointing sideways or downward.
                float normalY = normal.y / math.max(math.length(normal), 1e-6f);
                triAreas[i] = normalY >= UpwardThreshold ? area : 0f;
                totalArea  += triAreas[i];
            }

            if (totalArea < 1e-6f)
            {
                triAreas.Dispose();
                return;
            }

            var rng = new Random(seed == 0 ? 1u : seed);

            for (int i = 0; i < triCount; i++)
            {
                // triAreas[i] == 0 means triangle was rejected by the upward-facing check.
                if (triAreas[i] <= 0f) continue;
                int bladesInTri = (int)math.round((triAreas[i] / totalArea) * bladeCount);
                if (bladesInTri <= 0) continue;

                float3 a = vertices[indices[i * 3]];
                float3 b = vertices[indices[i * 3 + 1]];
                float3 c = vertices[indices[i * 3 + 2]];

                for (int j = 0; j < bladesInTri; j++)
                {
                    // Uniform barycentric sampling (Osada et al. method).
                    float r1 = math.sqrt(rng.NextFloat());
                    float r2 = rng.NextFloat();
                    float3 pos = (1f - r1) * a + (r1 * (1f - r2)) * b + (r1 * r2) * c;

                    float height = math.lerp(biome.MinBladeHeight, biome.MaxBladeHeight, rng.NextFloat());

                    float facing = rng.NextFloat() * (math.PI * 2f);

                    float noise  = (rng.NextFloat() * 2f - 1f) * biome.ColorNoiseScale;
                    float3 tint  = math.clamp(biome.BaseColor + noise, 0f, 1f);

                    output.Add(new GrassBladeData
                    {
                        WorldPosition = pos,
                        Height        = height,
                        ColorTint     = tint,
                        FacingAngle   = facing,
                    });
                }
            }

            triAreas.Dispose();
        }
    }
}
