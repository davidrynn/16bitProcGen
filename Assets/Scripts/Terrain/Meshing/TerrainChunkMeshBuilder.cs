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

                var cellResolution = new int3(
                    math.max(grid.Resolution.x - 1, 0),
                    math.max(grid.Resolution.y - 1, 0),
                    math.max(grid.Resolution.z - 1, 0));

                var cellCount = cellResolution.x * cellResolution.y * cellResolution.z;
                NativeArray<int> vertexIndices = default;
                NativeArray<sbyte> cellSigns = default;

                if (cellCount > 0)
                {
                    vertexIndices = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                    cellSigns = new NativeArray<sbyte>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
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
                        Vertices = vertices,
                        Indices = indices,
                        VertexIndices = vertexIndices,
                        CellSigns = cellSigns,
                        CellResolution = cellResolution
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
                    if (vertexIndices.IsCreated)
                    {
                        vertexIndices.Dispose();
                    }

                    if (cellSigns.IsCreated)
                    {
                        cellSigns.Dispose();
                    }

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
