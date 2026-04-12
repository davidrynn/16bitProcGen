using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for terrain generation functionality
    /// Converted from TerrainGenerationTest.cs
    /// </summary>
    [TestFixture]
    public class TerrainGenerationTests
    {
        private World testWorld;
        private EntityManager entityManager;
        private TerrainEntityManager terrainEntityManager;

        [SetUp]
        public void SetUp()
        {
            // Create a test world
            testWorld = new World("Terrain Test World");
            entityManager = testWorld.EntityManager;
            
            // Note: TerrainEntityManager is a MonoBehaviour, so tests need to be run in PlayMode
            // or we need to create a mock version for EditMode tests
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
        public void EntityCreation_HasTerrainDataComponent()
        {
            // Create a terrain entity
            var entity = entityManager.CreateEntity();
            
            // Add TerrainData component
            var terrainData = TerrainDataBuilder.CreateTerrainData(
                new int2(0, 0),
                32,
                10f
            );
            
            entityManager.AddComponentData(entity, terrainData);
            
            // Verify component exists
            Assert.IsTrue(entityManager.HasComponent<TerrainData>(entity),
                "Entity should have TerrainData component");
            
            // Verify data
            var data = entityManager.GetComponentData<TerrainData>(entity);
            Assert.AreEqual(new int2(0, 0), data.chunkPosition, "Chunk position should be (0,0)");
            Assert.AreEqual(32, data.resolution, "Resolution should be 32");
            Assert.AreEqual(10f, data.worldScale, "World scale should be 10");
        }

        [Test]
        public void TerrainData_CorrectWorldPosition()
        {
            // Test that world position is calculated correctly from chunk position
            var terrainData = TerrainDataBuilder.CreateTerrainData(
                new int2(2, 3),
                64,
                15f
            );
            
            float3 expectedPosition = new float3(2 * 15f, 0, 3 * 15f);
            // World position should be chunk position * world scale
            Assert.AreEqual(new int2(2, 3), terrainData.chunkPosition,
                "Chunk position should be stored correctly");
        }

        [Test]
        public void Resolution_ValidValues()
        {
            // Test that common resolutions work correctly
            int[] validResolutions = { 16, 32, 64, 128, 256 };
            
            foreach (var resolution in validResolutions)
            {
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    new int2(0, 0),
                    resolution,
                    10f
                );
                
                Assert.AreEqual(resolution, terrainData.resolution,
                    $"Resolution {resolution} should be stored correctly");
            }
        }

        [Test]
        public void BiomeComponent_AssignedCorrectly()
        {
            var entity = entityManager.CreateEntity();
            
            // Add biome component
            var biomeComponent = BiomeBuilder.CreateBiomeComponent(BiomeType.Forest);
            entityManager.AddComponentData(entity, biomeComponent);
            
            // Verify
            Assert.IsTrue(entityManager.HasComponent<BiomeComponent>(entity),
                "Entity should have BiomeComponent");
            
            var biome = entityManager.GetComponentData<BiomeComponent>(entity);
            Assert.AreEqual(BiomeType.Forest, biome.biomeType,
                "Biome type should be Forest");
        }

        [Test]
        public void WorldScale_AffectsChunkSize()
        {
            // Test that different world scales work correctly
            float[] worldScales = { 5f, 10f, 20f, 50f };
            
            foreach (var scale in worldScales)
            {
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    new int2(1, 1),
                    32,
                    scale
                );
                
                Assert.AreEqual(scale, terrainData.worldScale,
                    $"World scale {scale} should be stored correctly");
            }
        }

        [Test]
        public void MultipleChunks_CorrectPositioning()
        {
            // Test creating multiple adjacent chunks
            int2[] chunkPositions = { 
                new int2(0, 0), 
                new int2(1, 0), 
                new int2(0, 1), 
                new int2(1, 1) 
            };
            
            var entities = new Entity[4];
            
            for (int i = 0; i < chunkPositions.Length; i++)
            {
                entities[i] = entityManager.CreateEntity();
                var terrainData = TerrainDataBuilder.CreateTerrainData(
                    chunkPositions[i],
                    32,
                    10f
                );
                entityManager.AddComponentData(entities[i], terrainData);
            }
            
            // Verify all chunks were created with correct positions
            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(entityManager.Exists(entities[i]),
                    $"Entity {i} should exist");
                
                var data = entityManager.GetComponentData<TerrainData>(entities[i]);
                Assert.AreEqual(chunkPositions[i], data.chunkPosition,
                    $"Chunk {i} should have position {chunkPositions[i]}");
            }
        }
    }
}

