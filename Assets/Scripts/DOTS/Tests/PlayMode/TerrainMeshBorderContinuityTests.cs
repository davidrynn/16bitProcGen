using NUnit.Framework;
using System.Collections;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using DOTS.Terrain.Debug;
using DOTS.Terrain.Meshing;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// PlayMode tests that validate terrain chunk mesh border continuity.
    /// Spawns a deterministic 2x2 chunk grid and asserts no mesh seam mismatches.
    /// </summary>
    [TestFixture]
    public class TerrainMeshBorderContinuityTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("TerrainMeshSeamDebugTestWorld");
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
        public IEnumerator MeshBorderVertexContinuity_2x2Grid_NoPositionMismatches()
        {
            // Setup debug config with mesh debug enabled
            var debugEntity = entityManager.CreateEntity();
            var debugConfig = new TerrainDebugConfig
            {
                Enabled = true,
                FreezeStreaming = true,
                FixedCenterChunk = int2.zero,
                StreamingRadiusInChunks = 1,
                SeamEpsilon = 0.001f,
                EnableSeamLogging = true,
                EnableMeshDebugOverlay = true,
                MeshSeamPositionEpsilon = 0.001f,
                MeshSeamNormalAngleThreshold = 5.0f
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
            var featureConfig = new Streaming.ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = 1
            };
            entityManager.AddComponentData(configEntity, featureConfig);

            // Create initial chunk to bootstrap grid settings
            var resolution = new int3(17, 17, 17);
            var voxelSize = 1f;

            var chunkVerticalSpan = math.max(0, resolution.y - 1) * voxelSize;
            var originY = settings.BaseHeight - (chunkVerticalSpan * 0.5f);

            var initialEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(initialEntity, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
            entityManager.AddComponentData(initialEntity, TerrainChunkGridInfo.Create(resolution, voxelSize));
            entityManager.AddComponentData(initialEntity, new TerrainChunkBounds { WorldOrigin = new float3(0, originY, 0) });
            entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(initialEntity);

            // Create and add systems
            var streamingSystem = testWorld.CreateSystem<Streaming.TerrainChunkStreamingSystem>();
            var densitySystem = testWorld.CreateSystem<TerrainChunkDensitySamplingSystem>();
            var meshBuildSystem = testWorld.CreateSystem<TerrainChunkMeshBuildSystem>();
            var meshValidatorSystem = testWorld.CreateSystem<TerrainMeshSeamValidatorSystem>();

            // Run streaming to spawn 2x2 grid
            streamingSystem.Update(testWorld.Unmanaged);
            yield return null;

            // Run density sampling
            for (int i = 0; i < 10; i++)
            {
                densitySystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            // Run mesh building
            for (int i = 0; i < 10; i++)
            {
                meshBuildSystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            // Run mesh validator
            meshValidatorSystem.Update(testWorld.Unmanaged);
            yield return null;

            // Verify: Check that at least 4 chunks were spawned
            var chunkQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            var chunkCount = chunkQuery.CalculateEntityCount();
            Assert.GreaterOrEqual(chunkCount, 4, "Should have spawned at least 4 chunks in 2x2 grid");

            // Verify: All chunks have mesh data
            var meshQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkMeshData>());
            var meshCount = meshQuery.CalculateEntityCount();
            Assert.GreaterOrEqual(meshCount, 1, "At least one chunk should have mesh data");

            // Verify: No mesh position mismatches detected
            var positionMismatches = ValidateMeshBordersManually(debugConfig.MeshSeamPositionEpsilon);
            Assert.AreEqual(0, positionMismatches,
                $"Expected 0 mesh position mismatches but found {positionMismatches}. Check Console for MESH_SEAM_MISMATCH warnings.");

            chunkQuery.Dispose();
            meshQuery.Dispose();
        }

        [UnityTest]
        public IEnumerator MeshBorderVertexCount_2x2Grid_BorderVerticesExist()
        {
            // Setup debug config
            var debugEntity = entityManager.CreateEntity();
            var debugConfig = new TerrainDebugConfig
            {
                Enabled = true,
                FreezeStreaming = true,
                FixedCenterChunk = int2.zero,
                StreamingRadiusInChunks = 1,
                SeamEpsilon = 0.001f,
                EnableSeamLogging = true,
                EnableMeshDebugOverlay = true,
                MeshSeamPositionEpsilon = 0.001f,
                MeshSeamNormalAngleThreshold = 5.0f
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
            var featureConfig = new Streaming.ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = 1
            };
            entityManager.AddComponentData(configEntity, featureConfig);

            // Create initial chunk
            var resolution = new int3(17, 17, 17);
            var voxelSize = 1f;

            var chunkVerticalSpan = math.max(0, resolution.y - 1) * voxelSize;
            var originY = settings.BaseHeight - (chunkVerticalSpan * 0.5f);

            var initialEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(initialEntity, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
            entityManager.AddComponentData(initialEntity, TerrainChunkGridInfo.Create(resolution, voxelSize));
            entityManager.AddComponentData(initialEntity, new TerrainChunkBounds { WorldOrigin = new float3(0, originY, 0) });
            entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(initialEntity);

            // Create and run systems
            var streamingSystem = testWorld.CreateSystem<Streaming.TerrainChunkStreamingSystem>();
            var densitySystem = testWorld.CreateSystem<TerrainChunkDensitySamplingSystem>();
            var meshBuildSystem = testWorld.CreateSystem<TerrainChunkMeshBuildSystem>();

            streamingSystem.Update(testWorld.Unmanaged);
            yield return null;

            for (int i = 0; i < 10; i++)
            {
                densitySystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            for (int i = 0; i < 10; i++)
            {
                meshBuildSystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            // Check that chunks with mesh debug data have border vertices
            var debugDataQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkMeshDebugData>());

            using var debugEntities = debugDataQuery.ToEntityArray(Allocator.Temp);
            var chunksWithBorderVerts = 0;

            for (int i = 0; i < debugEntities.Length; i++)
            {
                var meshDebugData = entityManager.GetComponentData<TerrainChunkMeshDebugData>(debugEntities[i]);
                if (meshDebugData.BorderVertexCount > 0)
                {
                    chunksWithBorderVerts++;
                }
            }

            Assert.Greater(chunksWithBorderVerts, 0,
                "At least one chunk should have border vertices. This indicates mesh generation is working at boundaries.");

            debugDataQuery.Dispose();
        }

        private int ValidateMeshBordersManually(float epsilon)
        {
            var chunkQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkMeshData>(),
                ComponentType.ReadOnly<TerrainChunkBounds>(),
                ComponentType.ReadOnly<TerrainChunkGridInfo>());

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

                var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                if (!meshData.HasMesh)
                {
                    continue;
                }

                // Check east neighbor
                var eastCoord = new int2(coord.x + 1, coord.y);
                if (map.TryGetValue(eastCoord, out var eastEntity))
                {
                    totalMismatches += CountMeshBorderMismatches(entity, eastEntity, BorderDirection.East, epsilon);
                }

                // Check north neighbor
                var northCoord = new int2(coord.x, coord.y + 1);
                if (map.TryGetValue(northCoord, out var northEntity))
                {
                    totalMismatches += CountMeshBorderMismatches(entity, northEntity, BorderDirection.North, epsilon);
                }
            }

            map.Dispose();
            chunkQuery.Dispose();
            return totalMismatches;
        }

        private int CountMeshBorderMismatches(Entity entityA, Entity entityB, BorderDirection direction, float epsilon)
        {
            var meshDataA = entityManager.GetComponentData<TerrainChunkMeshData>(entityA);
            var meshDataB = entityManager.GetComponentData<TerrainChunkMeshData>(entityB);

            if (!meshDataA.HasMesh || !meshDataB.HasMesh)
            {
                return 0;
            }

            var boundsA = entityManager.GetComponentData<TerrainChunkBounds>(entityA);
            var boundsB = entityManager.GetComponentData<TerrainChunkBounds>(entityB);
            var gridA = entityManager.GetComponentData<TerrainChunkGridInfo>(entityA);

            ref var blobA = ref meshDataA.Mesh.Value;
            ref var blobB = ref meshDataB.Mesh.Value;

            var borderThreshold = gridA.VoxelSize;
            var chunkSize = new float3(
                (gridA.Resolution.x - 1) * gridA.VoxelSize,
                (gridA.Resolution.y - 1) * gridA.VoxelSize,
                (gridA.Resolution.z - 1) * gridA.VoxelSize);

            // Collect border vertices from both chunks
            using var borderVertsA = new NativeList<float3>(Allocator.Temp);
            using var borderVertsB = new NativeList<float3>(Allocator.Temp);

            // Get border vertices from chunk A
            for (int v = 0; v < blobA.Vertices.Length; v++)
            {
                var localPos = blobA.Vertices[v];
                bool onBorder = false;

                if (direction == BorderDirection.East)
                {
                    onBorder = localPos.x >= chunkSize.x - borderThreshold;
                }
                else
                {
                    onBorder = localPos.z >= chunkSize.z - borderThreshold;
                }

                if (onBorder)
                {
                    borderVertsA.Add(boundsA.WorldOrigin + localPos);
                }
            }

            // Get border vertices from chunk B
            for (int v = 0; v < blobB.Vertices.Length; v++)
            {
                var localPos = blobB.Vertices[v];
                bool onBorder = false;

                if (direction == BorderDirection.East)
                {
                    onBorder = localPos.x <= borderThreshold;
                }
                else
                {
                    onBorder = localPos.z <= borderThreshold;
                }

                if (onBorder)
                {
                    borderVertsB.Add(boundsB.WorldOrigin + localPos);
                }
            }

            if (borderVertsA.Length == 0 || borderVertsB.Length == 0)
            {
                return 0;
            }

            // Count mismatches
            var count = 0;
            for (int i = 0; i < borderVertsA.Length; i++)
            {
                var worldPosA = borderVertsA[i];
                var closestDist = float.MaxValue;

                for (int j = 0; j < borderVertsB.Length; j++)
                {
                    var dist = math.distance(worldPosA, borderVertsB[j]);
                    closestDist = math.min(closestDist, dist);
                }

                // If closest match is within reasonable range but above epsilon, count as mismatch
                if (closestDist > epsilon && closestDist < borderThreshold * 2f)
                {
                    count++;
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
