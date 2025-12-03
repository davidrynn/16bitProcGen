using DOTS.Terrain.Rendering;
using DOTS.Terrain.SDF;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Meshing
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkMeshBuildSystem))]
    public partial struct TerrainChunkMeshUploadSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkNeedsRenderUpload>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var settings = TerrainChunkRenderSettingsProvider.GetOrLoad();
            if (settings == null || settings.ChunkMaterial == null)
            {
                return;
            }

            var entityManager = state.EntityManager;
            var material = settings.ChunkMaterial;

            var entities = new NativeList<Entity>(Allocator.Temp);
            var meshes = new NativeList<BlobAssetReference<TerrainChunkMeshBlob>>(Allocator.Temp);

            foreach (var (meshData, entity) in SystemAPI
                         .Query<RefRO<TerrainChunkMeshData>>()
                         .WithAll<TerrainChunkNeedsRenderUpload>()
                         .WithEntityAccess())
            {
                entities.Add(entity);
                meshes.Add(meshData.ValueRO.Mesh);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var blob = meshes[i];

                if (!blob.IsCreated || blob.Value.Vertices.Length == 0 || blob.Value.Indices.Length == 0)
                {
                    ecb.RemoveComponent<TerrainChunkNeedsRenderUpload>(entity);
                    continue;
                }

                var unityMesh = GetOrCreateMesh(entityManager, entity);
                UploadMesh(blob, unityMesh);
                EnsureRenderMeshArray(entityManager, entity, unityMesh, material);

                var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);
                if (entityManager.HasComponent<MaterialMeshInfo>(entity))
                {
                    ecb.SetComponent(entity, materialMeshInfo);
                }
                else
                {
                    ecb.AddComponent(entity, materialMeshInfo);
                }

                ecb.RemoveComponent<TerrainChunkNeedsRenderUpload>(entity);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            entities.Dispose();
            meshes.Dispose();
        }

        private static Mesh GetOrCreateMesh(EntityManager entityManager, Entity entity)
        {
            if (entityManager.HasComponent<Mesh>(entity))
            {
                return entityManager.GetComponentObject<Mesh>(entity);
            }

            var mesh = new Mesh
            {
                name = $"TerrainChunk_{entity.Index}",
                indexFormat = IndexFormat.UInt32
            };

            entityManager.AddComponentObject(entity, mesh);
            return mesh;
        }

        private static void UploadMesh(BlobAssetReference<TerrainChunkMeshBlob> blob, Mesh mesh)
        {
            var vertexCount = blob.Value.Vertices.Length;
            var indexCount = blob.Value.Indices.Length;

            var meshDataArray = Mesh.AllocateWritableMeshData(1);
            var meshData = meshDataArray[0];

            meshData.SetVertexBufferParams(vertexCount,
                new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3));

            var vertexBuffer = meshData.GetVertexData<Vector3>();
            for (int i = 0; i < vertexCount; i++)
            {
                var v = blob.Value.Vertices[i];
                vertexBuffer[i] = new Vector3(v.x, v.y, v.z);
            }

            meshData.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
            var indexBuffer = meshData.GetIndexData<int>();
            for (int i = 0; i < indexCount; i++)
            {
                indexBuffer[i] = blob.Value.Indices[i];
            }

            var subMesh = new SubMeshDescriptor(0, indexCount, MeshTopology.Triangles)
            {
                vertexCount = vertexCount
            };

            Mesh.ApplyAndDisposeWritableMeshData(meshDataArray, mesh);
            mesh.subMeshCount = 1;
            mesh.SetSubMesh(0, subMesh, MeshUpdateFlags.DontRecalculateBounds);
            mesh.RecalculateBounds();
        }

        private static void EnsureRenderMeshArray(EntityManager entityManager, Entity entity, Mesh mesh, Material material)
        {
            var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });

            if (entityManager.HasComponent<RenderMeshArray>(entity))
            {
                entityManager.SetSharedComponentManaged(entity, renderMeshArray);
            }
            else
            {
                entityManager.AddSharedComponentManaged(entity, renderMeshArray);
            }
        }
    }
}
