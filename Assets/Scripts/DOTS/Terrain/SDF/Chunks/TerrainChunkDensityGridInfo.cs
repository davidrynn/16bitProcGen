using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    /// <summary>
    /// Describes the resolution of the density sample grid stored in <see cref="TerrainChunkDensity"/>.
    /// This may differ from <see cref="TerrainChunkGridInfo.Resolution"/> when padding is used for seam stitching.
    /// </summary>
    public struct TerrainChunkDensityGridInfo : IComponentData
    {
        public int3 Resolution;
    }
}
