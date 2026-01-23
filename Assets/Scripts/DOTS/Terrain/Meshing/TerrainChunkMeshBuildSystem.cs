using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Meshing
{
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainChunkMeshBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkDensity>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
            
            foreach (var (density, grid, densityGrid, bounds, entity) in SystemAPI
                         .Query<RefRW<TerrainChunkDensity>, RefRO<TerrainChunkGridInfo>, RefRO<TerrainChunkDensityGridInfo>, RefRO<TerrainChunkBounds>>()
                         .WithAll<TerrainChunkNeedsMeshBuild>()
                         .WithEntityAccess())
            {
                var meshBlob = TerrainChunkMeshBuilder.BuildMeshBlob(ref density.ValueRW, grid.ValueRO, densityGrid.ValueRO, bounds.ValueRO);

                if (!meshBlob.IsCreated)
                {
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

                if (!entityManager.HasComponent<TerrainChunkNeedsRenderUpload>(entity))
                {
                    ecb.AddComponent<TerrainChunkNeedsRenderUpload>(entity);
                }

                if (!entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity))
                {
                    ecb.AddComponent<TerrainChunkNeedsColliderBuild>(entity);
                }

                if (entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                {
                    ecb.RemoveComponent<TerrainChunkNeedsMeshBuild>(entity);
                }

                // Update debug state if present
                if (entityManager.HasComponent<DOTS.Terrain.Debug.TerrainChunkDebugState>(entity))
                {
                    var debugState = entityManager.GetComponentData<DOTS.Terrain.Debug.TerrainChunkDebugState>(entity);
                    debugState.Stage = DOTS.Terrain.Debug.TerrainChunkDebugState.StageMeshReady;
                    ecb.SetComponent(entity, debugState);
                }
            }
            
            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
