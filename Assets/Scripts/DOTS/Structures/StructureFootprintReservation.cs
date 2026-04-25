using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Structures
{
    /// <summary>
    /// AABB exclusion zone published by an accepted structure anchor.
    /// Scatter and detail systems query these to avoid spawning props
    /// inside structure footprints.
    /// </summary>
    public struct StructureFootprintReservation : IBufferElementData
    {
        public uint StableAnchorId;
        public float3 Center;
        public float3 Extents;
    }
}
