using System.Collections.Generic;
using DOTS.Terrain.Rendering;
using DOTS.Terrain;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
#if UNITY_ENTITIES_GRAPHICS
using Unity.Entities.Graphics;
using Unity.Rendering;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Meshing
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkMeshBuildSystem))]
    public partial struct TerrainChunkMeshUploadSystem : ISystem
    {
        private static bool loggedMissingMaterial;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainChunkNeedsRenderUpload>();
            loggedMissingMaterial = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            var settings = TerrainChunkRenderSettingsProvider.GetOrLoad();
            if (settings == null || settings.ChunkMaterial == null)
            {
                if (!loggedMissingMaterial)
                {
                    loggedMissingMaterial = true;
                    Debug.LogWarning("[DOTS Terrain] TerrainChunkMeshUploadSystem: No chunk material available (TerrainChunkRenderSettings missing or has no material). Chunks will not render until a material is assigned.");
                }
                return;
            }

            var entityManager = state.EntityManager;
            var material = settings.ChunkMaterial;

            var uploadItems = new List<UploadItem>();

            foreach (var (meshData, entity) in SystemAPI
                         .Query<RefRO<TerrainChunkMeshData>>()
                         .WithAll<TerrainChunkNeedsRenderUpload>()
                         .WithEntityAccess())
            {
                var mesh = entityManager.HasComponent<Mesh>(entity)
                    ? entityManager.GetComponentObject<Mesh>(entity)
                    : null;

                var needsMeshComponent = mesh == null;
                if (needsMeshComponent)
                {
                    mesh = CreateMeshInstance(entity);
                }

                uploadItems.Add(new UploadItem
                {
                    Entity = entity,
                    Blob = meshData.ValueRO.Mesh,
                    Mesh = mesh,
                    NeedsMeshComponent = needsMeshComponent
                });
            }

            if (uploadItems.Count == 0)
            {
                return;
            }

            foreach (var item in uploadItems)
            {
                if (item.NeedsMeshComponent)
                {
                    entityManager.AddComponentObject(item.Entity, item.Mesh);
                }
            }

            foreach (var item in uploadItems)
            {
                var blob = item.Blob;
                if (!blob.IsCreated || blob.Value.Vertices.Length == 0 || blob.Value.Indices.Length == 0)
                {
                    if (entityManager.HasComponent<TerrainChunkNeedsRenderUpload>(item.Entity))
                    {
                        entityManager.RemoveComponent<TerrainChunkNeedsRenderUpload>(item.Entity);
                    }

                    continue;
                }

                UploadMesh(blob, item.Mesh);
#if UNITY_ENTITIES_GRAPHICS
                EnsureEntitiesGraphicsComponents(entityManager, item.Entity, item.Mesh, material);
#endif

                if (entityManager.HasComponent<TerrainChunkNeedsRenderUpload>(item.Entity))
                {
                    entityManager.RemoveComponent<TerrainChunkNeedsRenderUpload>(item.Entity);
                }

                // Update debug state if present
                if (entityManager.HasComponent<DOTS.Terrain.Debug.TerrainChunkDebugState>(item.Entity))
                {
                    var debugState = entityManager.GetComponentData<DOTS.Terrain.Debug.TerrainChunkDebugState>(item.Entity);
                    debugState.Stage = DOTS.Terrain.Debug.TerrainChunkDebugState.StageUploaded;
                    entityManager.SetComponentData(item.Entity, debugState);
                }
            }
        }

        private static Mesh CreateMeshInstance(Entity entity)
        {
            return new Mesh
            {
                name = $"TerrainChunk_{entity.Index}",
                indexFormat = IndexFormat.UInt32
            };
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

        private static void EnsureEntitiesGraphicsComponents(EntityManager entityManager, Entity entity, Mesh mesh, Material material)
        {
#if UNITY_ENTITIES_GRAPHICS
            var renderMeshArray = new RenderMeshArray(new[] { material }, new[] { mesh });
            var materialMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0);

            // RenderMeshUtility.AddComponents adds the *minimum required* Entities Graphics components.
            // Without these (WorldRenderBounds, RenderFilterSettings, PerInstanceCullingTag, etc.) the entity will never render.
            if (!entityManager.HasComponent<RenderFilterSettings>(entity) || !entityManager.HasComponent<WorldRenderBounds>(entity))
            {
                var renderMeshDescription = new RenderMeshDescription(
                    shadowCastingMode: ShadowCastingMode.On,
                    receiveShadows: true);

                RenderMeshUtility.AddComponents(entity, entityManager, renderMeshDescription, renderMeshArray, materialMeshInfo);
            }
            else
            {
                // Already renderable; just refresh the mesh/material bindings.
                entityManager.SetSharedComponentManaged(entity, renderMeshArray);
                entityManager.SetComponentData(entity, materialMeshInfo);
            }
#endif
        }

        private struct UploadItem
        {
            public Entity Entity;
            public BlobAssetReference<TerrainChunkMeshBlob> Blob;
            public Mesh Mesh;
            public bool NeedsMeshComponent;
        }
    }
}
