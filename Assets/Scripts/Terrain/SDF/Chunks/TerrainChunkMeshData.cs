using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.SDF
{
    public struct TerrainChunkMeshData : IComponentData
    {
        public BlobAssetReference<TerrainChunkMeshBlob> Mesh;

        public bool HasMesh => Mesh.IsCreated;

        public void Dispose()
        {
            if (Mesh.IsCreated)
            {
                Mesh.Dispose();
            }
        }
    }

    public struct TerrainChunkMeshBlob
    {
        public BlobArray<float3> Vertices;
        public BlobArray<int> Indices;
    }

    public struct TerrainChunkNeedsMeshBuild : IComponentData
    {
    }
}
