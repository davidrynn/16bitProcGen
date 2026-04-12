using Unity.Entities;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Sparse divergence from a chunk's deterministic default tree state.
    /// Only changed trees should have a delta entry; untouched generated trees store nothing.
    /// </summary>
    public struct TreeStateDelta : IBufferElementData
    {
        public ushort StableLocalId;
        public TreeStateStage Stage;
        public uint ModifiedAtTick;
        public uint NextChangeTick;
    }

    /// <summary>
    /// Minimal persistent tree lifecycle stages for seeded trees.
    /// The current far-tree renderer only draws the default full-size mesh.
    /// </summary>
    public enum TreeStateStage : byte
    {
        Full = 0,
        Damaged = 1,
        Stump = 2,
        Sapling = 3,
        Growing = 4,
    }
}