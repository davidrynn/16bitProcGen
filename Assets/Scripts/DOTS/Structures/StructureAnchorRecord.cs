using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Structures
{
    /// <summary>
    /// One accepted structure placement in the world. Stored as a dynamic buffer
    /// on a singleton entity so planning, realization, and persistence systems
    /// can all access the same canonical anchor list.
    ///
    /// StableAnchorId is derived from hash(worldSeed, planningCell, familyId),
    /// never from acceptance order, so it survives streaming and seed replay.
    /// </summary>
    public struct StructureAnchorRecord : IBufferElementData
    {
        public StructureFamilyId Family;
        public int2 PlanningCell;
        public float3 WorldPosition;
        public quaternion Rotation;
        public float Radius;
        public uint StableAnchorId;
        public uint GenerationVersion;
        public FixedString64Bytes TemplateId;
        public StructurePlacementSource Source;
        public StructurePersistenceFlags PersistenceFlags;
    }
}
