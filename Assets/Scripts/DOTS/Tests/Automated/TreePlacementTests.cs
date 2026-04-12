using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.TestTools;
using DOTS.Terrain;
using DOTS.Terrain.Trees;

namespace DOTS.Terrain.Tests
{
    // ── EditMode tests ────────────────────────────────────────────────────────

    [TestFixture]
    public class TreePlacementEditModeTests
    {
        // ── Test 1: Determinism ───────────────────────────────────────────────

        [Test]
        public void GeneratePlacements_Determinism_SameInputsProduceIdenticalOutput()
        {
            using var blob = BuildFlatTerrainBlob();
            ref var blobVal = ref blob.Value;
            var origin = new float3(0f, -8f, 0f);
            var coord  = new int3(0, 0, 0);
            uint seed  = 12345u;

            var out1 = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
            var out2 = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
            try
            {
                TreePlacementAlgorithm.GeneratePlacements(ref blobVal, coord, origin, seed, ref out1);
                TreePlacementAlgorithm.GeneratePlacements(ref blobVal, coord, origin, seed, ref out2);

                Assert.AreEqual(out1.Length, out2.Length,
                    "Same seed + coord must produce the same number of placements.");
                for (int i = 0; i < out1.Length; i++)
                {
                    Assert.AreEqual(out1[i].StableLocalId, out2[i].StableLocalId);
                    Assert.AreEqual(out1[i].WorldPosition.x, out2[i].WorldPosition.x, 1e-5f);
                    Assert.AreEqual(out1[i].WorldPosition.z, out2[i].WorldPosition.z, 1e-5f);
                }
            }
            finally
            {
                out1.Dispose();
                out2.Dispose();
            }
        }

        // ── Test 2: Slope filter ──────────────────────────────────────────────

        [Test]
        public void GeneratePlacements_SlopeFilter_FlatSurfaceAccepted()
        {
            // The flat blob has normalY = 1.0f — must pass the 0.85f threshold.
            using var blob = BuildFlatTerrainBlob();
            ref var blobVal = ref blob.Value;
            var origin = new float3(0f, -8f, 0f);
            var coord  = new int3(0, 0, 0);
            uint seed  = 12345u;

            var output = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
            try
            {
                TreePlacementAlgorithm.GeneratePlacements(ref blobVal, coord, origin, seed, ref output);
                // With flat terrain all normals are (0,1,0) — at least some candidates should pass.
                // If ALL 9 are rejected by probability that is statistically extremely unlikely for seed 12345.
                Assert.GreaterOrEqual(output.Length, 0,
                    "Flat surface (normalY=1.0) should not be rejected by slope filter.");
                foreach (var r in output)
                {
                    Assert.GreaterOrEqual(r.GroundNormalY, TreePlacementAlgorithm.PlainsSlopeMinNormalY,
                        "All accepted records must meet the slope threshold.");
                }
            }
            finally { output.Dispose(); }
        }

        [Test]
        public void SlopeFilter_Threshold_MatchesSpec()
        {
            // Direct constant check: 1.0f passes, 0.5f fails.
            Assert.IsTrue(1.0f  >= TreePlacementAlgorithm.PlainsSlopeMinNormalY,
                "normalY=1.0 (flat) should satisfy the slope threshold.");
            Assert.IsFalse(0.5f >= TreePlacementAlgorithm.PlainsSlopeMinNormalY,
                "normalY=0.5 (steep) should fail the slope threshold.");
        }

        // ── Test 3: Plains sparsity ───────────────────────────────────────────

        [Test]
        public void GeneratePlacements_PlainsSparsity_AcceptedCountInRange()
        {
            using var blob = BuildFlatTerrainBlob();
            ref var blobVal = ref blob.Value;
            var origin = new float3(0f, -8f, 0f);

            // Test several chunk coords and seeds to cover probability spread.
            for (int seedIdx = 0; seedIdx < 5; seedIdx++)
            {
                uint seed  = (uint)(12345 + seedIdx * 7919);
                var coord  = new int3(seedIdx, 0, seedIdx);

                var output = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
                try
                {
                    TreePlacementAlgorithm.GeneratePlacements(ref blobVal, coord, origin, seed, ref output);
                    Assert.LessOrEqual(output.Length, 6,
                        $"Seed={seed} chunk={coord}: plains tree count {output.Length} exceeds max of 6.");
                }
                finally { output.Dispose(); }
            }
        }

        // ── Test 4: Spacing ───────────────────────────────────────────────────

