using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
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
                UnityEngine.Debug.LogWarning("[TerrainChunkDensitySamplingSystem] SDFTerrainFieldSettings singleton not found. Waiting for TerrainBootstrapAuthoring...");
                return;
            }

            if (chunkQuery.IsEmpty)
            {
                return;
            }

            var settings = SystemAPI.GetSingleton<SDFTerrainFieldSettings>();
            var field = new SDFTerrainField
            {
                BaseHeight = settings.BaseHeight,
                Amplitude = settings.Amplitude,
                Frequency = settings.Frequency,
                NoiseValue = settings.NoiseValue
            };

            var edits = CopyEditsToTempArray(ref state);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            try
            {
                var entityManager = state.EntityManager;
                using var chunkEntities = chunkQuery.ToEntityArray(Allocator.Temp);

                foreach (var entity in chunkEntities)
                {
                    var grid = entityManager.GetComponentData<TerrainChunkGridInfo>(entity);
                    var bounds = entityManager.GetComponentData<TerrainChunkBounds>(entity);

                    if (grid.VoxelCount <= 0)
                    {
                        continue;
                    }

                    // Surface Nets needs one extra cell on +X/+Y/+Z to stitch chunk boundaries.
                    // We provide this by sampling one extra layer of density samples on all axes.
                    var densityResolution = new int3(
                        math.max(1, grid.Resolution.x + 1),
                        math.max(1, grid.Resolution.y + 1),
                        math.max(1, grid.Resolution.z + 1));

                    var densityCount = densityResolution.x * densityResolution.y * densityResolution.z;
                    if (densityCount <= 0)
                    {
                        continue;
                    }

                    var densities = new NativeArray<float>(densityCount, Allocator.TempJob);

                    var job = new TerrainChunkDensitySamplingJob
                    {
                        Resolution = densityResolution,
                        VoxelSize = grid.VoxelSize,
                        ChunkOrigin = bounds.WorldOrigin,
                        Field = field,
                        Edits = edits,
                        Densities = densities
                    };

                    job.Run();

                    var builder = new BlobBuilder(Allocator.Temp);
                    ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
                    root.Resolution = densityResolution;
                    root.WorldOrigin = bounds.WorldOrigin;
                    root.VoxelSize = grid.VoxelSize;
                    var values = builder.Allocate(ref root.Values, densities.Length);
                    for (int i = 0; i < densities.Length; i++)
                    {
                        values[i] = densities[i];
                    }

                    var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Persistent);
                    builder.Dispose();
                    densities.Dispose();

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
                    {
                        ecb.SetComponent(entity, new TerrainChunkDensityGridInfo { Resolution = densityResolution });
                    }
                    else
                    {
                        ecb.AddComponent(entity, new TerrainChunkDensityGridInfo { Resolution = densityResolution });
                    }

                    if (entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(entity))
                    {
                        ecb.RemoveComponent<TerrainChunkNeedsDensityRebuild>(entity);
                    }

                    if (!entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                    {
                        ecb.AddComponent<TerrainChunkNeedsMeshBuild>(entity);
                    }

                    // Update debug state if present
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
                if (edits.IsCreated)
                {
                    edits.Dispose();
                }
                ecb.Dispose();
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
