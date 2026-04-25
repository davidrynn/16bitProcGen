using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Structures;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class StructurePlacementComponentTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("StructureTest");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated) _world.Dispose();
        }

        [Test]
        public void StructureAnchorRecord_CanAddBufferToEntity()
        {
            var entity = _em.CreateEntity();
            var buffer = _em.AddBuffer<StructureAnchorRecord>(entity);

            buffer.Add(new StructureAnchorRecord
            {
                Family = StructureFamilyId.Relic,
                PlanningCell = new int2(3, 7),
                WorldPosition = new float3(100f, 20f, 300f),
                Rotation = quaternion.identity,
                Radius = 15f,
                StableAnchorId = 42,
                GenerationVersion = 1,
                TemplateId = "Odd_Head_Relic_v1",
                Source = StructurePlacementSource.SeededAnchor,
                PersistenceFlags = StructurePersistenceFlags.None,
            });

            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(StructureFamilyId.Relic, buffer[0].Family);
            Assert.AreEqual((uint)42, buffer[0].StableAnchorId);
            Assert.AreEqual("Odd_Head_Relic_v1", buffer[0].TemplateId.ToString());
        }

        [Test]
        public void StructureAnchorRecord_MultipleAnchorsInBuffer()
        {
            var entity = _em.CreateEntity();
            var buffer = _em.AddBuffer<StructureAnchorRecord>(entity);

            buffer.Add(new StructureAnchorRecord
            {
                Family = StructureFamilyId.Relic,
                StableAnchorId = 1,
                WorldPosition = new float3(0f, 0f, 0f),
            });
            buffer.Add(new StructureAnchorRecord
            {
                Family = StructureFamilyId.Dungeon,
                StableAnchorId = 2,
                WorldPosition = new float3(500f, 0f, 500f),
            });

            Assert.AreEqual(2, buffer.Length);
            Assert.AreEqual(StructureFamilyId.Relic, buffer[0].Family);
            Assert.AreEqual(StructureFamilyId.Dungeon, buffer[1].Family);
        }

        [Test]
        public void StructureFootprintReservation_CanAddBufferToEntity()
        {
            var entity = _em.CreateEntity();
            var buffer = _em.AddBuffer<StructureFootprintReservation>(entity);

            buffer.Add(new StructureFootprintReservation
            {
                StableAnchorId = 42,
                Center = new float3(100f, 20f, 300f),
                Extents = new float3(15f, 10f, 15f),
            });

            Assert.AreEqual(1, buffer.Length);
            Assert.AreEqual(new float3(15f, 10f, 15f), buffer[0].Extents);
        }

        [Test]
        public void StructurePlacementSingleton_CanAttachWithBuffers()
        {
            var entity = _em.CreateEntity();
            _em.AddComponent<StructurePlacementSingleton>(entity);
            _em.AddBuffer<StructureAnchorRecord>(entity);
            _em.AddBuffer<StructureFootprintReservation>(entity);

            Assert.IsTrue(_em.HasComponent<StructurePlacementSingleton>(entity));
            Assert.IsTrue(_em.HasBuffer<StructureAnchorRecord>(entity));
            Assert.IsTrue(_em.HasBuffer<StructureFootprintReservation>(entity));
        }

        [Test]
        public void StructureRealizedTag_StoresAnchorId()
        {
            var entity = _em.CreateEntity();
            _em.AddComponentData(entity, new StructureRealizedTag { StableAnchorId = 99 });

            var tag = _em.GetComponentData<StructureRealizedTag>(entity);
            Assert.AreEqual((uint)99, tag.StableAnchorId);
        }

        [Test]
        public void PersistenceFlags_CombineFlagsCorrectly()
        {
            var flags = StructurePersistenceFlags.Locked | StructurePersistenceFlags.Discovered;

            Assert.IsTrue((flags & StructurePersistenceFlags.Locked) != 0);
            Assert.IsTrue((flags & StructurePersistenceFlags.Discovered) != 0);
            Assert.IsFalse((flags & StructurePersistenceFlags.Modified) != 0);
            Assert.IsFalse((flags & StructurePersistenceFlags.Destroyed) != 0);
        }

        [Test]
        public void FamilyRuleset_CanInstantiateWithDefaults()
        {
            var ruleset = UnityEngine.ScriptableObject.CreateInstance<StructureFamilyRuleset>();

            Assert.AreEqual(StructureFamilyId.Dungeon, ruleset.Family); // default enum value
            Assert.AreEqual(200f, ruleset.MinSpacing);
            Assert.AreEqual(0.8f, ruleset.MinSlopeNormalY);
            Assert.AreEqual(1f, ruleset.RealizationScale);
            Assert.IsNotNull(ruleset);

            UnityEngine.Object.DestroyImmediate(ruleset);
        }

        [Test]
        public void FamilyRuleset_CanConfigureForRelicFamily()
        {
            var ruleset = UnityEngine.ScriptableObject.CreateInstance<StructureFamilyRuleset>();
            ruleset.Family = StructureFamilyId.Relic;
            ruleset.MinSpacing = 400f;
            ruleset.MaxSpacing = 800f;
            ruleset.FootprintExtents = new UnityEngine.Vector3(20f, 15f, 20f);
            ruleset.DefaultTemplateId = "Odd_Head_Relic_v1";
            ruleset.RealizationScale = 15f;

            Assert.AreEqual(StructureFamilyId.Relic, ruleset.Family);
            Assert.AreEqual(400f, ruleset.MinSpacing);
            Assert.AreEqual("Odd_Head_Relic_v1", ruleset.DefaultTemplateId);
            Assert.AreEqual(15f, ruleset.RealizationScale);

            UnityEngine.Object.DestroyImmediate(ruleset);
        }
    }
}
