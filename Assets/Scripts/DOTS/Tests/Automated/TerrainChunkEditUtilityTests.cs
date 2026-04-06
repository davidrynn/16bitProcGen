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

        [Test]
        public void SnapToGlobalLattice_HandlesPositiveAndNegativeCoordinates()
        {
            var snappedPositive = TerrainChunkEditUtility.SnapToGlobalLattice(
                new float3(3.2f, -1.1f, 5.9f),
                float3.zero,
                4f);

            var snappedNegative = TerrainChunkEditUtility.SnapToGlobalLattice(
                new float3(-6.1f, 0.2f, -2.2f),
                float3.zero,
                4f);

            Assert.AreEqual(new float3(4f, 0f, 4f), snappedPositive);
            Assert.AreEqual(new float3(-8f, 0f, -4f), snappedNegative);
        }

        [Test]
        public void ComputeQuantizedCellSize_UsesVoxelMultipleAndClampsFraction()
        {
            var grid = TerrainChunkGridInfo.Create(new int3(17, 17, 17), 0.5f);

            var quantized = TerrainChunkEditUtility.ComputeQuantizedCellSize(in grid, 0.3f);
            var clampedMin = TerrainChunkEditUtility.ComputeQuantizedCellSize(in grid, 0.01f);
            var clampedMax = TerrainChunkEditUtility.ComputeQuantizedCellSize(in grid, 2f);

            Assert.AreEqual(2.5f, quantized, 1e-4f);
            Assert.AreEqual(2f, clampedMin, 1e-4f);
            Assert.AreEqual(8f, clampedMax, 1e-4f);
        }

        [Test]
        public void SnapToChunkLocalLattice_UsesCellCentersDeterministically()
        {
            var origin = new float3(7f, 0f, 0f);
            var cellSize = 2f;

            var nearLower = TerrainChunkEditUtility.SnapToChunkLocalLattice(new float3(7.1f, 0f, 0f), origin, cellSize);
            var boundary = TerrainChunkEditUtility.SnapToChunkLocalLattice(new float3(9f, 0f, 0f), origin, cellSize);
            var nearUpper = TerrainChunkEditUtility.SnapToChunkLocalLattice(new float3(10.9f, 0f, 0f), origin, cellSize);

            Assert.AreEqual(8f, nearLower.x, 1e-4f);
            Assert.AreEqual(10f, boundary.x, 1e-4f);
            Assert.AreEqual(10f, nearUpper.x, 1e-4f);
        }

        [Test]
        public void TryFindOwningChunk_UsesHalfOpenBoundsAtSeam()
        {
            var chunkA = CreateChunk(float3.zero, new int3(8, 8, 8), 1f, new int3(0, 0, 0));
            var chunkB = CreateChunk(new float3(7f, 0f, 0f), new int3(8, 8, 8), 1f, new int3(1, 0, 0));

            var chunkEntities = new NativeArray<Entity>(2, Allocator.Temp);
            try
            {
                chunkEntities[0] = chunkA;
                chunkEntities[1] = chunkB;

                var found = TerrainChunkEditUtility.TryFindOwningChunk(
                    entityManager,
                    chunkEntities,
                    new float3(7f, 1f, 1f),
                    out _,
                    out var chunk,
                    out _,
                    out _);

                Assert.IsTrue(found);
                Assert.AreEqual(new int3(1, 0, 0), chunk.ChunkCoord);
            }
            finally
            {
                chunkEntities.Dispose();
            }
        }

        [Test]
        public void MarkChunksDirty_BoxTouchingSeam_DirtiesNeighbor()
        {
            var chunkA = CreateChunk(float3.zero, new int3(8, 8, 8), 1f, new int3(0, 0, 0));
            var chunkB = CreateChunk(new float3(7f, 0f, 0f), new int3(8, 8, 8), 1f, new int3(1, 0, 0));

            var chunkEntities = new NativeArray<Entity>(2, Allocator.Temp);
            try
            {
                chunkEntities[0] = chunkA;
                chunkEntities[1] = chunkB;

                var seamEdit = SDFEdit.CreateBox(new float3(7f, 2f, 2f), new float3(0.5f, 0.5f, 0.5f), SDFEditOperation.Subtract);
                TerrainChunkEditUtility.MarkChunksDirty(entityManager, chunkEntities, in seamEdit);

                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkA), "Chunk A should be flagged.");
                Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunkB), "Neighbor chunk should be flagged when edit touches seam.");
            }
            finally
            {
                chunkEntities.Dispose();
            }
        }

        private Entity CreateChunk(float3 worldOrigin, int3 resolution = default, float voxelSize = 1f, int3 chunkCoord = default)
        {
            var entity = entityManager.CreateEntity(typeof(TerrainChunk), typeof(TerrainChunkGridInfo), typeof(TerrainChunkBounds));
            if (resolution.x <= 0 || resolution.y <= 0 || resolution.z <= 0)
            {
                resolution = new int3(8, 8, 8);
            }

            entityManager.SetComponentData(entity, new TerrainChunk { ChunkCoord = chunkCoord });
            entityManager.SetComponentData(entity, TerrainChunkGridInfo.Create(resolution, voxelSize));
            entityManager.SetComponentData(entity, new TerrainChunkBounds { WorldOrigin = worldOrigin });
            return entity;
        }
    }
}
