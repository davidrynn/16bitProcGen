using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// One accepted rock site on a terrain chunk. StableLocalId is derived from
    /// deterministic candidate slot identity so sparse deltas survive regeneration.
    /// </summary>
    public struct RockPlacementRecord : IBufferElementData
    {
        public float3 WorldPosition;
        public float  GroundNormalY;
        public float  UniformScale;
        public float  YawRadians;
        public byte   RockTypeId;
        public ushort StableLocalId;
    }
}
