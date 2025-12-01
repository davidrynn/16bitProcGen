using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DOTS.Terrain.Meshing
{
    [BurstCompile]
    public struct SurfaceNetsJob : IJob
    {
        [ReadOnly] public NativeArray<float> Densities;
        public int3 Resolution;
        public float VoxelSize;
        public float3 ChunkOrigin;

        public NativeList<float3> Vertices;
        public NativeList<int> Indices;

        private static readonly int3[] CornerOffsets =
        {
            new int3(0, 0, 0),
            new int3(1, 0, 0),
            new int3(1, 0, 1),
            new int3(0, 0, 1),
            new int3(0, 1, 0),
            new int3(1, 1, 0),
            new int3(1, 1, 1),
            new int3(0, 1, 1)
        };

        public void Execute()
        {
            // Abort early if required buffers are missing (prevents invalid memory access).
            if (!Densities.IsCreated || !Vertices.IsCreated || !Indices.IsCreated)
            {
                return;
            }

            // Surface Nets operates on cells, so we subtract one from each resolution axis to get cell counts.
            var cellResolution = new int3(
                math.max(Resolution.x - 1, 0),
                math.max(Resolution.y - 1, 0),
                math.max(Resolution.z - 1, 0));

            // If any axis collapses to zero cells there is nothing to process.
            if (cellResolution.x <= 0 || cellResolution.y <= 0 || cellResolution.z <= 0)
            {
                return;
            }

            // Iterate each cell in z/y/x order, producing at most one vertex per cell.
            for (int z = 0; z < cellResolution.z; z++)
            {
                for (int y = 0; y < cellResolution.y; y++)
                {
                    for (int x = 0; x < cellResolution.x; x++)
                    {
                        ProcessCell(x, y, z);
                    }
                }
            }
        }

        private void ProcessCell(int cellX, int cellY, int cellZ)
        {
            float minDensity = float.MaxValue;
            float maxDensity = float.MinValue;

            float totalWeight = 0f;
            float3 weightedPosition = float3.zero;

            for (int corner = 0; corner < 8; corner++)
            {
                var offset = CornerOffsets[corner];
                var samplePos = new int3(cellX + offset.x, cellY + offset.y, cellZ + offset.z);
                var density = SampleDensity(samplePos);

                minDensity = math.min(minDensity, density);
                maxDensity = math.max(maxDensity, density);

                var worldPos = ChunkOrigin + (new float3(samplePos) * VoxelSize);
                var weight = 1f / (math.abs(density) + 1e-5f); // Pull vertex toward samples close to the surface.

                weightedPosition += worldPos * weight;
                totalWeight += weight;
            }

            if (minDensity >= 0f || maxDensity <= 0f)
            {
                return;
            }

            var vertex = totalWeight > 0f
                ? weightedPosition / totalWeight
                : ChunkOrigin + (new float3(cellX, cellY, cellZ) + 0.5f) * VoxelSize;

            Vertices.Add(vertex);
            // Surface Nets index connectivity will be added in a follow-up step (Phase 3.2)
        }

        private float SampleDensity(int3 position)
        {
            var index = position.x + Resolution.x * (position.y + Resolution.y * position.z);
            return Densities[index];
        }
    }
}
