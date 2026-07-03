using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DOTS.Structures;
using DOTS.Terrain;

namespace DOTS.Tests.EditMode
{
    [TestFixture]
    public class StructureAnchorPlanningTests
    {
        private TerrainSampleData _terrain;
        private NativeArray<FamilyRuleData> _families;

        [SetUp]
        public void SetUp()
        {
            // Flat terrain with known parameters so elevation checks pass
            _terrain = new TerrainSampleData
            {
                WorldSeed = 12345u,
                FieldSettings = new TerrainFieldSettings
                {
                    BaseHeight = 30f,
                    ElevationLowFrequency = 0.005f,
                    ElevationLowAmplitude = 20f,
                    ElevationMidFrequency = 0.02f,
                    ElevationMidAmplitude = 5f,
                    ElevationHighFrequency = 0.05f,
                    ElevationHighAmplitude = 2f,
                    ElevationExponent = 1.4f,
                },
            };

            _families = new NativeArray<FamilyRuleData>(2, Allocator.Temp);
            _families[0] = new FamilyRuleData
            {
                Family = StructureFamilyId.Relic,
                MinSpacing = 200f,
                MaxSpacing = 600f,
                CandidatesPerCell = 2,
                MinSlopeNormalY = 0.7f,
                MinElevation = 1f,
                MaxElevation = 200f,
                FootprintRadius = 15f,
                DefaultTemplateId = "Odd_Head_Relic_v1",
                RealizationScale = 15f,
            };
            _families[1] = new FamilyRuleData
            {
                Family = StructureFamilyId.Dungeon,
                MinSpacing = 300f,
                MaxSpacing = 800f,
                CandidatesPerCell = 1,
                MinSlopeNormalY = 0.8f,
                MinElevation = 5f,
                MaxElevation = 150f,
                FootprintRadius = 20f,
                DefaultTemplateId = "Dungeon_WFC",
                RealizationScale = 1f,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_families.IsCreated) _families.Dispose();
        }

        [Test]
        public void SameSeed_ProducesSameAnchors()
        {
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var cellMin = new int2(-2, -2);
            var cellMax = new int2(1, 1);

            var accepted1 = new NativeList<StructureAnchorRecord>(16, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing, in _terrain, ref accepted1);

            var accepted2 = new NativeList<StructureAnchorRecord>(16, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing, in _terrain, ref accepted2);

            Assert.AreEqual(accepted1.Length, accepted2.Length, "Anchor count should be identical for same seed");
            for (int i = 0; i < accepted1.Length; i++)
            {
                Assert.AreEqual(accepted1[i].StableAnchorId, accepted2[i].StableAnchorId,
                    $"Anchor [{i}] StableAnchorId mismatch");
                Assert.AreEqual(accepted1[i].WorldPosition.x, accepted2[i].WorldPosition.x, 0.001f,
                    $"Anchor [{i}] WorldPosition.x mismatch");
                Assert.AreEqual(accepted1[i].WorldPosition.z, accepted2[i].WorldPosition.z, 0.001f,
                    $"Anchor [{i}] WorldPosition.z mismatch");
            }

            accepted1.Dispose();
            accepted2.Dispose();
            existing.Dispose();
        }

        [Test]
        public void DifferentSeed_ProducesDifferentAnchors()
        {
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var cellMin = new int2(-2, -2);
            var cellMax = new int2(1, 1);

            var accepted1 = new NativeList<StructureAnchorRecord>(16, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing, in _terrain, ref accepted1);

            var terrain2 = _terrain;
            terrain2.WorldSeed = 99999u;
            var accepted2 = new NativeList<StructureAnchorRecord>(16, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                99999u, 1, cellMin, cellMax, _families, existing, in terrain2, ref accepted2);

            // At least one anchor should differ in position
            bool anyDifferent = false;
            int minLen = math.min(accepted1.Length, accepted2.Length);
            for (int i = 0; i < minLen; i++)
            {
                if (math.distance(accepted1[i].WorldPosition, accepted2[i].WorldPosition) > 1f)
                {
                    anyDifferent = true;
                    break;
                }
            }
            if (accepted1.Length != accepted2.Length) anyDifferent = true;

            Assert.IsTrue(anyDifferent, "Different seeds should produce different anchor placements");

            accepted1.Dispose();
            accepted2.Dispose();
            existing.Dispose();
        }

        [Test]
        public void NoSameFamilyAnchors_CloserThanMinSpacing()
        {
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var cellMin = new int2(-4, -4);
            var cellMax = new int2(3, 3);

            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing, in _terrain, ref accepted);

