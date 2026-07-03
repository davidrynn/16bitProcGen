using Unity.Collections;
using Unity.Mathematics;
using DOTS.Terrain.SurfaceScatter;
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

                uint candidateHash = SurfaceScatterPlacementMath.CandidateHash(worldSeed, chunkCoord, cellX, cellZ);
                var jitter   = CandidateJitterFromHash(candidateHash);
                float worldX = worldOrigin.x + localX + jitter.x;
                float worldZ = worldOrigin.z + localZ + jitter.y;

                // a. Scan the density blob along Y for the surface height.
                if (!SurfaceScatterPlacementMath.TryFindSurfaceHeight(worldX, worldZ, ref blob, out float surfaceY, out int surfaceIY))
                    continue;

                // b/c. Compute grid indices and surface normal via central difference.
                int ix = math.clamp(
                    (int)math.round((worldX - blob.WorldOrigin.x) / blob.VoxelSize),
                    0, blob.Resolution.x - 1);
                int iz = math.clamp(
                    (int)math.round((worldZ - blob.WorldOrigin.z) / blob.VoxelSize),
                    0, blob.Resolution.z - 1);

                float3 normal  = SurfaceScatterPlacementMath.ComputeNormal(ix, surfaceIY, iz, ref blob);
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
            return CandidateJitterFromHash(SurfaceScatterPlacementMath.CandidateHash(worldSeed, chunkCoord, cellX, cellZ));
        }

        private static float2 CandidateJitterFromHash(uint hash)
        {
            var jx = ((hash >> 8)  & 0xFFFFu) * (1f / 65535f) * 2f - 1f; // → [-1, 1]
            // Only 12 bits remain after shifting 20 positions in a 32-bit hash.
            var jz = ((hash >> 20) & 0xFFFu) * (1f / 4095f) * 2f - 1f;
            return new float2(jx, jz) * CellJitterRadius;
        }

        // Hash, surface-height, and normal math now come from SurfaceScatterPlacementMath
        // (cleanup round 1, plan row C2) — trees were the last family carrying private
        // copies; the shared implementations are constant-for-constant identical, so
        // placement determinism is unchanged.
    }
}
