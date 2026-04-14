using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Pure-static placement algorithm for tree placement records.
    /// Separated from TreePlacementGenerationSystem so that Burst does not attempt to
    /// compile these methods as C-ABI function-pointer entry points (BC1064/BC1067).
    /// Burst inlines GeneratePlacements when called from the [BurstCompile] OnUpdate.
    /// Also callable directly from EditMode/PlayMode tests without any ECS world setup.
    /// </summary>
    public static class TreePlacementAlgorithm
    {
        public const int CandidateGridSize      = 3;
        public const float MinTreeSpacing        = 5.0f;
        public const float CellJitterRadius      = 1.5f;
        public const float PlainsSlopeMinNormalY         = 0.85f;
        // Two-layer probability: sparse base keeps most terrain open; a coarse cluster
        // noise (layer 5) boosts local acceptance to form natural groves. The effective
        // threshold at any candidate = Base + clusterMask * ClusterBoost, ranging from
        // ~0.05 (open ground) to ~0.60 (grove centre).
        //
        // Tuning guide:
        //   PlainsProbabilityBase         — overall sparseness outside groves.
        //                                   Raise (e.g. 0.10f) for more scattered trees everywhere.
        //   PlainsProbabilityClusterBoost — how dense grove centres get.
        //                                   Raise to pack groves tighter without affecting open areas.
        //   TreeClusterNoiseScale         — grove patch size.
        //                                   Decrease (e.g. 0.012f) for larger, wider groves.
        //                                   Increase (e.g. 0.030f) for smaller, tighter clumps.
        public const float PlainsProbabilityBase         = 0.01f;
        public const float PlainsProbabilityClusterBoost = 0.15f;
        public const float TreeClusterNoiseScale          = 0.020f; // ~50 world-unit wavelength → 3–4 chunk wide groves
        public const byte PlainsTreeVariantCount          = 3;

        /// <summary>
        /// Core placement algorithm.
        /// Generates up to 9 candidates (3×3 grid) per chunk and filters by surface existence,
        /// slope, probability noise, and minimum spacing.
        /// </summary>
        /// <param name="blob">Density blob for surface height and normal lookup.</param>
        /// <param name="chunkCoord">Chunk grid coordinate (used in per-cell jitter hash).</param>
        /// <param name="worldOrigin">World-space corner of the chunk.</param>
        /// <param name="worldSeed">World seed from TerrainGenerationContext.</param>
        /// <param name="output">Accepted records written here.</param>
        public static void GeneratePlacements(
            ref TerrainChunkDensityBlob blob,
            int3   chunkCoord,
            float3 worldOrigin,
            uint   worldSeed,
            ref NativeList<TreePlacementRecord> output)
        {
            // 3×3 jittered candidate grid — cell size equals MinTreeSpacing.
            for (int cellZ = 0; cellZ < CandidateGridSize; cellZ++)
            for (int cellX = 0; cellX < CandidateGridSize; cellX++)
            {
                float localX = (cellX + 0.5f) * MinTreeSpacing;
                float localZ = (cellZ + 0.5f) * MinTreeSpacing;

                uint candidateHash = CandidateHash(worldSeed, chunkCoord, cellX, cellZ);
                var jitter   = CandidateJitterFromHash(candidateHash);
                float worldX = worldOrigin.x + localX + jitter.x;
                float worldZ = worldOrigin.z + localZ + jitter.y;

                // a. Binary-search the density blob along Y for the surface height.
                if (!TryFindSurfaceHeight(worldX, worldZ, ref blob, out float surfaceY, out int surfaceIY))
                    continue;

                // b/c. Compute grid indices and surface normal via central difference.
                int ix = math.clamp(
                    (int)math.round((worldX - blob.WorldOrigin.x) / blob.VoxelSize),
                    0, blob.Resolution.x - 1);
                int iz = math.clamp(
                    (int)math.round((worldZ - blob.WorldOrigin.z) / blob.VoxelSize),
                    0, blob.Resolution.z - 1);

                float3 normal  = ComputeNormal(ix, surfaceIY, iz, ref blob);
                float  normalY = normal.y;

                // d. Slope filter — reject steep terrain.
                if (normalY < PlainsSlopeMinNormalY) continue;

                // e. Two-layer probability filter.
                //    Layer 3: per-candidate noise gates individual tree acceptance.
                //    Layer 5: coarse cluster noise boosts the threshold in grove regions.
                //    Never reuse layers 0–2 (terrain elevation) to avoid correlation.
                var candidate2D      = new float2(worldX, worldZ);
                float probNoise      = snoise((candidate2D + SDFMath.SeedLayerOffset(worldSeed, 3u)) * 0.06f);
                float normalizedProb = probNoise * 0.5f + 0.5f; // → [0, 1]

                float clusterNoise     = snoise((candidate2D + SDFMath.SeedLayerOffset(worldSeed, 5u)) * TreeClusterNoiseScale);
                float clusterMask      = clusterNoise * 0.5f + 0.5f; // → [0, 1]
                float effectiveThreshold = PlainsProbabilityBase + clusterMask * PlainsProbabilityClusterBoost;

                if (normalizedProb > effectiveThreshold) continue;

                // f. Spacing check — reject if too close to any already-accepted site.
                var worldPos3 = new float3(worldX, surfaceY, worldZ);
                bool tooClose = false;
                for (int i = 0; i < output.Length; i++)
                {
                    if (math.distance(worldPos3.xz, output[i].WorldPosition.xz) < MinTreeSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }
                if (tooClose) continue;

                var stableLocalId = CandidateLocalId(cellX, cellZ);
                var variantIndex  = (byte)(candidateHash % PlainsTreeVariantCount);
                var yaw01         = ((candidateHash >> 2) & 0xFFFFu) * (1f / 65536f);

                output.Add(new TreePlacementRecord
                {
                    WorldPosition = worldPos3,
                    GroundNormalY = normalY,
                    YawRadians    = yaw01 * math.PI * 2f,
                    TreeTypeId    = variantIndex,
                    StableLocalId = stableLocalId,
                });
            }
        }

        /// <summary>
        /// Deterministic candidate identity local to a chunk. This is keyed from the raw
        /// candidate slot, not accepted-order, so tree deltas remain stable when neighbors
        /// are rejected or re-accepted after terrain changes.
        /// </summary>
        public static ushort CandidateLocalId(int cellX, int cellZ)
        {
            return (ushort)(cellZ * CandidateGridSize + cellX);
        }

        /// <summary>
        /// Deterministic per-cell jitter hash. Encodes world seed, chunk coord, and cell
        /// index so adjacent cells from different chunks never share the same offset.
        /// </summary>
        public static float2 CandidateJitter(uint worldSeed, int3 chunkCoord, int cellX, int cellZ)
        {
            return CandidateJitterFromHash(CandidateHash(worldSeed, chunkCoord, cellX, cellZ));
        }

        private static float2 CandidateJitterFromHash(uint hash)
        {
            var jx = ((hash >> 8)  & 0xFFFFu) * (1f / 65535f) * 2f - 1f; // → [-1, 1]
            // Only 12 bits remain after shifting 20 positions in a 32-bit hash.
            var jz = ((hash >> 20) & 0xFFFu) * (1f / 4095f) * 2f - 1f;
            return new float2(jx, jz) * CellJitterRadius;
        }

        private static uint CandidateHash(uint worldSeed, int3 chunkCoord, int cellX, int cellZ)
        {
            var hash = worldSeed;
            hash ^= (uint)chunkCoord.x * 2654435761u;
            hash ^= (uint)chunkCoord.z * 1013904223u;
            hash ^= (uint)cellX * 374761393u;
            hash ^= (uint)cellZ * 668265263u;
            hash *= 0x9e3779b9u;
            return hash;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        /// <summary>
        /// Finds the world-space surface height and the Y grid index of the surface voxel
        /// for a given (worldX, worldZ) position in the density blob.
        /// Returns false if no surface crossing is found in the blob column.
        /// </summary>
        private static bool TryFindSurfaceHeight(
            float worldX, float worldZ,
            ref TerrainChunkDensityBlob blob,
            out float surfaceY, out int surfaceIY)
        {
            surfaceY  = 0f;
            surfaceIY = 0;

            int ix = math.clamp(
                (int)math.round((worldX - blob.WorldOrigin.x) / blob.VoxelSize),
                0, blob.Resolution.x - 1);
            int iz = math.clamp(
                (int)math.round((worldZ - blob.WorldOrigin.z) / blob.VoxelSize),
                0, blob.Resolution.z - 1);

            // Walk from top (air) down to find first solid-above-air crossing.
            for (int iy = blob.Resolution.y - 2; iy >= 0; iy--)
            {
                float dAbove = blob.GetDensity(ix, iy + 1, iz);
                float dBelow = blob.GetDensity(ix, iy,     iz);

                // Surface crossing: above is air (≥ 0), below is solid (< 0).
                if (dAbove >= 0f && dBelow < 0f)
                {
                    // Linear interpolation within the voxel to sub-voxel precision.
                    float t   = dAbove / (dAbove - dBelow);        // fraction from iy+1 down to iy
                    surfaceY  = blob.WorldOrigin.y + (iy + 1 - t) * blob.VoxelSize;
                    surfaceIY = iy + 1;                            // voxel just above solid
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Computes the outward surface normal via central differences on the density blob.
        /// Falls back to (0,1,0) if the gradient is zero (degenerate flat region).
        /// </summary>
        private static float3 ComputeNormal(int ix, int iy, int iz, ref TerrainChunkDensityBlob blob)
        {
            int ixp = math.min(ix + 1, blob.Resolution.x - 1);
            int ixm = math.max(ix - 1, 0);
            int iyp = math.min(iy + 1, blob.Resolution.y - 1);
            int iym = math.max(iy - 1, 0);
            int izp = math.min(iz + 1, blob.Resolution.z - 1);
            int izm = math.max(iz - 1, 0);

            var grad = new float3(
                blob.GetDensity(ixp, iy,  iz)  - blob.GetDensity(ixm, iy,  iz),
                blob.GetDensity(ix,  iyp, iz)  - blob.GetDensity(ix,  iym, iz),
                blob.GetDensity(ix,  iy,  izp) - blob.GetDensity(ix,  iy,  izm));

            return math.normalizesafe(grad, new float3(0f, 1f, 0f));
        }
    }
}
