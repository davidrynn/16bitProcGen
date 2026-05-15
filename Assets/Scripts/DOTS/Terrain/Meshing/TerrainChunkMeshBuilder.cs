using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Meshing
{
    /// <summary>
    /// Holds all temporary native collections allocated for one SurfaceNets job.
    /// Call Dispose() if the job is abandoned, or after BuildBlobFromJobData() returns.
    /// </summary>
    public struct SurfaceNetsJobData
    {
        public NativeArray<float>  Densities;
        public NativeList<float3>  Vertices;
        public NativeList<int>     Indices;
        public NativeArray<int>    VertexIndices;
        public NativeArray<sbyte>  CellSigns;
        public TerrainChunkGridInfo Grid;

        public bool IsValid => Densities.IsCreated;

        public void Dispose()
        {
            if (Densities.IsCreated)     Densities.Dispose();
            if (Vertices.IsCreated)      Vertices.Dispose();
            if (Indices.IsCreated)       Indices.Dispose();
            if (VertexIndices.IsCreated) VertexIndices.Dispose();
            if (CellSigns.IsCreated)     CellSigns.Dispose();
            this = default;
        }
    }

    public static class TerrainChunkMeshBuilder
    {
        /// <summary>
        /// Schedules a SurfaceNets job on a worker thread without blocking the main thread.
        /// Call JobHandle.CompleteAll on all returned handles, then BuildBlobFromJobData for each result.
        /// </summary>
        public static (SurfaceNetsJobData data, JobHandle handle) ScheduleSurfaceNetsJob(
            ref TerrainChunkDensity density,
            in TerrainChunkGridInfo grid,
            in TerrainChunkDensityGridInfo densityGrid)
        {
            var voxelCount = density.Length;
            if (voxelCount == 0)
                return (default, default);

            var densities = new NativeArray<float>(voxelCount, Allocator.TempJob);
            for (int i = 0; i < voxelCount; i++)
                densities[i] = density.Data.Value.Values[i];

            var baseCellResolution = new int3(
                math.max(grid.Resolution.x - 1, 0),
                math.max(grid.Resolution.y - 1, 0),
                math.max(grid.Resolution.z - 1, 0));

            var cellResolution = new int3(
                math.max(densityGrid.Resolution.x - 1, 0),
                math.max(densityGrid.Resolution.y - 1, 0),
                math.max(densityGrid.Resolution.z - 1, 0));

            var cellCount = cellResolution.x * cellResolution.y * cellResolution.z;
            var vertexIndices = cellCount > 0
                ? new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
                : default;
            var cellSigns = cellCount > 0
                ? new NativeArray<sbyte>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory)
                : default;

            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices  = new NativeList<int>(Allocator.TempJob);

            var handle = new SurfaceNetsJob
            {
                Densities          = densities,
                Resolution         = densityGrid.Resolution,
                VoxelSize          = grid.VoxelSize,
                Vertices           = vertices,
                Indices            = indices,
                VertexIndices      = vertexIndices,
                CellSigns          = cellSigns,
                CellResolution     = cellResolution,
                BaseCellResolution = baseCellResolution
            }.Schedule();

            return (new SurfaceNetsJobData
            {
                Densities     = densities,
                Vertices      = vertices,
                Indices       = indices,
                VertexIndices = vertexIndices,
                CellSigns     = cellSigns,
                Grid          = grid
            }, handle);
        }

        /// <summary>
        /// Builds a mesh blob from a completed SurfaceNetsJobData. Disposes all job allocations.
        /// Must be called only after the corresponding JobHandle has been completed.
        /// </summary>
        public static BlobAssetReference<TerrainChunkMeshBlob> BuildBlobFromJobData(ref SurfaceNetsJobData data)
        {
            if (!data.IsValid || data.Vertices.Length == 0)
            {
                data.Dispose();
                return default;
            }

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkMeshBlob>();

            var vertexArray = builder.Allocate(ref root.Vertices, data.Vertices.Length);
            for (int i = 0; i < data.Vertices.Length; i++)
                vertexArray[i] = data.Vertices[i];

            var indexArray = builder.Allocate(ref root.Indices, data.Indices.Length);
            for (int i = 0; i < data.Indices.Length; i++)
                indexArray[i] = data.Indices[i];

            var blob = builder.CreateBlobAssetReference<TerrainChunkMeshBlob>(Allocator.Persistent);
            builder.Dispose();
            data.Dispose();
            return blob;
        }

        /// <summary>Synchronous single-chunk build. Used by tests and tools.</summary>
        public static BlobAssetReference<TerrainChunkMeshBlob> BuildMeshBlob(ref TerrainChunkDensity density, in TerrainChunkGridInfo grid, in TerrainChunkDensityGridInfo densityGrid, in TerrainChunkBounds bounds)
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

                var baseCellResolution = new int3(
                    math.max(grid.Resolution.x - 1, 0),
                    math.max(grid.Resolution.y - 1, 0),
                    math.max(grid.Resolution.z - 1, 0));

                var cellResolution = new int3(
                    math.max(densityGrid.Resolution.x - 1, 0),
                    math.max(densityGrid.Resolution.y - 1, 0),
                    math.max(densityGrid.Resolution.z - 1, 0));

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
                        Resolution = densityGrid.Resolution,
                        VoxelSize = grid.VoxelSize,
                        Vertices = vertices,
                        Indices = indices,
                        VertexIndices = vertexIndices,
                        CellSigns = cellSigns,
                        CellResolution = cellResolution,
                        BaseCellResolution = baseCellResolution
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
