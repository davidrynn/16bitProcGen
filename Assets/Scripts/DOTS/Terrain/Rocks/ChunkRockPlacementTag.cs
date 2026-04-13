using Unity.Entities;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Present on a chunk whose rock placement records are current.
    /// Remove to trigger regeneration.
    /// </summary>
    public struct ChunkRockPlacementTag : IComponentData
    {
        public uint GenerationVersion;
    }
}
