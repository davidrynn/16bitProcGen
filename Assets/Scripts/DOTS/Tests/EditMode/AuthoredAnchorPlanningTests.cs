using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DOTS.Structures;
using DOTS.Terrain;

namespace DOTS.Tests.EditMode
{
    /// <summary>
    /// Covers the authored anchor candidate source (STRUCTURE_PLACEMENT_SPEC.md §9.5,
    /// ticket V12): exact placement, seed-independent identity, override-by-spacing
    /// against the procedural planner, and persistence precedence.
    /// </summary>
    [TestFixture]
    public class AuthoredAnchorPlanningTests
    {
        private TerrainSampleData _terrain;
        private NativeArray<FamilyRuleData> _families;

        private static readonly int2 CellMin = new int2(-4, -4);
        private static readonly int2 CellMax = new int2(3, 3);

        [SetUp]
        public void SetUp()
        {
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

            _families = new NativeArray<FamilyRuleData>(1, Allocator.Temp);
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
                DefaultTemplateId = "relic_hand",
                RealizationScale = 15f,
            };
        }

        [TearDown]
        public void TearDown()
        {
            if (_families.IsCreated) _families.Dispose();
        }

        private static AuthoredAnchorInput MakeHeroInput() => new AuthoredAnchorInput
        {
            AuthorId = new FixedString64Bytes("vista_hero_hand"),
            Family = StructureFamilyId.Relic,
            TemplateId = new FixedString64Bytes("relic_hand_hero"),
            PositionXZ = new float2(0f, 900f),
            SnapToTerrain = true,
            YawDegrees = 180f,
            FootprintRadius = 0f,
        };

        private void Generate(
            uint worldSeed,
            NativeArray<AuthoredAnchorInput> authored,
            NativeList<StructureAnchorRecord> existing,
            ref NativeList<StructureAnchorRecord> accepted)
        {
            var terrain = _terrain;
            terrain.WorldSeed = worldSeed;
            StructureAnchorPlanningAlgorithm.GenerateAnchors(
                worldSeed, 1, CellMin, CellMax,
                _families, existing, authored, in terrain, ref accepted);
        }

        private static int FindById(NativeList<StructureAnchorRecord> anchors, uint id)
        {
            for (int i = 0; i < anchors.Length; i++)
                if (anchors[i].StableAnchorId == id) return i;
            return -1;
        }

        [Test]
        public void AuthoredAnchor_PlacedExactly_WithTerrainSnappedY()
        {
            var authored = new NativeArray<AuthoredAnchorInput>(1, Allocator.Temp);
            authored[0] = MakeHeroInput();
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);

            Generate(12345u, authored, existing, ref accepted);

            uint id = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(authored[0].AuthorId);
            int idx = FindById(accepted, id);
            Assert.GreaterOrEqual(idx, 0, "Authored anchor must be accepted");

            var a = accepted[idx];
            Assert.AreEqual(0f, a.WorldPosition.x, 0.001f, "Authored X must be exact");
            Assert.AreEqual(900f, a.WorldPosition.z, 0.001f, "Authored Z must be exact");

            float expectedY = StructureAnchorPlanningAlgorithm.SampleTerrainHeight(
                new float2(0f, 900f), in _terrain);
            Assert.AreEqual(expectedY, a.WorldPosition.y, 0.001f, "Y must snap to terrain height");

            Assert.AreEqual(StructurePlacementSource.Authored, a.Source);
            Assert.AreEqual("relic_hand_hero", a.TemplateId.ToString(),
                "Explicit TemplateId must pass through untouched");
            Assert.AreEqual(StructureFamilyId.Relic, a.Family);

