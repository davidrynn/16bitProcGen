using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Canonical identity for an interactive promoted tree entity.
    /// This lets spawned tree entities map back to deterministic chunk generation and sparse deltas.
    /// </summary>
    public struct TreePersistentIdentity : IComponentData
    {
        public int3 ChunkCoord;
        public ushort StableLocalId;
        public uint GenerationVersion;
    }
}