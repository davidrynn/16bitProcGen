using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for biome system functionality
    /// Converted from BiomeSystemTest.cs
    /// </summary>
    [TestFixture]
    public class BiomeSystemTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Biome Test World");
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
        public void BiomeBuilder_CreatesComponent()
        {
            // Test BiomeBuilder functionality
            var biomeComponent = BiomeBuilder.CreateBiomeComponent(BiomeType.Forest);
            
            Assert.AreEqual(BiomeType.Forest, biomeComponent.biomeType,
                "Biome type should be Forest");
        }

        [Test]
        public void BiomeComponent_AllTypesValid()
        {
            // Test all biome types can be created
            var biomeTypes = new[] {
                BiomeType.Plains,
                BiomeType.Desert,
                BiomeType.Forest,
                BiomeType.Mountains,
                BiomeType.Swamp,
                BiomeType.Arctic
            };
            
            foreach (var type in biomeTypes)
            {
                var component = BiomeBuilder.CreateBiomeComponent(type);
                Assert.AreEqual(type, component.biomeType,
                    $"Biome type {type} should be created correctly");
            }
        }

        [Test]
        public void Entity_CanHaveBiomeComponent()
        {
            var entity = entityManager.CreateEntity();
            var biomeComponent = BiomeBuilder.CreateBiomeComponent(BiomeType.Mountains);
            
            entityManager.AddComponentData(entity, biomeComponent);
            
            Assert.IsTrue(entityManager.HasComponent<BiomeComponent>(entity),
                "Entity should have BiomeComponent");
            
            var component = entityManager.GetComponentData<BiomeComponent>(entity);
            Assert.AreEqual(BiomeType.Mountains, component.biomeType,
                "Biome type should match");
        }

        [Test]
        public void TerrainEntity_WithBiomeAssignment()
        {
            // Create terrain entity with biome
            var entity = entityManager.CreateEntity();
            
            var terrainData = TerrainDataBuilder.CreateTerrainData(
                new int2(0, 0),
                32,
                10f
            );
            
            var biomeComponent = BiomeBuilder.CreateBiomeComponent(BiomeType.Desert);
            
            entityManager.AddComponentData(entity, terrainData);
            entityManager.AddComponentData(entity, biomeComponent);
            
            Assert.IsTrue(entityManager.HasComponent<TerrainData>(entity),
                "Entity should have TerrainData");
            Assert.IsTrue(entityManager.HasComponent<BiomeComponent>(entity),
                "Entity should have BiomeComponent");
            
            var biome = entityManager.GetComponentData<BiomeComponent>(entity);
            Assert.AreEqual(BiomeType.Desert, biome.biomeType,
                "Biome should be Desert");
        }

        [Test]
        public void BiomeType_ValidationWorks()
        {
            // Test that biome types are distinct
            var plains = BiomeType.Plains;
            var forest = BiomeType.Forest;
            var desert = BiomeType.Desert;
            
            Assert.AreNotEqual(plains, forest, "Plains should differ from Forest");
            Assert.AreNotEqual(plains, desert, "Plains should differ from Desert");
            Assert.AreNotEqual(forest, desert, "Forest should differ from Desert");
        }

        [Test]
        public void MultipleBiomes_CanCoexist()
        {
            // Create multiple entities with different biomes
            var entities = new Entity[3];
            var biomeTypes = new[] { BiomeType.Plains, BiomeType.Forest, BiomeType.Desert };
            
            for (int i = 0; i < entities.Length; i++)
            {
                entities[i] = entityManager.CreateEntity();
                var biomeComponent = BiomeBuilder.CreateBiomeComponent(biomeTypes[i]);
                entityManager.AddComponentData(entities[i], biomeComponent);
            }
            
            // Verify each has correct biome
            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(entityManager.Exists(entities[i]),
                    $"Entity {i} should exist");
                
                var component = entityManager.GetComponentData<BiomeComponent>(entities[i]);
                Assert.AreEqual(biomeTypes[i], component.biomeType,
                    $"Entity {i} should have biome {biomeTypes[i]}");
            }
        }

        [Test]
        public void BiomeComponent_CanBeUpdated()
        {
            var entity = entityManager.CreateEntity();
            
            // Start with Plains
            var biomeComponent = BiomeBuilder.CreateBiomeComponent(BiomeType.Plains);
            entityManager.AddComponentData(entity, biomeComponent);
            
            // Update to Forest
            biomeComponent.biomeType = BiomeType.Forest;
            entityManager.SetComponentData(entity, biomeComponent);
            
            var updated = entityManager.GetComponentData<BiomeComponent>(entity);
            Assert.AreEqual(BiomeType.Forest, updated.biomeType,
                "Biome should be updated to Forest");
        }
    }
}

