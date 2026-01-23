using Unity.Entities;

namespace DOTS.Terrain
{
    /// <summary>
    /// Tag component added when a chunk's physics collider needs rebuilding.
    /// </summary>
    public struct TerrainChunkNeedsColliderBuild : IComponentData
    {
    }
}
