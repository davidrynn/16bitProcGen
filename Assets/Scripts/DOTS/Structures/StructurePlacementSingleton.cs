using Unity.Entities;

namespace DOTS.Structures
{
    /// <summary>
    /// Tag on the singleton entity that owns the StructureAnchorRecord
    /// and StructureFootprintReservation dynamic buffers. Created once
    /// by the anchor planning system.
    /// </summary>
    public struct StructurePlacementSingleton : IComponentData { }
}
