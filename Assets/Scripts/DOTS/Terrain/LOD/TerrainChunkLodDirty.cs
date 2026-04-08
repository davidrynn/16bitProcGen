using Unity.Entities;

namespace DOTS.Terrain.LOD
{
    /// <summary>
    /// Tag added to chunks when their LOD level changes. Consumed by seam and skirt systems in M2.
    /// </summary>
    public struct TerrainChunkLodDirty : IComponentData { }
}
