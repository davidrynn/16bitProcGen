using Unity.Entities;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Present on a chunk whose pebble-cluster placement records are current.
    /// Remove to trigger regeneration.
    /// </summary>
    public struct ChunkPebblePlacementTag : IComponentData
    {
        public uint GenerationVersion;
    }
}
