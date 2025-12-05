using DOTS.Terrain.SDF;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;

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
        public void MarkChunksDirty_AddsTagToAllChunks()
        {
            var chunkA = entityManager.CreateEntity(typeof(TerrainChunk));
            var chunkB = entityManager.CreateEntity(typeof(TerrainChunk));

            var chunkEntities = new NativeArray<Entity>(2, Allocator.Temp);
            try
            {
                chunkEntities[0] = chunkA;
                chunkEntities[1] = chunkB;

                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunkEntities);

                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkA));
                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkB));
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
                    TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunkEntities));

                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunk));
            }
            finally
            {
                chunkEntities.Dispose();
            }
        }
    }
}