            // Check relic spacing
            float relicMinSpacing = _families[0].MinSpacing;
            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i].Family != StructureFamilyId.Relic) continue;
                for (int j = i + 1; j < accepted.Length; j++)
                {
                    if (accepted[j].Family != StructureFamilyId.Relic) continue;
                    float dist = math.distance(accepted[i].WorldPosition, accepted[j].WorldPosition);
                    Assert.GreaterOrEqual(dist, relicMinSpacing,
                        $"Relic anchors [{i}] and [{j}] are {dist:F1} apart, min is {relicMinSpacing}");
                }
            }

            // Check dungeon spacing
            float dungeonMinSpacing = _families[1].MinSpacing;
            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i].Family != StructureFamilyId.Dungeon) continue;
                for (int j = i + 1; j < accepted.Length; j++)
                {
                    if (accepted[j].Family != StructureFamilyId.Dungeon) continue;
                    float dist = math.distance(accepted[i].WorldPosition, accepted[j].WorldPosition);
                    Assert.GreaterOrEqual(dist, dungeonMinSpacing,
                        $"Dungeon anchors [{i}] and [{j}] are {dist:F1} apart, min is {dungeonMinSpacing}");
                }
            }

            accepted.Dispose();
            existing.Dispose();
        }

        [Test]
        public void LockedAnchors_SurviveRegeneration()
        {
            var cellMin = new int2(-2, -2);
            var cellMax = new int2(1, 1);

            // First pass — generate normally
            var existing1 = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted1 = new NativeList<StructureAnchorRecord>(16, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing1, in _terrain, ref accepted1);

            Assert.Greater(accepted1.Length, 0, "First pass should produce at least one anchor");

            // Mark first anchor as locked
            var lockedAnchor = accepted1[0];
            lockedAnchor.PersistenceFlags = StructurePersistenceFlags.Locked;

            // Second pass — feed the locked anchor as existing
            var existing2 = new NativeList<StructureAnchorRecord>(1, Allocator.Temp);
            existing2.Add(lockedAnchor);

            var accepted2 = new NativeList<StructureAnchorRecord>(16, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing2, in _terrain, ref accepted2);

            // Locked anchor must appear in results
            bool foundLocked = false;
            for (int i = 0; i < accepted2.Length; i++)
            {
                if (accepted2[i].StableAnchorId == lockedAnchor.StableAnchorId)
                {
                    foundLocked = true;
                    Assert.AreEqual(StructurePersistenceFlags.Locked, accepted2[i].PersistenceFlags);
                    break;
                }
            }
            Assert.IsTrue(foundLocked, "Locked anchor must survive regeneration");

            accepted1.Dispose();
            accepted2.Dispose();
            existing1.Dispose();
            existing2.Dispose();
        }

        [Test]
        public void StableAnchorId_IsDeterministicAndUnique()
        {
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var cellMin = new int2(-3, -3);
            var cellMax = new int2(2, 2);

            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing, in _terrain, ref accepted);

            // All IDs must be unique
            for (int i = 0; i < accepted.Length; i++)
            {
                for (int j = i + 1; j < accepted.Length; j++)
                {
                    Assert.AreNotEqual(accepted[i].StableAnchorId, accepted[j].StableAnchorId,
                        $"Duplicate StableAnchorId at [{i}] and [{j}]");
                }
            }

            accepted.Dispose();
            existing.Dispose();
        }

        [Test]
        public void GenerateAnchors_ProducesNonZeroResults()
        {
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var cellMin = new int2(-4, -4);
            var cellMax = new int2(3, 3);

            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                12345u, 1, cellMin, cellMax, _families, existing, in _terrain, ref accepted);

            Assert.Greater(accepted.Length, 0,
                "64 planning cells with 2 families should produce at least some accepted anchors");

            // Should have both families represented
            bool hasRelic = false, hasDungeon = false;
            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i].Family == StructureFamilyId.Relic) hasRelic = true;
                if (accepted[i].Family == StructureFamilyId.Dungeon) hasDungeon = true;
            }
            Assert.IsTrue(hasRelic, "Should have at least one relic anchor");
            Assert.IsTrue(hasDungeon, "Should have at least one dungeon anchor");

            accepted.Dispose();
            existing.Dispose();
        }

        [Test]
        public void TerrainHeight_SamplesConsistently()
        {
            float2 pos = new float2(100f, 200f);
            float h1 = StructureAnchorPlanningAlgorithm.SampleTerrainHeight(pos, in _terrain);
            float h2 = StructureAnchorPlanningAlgorithm.SampleTerrainHeight(pos, in _terrain);

            Assert.AreEqual(h1, h2, 0.0001f, "Terrain height must be deterministic");
            Assert.Greater(h1, 0f, "Terrain height should be positive with BaseHeight=30");
        }
    }
}
