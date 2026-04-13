using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Generates deterministic rock placement records for each density-sampled terrain chunk.
    /// Regenerates when the chunk tag is missing or generation version changes.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct RockPlacementGenerationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainGenerationContext>();
            state.RequireForUpdate<TerrainFieldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var context = SystemAPI.GetSingleton<TerrainGenerationContext>();
            var tagLookup = SystemAPI.GetComponentLookup<ChunkRockPlacementTag>(true);
            var placementLookup = SystemAPI.GetBufferLookup<RockPlacementRecord>(true);
            var deltaLookup = SystemAPI.GetBufferLookup<RockStateDelta>(true);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);

            foreach (var (chunkRO, boundsRO, densityRO, entity) in
                     SystemAPI.Query<
                             RefRO<TerrainChunk>,
                             RefRO<TerrainChunkBounds>,
                             RefRO<TerrainChunkDensity>>()
                         .WithEntityAccess())
            {
                if (tagLookup.HasComponent(entity) &&
                    tagLookup[entity].GenerationVersion == context.GenerationVersion)
                {
                    continue;
                }

                var blobRef = densityRO.ValueRO.Data;
                if (!blobRef.IsCreated)
                {
                    continue;
                }

                var accepted = new NativeList<RockPlacementRecord>(16, Allocator.Temp);
                ref var blob = ref blobRef.Value;

                RockPlacementAlgorithm.GeneratePlacements(
                    ref blob,
                    chunkRO.ValueRO.ChunkCoord,
                    boundsRO.ValueRO.WorldOrigin,
                    context.WorldSeed,
                    ref accepted);

                if (deltaLookup.HasBuffer(entity))
                {
                    RockPlacementDeltaUtility.ApplyStateDeltas(
                        ref accepted,
                        deltaLookup[entity].AsNativeArray());
                }

                var buf = SurfaceScatterLifecycleUtility.SetOrAddPlacementBuffer<RockPlacementRecord>(
                    entity,
                    ref placementLookup,
                    ref ecb);

                for (int i = 0; i < accepted.Length; i++)
                {
                    buf.Add(accepted[i]);
                }

                var newTag = new ChunkRockPlacementTag
                {
                    GenerationVersion = context.GenerationVersion,
                };
                SurfaceScatterLifecycleUtility.SetOrAddGenerationTag(
                    entity,
                    in newTag,
                    ref tagLookup,
                    ref ecb);

                accepted.Dispose();
            }
        }
    }
}
