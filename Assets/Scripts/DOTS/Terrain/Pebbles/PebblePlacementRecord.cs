using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// One accepted pebble-cluster site on a terrain chunk. Decorative family:
    /// no gameplay state deltas (TERRAIN_SURFACE_SCATTER_PLAN §7.4), so the record
    /// carries render data only. StableLocalId kept for parity with other families
    /// and as a hook should a delta model ever be needed.
    /// </summary>
    public struct PebblePlacementRecord : IBufferElementData
    {
        public float3 WorldPosition;
        public float  GroundNormalY;
        public float  UniformScale;
        public float  YawRadians;
        public byte   PebbleTypeId;
        public ushort StableLocalId;
    }
}
