using DOTS.Terrain.SurfaceScatter;
using NUnit.Framework;
using Unity.Entities;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SurfaceScatterRenderConfigBootstrapUtilityTests
    {
        private sealed class DummyRenderConfig : IComponentData
        {
            public int Version;
        }

        [Test]
        public void SetOrCreateManagedSingleton_CreatesConfig_WhenMissing()
        {
            using var world = new World("SurfaceScatterRenderConfigBootstrapUtilityTests_Create");
            var entityManager = world.EntityManager;

            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                entityManager,
                new DummyRenderConfig { Version = 1 },
                nameof(DummyRenderConfig));

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DummyRenderConfig>());
            Assert.AreEqual(1, query.CalculateEntityCount());

            var singleton = query.GetSingletonEntity();
            var config = entityManager.GetComponentObject<DummyRenderConfig>(singleton);
            Assert.AreEqual(1, config.Version);
        }

        [Test]
        public void SetOrCreateManagedSingleton_UpdatesExistingSingleton_InPlace()
        {
            using var world = new World("SurfaceScatterRenderConfigBootstrapUtilityTests_Update");
            var entityManager = world.EntityManager;

            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                entityManager,
                new DummyRenderConfig { Version = 1 },
                nameof(DummyRenderConfig));

            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                entityManager,
                new DummyRenderConfig { Version = 9 },
                nameof(DummyRenderConfig));

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DummyRenderConfig>());
            Assert.AreEqual(1, query.CalculateEntityCount());

            var singleton = query.GetSingletonEntity();
            var config = entityManager.GetComponentObject<DummyRenderConfig>(singleton);
            Assert.AreEqual(9, config.Version);
        }

        [Test]
        public void SetOrCreateManagedSingleton_RemovesDuplicates_KeepingSingleUpdatedConfig()
        {
            using var world = new World("SurfaceScatterRenderConfigBootstrapUtilityTests_Dedup");
            var entityManager = world.EntityManager;

            var first = entityManager.CreateEntity();
            var second = entityManager.CreateEntity();
            var third = entityManager.CreateEntity();

            entityManager.AddComponentObject(first, new DummyRenderConfig { Version = 1 });
            entityManager.AddComponentObject(second, new DummyRenderConfig { Version = 2 });
            entityManager.AddComponentObject(third, new DummyRenderConfig { Version = 3 });

            SurfaceScatterRenderConfigBootstrapUtility.SetOrCreateManagedSingleton(
                entityManager,
                new DummyRenderConfig { Version = 42 },
                nameof(DummyRenderConfig));

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<DummyRenderConfig>());
            Assert.AreEqual(1, query.CalculateEntityCount());

            var singleton = query.GetSingletonEntity();
            var config = entityManager.GetComponentObject<DummyRenderConfig>(singleton);
            Assert.AreEqual(42, config.Version);
        }
    }
}
