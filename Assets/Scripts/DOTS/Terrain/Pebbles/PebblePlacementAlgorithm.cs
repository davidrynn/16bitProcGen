using DOTS.Terrain.SurfaceScatter;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Pure-static placement for pebble-cluster patches. Unlike the rock family's uniform
    /// distribution, candidates are gated by a low-frequency "rocky zone" mask first and
    /// only roll acceptance inside zones — producing the biome spec's "clustered, not
    /// uniform" read. All tuning comes from <see cref="PebblePlacementParams"/> (data-driven
    /// per TERRAIN_SURFACE_SCATTER_PLAN §7.2). Separated from the system for Burst-friendly
    /// direct invocation in tests.
    /// </summary>
    public static class PebblePlacementAlgorithm
    {
        /// <summary>Candidate cells are clamped to this per axis so degenerate MinSpacing cannot explode candidate counts.</summary>
        public const int MaxCandidateCellsPerAxis = 8;

        public const float CellJitterFraction = 0.35f;

        /// <summary>
        /// Noise layer index for the rocky-zone mask. 4 is taken by rock probability;
        /// 6 keeps pebble zones decorrelated from both rocks and trees so pebble fields
        /// read as their own feature rather than shadowing rock placement.
        /// </summary>
        public const uint ZoneNoiseLayer = 6u;

        /// <summary>True when (worldX, worldZ) falls inside a rocky zone for this seed/tuning.</summary>
        public static bool IsInRockyZone(float2 worldXZ, uint worldSeed, float zoneNoiseFrequency, float zoneThreshold)
        {
            float n = snoise((worldXZ + SDFMath.SeedLayerOffset(worldSeed, ZoneNoiseLayer)) * zoneNoiseFrequency);
            return n > zoneThreshold;
        }

        public static void GeneratePlacements(
            ref TerrainChunkDensityBlob blob,
            int3 chunkCoord,
            float3 worldOrigin,
            uint worldSeed,
            in PebblePlacementParams p,
            ref NativeList<PebblePlacementRecord> output)
        {
            float spacing = math.max(p.MinSpacing, 0.5f);
            float sizeX = (blob.Resolution.x - 1) * blob.VoxelSize;
            float sizeZ = (blob.Resolution.z - 1) * blob.VoxelSize;
            int cellsX = math.clamp((int)(sizeX / spacing), 1, MaxCandidateCellsPerAxis);
            int cellsZ = math.clamp((int)(sizeZ / spacing), 1, MaxCandidateCellsPerAxis);
            float cellW = sizeX / cellsX;
            float cellD = sizeZ / cellsZ;
            byte variantCount = (byte)math.max((int)p.VariantCount, 1);

            for (int cellZ = 0; cellZ < cellsZ; cellZ++)
            for (int cellX = 0; cellX < cellsX; cellX++)
            {
                var hash = SurfaceScatterPlacementMath.CandidateHash(worldSeed, chunkCoord, cellX, cellZ);

                float jx = (((hash >> 8) & 0xFFFFu) * (1f / 65535f) * 2f - 1f) * cellW * CellJitterFraction;
                float jz = (((hash >> 20) & 0xFFFu) * (1f / 4095f) * 2f - 1f) * cellD * CellJitterFraction;
                float worldX = worldOrigin.x + (cellX + 0.5f) * cellW + jx;
                float worldZ = worldOrigin.z + (cellZ + 0.5f) * cellD + jz;

                var candidate2D = new float2(worldX, worldZ);
                if (!IsInRockyZone(candidate2D, worldSeed, p.ZoneNoiseFrequency, p.ZoneThreshold))
                {
                    continue;
                }

                // Low 8 hash bits drive acceptance; bits 8-31 are spent on jitter above,
                // so acceptance stays decorrelated from position within the cell.
                float accept01 = (hash & 0xFFu) * (1f / 255f);
                if (accept01 > p.InZoneProbability)
                {
                    continue;
                }

                if (!SurfaceScatterPlacementMath.TryFindSurfaceHeight(worldX, worldZ, ref blob, out float surfaceY, out int surfaceIY))
                {
                    continue;
                }

                int ix = math.clamp(
                    (int)math.round((worldX - blob.WorldOrigin.x) / blob.VoxelSize),
                    0, blob.Resolution.x - 1);
                int iz = math.clamp(
                    (int)math.round((worldZ - blob.WorldOrigin.z) / blob.VoxelSize),
                    0, blob.Resolution.z - 1);

                float3 normal = SurfaceScatterPlacementMath.ComputeNormal(ix, surfaceIY, iz, ref blob);
                if (normal.y < p.MinGroundNormalY)
                {
                    continue;
                }

                var worldPos3 = new float3(worldX, surfaceY, worldZ);
                var tooClose = false;
                for (int i = 0; i < output.Length; i++)
                {
                    if (math.distance(worldPos3.xz, output[i].WorldPosition.xz) < spacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (tooClose)
                {
                    continue;
                }

                var scale01 = ((hash >> 4) & 0xFFFu) * (1f / 4095f);
                var yaw01 = ((hash >> 16) & 0xFFFu) * (1f / 4095f);

                output.Add(new PebblePlacementRecord
                {
                    WorldPosition = worldPos3,
                    GroundNormalY = normal.y,
                    UniformScale = math.lerp(p.MinUniformScale, p.MaxUniformScale, scale01),
                    YawRadians = yaw01 * math.PI * 2f,
                    PebbleTypeId = (byte)(hash % variantCount),
                    StableLocalId = (ushort)(cellZ * cellsX + cellX),
                });
            }
        }
    }
}
