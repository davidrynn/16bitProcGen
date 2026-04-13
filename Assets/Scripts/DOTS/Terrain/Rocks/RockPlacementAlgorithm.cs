using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Pure-static placement algorithm for deterministic rock placement records.
    /// Separated from the system for Burst-friendly direct invocation in tests.
    /// </summary>
    public static class RockPlacementAlgorithm
    {
        public const int CandidateGridSize   = 4;
        public const float MinRockSpacing    = 4.0f;
        public const float CellJitterRadius  = 1.2f;
        public const float MinGroundNormalY  = 0.70f;
        public const float RockProbability   = 0.45f;
        public const float MinUniformScale   = 0.80f;
        public const float MaxUniformScale   = 1.35f;

        public static void GeneratePlacements(
            ref TerrainChunkDensityBlob blob,
            int3 chunkCoord,
            float3 worldOrigin,
            uint worldSeed,
            ref NativeList<RockPlacementRecord> output)
        {
            for (int cellZ = 0; cellZ < CandidateGridSize; cellZ++)
            for (int cellX = 0; cellX < CandidateGridSize; cellX++)
            {
                float localX = (cellX + 0.5f) * MinRockSpacing;
                float localZ = (cellZ + 0.5f) * MinRockSpacing;

                var jitter = CandidateJitter(worldSeed, chunkCoord, cellX, cellZ);
                float worldX = worldOrigin.x + localX + jitter.x;
                float worldZ = worldOrigin.z + localZ + jitter.y;

                if (!TryFindSurfaceHeight(worldX, worldZ, ref blob, out float surfaceY, out int surfaceIY))
                    continue;

                int ix = math.clamp(
                    (int)math.round((worldX - blob.WorldOrigin.x) / blob.VoxelSize),
                    0, blob.Resolution.x - 1);
                int iz = math.clamp(
                    (int)math.round((worldZ - blob.WorldOrigin.z) / blob.VoxelSize),
                    0, blob.Resolution.z - 1);

                float3 normal = ComputeNormal(ix, surfaceIY, iz, ref blob);
                float normalY = normal.y;
                if (normalY < MinGroundNormalY)
                    continue;

                // Reserve layer index 4 for rocks so probability decorrelates from terrain and trees.
                var candidate2D = new float2(worldX, worldZ);
                float probNoise = snoise((candidate2D + SDFMath.SeedLayerOffset(worldSeed, 4u)) * 0.07f);
                float normalizedProb = probNoise * 0.5f + 0.5f;
                if (normalizedProb > RockProbability)
                    continue;

                var worldPos3 = new float3(worldX, surfaceY, worldZ);
                var tooClose = false;
                for (int i = 0; i < output.Length; i++)
                {
                    if (math.distance(worldPos3.xz, output[i].WorldPosition.xz) < MinRockSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                    continue;

                var stableLocalId = CandidateLocalId(cellX, cellZ);
                var hash = CandidateHash(worldSeed, chunkCoord, cellX, cellZ);
                var scale01 = ((hash >> 8) & 0xFFFFu) * (1f / 65535f);
                var yaw01 = ((hash >> 20) & 0xFFFu) * (1f / 4095f);

                output.Add(new RockPlacementRecord
                {
                    WorldPosition = worldPos3,
                    GroundNormalY = normalY,
                    UniformScale = math.lerp(MinUniformScale, MaxUniformScale, scale01),
                    YawRadians = yaw01 * math.PI * 2f,
                    RockTypeId = 0, // single type for now; extend here when multi-type meshes are supported
                    StableLocalId = stableLocalId,
                });
            }
        }

        public static ushort CandidateLocalId(int cellX, int cellZ)
        {
            return (ushort)(cellZ * CandidateGridSize + cellX);
        }

        public static float2 CandidateJitter(uint worldSeed, int3 chunkCoord, int cellX, int cellZ)
        {
            var h = CandidateHash(worldSeed, chunkCoord, cellX, cellZ);
            var jx = ((h >> 8) & 0xFFFFu) * (1f / 65535f) * 2f - 1f;
            // Only 12 bits remain after shifting 20 positions in a 32-bit hash.
            var jz = ((h >> 20) & 0xFFFu) * (1f / 4095f) * 2f - 1f;
            return new float2(jx, jz) * CellJitterRadius;
        }

        private static uint CandidateHash(uint worldSeed, int3 chunkCoord, int cellX, int cellZ)
        {
            var h = worldSeed;
            h ^= (uint)chunkCoord.x * 2654435761u;
            h ^= (uint)chunkCoord.z * 1013904223u;
            h ^= (uint)cellX * 374761393u;
            h ^= (uint)cellZ * 668265263u;
            h *= 0x9e3779b9u;
            return h;
        }

        private static bool TryFindSurfaceHeight(
            float worldX,
            float worldZ,
            ref TerrainChunkDensityBlob blob,
            out float surfaceY,
            out int surfaceIY)
        {
            surfaceY = 0f;
            surfaceIY = 0;

            int ix = math.clamp(
                (int)math.round((worldX - blob.WorldOrigin.x) / blob.VoxelSize),
                0, blob.Resolution.x - 1);
            int iz = math.clamp(
                (int)math.round((worldZ - blob.WorldOrigin.z) / blob.VoxelSize),
                0, blob.Resolution.z - 1);

            for (int iy = blob.Resolution.y - 2; iy >= 0; iy--)
            {
                float dAbove = blob.GetDensity(ix, iy + 1, iz);
                float dBelow = blob.GetDensity(ix, iy, iz);
                if (dAbove >= 0f && dBelow < 0f)
                {
                    float t = dAbove / (dAbove - dBelow);
                    surfaceY = blob.WorldOrigin.y + (iy + 1 - t) * blob.VoxelSize;
                    surfaceIY = iy + 1;
                    return true;
                }
            }

            return false;
        }

        private static float3 ComputeNormal(int ix, int iy, int iz, ref TerrainChunkDensityBlob blob)
        {
            int ixp = math.min(ix + 1, blob.Resolution.x - 1);
            int ixm = math.max(ix - 1, 0);
            int iyp = math.min(iy + 1, blob.Resolution.y - 1);
            int iym = math.max(iy - 1, 0);
            int izp = math.min(iz + 1, blob.Resolution.z - 1);
            int izm = math.max(iz - 1, 0);

            var grad = new float3(
                blob.GetDensity(ixp, iy, iz) - blob.GetDensity(ixm, iy, iz),
                blob.GetDensity(ix, iyp, iz) - blob.GetDensity(ix, iym, iz),
                blob.GetDensity(ix, iy, izp) - blob.GetDensity(ix, iy, izm));

            return math.normalizesafe(grad, new float3(0f, 1f, 0f));
        }
    }
}
