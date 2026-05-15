using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using DOTS.Terrain.Core;

namespace DOTS.Terrain
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.LOD.TerrainChunkLodApplySystem))]
    [UpdateBefore(typeof(DOTS.Terrain.Meshing.TerrainChunkMeshBuildSystem))]
    public partial struct TerrainChunkDensitySamplingSystem : ISystem
    {
        private EntityQuery chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            // Don't use RequireForUpdate - it prevents OnUpdate from running if requirements aren't met immediately
            // We'll check manually in OnUpdate instead
            chunkQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkGridInfo>(),
                ComponentType.ReadOnly<TerrainChunkBounds>(),
                ComponentType.ReadOnly<TerrainChunkNeedsDensityRebuild>());
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check requirements manually - this allows OnUpdate to run and log diagnostic info
            if (!SystemAPI.HasSingleton<SDFTerrainFieldSettings>())
            {
                DebugSettings.LogWarning("[TerrainChunkDensitySamplingSystem] SDFTerrainFieldSettings singleton not found. Waiting for TerrainBootstrapAuthoring...");
                return;
            }

            if (chunkQuery.IsEmpty)
            {
                return;
            }

            var settings = SystemAPI.GetSingleton<SDFTerrainFieldSettings>();

            var useLayeredNoise = false;
            TerrainFieldSettings layeredSettings = default;
            uint worldSeed = 0;

            if (SystemAPI.HasSingleton<TerrainFieldSettings>() && SystemAPI.HasSingleton<TerrainGenerationContext>())
            {
                layeredSettings = SystemAPI.GetSingleton<TerrainFieldSettings>();
                worldSeed = SystemAPI.GetSingleton<TerrainGenerationContext>().WorldSeed;
                useLayeredNoise = true;
            }

            var field = new SDFTerrainField
            {
                BaseHeight      = settings.BaseHeight,
                Amplitude       = settings.Amplitude,
                Frequency       = settings.Frequency,
                NoiseValue      = settings.NoiseValue,
                UseLayeredNoise = useLayeredNoise,
                WorldSeed       = worldSeed,
                LayeredSettings = layeredSettings
            };

            var maxDensityRebuilds = int.MaxValue;
            if (SystemAPI.TryGetSingleton<DOTS.Terrain.LOD.TerrainLodSettings>(out var lodSettings)
                && lodSettings.MaxDensityRebuildsPerFrame > 0)
            {
                maxDensityRebuilds = lodSettings.MaxDensityRebuildsPerFrame;
            }

            var edits = CopyEditsToTempArray(ref state);
            var entityManager = state.EntityManager;
            using var chunkEntities = chunkQuery.ToEntityArray(Allocator.Temp);

            // Upper bound on how many we can schedule this frame.
            int capacity = math.min(chunkEntities.Length, maxDensityRebuilds);

            // Managed arrays are fine here — this all runs on the main thread.
            var scheduledEntities    = new Entity[capacity];
            var densityArrays        = new NativeArray<float>[capacity];
            var densityResolutions   = new int3[capacity];
            var gridInfos            = new TerrainChunkGridInfo[capacity];
            var boundsInfos          = new TerrainChunkBounds[capacity];

            // NativeArray<JobHandle> required by JobHandle.CompleteAll.
            var handles = new NativeArray<JobHandle>(capacity, Allocator.Temp);
            int scheduledCount = 0;

            try
            {
                // ── Phase 1: schedule all density jobs ────────────────────────────────
                // Jobs are dispatched to worker threads and run in parallel.
                // Multiple jobs sharing the same [ReadOnly] edits array is safe.
                foreach (var entity in chunkEntities)
                {
                    if (scheduledCount >= maxDensityRebuilds)
                        break;

                    var grid = entityManager.GetComponentData<TerrainChunkGridInfo>(entity);
                    if (grid.VoxelCount <= 0)
                        continue;

                    var bounds = entityManager.GetComponentData<TerrainChunkBounds>(entity);

                    // Surface Nets needs one extra cell on +X/+Y/+Z to stitch chunk boundaries.
                    var densityResolution = new int3(
                        math.max(1, grid.Resolution.x + 1),
                        math.max(1, grid.Resolution.y + 1),
                        math.max(1, grid.Resolution.z + 1));

                    var densityCount = densityResolution.x * densityResolution.y * densityResolution.z;
                    if (densityCount <= 0)
                        continue;

                    var densities = new NativeArray<float>(densityCount, Allocator.TempJob);
                    handles[scheduledCount] = new TerrainChunkDensitySamplingJob
                    {
                        Resolution  = densityResolution,
                        VoxelSize   = grid.VoxelSize,
                        ChunkOrigin = bounds.WorldOrigin,
                        Field       = field,
                        Edits       = edits,
                        Densities   = densities
                    }.Schedule();

                    scheduledEntities[scheduledCount]  = entity;
                    densityArrays[scheduledCount]      = densities;
                    densityResolutions[scheduledCount] = densityResolution;
                    gridInfos[scheduledCount]          = grid;
                    boundsInfos[scheduledCount]        = bounds;
                    scheduledCount++;
                }

                // ── Phase 2: complete all jobs ────────────────────────────────────────
                // Worker threads finish in parallel; main thread waits once for all of them.
                if (scheduledCount > 0)
                    JobHandle.CompleteAll(handles.GetSubArray(0, scheduledCount));

                // ── Phase 3: build blobs and issue ECB commands (main thread) ─────────
                // BlobBuilder is not thread-safe, so this remains serial, but the
                // expensive noise sampling above already ran concurrently.
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                try
                {
                    for (int i = 0; i < scheduledCount; i++)
                    {
                        var entity           = scheduledEntities[i];
                        var densities        = densityArrays[i];
                        var densityResolution = densityResolutions[i];
                        var bounds           = boundsInfos[i];
                        var grid             = gridInfos[i];

                        var builder = new BlobBuilder(Allocator.Temp);
                        ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
                        root.Resolution  = densityResolution;
                        root.WorldOrigin = bounds.WorldOrigin;
                        root.VoxelSize   = grid.VoxelSize;
                        var values = builder.Allocate(ref root.Values, densities.Length);
                        for (int j = 0; j < densities.Length; j++)
                            values[j] = densities[j];

                        var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Persistent);
                        builder.Dispose();
                        densities.Dispose();
                        densityArrays[i] = default; // mark disposed so finally block skips it

                        if (entityManager.HasComponent<TerrainChunkDensity>(entity))
                        {
                            var existing = entityManager.GetComponentData<TerrainChunkDensity>(entity);
                            existing.Dispose();
                            ecb.SetComponent(entity, TerrainChunkDensity.FromBlob(blob));
                        }
                        else
                        {
                            ecb.AddComponent(entity, TerrainChunkDensity.FromBlob(blob));
                        }

                        if (entityManager.HasComponent<TerrainChunkDensityGridInfo>(entity))
                            ecb.SetComponent(entity, new TerrainChunkDensityGridInfo { Resolution = densityResolution });
                        else
                            ecb.AddComponent(entity, new TerrainChunkDensityGridInfo { Resolution = densityResolution });

                        if (entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(entity))
                            ecb.RemoveComponent<TerrainChunkNeedsDensityRebuild>(entity);

                        if (!entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                            ecb.AddComponent<TerrainChunkNeedsMeshBuild>(entity);

                        if (entityManager.HasComponent<DOTS.Terrain.Debug.TerrainChunkDebugState>(entity))
                        {
                            var debugState = entityManager.GetComponentData<DOTS.Terrain.Debug.TerrainChunkDebugState>(entity);
                            debugState.Stage = DOTS.Terrain.Debug.TerrainChunkDebugState.StageDensityReady;
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
                // Dispose any density arrays not consumed in Phase 3 (e.g. on exception).
                for (int i = 0; i < scheduledCount; i++)
                    if (densityArrays[i].IsCreated) densityArrays[i].Dispose();

                if (edits.IsCreated) edits.Dispose();
                handles.Dispose();
            }
        }

        private NativeArray<SDFEdit> CopyEditsToTempArray(ref SystemState state)
        {
            if (SystemAPI.TryGetSingletonBuffer<SDFEdit>(out var editBuffer) && editBuffer.Length > 0)
            {
                var edits = new NativeArray<SDFEdit>(editBuffer.Length, Allocator.TempJob);
                editBuffer.AsNativeArray().CopyTo(edits);
                return edits;
            }

            // Return an empty but valid array instead of default (which is uninitialized)
            return new NativeArray<SDFEdit>(0, Allocator.TempJob);
        }
    }

    [BurstCompile]
    public struct TerrainChunkDensitySamplingJob : IJob
    {
        public int3 Resolution;
        public float VoxelSize;
        public float3 ChunkOrigin;
        public SDFTerrainField Field;

        [ReadOnly] public NativeArray<SDFEdit> Edits;
        public NativeArray<float> Densities;

        public void Execute()
        {
            var resX = Resolution.x;
            var resY = Resolution.y;
            var resZ = Resolution.z;

            for (int z = 0; z < resZ; z++)
            {
                for (int y = 0; y < resY; y++)
                {
                    for (int x = 0; x < resX; x++)
                    {
                        var index = x + resX * (y + resY * z);
                        var worldPos = ChunkOrigin + new float3(x, y, z) * VoxelSize;
                        Densities[index] = Field.Sample(worldPos, Edits);
                    }
                }
            }
        }
    }
}
