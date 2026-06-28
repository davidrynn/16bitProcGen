using System.Collections.Generic;
using DOTS.Terrain.SurfaceScatter;
using NUnit.Framework;
using UnityEngine;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SurfaceScatterLodUtilityTests
    {
        private readonly List<Mesh> _createdMeshes = new List<Mesh>();

        [TearDown]
        public void TearDown()
        {
            foreach (Mesh mesh in _createdMeshes)
            {
                Object.DestroyImmediate(mesh);
            }

            _createdMeshes.Clear();
        }

        private Mesh CreateMesh(string name)
        {
            var mesh = new Mesh { name = name };
            _createdMeshes.Add(mesh);
            return mesh;
        }

        [Test]
        public void SelectLodLevel_DisabledSwapDistance_ReturnsNear()
        {
            Assert.AreEqual(
                SurfaceScatterLodUtility.NearLod,
                SurfaceScatterLodUtility.SelectLodLevel(1_000_000f, 0f),
                "Zero swap distance must disable LOD regardless of instance distance.");

            Assert.AreEqual(
                SurfaceScatterLodUtility.NearLod,
                SurfaceScatterLodUtility.SelectLodLevel(1_000_000f, -25f),
                "Negative swap distance must disable LOD regardless of instance distance.");
        }

        [Test]
        public void SelectLodLevel_BelowThreshold_ReturnsNear()
        {
            const float swapDistance = 60f;
            const float distance = 59f;

            Assert.AreEqual(
                SurfaceScatterLodUtility.NearLod,
                SurfaceScatterLodUtility.SelectLodLevel(distance * distance, swapDistance));
        }

        [Test]
        public void SelectLodLevel_ExactlyAtThreshold_ReturnsNear()
        {
            const float swapDistance = 60f;

            Assert.AreEqual(
                SurfaceScatterLodUtility.NearLod,
                SurfaceScatterLodUtility.SelectLodLevel(swapDistance * swapDistance, swapDistance),
                "Boundary is exclusive: an instance exactly at the swap distance stays near.");
        }

        [Test]
        public void SelectLodLevel_BeyondThreshold_ReturnsFar()
        {
            const float swapDistance = 60f;
            const float distance = 61f;

            Assert.AreEqual(
                SurfaceScatterLodUtility.FarLod,
                SurfaceScatterLodUtility.SelectLodLevel(distance * distance, swapDistance));
        }

        [Test]
        public void GetBucketIndex_NearLod_ReturnsVariantIndex()
        {
            const int maxVariants = 8;

            Assert.AreEqual(0, SurfaceScatterLodUtility.GetBucketIndex(0, SurfaceScatterLodUtility.NearLod, maxVariants));
            Assert.AreEqual(7, SurfaceScatterLodUtility.GetBucketIndex(7, SurfaceScatterLodUtility.NearLod, maxVariants));
        }

        [Test]
        public void GetBucketIndex_FarLod_ReturnsVariantIndexOffsetByMaxVariants()
        {
            const int maxVariants = 8;

            Assert.AreEqual(8, SurfaceScatterLodUtility.GetBucketIndex(0, SurfaceScatterLodUtility.FarLod, maxVariants));
            Assert.AreEqual(15, SurfaceScatterLodUtility.GetBucketIndex(7, SurfaceScatterLodUtility.FarLod, maxVariants));
        }

        [Test]
        public void AutoPairFarMeshes_PairsBySuffixName()
        {
            Mesh near0 = CreateMesh("Boulder_01");
            Mesh near1 = CreateMesh("Boulder_02");
            Mesh far0 = CreateMesh("Boulder_01_Far");
            Mesh far1 = CreateMesh("Boulder_02_Far");
            Mesh unrelated = CreateMesh("PebbleCluster_01");

            Mesh[] result = SurfaceScatterLodUtility.AutoPairFarMeshes(
                new[] { near0, near1 },
                new[] { unrelated, far1, far0 },
                existingLodVariants: null);

            Assert.AreEqual(2, result.Length, "Result must be index-aligned to the near variants.");
            Assert.AreSame(far0, result[0]);
            Assert.AreSame(far1, result[1]);
        }

        [Test]
        public void AutoPairFarMeshes_PreservesManualAssignments()
        {
            Mesh near0 = CreateMesh("Boulder_01");
            Mesh autoCandidate = CreateMesh("Boulder_01_Far");
            Mesh manual = CreateMesh("Boulder_Custom_Far");

            Mesh[] result = SurfaceScatterLodUtility.AutoPairFarMeshes(
                new[] { near0 },
                new[] { autoCandidate },
                new[] { manual });

            Assert.AreSame(manual, result[0], "A manually assigned far mesh must never be overwritten by auto-pairing.");
        }

        [Test]
        public void AutoPairFarMeshes_NoMatch_LeavesSlotNull()
        {
            Mesh near0 = CreateMesh("Boulder_01");
            Mesh unrelated = CreateMesh("Shrub_01_Far");

            Mesh[] result = SurfaceScatterLodUtility.AutoPairFarMeshes(
                new[] { near0 },
                new[] { unrelated },
                existingLodVariants: null);

            Assert.IsNull(result[0], "No matching '<name>_Far' candidate means the variant stays full-detail (null slot).");
        }

        [Test]
        public void AutoPairFarMeshes_NullNearEntry_LeavesSlotNull()
        {
            Mesh near1 = CreateMesh("Boulder_02");
            Mesh far1 = CreateMesh("Boulder_02_Far");

            Mesh[] result = SurfaceScatterLodUtility.AutoPairFarMeshes(
                new Mesh[] { null, near1 },
                new[] { far1 },
                existingLodVariants: null);

            Assert.IsNull(result[0]);
            Assert.AreSame(far1, result[1]);
        }

        [Test]
        public void AutoPairFarMeshes_NullOrEmptyNearVariants_ReturnsExistingUnchanged()
        {
            Mesh[] existing = { CreateMesh("Boulder_01_Far") };

            Assert.AreSame(existing, SurfaceScatterLodUtility.AutoPairFarMeshes(null, new List<Mesh>(), existing));
            Assert.AreSame(existing, SurfaceScatterLodUtility.AutoPairFarMeshes(new Mesh[0], new List<Mesh>(), existing));
        }

        [Test]
        public void AutoPairFarMeshes_RealignsLengthToNearVariants()
        {
            Mesh near0 = CreateMesh("Boulder_01");
            Mesh near1 = CreateMesh("Boulder_02");
            Mesh manual = CreateMesh("Manual_Far");
            Mesh far1 = CreateMesh("Boulder_02_Far");

            // Existing array shorter than near variants: index 0 preserved, index 1 auto-filled.
            Mesh[] result = SurfaceScatterLodUtility.AutoPairFarMeshes(
                new[] { near0, near1 },
                new[] { far1 },
                new[] { manual });

            Assert.AreEqual(2, result.Length);
            Assert.AreSame(manual, result[0]);
            Assert.AreSame(far1, result[1]);
        }
    }
}
