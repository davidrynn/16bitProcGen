using Unity.Entities;

namespace DOTS.Terrain
{
    /// <summary>
    /// Tag component set on chunks that require their density field to be rebuilt.
    /// </summary>
    public struct TerrainChunkNeedsDensityRebuild : IComponentData
    {
    }
}
