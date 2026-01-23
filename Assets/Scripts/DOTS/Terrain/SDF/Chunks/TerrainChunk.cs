using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    public struct TerrainChunk : IComponentData
    {
        public int3 ChunkCoord;
    }
}