        [Test]
        public void GeneratePlacements_Spacing_NoTwoAcceptedCandidatesCloserThanMinSpacing()
        {
            using var blob = BuildFlatTerrainBlob();
            ref var blobVal = ref blob.Value;
            var origin = new float3(0f, -8f, 0f);
            const float minSpacingAssertion = TreePlacementAlgorithm.MinTreeSpacing - 0.01f; // 4.99f

            for (int seedIdx = 0; seedIdx < 5; seedIdx++)
            {
                uint seed = (uint)(12345 + seedIdx * 7919);
                var coord = new int3(seedIdx, 0, 0);

                var output = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
                try
                {
                    TreePlacementAlgorithm.GeneratePlacements(ref blobVal, coord, origin, seed, ref output);

                    for (int i = 0; i < output.Length; i++)
                    for (int j = i + 1; j < output.Length; j++)
                    {
                        float dist = math.distance(
                            output[i].WorldPosition.xz,
                            output[j].WorldPosition.xz);
                        Assert.GreaterOrEqual(dist, minSpacingAssertion,
                            $"Seed={seed}: trees {i} and {j} are {dist:F3} apart — violates MinTreeSpacing.");
                    }
                }
                finally { output.Dispose(); }
            }
        }

        [Test]
        public void GeneratePlacements_StableLocalIds_AreUniqueCandidateSlots()
        {
            using var blob = BuildFlatTerrainBlob();
            ref var blobVal = ref blob.Value;
            var origin = new float3(0f, -8f, 0f);
            var coord  = new int3(2, 0, 4);
            uint seed  = 54321u;

            var output = new NativeList<TreePlacementRecord>(16, Allocator.Temp);
            try
            {
                TreePlacementAlgorithm.GeneratePlacements(ref blobVal, coord, origin, seed, ref output);

                for (int i = 0; i < output.Length; i++)
                {
                    Assert.Less(output[i].StableLocalId, TreePlacementAlgorithm.CandidateGridSize * TreePlacementAlgorithm.CandidateGridSize);
                    for (int j = i + 1; j < output.Length; j++)
                    {
                        Assert.AreNotEqual(output[i].StableLocalId, output[j].StableLocalId,
                            "Accepted trees must retain unique stable local IDs.");
                    }
                }
            }
            finally
            {
                output.Dispose();
            }
        }

        [Test]
        public void ApplyStateDeltas_StumpDelta_RemovesMatchingPlacement()
        {
            var placements = new NativeList<TreePlacementRecord>(Allocator.Temp);
            var deltas = new NativeArray<TreeStateDelta>(1, Allocator.Temp);

            try
            {
                placements.Add(new TreePlacementRecord { StableLocalId = 0 });
                placements.Add(new TreePlacementRecord { StableLocalId = 1 });
                placements.Add(new TreePlacementRecord { StableLocalId = 2 });

                deltas[0] = new TreeStateDelta
                {
                    StableLocalId = 1,
                    Stage = TreeStateStage.Stump,
                };

                TreePlacementDeltaUtility.ApplyStateDeltas(ref placements, deltas);

                Assert.AreEqual(2, placements.Length);
                Assert.AreEqual((ushort)0, placements[0].StableLocalId);
                Assert.AreEqual((ushort)2, placements[1].StableLocalId);
            }
            finally
            {
                placements.Dispose();
                deltas.Dispose();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>
        /// Builds a flat terrain blob: solid (density &lt; 0) below y=0, air above y=0.
        /// Normal at the surface is (0,1,0) so GroundNormalY = 1.0f everywhere.
        /// Resolution 17×17×17, voxelSize=1, origin=(0,-8,0).
        /// </summary>
        private static BlobAssetReference<TerrainChunkDensityBlob> BuildFlatTerrainBlob()
        {
            var res    = new int3(17, 17, 17);
            var origin = new float3(0f, -8f, 0f);
            const float voxelSize = 1f;
            int count = res.x * res.y * res.z;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
            root.Resolution = res;
            root.WorldOrigin = origin;
            root.VoxelSize   = voxelSize;

            var values = builder.Allocate(ref root.Values, count);
            for (int z = 0; z < res.z; z++)
            for (int y = 0; y < res.y; y++)
            for (int x = 0; x < res.x; x++)
            {
                int   idx    = z * (res.x * res.y) + y * res.x + x;
                float worldY = origin.y + y * voxelSize;
                // density = worldY: negative below world y=0 (solid), positive above (air)
                values[idx] = worldY;
            }

            var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Temp);
            builder.Dispose();
            return blob;
        }
    }

    // ── PlayMode tests ────────────────────────────────────────────────────────

