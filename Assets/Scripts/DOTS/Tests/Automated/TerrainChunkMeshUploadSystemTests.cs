using DOTS.Terrain.Meshing;
using DOTS.Terrain.Rendering;
using DOTS.Terrain.SDF;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkMeshUploadSystemTests
    {
        [SetUp]
        public void SetUp()
        {
            TerrainChunkRenderSettingsProvider.ResetCache();
        }

        [TearDown]
        public void TearDown()
        {
            TerrainChunkRenderSettingsProvider.ResetCache();
        }

        [Test]
        public void MeshUploadSystem_AttachesMeshAndRenderComponents()
        {
            using var world = new World("TerrainChunkMeshUploadSystemTests");
            var entityManager = world.EntityManager;

            var settings = ScriptableObject.CreateInstance<TerrainChunkRenderSettings>();
            settings.name = "TestTerrainRenderSettings";
                settings.SetChunkMaterial(CreateTestMaterial());
            TerrainChunkRenderSettingsProvider.OverrideSettings = settings;

            var systemHandle = world.CreateSystem<TerrainChunkMeshUploadSystem>();
            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(systemHandle);

            var entity = entityManager.CreateEntity();
            var blob = CreateMeshBlob();
            entityManager.AddComponentData(entity, new TerrainChunkMeshData { Mesh = blob });
            entityManager.AddComponent<TerrainChunkNeedsRenderUpload>(entity);

            simGroup.Update();

            Assert.IsFalse(entityManager.HasComponent<TerrainChunkNeedsRenderUpload>(entity));
            Assert.IsTrue(entityManager.HasComponent<RenderMeshArray>(entity));
            Assert.IsTrue(entityManager.HasComponent<MaterialMeshInfo>(entity));
            Assert.IsTrue(entityManager.HasComponent<Mesh>(entity));

            var renderMeshArray = entityManager.GetSharedComponentManaged<RenderMeshArray>(entity);
            Assert.IsNotNull(renderMeshArray.MeshReferences);
            Assert.AreEqual(1, renderMeshArray.MeshReferences.Length);
            Assert.IsNotNull(renderMeshArray.MeshReferences[0].Value);

            var mesh = entityManager.GetComponentObject<Mesh>(entity);
            Assert.Greater(mesh.vertexCount, 0);
            Assert.Greater(mesh.GetIndexCount(0), 0);

            var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
            meshData.Dispose();
            Object.DestroyImmediate(mesh);
            Object.DestroyImmediate(renderMeshArray.MaterialReferences[0].Value);
            Object.DestroyImmediate(settings);
        }

        private static BlobAssetReference<TerrainChunkMeshBlob> CreateMeshBlob()
        {
            var builder = new BlobBuilder(Unity.Collections.Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkMeshBlob>();
            var vertices = builder.Allocate(ref root.Vertices, 3);
            vertices[0] = float3.zero;
            vertices[1] = new float3(0f, 1f, 0f);
            vertices[2] = new float3(1f, 0f, 0f);

            var indices = builder.Allocate(ref root.Indices, 3);
            indices[0] = 0;
            indices[1] = 1;
            indices[2] = 2;

            var blob = builder.CreateBlobAssetReference<TerrainChunkMeshBlob>(Unity.Collections.Allocator.Persistent);
            builder.Dispose();
            return blob;
        }

        private static Material CreateTestMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
            return new Material(shader);
        }
    }
}
