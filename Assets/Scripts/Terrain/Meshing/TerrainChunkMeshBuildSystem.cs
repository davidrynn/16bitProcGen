using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Terrain.SDF;

namespace DOTS.Terrain.Meshing
{
    [BurstCompile]
    public partial struct TerrainChunkMeshBuildSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkDensity>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            foreach (var (density, grid, bounds, entity) in SystemAPI
                         .Query<RefRW<TerrainChunkDensity>, RefRO<TerrainChunkGridInfo>, RefRO<TerrainChunkBounds>>()
                         .WithAll<TerrainChunkNeedsMeshBuild>()
                         .WithEntityAccess())
            {
                var meshBlob = TerrainChunkMeshBuilder.BuildMeshBlob(ref density.ValueRW, grid.ValueRO, bounds.ValueRO);

                if (!meshBlob.IsCreated)
                {
                    continue;
                }

                if (entityManager.HasComponent<TerrainChunkMeshData>(entity))
                {
                    var existing = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                    existing.Dispose();
                    entityManager.SetComponentData(entity, new TerrainChunkMeshData { Mesh = meshBlob });
                }
                else
                {
                    entityManager.AddComponentData(entity, new TerrainChunkMeshData { Mesh = meshBlob });
                }

                if (entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(entity))
                {
                    entityManager.RemoveComponent<TerrainChunkNeedsMeshBuild>(entity);
                }
            }
        }
    }
}
