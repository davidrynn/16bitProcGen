using NUnit.Framework;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// PlayMode test that validates terrain chunk border continuity.
    /// Spawns a deterministic 2x2 chunk grid and asserts no density seam mismatches.
    /// </summary>
    [TestFixture]
    public class TerrainChunkBorderContinuityTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("TerrainSeamDebugTestWorld");
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

        [UnityTest]
        public IEnumerator BorderContinuity_2x2Grid_NoSeamMismatches()
        {
            // Setup debug config
            var debugEntity = entityManager.CreateEntity();
            var debugConfig = new DOTS.Terrain.Debug.TerrainDebugConfig
            {
                Enabled = true,
                FreezeStreaming = true,
                FixedCenterChunk = int2.zero,
                StreamingRadiusInChunks = 1, // Will spawn 2x2 grid around (0,0)
                SeamEpsilon = 0.001f,
                EnableSeamLogging = true
            };
            entityManager.AddComponentData(debugEntity, debugConfig);

            // Setup terrain field settings
            var settingsEntity = entityManager.CreateEntity();
            var settings = new SDFTerrainFieldSettings
            {
                BaseHeight = 0f,
                Amplitude = 10f,
                Frequency = 0.1f,
                NoiseValue = 1f
            };
            entityManager.AddComponentData(settingsEntity, settings);

            // Setup feature config singleton
            var configEntity = entityManager.CreateEntity();
            var featureConfig = new DOTS.Terrain.Streaming.ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = 1
            };
            entityManager.AddComponentData(configEntity, featureConfig);

            // Create initial chunk to bootstrap grid settings
            var resolution = new int3(17, 17, 17);
            var voxelSize = 1f;
            var chunkStride = (resolution.x - 1) * voxelSize;

            // Calculate originY using the same formula as the streaming system
            // Center chunk volume vertically around BaseHeight so the SdGround isosurface stays in-range.
            var chunkVerticalSpan = math.max(0, resolution.y - 1) * voxelSize;
            var originY = settings.BaseHeight - (chunkVerticalSpan * 0.5f);

            var initialEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(initialEntity, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
            entityManager.AddComponentData(initialEntity, TerrainChunkGridInfo.Create(resolution, voxelSize));
            entityManager.AddComponentData(initialEntity, new TerrainChunkBounds { WorldOrigin = new float3(0, originY, 0) });
            entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(initialEntity);

            // Create and add systems
            var streamingSystem = testWorld.CreateSystem<DOTS.Terrain.Streaming.TerrainChunkStreamingSystem>();
            var densitySystem = testWorld.CreateSystem<TerrainChunkDensitySamplingSystem>();
            var validatorSystem = testWorld.CreateSystem<DOTS.Terrain.Debug.TerrainSeamValidatorSystem>();

            // Run streaming to spawn 2x2 grid
            streamingSystem.Update(testWorld.Unmanaged);
            yield return null;

            // Run density sampling
            for (int i = 0; i < 10; i++) // Multiple frames to ensure all chunks processed
            {
                densitySystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            // Run validator
            validatorSystem.Update(testWorld.Unmanaged);
            yield return null;

            // Verify: Check that at least 4 chunks were spawned (2x2 grid)
            var chunkQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            var chunkCount = chunkQuery.CalculateEntityCount();
            Assert.GreaterOrEqual(chunkCount, 4, "Should have spawned at least 4 chunks in 2x2 grid");

            // Verify: All chunks have density data
            var densityQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkDensity>());
            var densityCount = densityQuery.CalculateEntityCount();
            Assert.AreEqual(chunkCount, densityCount, "All chunks should have density data");

            // Verify: No seam mismatches detected
            // The validator system logs warnings for mismatches, so we'll do manual validation here
            var seamMismatches = ValidateSeamsManually();
            Assert.AreEqual(0, seamMismatches, $"Expected 0 seam mismatches but found {seamMismatches}. Check Console for SEAM_MISMATCH warnings.");

            chunkQuery.Dispose();
            densityQuery.Dispose();
        }

        private int ValidateSeamsManually()
        {
            var debugConfig = new DOTS.Terrain.Debug.TerrainDebugConfig
            {
                SeamEpsilon = 0.001f,
                EnableSeamLogging = false // We'll count manually
            };

            var chunkQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkDensity>());

            using var chunkEntities = chunkQuery.ToEntityArray(Allocator.Temp);
            using var chunkComponents = chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);

            var map = new NativeParallelHashMap<int2, Entity>(chunkEntities.Length, Allocator.Temp);
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var coord = new int2(chunkComponents[i].ChunkCoord.x, chunkComponents[i].ChunkCoord.z);
                map.TryAdd(coord, chunkEntities[i]);
            }

            var totalMismatches = 0;

            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var entity = chunkEntities[i];
                var chunk = chunkComponents[i];
                var coord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);

                var density = entityManager.GetComponentData<TerrainChunkDensity>(entity);
                if (!density.IsCreated)
                {
                    continue;
                }

                // Check east neighbor
                var eastCoord = new int2(coord.x + 1, coord.y);
                if (map.TryGetValue(eastCoord, out var eastEntity))
                {
                    totalMismatches += CountBorderMismatches(entity, eastEntity, BorderDirection.East, debugConfig.SeamEpsilon);
                }

                // Check north neighbor
                var northCoord = new int2(coord.x, coord.y + 1);
                if (map.TryGetValue(northCoord, out var northEntity))
                {
                    totalMismatches += CountBorderMismatches(entity, northEntity, BorderDirection.North, debugConfig.SeamEpsilon);
                }
            }

            map.Dispose();
            chunkQuery.Dispose();
            return totalMismatches;
        }

        private int CountBorderMismatches(Entity entityA, Entity entityB, BorderDirection direction, float epsilon)
        {
            var densityA = entityManager.GetComponentData<TerrainChunkDensity>(entityA);
            var densityB = entityManager.GetComponentData<TerrainChunkDensity>(entityB);

            if (!densityA.IsCreated || !densityB.IsCreated)
            {
                return 0;
            }

            ref var blobA = ref densityA.Data.Value;
            ref var blobB = ref densityB.Data.Value;

            var res = blobA.Resolution;
            var count = 0;

            // The density grid is expanded by +1 for Surface Nets stitching.
            // Chunk stride = (originalResolution - 1) * voxelSize, so:
            // - Chunk A's sample at index (res.x - 2) is at the shared border world position
            // - Chunk B's sample at index 0 is at the same shared border world position
            // We also verify the overlap sample (res.x - 1 vs 1) for Surface Nets compatibility.

            if (direction == BorderDirection.East)
            {
                // Compare shared border: A's second-to-last column with B's first column
                var xA = res.x - 2;
                var xB = 0;

                for (int y = 0; y < res.y; y++)
                {
                    for (int z = 0; z < res.z; z++)
                    {
                        var valA = blobA.GetDensity(xA, y, z);
                        var valB = blobB.GetDensity(xB, y, z);
                        if (math.abs(valA - valB) > epsilon)
                        {
                            count++;
                        }
                    }
                }

                // Also compare the overlap sample for Surface Nets stitching
                xA = res.x - 1;
                xB = 1;

                for (int y = 0; y < res.y; y++)
                {
                    for (int z = 0; z < res.z; z++)
                    {
                        var valA = blobA.GetDensity(xA, y, z);
                        var valB = blobB.GetDensity(xB, y, z);
                        if (math.abs(valA - valB) > epsilon)
                        {
                            count++;
                        }
                    }
                }
            }
            else
            {
                // Compare shared border: A's second-to-last row with B's first row
                var zA = res.z - 2;
                var zB = 0;

                for (int y = 0; y < res.y; y++)
                {
                    for (int x = 0; x < res.x; x++)
                    {
                        var valA = blobA.GetDensity(x, y, zA);
                        var valB = blobB.GetDensity(x, y, zB);
                        if (math.abs(valA - valB) > epsilon)
                        {
                            count++;
                        }
                    }
                }

                // Also compare the overlap sample for Surface Nets stitching
                zA = res.z - 1;
                zB = 1;

                for (int y = 0; y < res.y; y++)
                {
                    for (int x = 0; x < res.x; x++)
                    {
                        var valA = blobA.GetDensity(x, y, zA);
                        var valB = blobB.GetDensity(x, y, zB);
                        if (math.abs(valA - valB) > epsilon)
                        {
                            count++;
                        }
                    }
                }
            }

            return count;
        }

        private enum BorderDirection
        {
            East,
            North
        }
    }
}
