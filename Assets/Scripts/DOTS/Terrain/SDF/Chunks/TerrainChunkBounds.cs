using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    public struct TerrainChunkBounds : IComponentData
    {
        public float3 WorldOrigin;
    }
}
