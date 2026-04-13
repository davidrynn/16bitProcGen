using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Drops render-only rock placement state for chunks queued for density rebuild.
    /// The next generation pass then rebuilds records from current density plus sparse deltas.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.LOD.TerrainChunkLodApplySystem))]
    [UpdateBefore(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct RockPlacementInvalidationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkNeedsDensityRebuild>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var tagLookup = SystemAPI.GetComponentLookup<ChunkRockPlacementTag>(true);
            var placementLookup = SystemAPI.GetBufferLookup<RockPlacementRecord>(true);
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<TerrainChunk>>()
                         .WithAll<TerrainChunkNeedsDensityRebuild>()
                         .WithEntityAccess())
            {
                SurfaceScatterLifecycleUtility.RemovePlacementStateIfPresent<RockPlacementRecord, ChunkRockPlacementTag>(
                    entity,
                    ref placementLookup,
                    ref tagLookup,
                    ref ecb);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
