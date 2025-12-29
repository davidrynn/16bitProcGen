using DOTS.Terrain.Bootstrap;
using DOTS.Terrain;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ChunkComponent = DOTS.Terrain.TerrainChunk;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainBootstrapAuthoringTests
    {
        private World testWorld;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Terrain Bootstrap Test World");
            World.DefaultGameObjectInjectionWorld = testWorld;
        }

        [TearDown]
        public void TearDown()
        {
            if (testWorld != null && testWorld.IsCreated)
            {
                testWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = null;

            foreach (var camera in Object.FindObjectsOfType<Camera>())
            {
                Object.DestroyImmediate(camera.gameObject);
            }

            foreach (var light in Object.FindObjectsOfType<Light>())
            {
                Object.DestroyImmediate(light.gameObject);
            }
        }

        [Test]
        public void RunBootstrap_CreatesChunkEntities()
        {
            var go = new GameObject("TerrainBootstrapTest");
            var authoring = go.AddComponent<TerrainBootstrapAuthoring>();

            var result = authoring.RunBootstrap();

            Assert.IsTrue(result, "Bootstrap should succeed when a world is provided.");

            var entityManager = testWorld.EntityManager;
            var chunkQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ChunkComponent>());
            Assert.Greater(chunkQuery.CalculateEntityCount(), 0, "Chunks should be spawned.");

            var fieldSettingsQuery = entityManager.CreateEntityQuery(typeof(SDFTerrainFieldSettings));
            Assert.AreEqual(1, fieldSettingsQuery.CalculateEntityCount(), "Field settings singleton should exist.");

            chunkQuery.Dispose();
            fieldSettingsQuery.Dispose();

            Object.DestroyImmediate(go);
        }
    }
}
