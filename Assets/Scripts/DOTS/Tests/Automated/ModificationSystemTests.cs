using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for terrain modification system
    /// Tests the PlayerModificationComponent used for player-driven terrain modifications
    /// </summary>
    [TestFixture]
    public class ModificationSystemTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Modification Test World");
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
        public void PlayerModificationComponent_CanBeCreated()
        {
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(entity, new PlayerModificationComponent
            {
                position = new float3(5, 0, 10),
                radius = 3.0f,
                strength = 0.8f,
                resolution = 32,
                removalType = GlobRemovalType.Medium,
                toolEfficiency = 1.0f,
                isMiningTool = true
            });
            
            Assert.IsTrue(entityManager.HasComponent<PlayerModificationComponent>(entity),
                "Entity should have PlayerModificationComponent");
        }

        [Test]
        public void ModificationPosition_StoredCorrectly()
        {
            var entity = entityManager.CreateEntity();
            var position = new float3(12.5f, 0, 7.3f);
            
            entityManager.AddComponentData(entity, new PlayerModificationComponent
            {
                position = position,
                radius = 2.0f,
                strength = 1.0f,
                resolution = 32,
                removalType = GlobRemovalType.Small
            });
            
            var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
            Assert.AreEqual(position, component.position,
                "Modification position should be stored correctly");
        }

        [Test]
        public void ModificationRadius_ValidValues()
        {
            float[] radii = { 1.0f, 2.5f, 5.0f, 10.0f };
            
            foreach (var radius in radii)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new PlayerModificationComponent
                {
                    position = float3.zero,
                    radius = radius,
                    strength = 1.0f,
                    resolution = 32,
                    removalType = GlobRemovalType.Medium
                });
                
                var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
                Assert.AreEqual(radius, component.radius, 0.001f,
                    $"Radius {radius} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void ModificationStrength_ValidRange()
        {
            // Test strength values between 0 and 1
            float[] strengths = { 0f, 0.25f, 0.5f, 0.75f, 1.0f };
            
            foreach (var strength in strengths)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new PlayerModificationComponent
                {
                    position = float3.zero,
                    radius = 5.0f,
                    strength = strength,
                    resolution = 32,
                    removalType = GlobRemovalType.Medium
                });
                
                var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
                Assert.AreEqual(strength, component.strength, 0.001f,
                    $"Strength {strength} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void GlobRemovalTypes_AllValid()
        {
            var types = new[] {
                GlobRemovalType.Small,
                GlobRemovalType.Medium,
                GlobRemovalType.Large
            };
            
            foreach (var type in types)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new PlayerModificationComponent
                {
                    position = float3.zero,
                    radius = 5.0f,
                    strength = 1.0f,
                    resolution = 32,
                    removalType = type
                });
                
                var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
                Assert.AreEqual(type, component.removalType,
                    $"Removal type {type} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void ModificationResolution_ValidValues()
        {
            int[] resolutions = { 16, 32, 64, 128 };
            
            foreach (var resolution in resolutions)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new PlayerModificationComponent
                {
                    position = float3.zero,
                    radius = 5.0f,
                    strength = 1.0f,
                    resolution = resolution,
                    removalType = GlobRemovalType.Medium
                });
                
                var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
                Assert.AreEqual(resolution, component.resolution,
                    $"Resolution {resolution} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }

        [Test]
        public void MultipleModifications_CanCoexist()
        {
            // Test multiple modifications at different locations
            var mod1 = entityManager.CreateEntity();
            var mod2 = entityManager.CreateEntity();
            var mod3 = entityManager.CreateEntity();
            
            entityManager.AddComponentData(mod1, new PlayerModificationComponent
            {
                position = new float3(0, 0, 0),
                radius = 3.0f,
                strength = 1.0f,
                resolution = 32,
                removalType = GlobRemovalType.Small
            });
            
            entityManager.AddComponentData(mod2, new PlayerModificationComponent
            {
                position = new float3(10, 0, 10),
                radius = 5.0f,
                strength = 0.5f,
                resolution = 64,
                removalType = GlobRemovalType.Medium
            });
            
            entityManager.AddComponentData(mod3, new PlayerModificationComponent
            {
                position = new float3(-5, 0, 5),
                radius = 2.0f,
                strength = 0.8f,
                resolution = 32,
                removalType = GlobRemovalType.Large
            });
            
            // Verify all exist with correct data
            var comp1 = entityManager.GetComponentData<PlayerModificationComponent>(mod1);
            var comp2 = entityManager.GetComponentData<PlayerModificationComponent>(mod2);
            var comp3 = entityManager.GetComponentData<PlayerModificationComponent>(mod3);
            
            Assert.AreEqual(GlobRemovalType.Small, comp1.removalType);
            Assert.AreEqual(GlobRemovalType.Medium, comp2.removalType);
            Assert.AreEqual(GlobRemovalType.Large, comp3.removalType);
        }

        [Test]
        public void ModificationData_CanBeUpdated()
        {
            var entity = entityManager.CreateEntity();
            
            // Initial modification
            entityManager.AddComponentData(entity, new PlayerModificationComponent
            {
                position = new float3(0, 0, 0),
                radius = 2.0f,
                strength = 0.5f,
                resolution = 32,
                removalType = GlobRemovalType.Small
            });
            
            // Update the modification
            var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
            component.position = new float3(5, 0, 5);
            component.radius = 4.0f;
            component.strength = 1.0f;
            component.removalType = GlobRemovalType.Large;
            entityManager.SetComponentData(entity, component);
            
            var updated = entityManager.GetComponentData<PlayerModificationComponent>(entity);
            Assert.AreEqual(new float3(5, 0, 5), updated.position);
            Assert.AreEqual(4.0f, updated.radius, 0.001f);
            Assert.AreEqual(GlobRemovalType.Large, updated.removalType);
        }

        [Test]
        public void ToolEfficiency_ValidRange()
        {
            // Test tool efficiency values between 0 and 1
            float[] efficiencies = { 0f, 0.5f, 1.0f };
            
            foreach (var efficiency in efficiencies)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentData(entity, new PlayerModificationComponent
                {
                    position = float3.zero,
                    radius = 5.0f,
                    strength = 1.0f,
                    resolution = 32,
                    removalType = GlobRemovalType.Medium,
                    toolEfficiency = efficiency
                });
                
                var component = entityManager.GetComponentData<PlayerModificationComponent>(entity);
                Assert.AreEqual(efficiency, component.toolEfficiency, 0.001f,
                    $"Tool efficiency {efficiency} should be stored correctly");
                
                entityManager.DestroyEntity(entity);
            }
        }
    }
}
