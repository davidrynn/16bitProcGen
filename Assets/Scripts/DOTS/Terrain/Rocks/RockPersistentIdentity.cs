using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Canonical identity for a promoted interactive rock entity.
    /// This maps spawned entities back to deterministic chunk generation.
    /// </summary>
    public struct RockPersistentIdentity : IComponentData
    {
        public int3 ChunkCoord;
        public ushort StableLocalId;
        public uint GenerationVersion;
    }
}
