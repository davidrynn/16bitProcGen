using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// One accepted tree site on a terrain chunk. Only valid placements are written —
    /// rejected candidates are discarded, not stored. StableLocalId is derived from
    /// the raw deterministic candidate slot so sparse deltas can survive regeneration.
    /// </summary>
    public struct TreePlacementRecord : IBufferElementData
    {
        public float3 WorldPosition;
        public float  GroundNormalY;  // dot(surface normal, up) — retained for visual tilt later
        public byte   TreeTypeId;     // 0 = generic plains tree (MVP)
        public ushort StableLocalId;
    }
}
