using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain.WFC;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Automated tests for WFC (Wave Function Collapse) system functionality
    /// Converted from WFCSystemTest.cs
    /// </summary>
    [TestFixture]
    public class WFCSystemTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("WFC Test World");
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
        public void WFCEntity_CanBeCreated()
        {
            // Create a WFC entity
            var entity = entityManager.CreateEntity();
            
            // Add WFC component with actual fields
            entityManager.AddComponentData(entity, new WFCComponent
            {
                gridSize = new int2(10, 10),
                patternSize = 1,
                cellSize = 1.0f,
                isCollapsed = false,
                needsGeneration = true,
                isGenerating = false,
                maxIterations = 1000
            });
            
            Assert.IsTrue(entityManager.Exists(entity), "WFC entity should exist");
            Assert.IsTrue(entityManager.HasComponent<WFCComponent>(entity),
                "Entity should have WFCComponent");
        }

        [Test]
        public void WFCComponent_HasCorrectGridSize()
        {
            var entity = entityManager.CreateEntity();
            var gridSize = new int2(15, 20);
            
            entityManager.AddComponentData(entity, new WFCComponent
            {
                gridSize = gridSize,
                patternSize = 1,
                cellSize = 1.0f,
                needsGeneration = true
            });
            
            var component = entityManager.GetComponentData<WFCComponent>(entity);
            Assert.AreEqual(gridSize, component.gridSize,
                "Grid size should be stored correctly");
        }

        [Test]
        public void WFCComponent_NeedsGenerationFlag()
        {
            var entity = entityManager.CreateEntity();
            
            entityManager.AddComponentData(entity, new WFCComponent
            {
                gridSize = new int2(10, 10),
                patternSize = 1,
                cellSize = 1.0f,
                needsGeneration = true,
                isGenerating = false
            });
            
            var component = entityManager.GetComponentData<WFCComponent>(entity);
            Assert.IsTrue(component.needsGeneration,
                "WFC should need generation initially");
            Assert.IsFalse(component.isGenerating,
                "WFC should not be generating initially");
        }

        [Test]
        public void WFCComponent_MaxIterations()
        {
            var entity = entityManager.CreateEntity();
            int maxIter = 5000;
            
            entityManager.AddComponentData(entity, new WFCComponent
            {
                gridSize = new int2(10, 10),
                patternSize = 1,
                cellSize = 1.0f,
                maxIterations = maxIter
            });
            
            var component = entityManager.GetComponentData<WFCComponent>(entity);
            Assert.AreEqual(maxIter, component.maxIterations,
                "Max iterations should be stored correctly");
        }

        [Test]
        public void MultipleWFCEntities_CanCoexist()
        {
            // Test that multiple WFC entities can exist simultaneously
            var entity1 = entityManager.CreateEntity();
            var entity2 = entityManager.CreateEntity();
            
            entityManager.AddComponentData(entity1, new WFCComponent
            {
                gridSize = new int2(10, 10),
                patternSize = 1,
                cellSize = 1.0f,
                maxIterations = 1000
            });
            
            entityManager.AddComponentData(entity2, new WFCComponent
            {
                gridSize = new int2(20, 20),
                patternSize = 2,
                cellSize = 2.0f,
                maxIterations = 2000
            });
            
            Assert.IsTrue(entityManager.Exists(entity1), "First WFC entity should exist");
            Assert.IsTrue(entityManager.Exists(entity2), "Second WFC entity should exist");
            
            var comp1 = entityManager.GetComponentData<WFCComponent>(entity1);
            var comp2 = entityManager.GetComponentData<WFCComponent>(entity2);
            
            Assert.AreNotEqual(comp1.gridSize, comp2.gridSize,
                "Different entities should have different grid sizes");
        }

        [Test]
        public void WFCGridSize_MustBePositive()
        {
            var entity = entityManager.CreateEntity();
            
            // Valid grid sizes
            int2[] validSizes = { new int2(5, 5), new int2(10, 15), new int2(32, 32) };
            
            foreach (var size in validSizes)
            {
                Assert.Greater(size.x, 0, $"Grid width {size.x} should be positive");
                Assert.Greater(size.y, 0, $"Grid height {size.y} should be positive");
            }
        }
    }
}

