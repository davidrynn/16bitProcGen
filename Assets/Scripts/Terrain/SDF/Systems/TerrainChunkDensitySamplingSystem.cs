using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace DOTS.Terrain.SDF
{
    [BurstCompile]
    public partial struct TerrainChunkDensitySamplingSystem : ISystem
    {
        private EntityQuery chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SDFTerrainFieldSettings>();
            chunkQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkGridInfo>(),
                ComponentType.ReadOnly<TerrainChunkBounds>());
        }

        public void OnUpdate(ref SystemState state)
        {
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

                    var densities = new NativeArray<float>(grid.VoxelCount, Allocator.TempJob);

                    var job = new TerrainChunkDensitySamplingJob
                    {
                        Resolution = grid.Resolution,
                        VoxelSize = grid.VoxelSize,
                        ChunkOrigin = bounds.WorldOrigin,
                        Field = field,
                        Edits = edits,
                        Densities = densities
                    };

                    job.Run();

                    var builder = new BlobBuilder(Allocator.Temp);
                    ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
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
                        entityManager.SetComponentData(entity, TerrainChunkDensity.FromBlob(blob));
                    }
                    else
                    {
                        entityManager.AddComponentData(entity, TerrainChunkDensity.FromBlob(blob));
                    }
                }
            }
            finally
            {
                if (edits.IsCreated)
                {
                    edits.Dispose();
                }
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

            return default;
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
