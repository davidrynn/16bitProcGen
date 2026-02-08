using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Terrain;
using DOTS.Terrain.Debug;

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

            // Check if debug mode is enabled
            var debugEnabled = false;
            if (SystemAPI.HasSingleton<TerrainDebugConfig>())
            {
                var debugConfig = SystemAPI.GetSingleton<TerrainDebugConfig>();
                debugEnabled = debugConfig.Enabled;
            }

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

                // Populate mesh debug data if debug is enabled
                if (debugEnabled)
                {
                    var meshDebugData = ComputeMeshDebugData(ref meshBlob, grid.ValueRO);
                    if (entityManager.HasComponent<TerrainChunkMeshDebugData>(entity))
                    {
                        ecb.SetComponent(entity, meshDebugData);
                    }
                    else
                    {
                        ecb.AddComponent(entity, meshDebugData);
                    }
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
                if (entityManager.HasComponent<TerrainChunkDebugState>(entity))
                {
                    var debugState = entityManager.GetComponentData<TerrainChunkDebugState>(entity);
                    debugState.Stage = TerrainChunkDebugState.StageMeshReady;
                    ecb.SetComponent(entity, debugState);
                }
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
        }

        private static TerrainChunkMeshDebugData ComputeMeshDebugData(
            ref BlobAssetReference<TerrainChunkMeshBlob> meshBlob,
            in TerrainChunkGridInfo grid)
        {
            ref var mesh = ref meshBlob.Value;
            var vertexCount = mesh.Vertices.Length;
            var indexCount = mesh.Indices.Length;

            // Compute bounds and border vertex count
            var boundsMin = new float3(float.MaxValue);
            var boundsMax = new float3(float.MinValue);
            var borderVertexCount = 0;

            // Chunk size in local space
            var chunkSizeX = (grid.Resolution.x - 1) * grid.VoxelSize;
            var chunkSizeZ = (grid.Resolution.z - 1) * grid.VoxelSize;
            var borderThreshold = grid.VoxelSize;

            for (int i = 0; i < vertexCount; i++)
            {
                var v = mesh.Vertices[i];
                boundsMin = math.min(boundsMin, v);
                boundsMax = math.max(boundsMax, v);

                // Check if vertex is on a border (within VoxelSize of chunk edge)
                var onWestBorder = v.x <= borderThreshold;
                var onEastBorder = v.x >= chunkSizeX - borderThreshold;
                var onSouthBorder = v.z <= borderThreshold;
                var onNorthBorder = v.z >= chunkSizeZ - borderThreshold;

                if (onWestBorder || onEastBorder || onSouthBorder || onNorthBorder)
                {
                    borderVertexCount++;
                }
            }

            // Handle empty mesh case
            if (vertexCount == 0)
            {
                boundsMin = float3.zero;
                boundsMax = float3.zero;
            }

            return new TerrainChunkMeshDebugData
            {
                VertexCount = vertexCount,
                TriangleCount = indexCount / 3,
                BorderVertexCount = borderVertexCount,
                BoundsMin = boundsMin,
                BoundsMax = boundsMax
            };
        }
    }
}
