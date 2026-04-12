using Unity.Entities;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Present on a chunk whose tree placement records are current.
    /// Remove to trigger regeneration.
    /// </summary>
    public struct ChunkTreePlacementTag : IComponentData
    {
        public uint GenerationVersion;
    }
}
