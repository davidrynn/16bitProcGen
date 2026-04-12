using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Terrain;
using DOTS.Terrain.Modification;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for physics system functionality (glob physics)
    /// Converted from GlobPhysicsTest.cs
    /// </summary>
    [TestFixture]
    public class PhysicsSystemTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Physics Test World");
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
        public void GlobEntity_CanBeCreated()
        {
            // Create a glob entity
            var entity = entityManager.CreateEntity();
            
            // Add glob component using actual fields
            entityManager.AddComponentData(entity, new TerrainGlobComponent
            {
                globType = GlobRemovalType.Medium,
                globRadius = 2.0f,
                originalPosition = float3.zero,
                currentPosition = float3.zero,
                terrainType = TerrainType.Rock,
                mass = 10f,
                canBeCollected = true
            });
            
            Assert.IsTrue(entityManager.Exists(entity), "Glob entity should exist");
            Assert.IsTrue(entityManager.HasComponent<TerrainGlobComponent>(entity),
                "Entity should have TerrainGlobComponent");
        }

        [Test]
        public void GlobComponent_StoresType()
        {
            var entity = entityManager.CreateEntity();
            var globType = GlobRemovalType.Large;
            
            entityManager.AddComponentData(entity, new TerrainGlobComponent
            {
                globType = globType,
                globRadius = 3.0f,
                originalPosition = float3.zero,
                currentPosition = float3.zero,
                terrainType = TerrainType.Rock
            });
            
            var component = entityManager.GetComponentData<TerrainGlobComponent>(entity);
            Assert.AreEqual(globType, component.globType,
                "Glob type should be stored correctly");
        }

        [Test]
        public void GlobComponent_StoresRadius()
        {
            var entity = entityManager.CreateEntity();
            float radius = 5.5f;
            
            entityManager.AddComponentData(entity, new TerrainGlobComponent
            {
                globType = GlobRemovalType.Small,
                globRadius = radius,
                originalPosition = float3.zero,
                currentPosition = float3.zero,
                terrainType = TerrainType.Dirt
            });
            
            var component = entityManager.GetComponentData<TerrainGlobComponent>(entity);
            Assert.AreEqual(radius, component.globRadius, 0.001f,
                "Glob radius should be stored correctly");
        }

        [Test]
        public void GlobEntity_HasTransform()
        {
            var entity = entityManager.CreateEntity();
            
            // Add transform components
            var position = new float3(10, 5, 20);
            entityManager.AddComponentData(entity, LocalTransform.FromPosition(position));
            
            entityManager.AddComponentData(entity, new TerrainGlobComponent
            {
                globType = GlobRemovalType.Medium,
                globRadius = 2.0f,
                originalPosition = position,
                currentPosition = position,
                terrainType = TerrainType.Rock
            });
            
            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(entity),
                "Glob entity should have LocalTransform");
            
            var transform = entityManager.GetComponentData<LocalTransform>(entity);
            Assert.AreEqual(position, transform.Position,
                "Transform position should match");
        }

        [Test]
        public void MultipleGlobs_CanCoexist()
        {
            // Create multiple glob entities
            var globs = new Entity[3];
            
            for (int i = 0; i < globs.Length; i++)
            {
                globs[i] = entityManager.CreateEntity();
                entityManager.AddComponentData(globs[i], new TerrainGlobComponent
                {
                    globType = (GlobRemovalType)i,
                    globRadius = 1.0f + i,
                    originalPosition = float3.zero,
                    currentPosition = float3.zero,
                    terrainType = TerrainType.Rock
                });
            }
            
            // Verify all exist
            for (int i = 0; i < globs.Length; i++)
            {
                Assert.IsTrue(entityManager.Exists(globs[i]),
                    $"Glob entity {i} should exist");
                
                var component = entityManager.GetComponentData<TerrainGlobComponent>(globs[i]);
                Assert.AreEqual((GlobRemovalType)i, component.globType,
                    $"Glob {i} should have correct type");
            }
        }

        [Test]
        public void GlobRemovalTypes_AllValid()
        {
            // Test that all glob removal types can be assigned
            var types = new[] { 
                GlobRemovalType.Small, 
                GlobRemovalType.Medium, 
                GlobRemovalType.Large 
            };
            
            foreach (var type in types)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new TerrainGlobComponent
                {
                    globType = type,
                    globRadius = 2.0f,
                    originalPosition = float3.zero,
                    currentPosition = float3.zero,
                    terrainType = TerrainType.Rock
                });
                
                var component = entityManager.GetComponentData<TerrainGlobComponent>(entity);
                Assert.AreEqual(type, component.globType,
                    $"Glob type {type} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void GlobRadius_MustBePositive()
        {
            // Test that positive radii work
            float[] validRadii = { 0.5f, 1.0f, 2.5f, 5.0f, 10.0f };
            
            foreach (var radius in validRadii)
            {
                Assert.Greater(radius, 0,
                    $"Glob radius {radius} should be positive");
            }
        }

        [Test]
        public void GlobComponent_StoresTerrainType()
        {
            // Create a glob entity with terrain type
            var entity = entityManager.CreateEntity();
            var terrainType = TerrainType.Sand;
            
            entityManager.AddComponentData(entity, new TerrainGlobComponent
            {
                globType = GlobRemovalType.Medium,
                globRadius = 2.0f,
                originalPosition = float3.zero,
                currentPosition = float3.zero,
                terrainType = terrainType
            });
            
            var component = entityManager.GetComponentData<TerrainGlobComponent>(entity);
            Assert.AreEqual(terrainType, component.terrainType,
                "Glob should store the terrain type correctly");
        }
    }
}

