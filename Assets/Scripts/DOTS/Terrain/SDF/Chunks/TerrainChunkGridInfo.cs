using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    public struct TerrainChunkGridInfo : IComponentData
    {
        public int3 Resolution;
        public float VoxelSize;

        public int VoxelCount => Resolution.x * Resolution.y * Resolution.z;

        public static TerrainChunkGridInfo Create(int3 resolution, float voxelSize)
        {
            return new TerrainChunkGridInfo
            {
                Resolution = resolution,
                VoxelSize = voxelSize
            };
        }
    }
}
