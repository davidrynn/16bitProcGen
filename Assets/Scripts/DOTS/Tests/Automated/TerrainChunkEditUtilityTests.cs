using DOTS.Terrain;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkEditUtilityTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("TerrainChunkEditUtilityTests");
            entityManager = testWorld.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (testWorld != null && testWorld.IsCreated)
            {
                testWorld.Dispose();
            }
        }

        [Test]
        public void MarkChunksDirty_AddsTagToAllChunks_WhenRadiusZero()
        {
            var chunkA = CreateChunk(float3.zero);
            var chunkB = CreateChunk(new float3(50f, 0f, 50f));

            var chunkEntities = new NativeArray<Entity>(2, Allocator.Temp);
            try
            {
                chunkEntities[0] = chunkA;
                chunkEntities[1] = chunkB;

                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunkEntities, float3.zero, 0f);

                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkA));
                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkB));
            }
            finally
            {
                chunkEntities.Dispose();
            }
        }

        [Test]
        public void MarkChunksDirty_DirtiesOnlyIntersectingChunks()
        {
            var chunkNear = CreateChunk(float3.zero);
            var chunkFar = CreateChunk(new float3(32f, 0f, 0f));

            var chunkEntities = new NativeArray<Entity>(2, Allocator.Temp);
            try
            {
                chunkEntities[0] = chunkNear;
                chunkEntities[1] = chunkFar;

                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunkEntities, new float3(4f, 0f, 4f), 6f);

                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkNear), "Intersecting chunk should be flagged");
                Assert.IsFalse(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkFar), "Non-intersecting chunk remains untouched");
            }
            finally
            {
                chunkEntities.Dispose();
            }
        }

        [Test]
        public void MarkChunksDirty_IsIdempotent()
        {
            var chunk = entityManager.CreateEntity(typeof(TerrainChunk));
            entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunk);

            var chunkEntities = new NativeArray<Entity>(1, Allocator.Temp);
            try
            {
                chunkEntities[0] = chunk;

                Assert.DoesNotThrow(() =>
                    TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunkEntities, float3.zero, 5f));

                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunk));
            }
            finally
            {
                chunkEntities.Dispose();
            }
        }

        private Entity CreateChunk(float3 worldOrigin)
        {
            var entity = entityManager.CreateEntity(typeof(TerrainChunk), typeof(TerrainChunkGridInfo), typeof(TerrainChunkBounds));
            entityManager.SetComponentData(entity, TerrainChunkGridInfo.Create(new int3(8, 8, 8), 1f));
            entityManager.SetComponentData(entity, new TerrainChunkBounds { WorldOrigin = worldOrigin });
            return entity;
        }
    }
}
