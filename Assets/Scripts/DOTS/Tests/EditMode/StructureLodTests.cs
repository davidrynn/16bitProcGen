using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using DOTS.Structures;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// Unit tests for the relic LOD / impostor subsystem. Covers component
    /// defaults, per-entity LOD params, template registry lookup, and the
    /// pure-static swap-decision helper.
    /// </summary>
    [TestFixture]
    public class StructureLodTests
    {
        private World _world;
        private EntityManager _em;

        [SetUp]
        public void SetUp()
        {
            _world = new World("StructureLodTest");
            _em = _world.EntityManager;
        }

        [TearDown]
        public void TearDown()
        {
            if (_world.IsCreated) _world.Dispose();
        }

        [Test]
        public void RelicLodState_DefaultsToZero()
        {
            var state = new RelicLodState();
            Assert.AreEqual((byte)0, state.CurrentLod);
        }

        [Test]
        public void RelicLodState_CanStoreAndRetrieveLodValue()
        {
            var entity = _em.CreateEntity();
            _em.AddComponentData(entity, new RelicLodState { CurrentLod = 0 });
            Assert.AreEqual((byte)0, _em.GetComponentData<RelicLodState>(entity).CurrentLod);

            _em.SetComponentData(entity, new RelicLodState { CurrentLod = 1 });
            Assert.AreEqual((byte)1, _em.GetComponentData<RelicLodState>(entity).CurrentLod);
        }

        [Test]
        public void RelicRenderConfig_EmptyTemplateListIsValid()
        {
            var cfg = new RelicRenderConfig();
            Assert.IsNotNull(cfg.Templates);
            Assert.AreEqual(0, cfg.Templates.Count);
            Assert.AreEqual(0f, cfg.LodSwapDistance);
            Assert.AreEqual(0f, cfg.LodHysteresis);
        }

        [Test]
        public void RelicRenderConfig_GetTemplate_ReturnsMatchOrFallback()
        {
            var cfg = new RelicRenderConfig();
            cfg.Templates.Add(new RelicTemplateEntry { TemplateId = "head", UniformScale = 15f });
            cfg.Templates.Add(new RelicTemplateEntry { TemplateId = "tower", UniformScale = 10f });

            // Exact match
            var head = cfg.GetTemplate(new FixedString64Bytes("head"));
            Assert.AreEqual("head", head.TemplateId);

            // Fallback to first entry when not found
            var fallback = cfg.GetTemplate(new FixedString64Bytes("nonexistent"));
            Assert.AreEqual("head", fallback.TemplateId);
        }

        [Test]
        public void RelicRenderConfig_GetTemplate_ReturnsNullWhenEmpty()
        {
            var cfg = new RelicRenderConfig();
            var result = cfg.GetTemplate(new FixedString64Bytes("anything"));
            Assert.IsNull(result);
        }

        [Test]
        public void RelicLodParams_CanStorePerEntityData()
        {
            var entity = _em.CreateEntity();
            var lod = new RelicLodParams
            {
                FullScale = 15f,
                ImpostorScale = 0.5f,
                FullBoundsLocal = new AABB { Center = default, Extents = new Unity.Mathematics.float3(1f) },
                ImpostorBoundsLocal = new AABB { Center = default, Extents = new Unity.Mathematics.float3(0.1f) },
            };
            _em.AddComponentData(entity, lod);

            var read = _em.GetComponentData<RelicLodParams>(entity);
            Assert.AreEqual(15f, read.FullScale);
            Assert.AreEqual(0.5f, read.ImpostorScale);
        }

        /// <summary>
        /// Guards the V16 LOD-dormancy decision: without an authored impostor mesh a
        /// relic must NOT participate in distance LOD (the old same-mesh fallback
        /// saved zero vertices and popped the relic's world size at the swap
        /// distance). If this fails after adding impostor art, that's the feature
        /// waking up as intended — not a regression.
        /// </summary>
        [Test]
        public void TemplateParticipatesInLod_RequiresAuthoredImpostorMesh()
        {
            Assert.IsFalse(
                RelicRealizationSystem.TemplateParticipatesInLod(null),
                "Null template must not participate in LOD.");

            var noImpostor = new RelicTemplateEntry { TemplateId = "hand", ImpostorMesh = null };
            Assert.IsFalse(
                RelicRealizationSystem.TemplateParticipatesInLod(noImpostor),
                "Template without authored impostor mesh must skip LOD (V16 dormancy).");

            var mesh = new UnityEngine.Mesh();
            try
            {
                var authored = new RelicTemplateEntry { TemplateId = "hand", ImpostorMesh = mesh };
                Assert.IsTrue(
                    RelicRealizationSystem.TemplateParticipatesInLod(authored),
                    "Authoring an impostor mesh must re-enable the LOD path.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(mesh);
            }
        }

        [Test]
        public void LodThreshold_Hysteresis_DoesNotFlipWithinBand()
        {
            // swap = 1000, hysteresis = 100  → near² = 900², far² = 1100²
            float nearCutoff = 900f;
            float farCutoff = 1100f;
            float nearCutoffSq = nearCutoff * nearCutoff;
            float farCutoffSq = farCutoff * farCutoff;

            // Inside the band: current LOD preserved regardless of which side is "target"
            float insideSq = 1000f * 1000f;
            Assert.AreEqual((byte)0, RelicLodSelectionSystem.ResolveTargetLod(insideSq, 0, nearCutoffSq, farCutoffSq));
            Assert.AreEqual((byte)1, RelicLodSelectionSystem.ResolveTargetLod(insideSq, 1, nearCutoffSq, farCutoffSq));

            // Clearly near → always LOD 0
            float nearSq = 500f * 500f;
            Assert.AreEqual((byte)0, RelicLodSelectionSystem.ResolveTargetLod(nearSq, 0, nearCutoffSq, farCutoffSq));
            Assert.AreEqual((byte)0, RelicLodSelectionSystem.ResolveTargetLod(nearSq, 1, nearCutoffSq, farCutoffSq));

            // Clearly far → always LOD 1
            float farSq = 2000f * 2000f;
            Assert.AreEqual((byte)1, RelicLodSelectionSystem.ResolveTargetLod(farSq, 0, nearCutoffSq, farCutoffSq));
            Assert.AreEqual((byte)1, RelicLodSelectionSystem.ResolveTargetLod(farSq, 1, nearCutoffSq, farCutoffSq));
        }
    }
}
