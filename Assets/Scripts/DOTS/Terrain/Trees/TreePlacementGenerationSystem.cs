using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Generates sparse plains tree placement records for each density-sampled terrain chunk.
    /// Regenerates when a chunk's placement tag is missing or its generation version no longer
    /// matches the terrain generation context. Produces a TreePlacementRecord DynamicBuffer on
    /// each chunk entity with zero or more accepted tree sites, then overlays sparse tree deltas.
    ///
    /// All candidate positions are derived from world-space coordinates — never chunk-local
    /// restarts — so placements are seam-safe and deterministic across streaming cycles.
    ///
    /// The placement algorithm lives in TreePlacementAlgorithm (separate static class) so that
    /// Burst does not treat the public static helpers as C-ABI function-pointer entry points,
    /// which would trigger BC1064/BC1067 on struct-typed parameters.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct TreePlacementGenerationSystem : ISystem
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
            var tagLookup = SystemAPI.GetComponentLookup<ChunkTreePlacementTag>(true);
            var placementLookup = SystemAPI.GetBufferLookup<TreePlacementRecord>(true);
            var deltaLookup = SystemAPI.GetBufferLookup<TreeStateDelta>(true);
            var ecb     = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
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
                if (!blobRef.IsCreated) continue;

                var accepted = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
                ref var blob = ref blobRef.Value;

                TreePlacementAlgorithm.GeneratePlacements(
                    ref blob,
                    chunkRO.ValueRO.ChunkCoord,
                    boundsRO.ValueRO.WorldOrigin,
                    context.WorldSeed,
                    ref accepted);

                if (deltaLookup.HasBuffer(entity))
                {
                    TreePlacementDeltaUtility.ApplyStateDeltas(
                        ref accepted,
                        deltaLookup[entity].AsNativeArray());
                }

                var buf = placementLookup.HasBuffer(entity)
                    ? ecb.SetBuffer<TreePlacementRecord>(entity)
                    : ecb.AddBuffer<TreePlacementRecord>(entity);

                for (int i = 0; i < accepted.Length; i++)
                    buf.Add(accepted[i]);

                if (tagLookup.HasComponent(entity))
                {
                    ecb.SetComponent(entity, new ChunkTreePlacementTag
                    {
                        GenerationVersion = context.GenerationVersion
                    });
                }
                else
                {
                    ecb.AddComponent(entity, new ChunkTreePlacementTag
                    {
                        GenerationVersion = context.GenerationVersion
                    });
                }

                accepted.Dispose();
            }
        }
    }
}
