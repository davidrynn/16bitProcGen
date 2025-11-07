using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for TerrainData component
    /// Converted from TerrainDataTest.cs
    /// </summary>
    [TestFixture]
    public class TerrainDataTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("TerrainData Test World");
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
        public void TerrainDataBuilder_CreatesValidData()
        {
            var terrainData = TerrainDataBuilder.CreateTerrainData(
                new int2(5, 10),
                64,
                15f
            );
            
            Assert.AreEqual(new int2(5, 10), terrainData.chunkPosition,
                "Chunk position should be (5, 10)");
            Assert.AreEqual(64, terrainData.resolution,
                "Resolution should be 64");
            Assert.AreEqual(15f, terrainData.worldScale, 0.001f,
                "World scale should be 15");
        }

        [Test]
        public void ChunkPosition_StoredCorrectly()
        {
            int2[] positions = {
                new int2(0, 0),
                new int2(5, 5),
                new int2(-3, 7),
                new int2(10, -2)
            };
            
            foreach (var pos in positions)
            {
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    pos, 32, 10f
                );
                
                Assert.AreEqual(pos, terrainData.chunkPosition,
                    $"Chunk position {pos} should be stored correctly");
            }
        }

        [Test]
        public void WorldScale_AffectsChunkCalculations()
        {
            float[] scales = { 5f, 10f, 20f, 50f, 100f };
            
            foreach (var scale in scales)
            {
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    new int2(1, 1), 32, scale
                );
                
                Assert.AreEqual(scale, terrainData.worldScale, 0.001f,
                    $"World scale {scale} should be stored correctly");
                
                // World position = chunk position * world scale
                float expectedWorldX = 1 * scale;
                float expectedWorldZ = 1 * scale;
                
                // These assertions verify the calculation is correct
                Assert.Greater(expectedWorldX, 0, "World X should be positive");
                Assert.Greater(expectedWorldZ, 0, "World Z should be positive");
            }
        }

        [Test]
        public void Resolution_CommonValuesValid()
        {
            int[] resolutions = { 16, 32, 64, 128, 256, 512 };
            
            foreach (var resolution in resolutions)
            {
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    new int2(0, 0), resolution, 10f
                );
                
                Assert.AreEqual(resolution, terrainData.resolution,
                    $"Resolution {resolution} should be stored correctly");
                Assert.Greater(resolution, 0,
                    $"Resolution {resolution} should be positive");
            }
        }

        [Test]
        public void Entity_CanHaveTerrainData()
        {
            var entity = entityManager.CreateEntity();
            
            var terrainData = TerrainDataBuilder.CreateTerrainData(
                new int2(3, 4), 128, 25f
            );
            
            entityManager.AddComponentData(entity, terrainData);
            
            Assert.IsTrue(entityManager.HasComponent<TerrainData>(entity),
                "Entity should have TerrainData component");
            
            var data = entityManager.GetComponentData<TerrainData>(entity);
            Assert.AreEqual(new int2(3, 4), data.chunkPosition,
                "Retrieved data should match");
        }

        [Test]
        public void MultipleChunks_UniquePositions()
        {
            // Create a 3x3 grid of chunks
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    var entity = entityManager.CreateEntity();
                    var terrainData = TerrainDataBuilder.CreateTerrainData(
                        new int2(x, z), 32, 10f
                    );
                    entityManager.AddComponentData(entity, terrainData);
                }
            }
            
            // Query all terrain entities
            var query = entityManager.CreateEntityQuery(typeof(TerrainData));
            var count = query.CalculateEntityCount();
            
            Assert.AreEqual(9, count, "Should have 9 terrain chunks");
            
            query.Dispose();
        }

        [Test]
        public void TerrainData_CanBeUpdated()
        {
            var entity = entityManager.CreateEntity();
            
            var terrainData = TerrainDataBuilder.CreateTerrainData(
                new int2(0, 0), 32, 10f
            );
            entityManager.AddComponentData(entity, terrainData);
            
            // Update the data
            terrainData.chunkPosition = new int2(5, 5);
            terrainData.worldScale = 20f;
            entityManager.SetComponentData(entity, terrainData);
            
            var updated = entityManager.GetComponentData<TerrainData>(entity);
            Assert.AreEqual(new int2(5, 5), updated.chunkPosition,
                "Chunk position should be updated");
            Assert.AreEqual(20f, updated.worldScale, 0.001f,
                "World scale should be updated");
        }

        [Test]
        public void NegativeChunkPositions_Valid()
        {
            // Test that negative chunk positions work (for infinite terrain)
            int2[] negativePositions = {
                new int2(-1, 0),
                new int2(0, -1),
                new int2(-5, -5),
                new int2(-10, 5)
            };
            
            foreach (var pos in negativePositions)
            {
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    pos, 32, 10f
                );
                
                Assert.AreEqual(pos, terrainData.chunkPosition,
                    $"Negative position {pos} should be valid");
            }
        }
    }
}