            authored.Dispose();
            existing.Dispose();
            accepted.Dispose();
        }

        [Test]
        public void AuthoredAnchorId_IsDeterministic_AndDistinctPerAuthorId()
        {
            var idA1 = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(
                new FixedString64Bytes("vista_hero_hand"));
            var idA2 = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(
                new FixedString64Bytes("vista_hero_hand"));
            var idB = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(
                new FixedString64Bytes("quest_shrine_01"));

            Assert.AreEqual(idA1, idA2, "Same AuthorId must hash to the same StableAnchorId");
            Assert.AreNotEqual(idA1, idB, "Different AuthorIds must hash differently");
        }

        [Test]
        public void AuthoredAnchor_IdenticalAcrossWorldSeeds()
        {
            var authored = new NativeArray<AuthoredAnchorInput>(1, Allocator.Temp);
            authored[0] = MakeHeroInput();
            uint id = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(authored[0].AuthorId);

            var existing1 = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted1 = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            Generate(12345u, authored, existing1, ref accepted1);

            var existing2 = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted2 = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            Generate(99999u, authored, existing2, ref accepted2);

            int idx1 = FindById(accepted1, id);
            int idx2 = FindById(accepted2, id);
            Assert.GreaterOrEqual(idx1, 0, "Authored anchor must exist under seed 12345");
            Assert.GreaterOrEqual(idx2, 0, "Authored anchor must exist under seed 99999");

            // Identity and XZ are seed-independent; Y may differ (terrain height varies with seed).
            Assert.AreEqual(accepted1[idx1].StableAnchorId, accepted2[idx2].StableAnchorId);
            Assert.AreEqual(accepted1[idx1].WorldPosition.x, accepted2[idx2].WorldPosition.x, 0.001f);
            Assert.AreEqual(accepted1[idx1].WorldPosition.z, accepted2[idx2].WorldPosition.z, 0.001f);

            authored.Dispose();
            existing1.Dispose();
            accepted1.Dispose();
            existing2.Dispose();
            accepted2.Dispose();
        }

        [Test]
        public void ProceduralCandidates_YieldToAuthoredAnchor()
        {
            var authored = new NativeArray<AuthoredAnchorInput>(1, Allocator.Temp);
            authored[0] = MakeHeroInput();
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);

            Generate(12345u, authored, existing, ref accepted);

            uint id = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(authored[0].AuthorId);
            int idx = FindById(accepted, id);
            Assert.GreaterOrEqual(idx, 0);

            // The spacing invariant must hold with the authored anchor included:
            // no procedural same-family anchor may sit within MinSpacing of it.
            float minSpacing = _families[0].MinSpacing;
            for (int i = 0; i < accepted.Length; i++)
            {
                if (i == idx || accepted[i].Family != StructureFamilyId.Relic) continue;
                float dist = math.distance(accepted[i].WorldPosition, accepted[idx].WorldPosition);
                Assert.GreaterOrEqual(dist, minSpacing,
                    $"Procedural anchor [{i}] is {dist:F1}u from the authored anchor, min is {minSpacing}");
            }

            authored.Dispose();
            existing.Dispose();
            accepted.Dispose();
        }

        [Test]
        public void LockedCopy_OutranksAuthoredInput_NoDuplicate()
        {
            var authored = new NativeArray<AuthoredAnchorInput>(1, Allocator.Temp);
            authored[0] = MakeHeroInput();
            uint id = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(authored[0].AuthorId);

            // First pass produces the authored record; simulate persistence by
            // locking it and moving it (as a player-modified structure would be).
            var existing1 = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted1 = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            Generate(12345u, authored, existing1, ref accepted1);

            int idx = FindById(accepted1, id);
            Assert.GreaterOrEqual(idx, 0);
            var lockedCopy = accepted1[idx];
            lockedCopy.PersistenceFlags = StructurePersistenceFlags.Locked;
            lockedCopy.WorldPosition.x += 50f;

            var existing2 = new NativeList<StructureAnchorRecord>(1, Allocator.Temp);
            existing2.Add(lockedCopy);
            var accepted2 = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            Generate(12345u, authored, existing2, ref accepted2);

            int count = 0;
            int foundIdx = -1;
            for (int i = 0; i < accepted2.Length; i++)
            {
                if (accepted2[i].StableAnchorId == id) { count++; foundIdx = i; }
            }
            Assert.AreEqual(1, count, "Locked copy and authored input must not duplicate");
            Assert.AreEqual(StructurePersistenceFlags.Locked, accepted2[foundIdx].PersistenceFlags,
                "The locked (persisted) copy must win over fresh authoring data");
            Assert.AreEqual(lockedCopy.WorldPosition.x, accepted2[foundIdx].WorldPosition.x, 0.001f,
                "The locked copy's position must be preserved, not re-stamped from authoring");

            authored.Dispose();
            existing1.Dispose();
            accepted1.Dispose();
            existing2.Dispose();
            accepted2.Dispose();
        }

        [Test]
        public void ExplicitY_UsedWhenSnapDisabled()
        {
            var input = MakeHeroInput();
            input.SnapToTerrain = false;
            input.ExplicitY = 123.5f;

            var authored = new NativeArray<AuthoredAnchorInput>(1, Allocator.Temp);
            authored[0] = input;
            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);

            Generate(12345u, authored, existing, ref accepted);

            int idx = FindById(accepted,
                StructureAnchorPlanningAlgorithm.AuthoredAnchorId(input.AuthorId));
            Assert.GreaterOrEqual(idx, 0);
            Assert.AreEqual(123.5f, accepted[idx].WorldPosition.y, 0.001f);

            authored.Dispose();
            existing.Dispose();
            accepted.Dispose();
        }

        [Test]
        public void DuplicateAuthorIds_FirstWins_NoDuplicateRecords()
        {
            var authored = new NativeArray<AuthoredAnchorInput>(2, Allocator.Temp);
            authored[0] = MakeHeroInput();
            var dup = MakeHeroInput();          // same AuthorId → same StableAnchorId
            dup.PositionXZ = new float2(400f, -400f);
            authored[1] = dup;

            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            Generate(12345u, authored, existing, ref accepted);

            uint id = StructureAnchorPlanningAlgorithm.AuthoredAnchorId(authored[0].AuthorId);
            int count = 0;
            int foundIdx = -1;
            for (int i = 0; i < accepted.Length; i++)
            {
                if (accepted[i].StableAnchorId == id) { count++; foundIdx = i; }
            }

            Assert.AreEqual(1, count,
                "Duplicate AuthorIds must produce exactly one record — realization diffing keys on StableAnchorId");
            Assert.AreEqual(900f, accepted[foundIdx].WorldPosition.z, 0.001f,
                "The first entry must win; the duplicate's position must not overwrite it");

            authored.Dispose();
            existing.Dispose();
            accepted.Dispose();
        }

        [Test]
        public void FootprintRadius_InheritsFamilyWhenZero()
        {
            var authored = new NativeArray<AuthoredAnchorInput>(2, Allocator.Temp);
            authored[0] = MakeHeroInput(); // FootprintRadius = 0 → inherit 15
            var custom = MakeHeroInput();
            custom.AuthorId = new FixedString64Bytes("custom_radius_anchor");
            custom.PositionXZ = new float2(500f, -500f);
            custom.FootprintRadius = 42f;
            authored[1] = custom;

            var existing = new NativeList<StructureAnchorRecord>(0, Allocator.Temp);
            var accepted = new NativeList<StructureAnchorRecord>(32, Allocator.Temp);
            Generate(12345u, authored, existing, ref accepted);

            int idxInherit = FindById(accepted,
                StructureAnchorPlanningAlgorithm.AuthoredAnchorId(authored[0].AuthorId));
            int idxCustom = FindById(accepted,
                StructureAnchorPlanningAlgorithm.AuthoredAnchorId(custom.AuthorId));
            Assert.GreaterOrEqual(idxInherit, 0);
            Assert.GreaterOrEqual(idxCustom, 0);
            Assert.AreEqual(_families[0].FootprintRadius, accepted[idxInherit].Radius, 0.001f,
                "Radius 0 must inherit the family footprint");
            Assert.AreEqual(42f, accepted[idxCustom].Radius, 0.001f,
                "Explicit radius must pass through");

            authored.Dispose();
            existing.Dispose();
            accepted.Dispose();
        }
    }
}
