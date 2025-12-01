using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using DOTS.Terrain.SDF;

namespace DOTS.Terrain.Meshing
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainChunkRenderPrepSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkMeshData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (meshData, entity) in SystemAPI.Query<RefRO<TerrainChunkMeshData>>().WithEntityAccess())
            {
                var mesh = meshData.ValueRO.Mesh;
                if (!mesh.IsCreated || mesh.Value.Vertices.Length == 0)
                {
                    continue;
                }

                var renderBounds = new RenderBounds { Value = ComputeBounds(mesh) };

                if (state.EntityManager.HasComponent<RenderBounds>(entity))
                {
                    ecb.SetComponent(entity, renderBounds);
                }
                else
                {
                    ecb.AddComponent(entity, renderBounds);
                }

                if (!state.EntityManager.HasComponent<LocalTransform>(entity))
                {
                    ecb.AddComponent(entity, LocalTransform.Identity); // Identity keeps chunks at origin until transform data is authored.
                }

                if (!state.EntityManager.HasComponent<MaterialMeshInfo>(entity))
                {
                    ecb.AddComponent(entity, default(MaterialMeshInfo)); // Placeholder material hookup until RenderMeshArray wiring is in place.
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        public static Unity.Mathematics.AABB ComputeBounds(BlobAssetReference<TerrainChunkMeshBlob> mesh)
        {
            ref var vertices = ref mesh.Value.Vertices;
            var vertexCount = vertices.Length;
            if (vertexCount == 0)
            {
                return new Unity.Mathematics.AABB { Center = float3.zero, Extents = float3.zero };
            }

            var min = vertices[0];
            var max = vertices[0];

            for (int i = 1; i < vertexCount; i++)
            {
                ref readonly var v = ref vertices[i];
                // Track min/max corners to build the chunk's axis-aligned bounds.
                min = math.min(min, v);
                max = math.max(max, v);
            }

            var center = (min + max) * 0.5f;
            var extents = (max - min) * 0.5f;
            return new Unity.Mathematics.AABB { Center = center, Extents = extents };
        }
    }
}
