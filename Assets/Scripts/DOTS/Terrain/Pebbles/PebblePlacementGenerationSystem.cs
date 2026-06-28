using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Generates deterministic pebble-cluster placement records for each density-sampled
    /// terrain chunk. Regenerates when the chunk tag is missing or generation version
    /// changes. Decorative family: no state-delta reapply step (contrast with rocks —
    /// TERRAIN_SURFACE_SCATTER_PLAN §7.4). Tuning comes from the optional
    /// PebblePlacementParams singleton (bootstrap-provided), falling back to defaults
    /// so the system works headless/in tests without a bootstrap.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct PebblePlacementGenerationSystem : ISystem
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
            var placementParams = SystemAPI.TryGetSingleton(out PebblePlacementParams configured)
                ? configured
                : PebblePlacementParams.Default;
            var tagLookup = SystemAPI.GetComponentLookup<ChunkPebblePlacementTag>(true);
            var placementLookup = SystemAPI.GetBufferLookup<PebblePlacementRecord>(true);
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

                var accepted = new NativeList<PebblePlacementRecord>(16, Allocator.Temp);
                ref var blob = ref blobRef.Value;

                PebblePlacementAlgorithm.GeneratePlacements(
                    ref blob,
                    chunkRO.ValueRO.ChunkCoord,
                    boundsRO.ValueRO.WorldOrigin,
                    context.WorldSeed,
                    in placementParams,
                    ref accepted);

                var buf = SurfaceScatterLifecycleUtility.SetOrAddPlacementBuffer<PebblePlacementRecord>(
                    entity,
                    ref placementLookup,
                    ref ecb);

                for (int i = 0; i < accepted.Length; i++)
                {
                    buf.Add(accepted[i]);
                }

                var newTag = new ChunkPebblePlacementTag
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
