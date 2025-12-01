using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DOTS.Terrain.SDF;

namespace DOTS.Terrain.Meshing
{
    public static class TerrainChunkMeshBuilder
    {
        public static BlobAssetReference<TerrainChunkMeshBlob> BuildMeshBlob(ref TerrainChunkDensity density, in TerrainChunkGridInfo grid, in TerrainChunkBounds bounds)
        {
            var voxelCount = density.Length;
            if (voxelCount == 0)
            {
                return default;
            }

            // Copy blob-stored densities into a temp array because SurfaceNets operates on NativeArray inputs.
            var densities = new NativeArray<float>(voxelCount, Allocator.TempJob);
            try
            {
                for (int i = 0; i < voxelCount; i++)
                {
                    densities[i] = density.Data.Value.Values[i];
                }

                // Each invocation allocates fresh vertex/index lists to collect job output.
                var vertices = new NativeList<float3>(Allocator.TempJob);
                var indices = new NativeList<int>(Allocator.TempJob);
                try
                {
                    var job = new SurfaceNetsJob
                    {
                        Densities = densities,
                        Resolution = grid.Resolution,
                        VoxelSize = grid.VoxelSize,
                        ChunkOrigin = bounds.WorldOrigin,
                        Vertices = vertices,
                        Indices = indices
                    };

                    job.Run();

                    // Persist the generated geometry inside a blob so ECS components can reference it safely.
                    var builder = new BlobBuilder(Allocator.Temp);
                    ref var root = ref builder.ConstructRoot<TerrainChunkMeshBlob>();
                    var vertexArray = builder.Allocate(ref root.Vertices, vertices.Length);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertexArray[i] = vertices[i];
                    }

                    var indexArray = builder.Allocate(ref root.Indices, indices.Length);
                    for (int i = 0; i < indices.Length; i++)
                    {
                        indexArray[i] = indices[i];
                    }

                    var blob = builder.CreateBlobAssetReference<TerrainChunkMeshBlob>(Allocator.Persistent);
                    builder.Dispose();
                    return blob;
                }
                finally
                {
                    vertices.Dispose();
                    indices.Dispose();
                }
            }
            finally
            {
                densities.Dispose();
            }
        }
    }
}
