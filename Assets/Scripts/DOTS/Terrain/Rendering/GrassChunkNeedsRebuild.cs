using Unity.Entities;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Tag component that signals <see cref="GrassChunkGenerationSystem"/> to rebuild the
    /// <see cref="GrassChunkBladeBuffer"/> for this chunk.
    ///
    /// Added by:
    ///   - <see cref="GrassChunkGenerationSystem"/> itself on first-time setup (no buffer yet)
    ///   - <c>TerrainChunkEditUtility</c> whenever the chunk's SDF is modified
    ///   - Any system that changes <see cref="TerrainChunkGrassSurface"/> density or biome
    ///
    /// Removed by <see cref="GrassChunkGenerationSystem"/> after a successful rebuild.
    /// Follows the same pattern as <c>TerrainChunkNeedsDensityRebuild</c>.
    /// </summary>
    public struct GrassChunkNeedsRebuild : IComponentData { }
}
