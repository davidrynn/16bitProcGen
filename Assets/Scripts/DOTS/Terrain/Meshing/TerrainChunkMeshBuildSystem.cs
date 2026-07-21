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
        // Drives nearest-first mesh ordering — see the comment at the schedule loop.
        private EntityQuery playerQuery;

        /// <summary>Sort key: Chebyshev chunk distance to the player, nearest first.</summary>
        private struct PendingMeshChunk : System.IComparable<PendingMeshChunk>
        {
            public int Distance;
            public int Index;
            public int CompareTo(PendingMeshChunk other) => Distance.CompareTo(other.Distance);
        }

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkDensity>();
            playerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<DOTS.Player.Components.PlayerTag>(),
                ComponentType.ReadOnly<Unity.Transforms.LocalTransform>());
        }

        /// <summary>
        /// Orders <paramref name="entities"/> nearest-player-first. Returns an uncreated array when
        /// there is no player / no LOD policy, in which case callers keep natural order.
        /// </summary>
        private NativeArray<PendingMeshChunk> BuildNearestFirstOrder(
            ref SystemState state, NativeArray<Entity> entities)
        {
            if (playerQuery.IsEmpty
                || !SystemAPI.TryGetSingleton<DOTS.Terrain.LOD.TerrainLodSettings>(out var lodPolicy))
                return default;

            // LOD0 stride, matching the density and collider systems, so all three agree on which
            // chunk the player occupies. Chunk footprint is LOD-invariant, so this is safe.
            var stride = math.max(0, lodPolicy.Lod0Resolution.x - 1) * lodPolicy.Lod0VoxelSize;
            if (stride <= 0f)
                return default;

            var em = state.EntityManager;
            var playerPos = em.GetComponentData<Unity.Transforms.LocalTransform>(
                playerQuery.GetSingletonEntity()).Position;
            var playerChunk = new int2(
                (int)math.floor(playerPos.x / stride),
                (int)math.floor(playerPos.z / stride));

            var order = new NativeArray<PendingMeshChunk>(entities.Length, Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var coord = em.GetComponentData<TerrainChunk>(entities[i]).ChunkCoord;
                order[i] = new PendingMeshChunk
                {
                    Distance = math.max(
                        math.abs(coord.x - playerChunk.x),
                        math.abs(coord.z - playerChunk.y)),
                    Index = i
                };
            }
            order.Sort();
            return order;
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
                // Nearest-player-first, matching TerrainChunkDensitySamplingSystem and
                // TerrainChunkColliderBuildSystem. Sorting the DENSITY queue alone is not enough:
                // a chunk whose density just finished still has to win a slot here (8/frame) before
                // it can get a collider, so an unordered mesh queue simply moves the stall one
                // stage later. See KNOWN_ISSUES BUG-019.
                using var meshEntities = query.ToEntityArray(Allocator.Temp);
                var order = BuildNearestFirstOrder(ref state, meshEntities);
                var useOrder = order.IsCreated;

                for (int o = 0; o < meshEntities.Length; o++)
                {
                    if (scheduledCount >= maxMeshBuilds) break;

                    var entity = meshEntities[useOrder ? order[o].Index : o];
                    var density = SystemAPI.GetComponentRW<TerrainChunkDensity>(entity);
                    var grid = SystemAPI.GetComponent<TerrainChunkGridInfo>(entity);
                    var densityGrid = SystemAPI.GetComponent<TerrainChunkDensityGridInfo>(entity);

                    var (data, handle) = TerrainChunkMeshBuilder.ScheduleSurfaceNetsJob(
                        ref density.ValueRW, grid, densityGrid);

                    if (!data.IsValid) continue;

                    scheduledEntities[scheduledCount] = entity;
                    jobDataArray[scheduledCount]      = data;
                    handles[scheduledCount]           = handle;
                    gridInfos[scheduledCount]         = grid;
                    scheduledCount++;
                }

                if (useOrder)
                    order.Dispose();

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

                        if (!meshBlob.IsCreated)
                        {
                            // U1 / BUG-018: a chunk that meshes to ZERO vertices (fully solid or
                            // fully air) still has to drop its rebuild tag. Previously this
                            // `continue` skipped the removal below, so the chunk re-queued every
                            // frame forever and permanently consumed one of the 8 mesh slots.
                            // Invisible while nearly every chunk has surface; fatal once vertical
                            // layers exist, where most stacked chunks are uniform.
                            if (entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                                ecb.RemoveComponent<TerrainChunkNeedsMeshBuild>(entity);
                            continue;
                        }

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