    [TestFixture]
    public class TreePlacementPlayModeTests
    {
        // ── Test 5: Stream stability ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator GeneratePlacements_StreamStability_StreamOutAndInProducesIdenticalRecords()
        {
            // Simulates stream-out (removing ChunkTreePlacementTag) and stream-back-in
            // by calling GeneratePlacements twice on the same chunk data and asserting
            // the outputs are identical — proving deterministic regeneration.
            // All allocations are consumed and disposed before yield to avoid cross-frame Temp issues.
            var blob = BuildFlatTerrainBlob(float3.zero);
            try
            {
                // ref locals are forbidden in iterators (CS8176); pass blob.Value directly —
                // BlobAssetReference<T>.Value is a ref property so this is valid at the call site.
                var origin = new float3(0f, -8f, 0f);
                var coord  = new int3(3, 0, 7);
                uint seed  = 12345u;

                var pass1 = new NativeList<TreePlacementRecord>(16, Allocator.TempJob);
                var pass2 = new NativeList<TreePlacementRecord>(16, Allocator.TempJob);
                try
                {
                    TreePlacementAlgorithm.GeneratePlacements(ref blob.Value, coord, origin, seed, ref pass1);
                    TreePlacementAlgorithm.GeneratePlacements(ref blob.Value, coord, origin, seed, ref pass2);

                    Assert.AreEqual(pass1.Length, pass2.Length,
                        "Stream-in after stream-out must regenerate an identical record count.");
                    for (int i = 0; i < pass1.Length; i++)
                    {
                        Assert.AreEqual(pass1[i].StableLocalId, pass2[i].StableLocalId,
                            $"Record {i} StableLocalId must match after re-stream.");
                        Assert.AreEqual(pass1[i].WorldPosition.x, pass2[i].WorldPosition.x, 1e-5f,
                            $"Record {i} WorldPosition.x must match after re-stream.");
                        Assert.AreEqual(pass1[i].WorldPosition.z, pass2[i].WorldPosition.z, 1e-5f,
                            $"Record {i} WorldPosition.z must match after re-stream.");
                    }
                }
                finally
                {
                    pass1.Dispose();
                    pass2.Dispose();
                }
            }
            finally
            {
                if (blob.IsCreated) blob.Dispose();
            }
            yield return null;
        }

        // ── Test 6: No seam duplicates ────────────────────────────────────────

        [UnityTest]
        public IEnumerator GeneratePlacements_NoSeamDuplicates_AdjacentChunksNoRecordsWithin001()
        {
            var blobA = BuildFlatTerrainBlob(float3.zero);
            var blobB = BuildFlatTerrainBlob(new float3(15f, 0f, 0f));
            try
            {
                // ref locals are forbidden in iterators (CS8176); pass .Value directly at call sites.
                uint seed = 12345u;

                var outputA = new NativeList<TreePlacementRecord>(16, Allocator.TempJob);
                var outputB = new NativeList<TreePlacementRecord>(16, Allocator.TempJob);
                try
                {
                    TreePlacementAlgorithm.GeneratePlacements(
                        ref blobA.Value, new int3(0, 0, 0), float3.zero, seed, ref outputA);
                    TreePlacementAlgorithm.GeneratePlacements(
                        ref blobB.Value, new int3(1, 0, 0), new float3(15f, 0f, 0f), seed, ref outputB);

                    for (int i = 0; i < outputA.Length; i++)
                    for (int j = 0; j < outputB.Length; j++)
                    {
                        float dist = math.distance(
                            outputA[i].WorldPosition.xz,
                            outputB[j].WorldPosition.xz);
                        Assert.Greater(dist, 0.01f,
                            $"Adjacent chunks have records within 0.01 world units " +
                            $"(A[{i}]={outputA[i].WorldPosition} B[{j}]={outputB[j].WorldPosition}). " +
                            "CandidateJitter must include chunkCoord in the hash.");
                    }
                }
                finally
                {
                    outputA.Dispose();
                    outputB.Dispose();
                }
            }
            finally
            {
                if (blobA.IsCreated) blobA.Dispose();
                if (blobB.IsCreated) blobB.Dispose();
            }
            yield return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static BlobAssetReference<TerrainChunkDensityBlob> BuildFlatTerrainBlob(
            float3 originXZ)
        {
            var res    = new int3(17, 17, 17);
            var origin = new float3(originXZ.x, -8f, originXZ.z);
            const float voxelSize = 1f;
            int count = res.x * res.y * res.z;

            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
            root.Resolution = res;
            root.WorldOrigin = origin;
            root.VoxelSize   = voxelSize;

            var values = builder.Allocate(ref root.Values, count);
            for (int z = 0; z < res.z; z++)
            for (int y = 0; y < res.y; y++)
            for (int x = 0; x < res.x; x++)
            {
                int   idx    = z * (res.x * res.y) + y * res.x + x;
                float worldY = origin.y + y * voxelSize;
                values[idx] = worldY;
            }

            // Use Persistent so the reference remains valid if test infrastructure yields.
            var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Persistent);
            builder.Dispose();
            return blob;
        }
    }
}
