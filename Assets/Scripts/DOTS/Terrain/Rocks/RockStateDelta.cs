using Unity.Entities;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Sparse divergence from a chunk's deterministic default rock state.
    /// Only changed rocks should have a delta entry.
    /// </summary>
    public struct RockStateDelta : IBufferElementData
    {
        public ushort StableLocalId;
        public RockStateStage Stage;
        public uint ModifiedAtTick;
        public uint NextChangeTick;
    }
}
