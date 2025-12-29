using Unity.Entities;

namespace DOTS.Terrain
{
    /// <summary>
    /// Tag component added whenever a chunk mesh blob needs to be pushed to a Unity Mesh for rendering.
    /// </summary>
    public struct TerrainChunkNeedsRenderUpload : IComponentData
    {
    }
}
