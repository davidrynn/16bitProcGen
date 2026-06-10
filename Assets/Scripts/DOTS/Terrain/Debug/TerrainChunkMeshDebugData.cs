using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Debug-only component that stores mesh statistics for a terrain chunk.
    /// Populated by TerrainChunkMeshBuildSystem when debug mode is enabled.
    /// Used by TerrainMeshSeamValidatorSystem and TerrainMeshBorderDebugSystem.
    /// </summary>
    public struct TerrainChunkMeshDebugData : IComponentData
    {
        /// <summary>
        /// Total number of vertices in the chunk mesh.
        /// </summary>
        public int VertexCount;

        /// <summary>
        /// Total number of triangles in the chunk mesh (IndexCount / 3).
        /// </summary>
        public int TriangleCount;

        /// <summary>
        /// Number of vertices located on chunk borders (within VoxelSize of edge).
        /// </summary>
        public int BorderVertexCount;

        /// <summary>
        /// Minimum corner of the mesh axis-aligned bounding box (local space).
        /// </summary>
        public float3 BoundsMin;

        /// <summary>
        /// Maximum corner of the mesh axis-aligned bounding box (local space).
        /// </summary>
        public float3 BoundsMax;
    }
}
