using Unity.Mathematics;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Family-agnostic placement math shared by surface-scatter placement algorithms
    /// (rocks, pebbles, future shrubs). Extracted from RockPlacementAlgorithm per
    /// TERRAIN_SURFACE_SCATTER_PLAN.md Phase 2: candidate hashing and density-blob
    /// surface sampling are generic; per-family rules (spacing, probability, zoning)
    /// stay in each family's algorithm.
    /// </summary>
    public static class SurfaceScatterPlacementMath
    {
        /// <summary>Deterministic per-candidate hash from world seed + chunk + cell identity.</summary>
        public static uint CandidateHash(uint worldSeed, int3 chunkCoord, int cellX, int cellZ)
        {
            var h = worldSeed;
            h ^= (uint)chunkCoord.x * 2654435761u;
            h ^= (uint)chunkCoord.z * 1013904223u;
            h ^= (uint)cellX * 374761393u;
            h ^= (uint)cellZ * 668265263u;
            h *= 0x9e3779b9u;
            return h;
        }

        /// <summary>
        /// Scans the density column at (worldX, worldZ) top-down for the air→solid crossing
        /// and interpolates the surface height. Returns false for all-air / all-solid columns.
        /// </summary>
        public static bool TryFindSurfaceHeight(
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

        /// <summary>Central-difference density gradient as a surface normal (safe up fallback).</summary>
        public static float3 ComputeNormal(int ix, int iy, int iz, ref TerrainChunkDensityBlob blob)
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
