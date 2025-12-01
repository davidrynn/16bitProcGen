using DOTS.Terrain.Meshing;
using DOTS.Terrain.SDF;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkRenderPrepSystemTests
    {
        [Test]
        public void ComputeBounds_ReturnsCenterAndExtents()
        {
            var blob = CreateMeshBlob(new[]
            {
                new float3(0f, 0f, 0f),
                new float3(2f, 4f, 6f)
            });

            var bounds = TerrainChunkRenderPrepSystem.ComputeBounds(blob);

            Assert.AreEqual(new float3(1f, 2f, 3f), bounds.Center);
            Assert.AreEqual(new float3(1f, 2f, 3f), bounds.Extents);

            blob.Dispose();
        }

        [Test]
        public void RenderPrepSystem_AddsRequiredComponents()
        {
            using var world = new World("TerrainChunkRenderPrepSystemTests");
            var entityManager = world.EntityManager;

            var systemHandle = world.CreateSystem<TerrainChunkRenderPrepSystem>();
            var simGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(systemHandle);

            var blob = CreateMeshBlob(new[]
            {
                new float3(-1f, 0f, 0f),
                new float3(1f, 2f, 3f)
            });

            var entity = entityManager.CreateEntity();
            entityManager.AddComponentData(entity, new TerrainChunkMeshData { Mesh = blob });

            simGroup.Update();

            Assert.IsTrue(entityManager.HasComponent<RenderBounds>(entity));
            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(entity));
            Assert.IsTrue(entityManager.HasComponent<MaterialMeshInfo>(entity));

            var renderBounds = entityManager.GetComponentData<RenderBounds>(entity);
            Assert.AreEqual(new float3(0f, 1f, 1.5f), renderBounds.Value.Center);
            Assert.AreEqual(new float3(1f, 1f, 1.5f), renderBounds.Value.Extents);

            var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
            meshData.Dispose();
        }

        private static BlobAssetReference<TerrainChunkMeshBlob> CreateMeshBlob(float3[] vertices)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkMeshBlob>();
            var data = builder.Allocate(ref root.Vertices, vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                data[i] = vertices[i];
            }

            var blob = builder.CreateBlobAssetReference<TerrainChunkMeshBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
    }
}
