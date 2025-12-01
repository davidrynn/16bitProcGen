using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.SDF
{
    public struct TerrainChunkBounds : IComponentData
    {
        public float3 WorldOrigin;
    }
}
