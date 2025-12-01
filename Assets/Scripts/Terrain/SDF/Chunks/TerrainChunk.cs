using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.SDF
{
    public struct TerrainChunk : IComponentData
    {
        public int3 ChunkCoord;
    }
}
