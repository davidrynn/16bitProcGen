using Unity.Burst;

using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DOTS.Terrain;
using DOTS.Terrain.Debug;

namespace DOTS.Terrain.Meshing
{
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.TerrainChunkDensitySamplingSystem))]
    [UpdateBefore(typeof(TerrainChunkMeshUploadSystem))]
    public partial struct TerrainChunkMeshBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkDensity>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var maxMeshBuilds = int.MaxValue;

            if (SystemAPI.TryGetSingleton<DOTS.Terrain.LOD.TerrainLodSettings>(out var lodSettings)
                && lodSettings.MaxMeshRebuildsPerFrame > 0)
            {
                maxMeshBuilds = lodSettings.MaxMeshRebuildsPerFrame;
            }

            var debugEnabled = SystemAPI.HasSingleton<TerrainDebugConfig>() &&
                               SystemAPI.GetSingleton<TerrainDebugConfig>().Enabled;

            // Gather all chunks eligible this frame.
            var query = SystemAPI.QueryBuilder()
                .WithAll<TerrainChunkNeedsMeshBuild, TerrainChunkDensity, TerrainChunkGridInfo,
                         TerrainChunkDensityGridInfo, TerrainChunkBounds>()
                .Build();

            int capacity = math.min(query.CalculateEntityCount(), maxMeshBuilds);
            if (capacity == 0) return;

            var scheduledEntities = new Entity[capacity];
            var jobDataArray      = new SurfaceNetsJobData[capacity];
            var handles           = new NativeArray<JobHandle>(capacity, Allocator.Temp);
            var gridInfos         = new TerrainChunkGridInfo[capacity];
            int scheduledCount    = 0;

            try
            {
                // ── Phase 1: schedule all SurfaceNets jobs (worker threads) ────────────
                foreach (var (density, grid, densityGrid, bounds, entity) in SystemAPI
                             .Query<RefRW<TerrainChunkDensity>, RefRO<TerrainChunkGridInfo>,
                                    RefRO<TerrainChunkDensityGridInfo>, RefRO<TerrainChunkBounds>>()
                             .WithAll<TerrainChunkNeedsMeshBuild>()
                             .WithEntityAccess())
                {
                    if (scheduledCount >= maxMeshBuilds) break;

                    var (data, handle) = TerrainChunkMeshBuilder.ScheduleSurfaceNetsJob(
                        ref density.ValueRW, grid.ValueRO, densityGrid.ValueRO);

                    if (!data.IsValid) continue;

                    scheduledEntities[scheduledCount] = entity;
                    jobDataArray[scheduledCount]      = data;
                    handles[scheduledCount]           = handle;
                    gridInfos[scheduledCount]         = grid.ValueRO;
                    scheduledCount++;
                }

                // ── Phase 2: complete all jobs (run concurrently on worker threads) ────
                if (scheduledCount > 0)
                    JobHandle.CompleteAll(handles.GetSubArray(0, scheduledCount));

                // ── Phase 3: build blobs and issue ECB commands (main thread) ─────────
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                try
                {
                    for (int i = 0; i < scheduledCount; i++)
                    {
                        var entity  = scheduledEntities[i];
                        var data    = jobDataArray[i];
                        var meshBlob = TerrainChunkMeshBuilder.BuildBlobFromJobData(ref data);
                        jobDataArray[i] = default; // mark consumed so finally skips it

                        if (!meshBlob.IsCreated) continue;

                        if (entityManager.HasComponent<TerrainChunkMeshData>(entity))
                        {
                            var existing = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                            existing.Dispose();
                            ecb.SetComponent(entity, new TerrainChunkMeshData { Mesh = meshBlob });
                        }
                        else
                        {
                            ecb.AddComponent(entity, new TerrainChunkMeshData { Mesh = meshBlob });
                        }

                        if (debugEnabled)
                        {
                            var meshDebugData = ComputeMeshDebugData(ref meshBlob, gridInfos[i]);
                            if (entityManager.HasComponent<TerrainChunkMeshDebugData>(entity))
                                ecb.SetComponent(entity, meshDebugData);
                            else
                                ecb.AddComponent(entity, meshDebugData);
                        }

                        if (!entityManager.HasComponent<TerrainChunkNeedsRenderUpload>(entity))
                            ecb.AddComponent<TerrainChunkNeedsRenderUpload>(entity);

                        if (!entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
                            ecb.AddComponent<TerrainChunkNeedsColliderBuild>(entity);

                        if (entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                            ecb.RemoveComponent<TerrainChunkNeedsMeshBuild>(entity);

                        if (entityManager.HasComponent<TerrainChunkDebugState>(entity))
                        {
                            var debugState = entityManager.GetComponentData<TerrainChunkDebugState>(entity);
                            debugState.Stage = TerrainChunkDebugState.StageMeshReady;
                            ecb.SetComponent(entity, debugState);
                        }
                    }

                    ecb.Playback(entityManager);
                }
                finally
                {
                    ecb.Dispose();
                }
            }
            finally
            {
                // Dispose any job data not consumed in Phase 3 (e.g. on exception or zero-vertex mesh).
                for (int i = 0; i < scheduledCount; i++)
                    if (jobDataArray[i].IsValid) jobDataArray[i].Dispose();

                handles.Dispose();
            }
        }

        private static TerrainChunkMeshDebugData ComputeMeshDebugData(
            ref BlobAssetReference<TerrainChunkMeshBlob> meshBlob,
            in TerrainChunkGridInfo grid)
        {
            ref var mesh = ref meshBlob.Value;
            var vertexCount = mesh.Vertices.Length;
            var indexCount = mesh.Indices.Length;

            // Compute bounds and border vertex count
            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);
            var borderVertexCount = 0;

            // Chunk size in local space
            var chunkSizeX = (grid.Resolution.x - 1) * grid.VoxelSize;
            var chunkSizeZ = (grid.Resolution.z - 1) * grid.VoxelSize;
            var borderThreshold = grid.VoxelSize;

            for (int i = 0; i < vertexCount; i++)
            {
                var v = mesh.Vertices[i];
                boundsMin = math.min(boundsMin, v);
                boundsMax = math.max(boundsMax, v);

                // Check if vertex is on a border (within VoxelSize of chunk edge)
                var onWestBorder = v.x <= borderThreshold;
                var onEastBorder = v.x >= chunkSizeX - borderThreshold;
                var onSouthBorder = v.z <= borderThreshold;
                var onNorthBorder = v.z >= chunkSizeZ - borderThreshold;

                if (onWestBorder || onEastBorder || onSouthBorder || onNorthBorder)
                {
                    borderVertexCount++;
                }
            }

            // Handle empty mesh case
            if (vertexCount == 0)
            {
                boundsMin = float3.zero;
                boundsMax = float3.zero;
            }

            return new TerrainChunkMeshDebugData
            {
                VertexCount = vertexCount,
                TriangleCount = indexCount / 3,
                BorderVertexCount = borderVertexCount,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax
            };
        }
    }
}
