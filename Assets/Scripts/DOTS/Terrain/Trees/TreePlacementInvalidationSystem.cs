using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Drops render-only tree placement state for any chunk queued for density rebuild.
    /// The next tree generation pass then rebuilds placements from current density plus sparse deltas.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.LOD.TerrainChunkLodApplySystem))]
    [UpdateBefore(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct TreePlacementInvalidationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkNeedsDensityRebuild>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tagLookup = SystemAPI.GetComponentLookup<ChunkTreePlacementTag>(true);
            var placementLookup = SystemAPI.GetBufferLookup<TreePlacementRecord>(true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<TerrainChunk>>()
                .WithAll<TerrainChunkNeedsDensityRebuild>()
                .WithEntityAccess())
            {
                if (placementLookup.HasBuffer(entity))
                {
                    ecb.RemoveComponent<TreePlacementRecord>(entity);
                }

                if (tagLookup.HasComponent(entity))
                {
                    ecb.RemoveComponent<ChunkTreePlacementTag>(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}