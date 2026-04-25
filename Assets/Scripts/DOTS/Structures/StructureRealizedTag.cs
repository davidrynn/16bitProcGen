using Unity.Entities;

namespace DOTS.Structures
{
    /// <summary>
    /// Attached to each entity spawned by a structure realizer.
    /// Links back to the anchor via StableAnchorId so cleanup
    /// and persistence can find realized entities.
    /// </summary>
    public struct StructureRealizedTag : IComponentData
    {
        public uint StableAnchorId;
    }
}
