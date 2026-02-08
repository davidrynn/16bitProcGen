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
    /// Diagnostic tests for terrain banding artifacts.
    /// These tests analyze mesh properties to identify the root cause of visible banding.
    /// See TERRAIN_BANDING_DIAGNOSTIC_SPEC.md for hypothesis details.
    /// </summary>
    [TestFixture]
    public class TerrainBandingDiagnosticTests
    {
        private World testWorld;
        private EntityManager entityManager;

        // Test configuration - document current settings for result interpretation
        private const int TestResolution = 17;
        private const float TestVoxelSize = 1f;
        private const float TestBaseHeight = 0f;
        private const float TestAmplitude = 10f;
        private const float TestFrequency = 0.1f;

        // Histogram analysis parameters
        private const int HistogramBuckets = 100;
        private const float ClusteringThreshold = 1.3f; // Peak must be > 1.3x average to count as clustering (lowered for better sensitivity)

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("TerrainBandingDiagnosticTestWorld");
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

        /// <summary>
        /// H2 Test: Determine if vertices cluster at specific Y values.
        /// If clustering exists, histogram will show peaks at regular intervals.
        /// A smooth distribution indicates no Y-position quantization.
        /// </summary>
        [UnityTest]
        public IEnumerator VertexYPositions_AreDistributedSmoothly()
        {
            // Setup debug config
            var debugEntity = entityManager.CreateEntity();
            var debugConfig = TerrainDebugConfig.Default;
            debugConfig.Enabled = true;
            debugConfig.FreezeStreaming = true;
            debugConfig.FixedCenterChunk = int2.zero;
            debugConfig.StreamingRadiusInChunks = 1; // Minimum radius for streaming to work
            entityManager.AddComponentData(debugEntity, debugConfig);

            // Setup terrain field settings
            var settingsEntity = entityManager.CreateEntity();
            var settings = new SDFTerrainFieldSettings
            {
                BaseHeight = TestBaseHeight,
                Amplitude = TestAmplitude,
                Frequency = TestFrequency,
                NoiseValue = 0f
            };
            entityManager.AddComponentData(settingsEntity, settings);

            // Setup feature config singleton
            var configEntity = entityManager.CreateEntity();
            var featureConfig = new Streaming.ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = 1
            };
            entityManager.AddComponentData(configEntity, featureConfig);

            // Create initial chunk to bootstrap grid settings (matching working tests pattern)
            var resolution = new int3(TestResolution, TestResolution, TestResolution);
            var chunkVerticalSpan = math.max(0, resolution.y - 1) * TestVoxelSize;
            var originY = settings.BaseHeight - (chunkVerticalSpan * 0.5f);

            var chunkEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(chunkEntity, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
            entityManager.AddComponentData(chunkEntity, TerrainChunkGridInfo.Create(resolution, TestVoxelSize));
            entityManager.AddComponentData(chunkEntity, new TerrainChunkBounds { WorldOrigin = new float3(0, originY, 0) });
            entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunkEntity);

            // Create systems (matching working tests pattern)
            var streamingSystem = testWorld.CreateSystem<Streaming.TerrainChunkStreamingSystem>();
            var densitySystem = testWorld.CreateSystem<TerrainChunkDensitySamplingSystem>();
            var meshBuildSystem = testWorld.CreateSystem<TerrainChunkMeshBuildSystem>();

            // Run streaming to initialize chunk grid
            streamingSystem.Update(testWorld.Unmanaged);
            yield return null;

            // Run density sampling system (10 iterations like working tests)
            for (int i = 0; i < 10; i++)
            {
                densitySystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            // Verify density was generated
            Assert.IsTrue(entityManager.HasComponent<TerrainChunkDensity>(chunkEntity),
                "Chunk should have density data after density sampling");
            Assert.IsTrue(entityManager.HasComponent<TerrainChunkNeedsMeshBuild>(chunkEntity),
                "Chunk should be flagged for mesh build after density sampling");

            // Run mesh build system (10 iterations like working tests)
            for (int i = 0; i < 10; i++)
            {
                meshBuildSystem.Update(testWorld.Unmanaged);
                yield return null;
            }

            // Verify mesh was generated
            Assert.IsTrue(entityManager.HasComponent<TerrainChunkMeshData>(chunkEntity),
                "Chunk should have mesh data after mesh build");

            var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(chunkEntity);
            Assert.IsTrue(meshData.HasMesh, "Mesh data should contain a valid mesh");

            Assert.Greater(meshData.Mesh.Value.Vertices.Length, 0, "Mesh should have vertices");

            // Extract vertices into NativeArray for analysis
            var vertices = new NativeArray<float3>(meshData.Mesh.Value.Vertices.Length, Allocator.Temp);
            var indices = new NativeArray<int>(meshData.Mesh.Value.Indices.Length, Allocator.Temp);
            try
            {
                for (int i = 0; i < meshData.Mesh.Value.Vertices.Length; i++)
                {
                    vertices[i] = meshData.Mesh.Value.Vertices[i];
                }

                for (int i = 0; i < meshData.Mesh.Value.Indices.Length; i++)
                {
                    indices[i] = meshData.Mesh.Value.Indices[i];
                }

                // Analyze mesh using TerrainMeshAnalyzer
                var analysis = TerrainMeshAnalyzer.Analyze(vertices, indices, HistogramBuckets);

                // Log diagnostic information
                UnityEngine.Debug.Log($"[BANDING_DIAG] Mesh Analysis Results:");
                UnityEngine.Debug.Log($"  Vertex Count: {analysis.VertexCount}");
                UnityEngine.Debug.Log($"  Triangle Count: {analysis.TriangleCount}");
                UnityEngine.Debug.Log($"  Y Range: [{analysis.MinY:F3}, {analysis.MaxY:F3}]");
                UnityEngine.Debug.Log($"  Avg Triangle Area: {analysis.AvgTriangleArea:F6}");
                UnityEngine.Debug.Log($"  Min Triangle Area: {analysis.MinTriangleArea:F6}");
                UnityEngine.Debug.Log($"  Max Aspect Ratio: {analysis.MaxAspectRatio:F3}");
                UnityEngine.Debug.Log($"  Degenerate Triangles: {analysis.DegenerateTriangleCount}");

                // Find clustering peaks
                int peakCount = TerrainMeshAnalyzer.FindClusteringPeaks(analysis.YHistogram, ClusteringThreshold);
                UnityEngine.Debug.Log($"  Clustering Peaks (>{ClusteringThreshold}x avg): {peakCount}");

                // Analyze Y spacing for quantization detection
                var spacingStats = TerrainMeshAnalyzer.AnalyzeVertexYSpacing(vertices);
                UnityEngine.Debug.Log($"  Y Spacing - Mean: {spacingStats.MeanSpacing:F4}, StdDev: {spacingStats.StandardDeviation:F4}");
                UnityEngine.Debug.Log($"  Y Spacing - Min: {spacingStats.MinSpacing:F4}, Max: {spacingStats.MaxSpacing:F4}");

                // Log histogram distribution for manual inspection
                LogHistogramDistribution(analysis.YHistogram, analysis.MinY, analysis.MaxY);

                // DIAGNOSTIC ASSERTION: Check if clustering exists
                // If this fails, H2 is confirmed (vertices cluster at specific Y values)
                // If this passes, H2 is ruled out (vertices are smoothly distributed)
                bool hasSignificantClustering = peakCount > (HistogramBuckets * 0.1f); // More than 10% of buckets are peaks

                if (hasSignificantClustering)
                {
                    UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H2 CONFIRMED: Significant vertex Y clustering detected. " +
                        $"{peakCount} peaks found (>{ClusteringThreshold}x average).");

                    // Get peak Y positions for debugging
                    var peakYs = TerrainMeshAnalyzer.GetPeakYPositions(
                        analysis.YHistogram, analysis.MinY, analysis.MaxY, ClusteringThreshold);
                    UnityEngine.Debug.Log($"[BANDING_DIAG] Peak Y positions: [{string.Join(", ", System.Array.ConvertAll(peakYs, y => y.ToString("F3")))}]");

                    // Calculate interval between peaks
                    if (peakYs.Length >= 2)
                    {
                        System.Array.Sort(peakYs);
                        float avgInterval = (peakYs[peakYs.Length - 1] - peakYs[0]) / (peakYs.Length - 1);
                        UnityEngine.Debug.Log($"[BANDING_DIAG] Average peak interval: {avgInterval:F3} (VoxelSize: {TestVoxelSize})");

                        if (math.abs(avgInterval - TestVoxelSize) < 0.1f)
                        {
                            UnityEngine.Debug.LogWarning($"[BANDING_DIAG] Peak interval matches VoxelSize - suggests H1 may also be true (voxel layer stepping)");
                        }
                    }
                }
                else
                {
                    UnityEngine.Debug.Log($"[BANDING_DIAG] H2 RULED OUT: Vertex Y positions are smoothly distributed. " +
                        $"Only {peakCount} minor peaks found.");
                }

                // The test reports findings but does not fail - this is diagnostic only
                // Uncomment the following line to make the test fail when clustering is detected:
                // Assert.IsFalse(hasSignificantClustering, $"Vertex Y clustering detected: {peakCount} peaks");

                // Instead, we assert basic mesh validity
                Assert.Greater(analysis.VertexCount, 0, "Mesh should have vertices");
                Assert.Greater(analysis.TriangleCount, 0, "Mesh should have triangles");
            }
            finally
            {
                vertices.Dispose();
                indices.Dispose();
            }
        }

        /// <summary>
        /// Logs histogram distribution in a readable format for manual inspection.
        /// Marks peaks with asterisks for easy identification.
        /// </summary>
        private void LogHistogramDistribution(float[] histogram, float minY, float maxY)
        {
            if (histogram == null || histogram.Length == 0) return;

            float sum = 0f;
            for (int i = 0; i < histogram.Length; i++)
            {
                sum += histogram[i];
            }
            float average = sum / histogram.Length;
            float peakThreshold = average * ClusteringThreshold;

            float range = maxY - minY;
            int bucketsToPrint = math.min(20, histogram.Length); // Print max 20 buckets for readability
            int bucketStep = histogram.Length / bucketsToPrint;

            UnityEngine.Debug.Log("[BANDING_DIAG] Y-Position Histogram (sampled):");
            for (int i = 0; i < histogram.Length; i += bucketStep)
            {
                float bucketY = minY + (i + 0.5f) / histogram.Length * range;
                float value = histogram[i];
                string marker = value > peakThreshold ? " ***PEAK***" : "";
                int barLength = (int)(value * 100); // Scale for visibility
                string bar = new string('#', math.min(barLength, 50));
                UnityEngine.Debug.Log($"  Y={bucketY,7:F2}: {bar}{marker}");
            }
        }

        /// <summary>
        /// H1 Test: Determine if band interval correlates with voxel size.
        /// If H1 is true, changing VoxelSize should change band interval proportionally.
        /// </summary>
        [UnityTest]
        public IEnumerator BandInterval_CorrelatesWithVoxelSize()
        {
            // Test with two different voxel sizes and compare vertex clustering patterns
            VoxelSizeTestResult results1 = default;
            VoxelSizeTestResult results2 = default;

            yield return RunTestWithVoxelSize(1.0f, (result) => results1 = result);
            yield return RunTestWithVoxelSize(0.5f, (result) => results2 = result);

            // Log detailed mesh statistics
            UnityEngine.Debug.Log($"[BANDING_DIAG] H1 Test - Detailed Mesh Statistics:");
            UnityEngine.Debug.Log($"  VoxelSize=1.0: {results1.vertexCount} vertices, Y-range=[{results1.minY:F3}, {results1.maxY:F3}]");
            UnityEngine.Debug.Log($"  VoxelSize=0.5: {results2.vertexCount} vertices, Y-range=[{results2.minY:F3}, {results2.maxY:F3}]");

            // Check if meshes were actually generated
            if (results1.vertexCount == 0 || results2.vertexCount == 0)
            {
                UnityEngine.Debug.LogError($"[BANDING_DIAG] H1 FAILED: Mesh generation failed (verts: {results1.vertexCount}, {results2.vertexCount})");
                Assert.Fail("Mesh generation failed - cannot perform correlation analysis");
            }

            // Analyze peak intervals
            var peaks1 = TerrainMeshAnalyzer.GetPeakYPositions(
                results1.histogram, results1.minY, results1.maxY, ClusteringThreshold);
            var peaks2 = TerrainMeshAnalyzer.GetPeakYPositions(
                results2.histogram, results2.minY, results2.maxY, ClusteringThreshold);

            float avgInterval1 = CalculateAveragePeakInterval(peaks1);
            float avgInterval2 = CalculateAveragePeakInterval(peaks2);

            // Log all peaks for manual inspection
            if (peaks1.Length > 0)
            {
                UnityEngine.Debug.Log($"  VoxelSize=1.0 peak Y positions: [{string.Join(", ", System.Array.ConvertAll(peaks1, y => y.ToString("F3")))}]");
            }
            if (peaks2.Length > 0)
            {
                UnityEngine.Debug.Log($"  VoxelSize=0.5 peak Y positions: [{string.Join(", ", System.Array.ConvertAll(peaks2, y => y.ToString("F3")))}]");
            }

            UnityEngine.Debug.Log($"[BANDING_DIAG] H1 Test Results:");
            UnityEngine.Debug.Log($"  VoxelSize=1.0: {peaks1.Length} peaks, avg interval={avgInterval1:F3}");
            UnityEngine.Debug.Log($"  VoxelSize=0.5: {peaks2.Length} peaks, avg interval={avgInterval2:F3}");

            if (peaks1.Length >= 2 && peaks2.Length >= 2)
            {
                float ratio = avgInterval1 / avgInterval2;
                UnityEngine.Debug.Log($"  Interval ratio: {ratio:F2} (expected ~2.0 if H1 true)");

                if (math.abs(ratio - 2.0f) < 0.3f)
                {
                    UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H1 CONFIRMED: Band interval correlates with VoxelSize (ratio={ratio:F2})");
                }
                else
                {
                    UnityEngine.Debug.Log($"[BANDING_DIAG] H1 RULED OUT: Band interval does not scale with VoxelSize (ratio={ratio:F2})");
                }
            }
            else
            {
                UnityEngine.Debug.Log($"[BANDING_DIAG] H1 INCONCLUSIVE: Insufficient peaks detected for correlation analysis");
            }

            Assert.Pass("Diagnostic test completed");
        }

        /// <summary>
        /// H3 Test: Determine if triangles at band locations are degenerate.
        /// Analyzes triangle quality uniformly across mesh.
        /// </summary>
        [UnityTest]
        public IEnumerator TriangleQuality_UniformAcrossMesh()
        {
            // Setup (same as VertexYPositions test)
            var debugEntity = entityManager.CreateEntity();
            var debugConfig = TerrainDebugConfig.Default;
            debugConfig.Enabled = true;
            debugConfig.FreezeStreaming = true;
            debugConfig.FixedCenterChunk = int2.zero;
            debugConfig.StreamingRadiusInChunks = 1;
            entityManager.AddComponentData(debugEntity, debugConfig);

            var settingsEntity = entityManager.CreateEntity();
            var settings = new SDFTerrainFieldSettings
            {
                BaseHeight = TestBaseHeight,
                Amplitude = TestAmplitude,
                Frequency = TestFrequency,
                NoiseValue = 0f
            };
            entityManager.AddComponentData(settingsEntity, settings);

            var configEntity = entityManager.CreateEntity();
            var featureConfig = new Streaming.ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = 1
            };
            entityManager.AddComponentData(configEntity, featureConfig);

            var resolution = new int3(TestResolution, TestResolution, TestResolution);
            var chunkVerticalSpan = math.max(0, resolution.y - 1) * TestVoxelSize;
            var originY = settings.BaseHeight - (chunkVerticalSpan * 0.5f);

            var chunkEntity = entityManager.CreateEntity();
            entityManager.AddComponentData(chunkEntity, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
            entityManager.AddComponentData(chunkEntity, TerrainChunkGridInfo.Create(resolution, TestVoxelSize));
            entityManager.AddComponentData(chunkEntity, new TerrainChunkBounds { WorldOrigin = new float3(0, originY, 0) });
            entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunkEntity);

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

            var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(chunkEntity);
            var vertices = new NativeArray<float3>(meshData.Mesh.Value.Vertices.Length, Allocator.Temp);
            var indices = new NativeArray<int>(meshData.Mesh.Value.Indices.Length, Allocator.Temp);

            try
            {
                for (int i = 0; i < meshData.Mesh.Value.Vertices.Length; i++)
                {
                    vertices[i] = meshData.Mesh.Value.Vertices[i];
                }

                for (int i = 0; i < meshData.Mesh.Value.Indices.Length; i++)
                {
                    indices[i] = meshData.Mesh.Value.Indices[i];
                }

                var analysis = TerrainMeshAnalyzer.Analyze(vertices, indices, HistogramBuckets);

                UnityEngine.Debug.Log($"[BANDING_DIAG] H3 Test - Triangle Quality Analysis:");
                UnityEngine.Debug.Log($"  Vertex Count: {analysis.VertexCount}");
                UnityEngine.Debug.Log($"  Total Triangles: {analysis.TriangleCount}");

                const float acceptableDegeneratePercent = 5.0f;  // Allow up to 5% degenerate triangles
                const float acceptableMaxAspectRatio = 50.0f;    // Reasonable threshold for max aspect ratio

                float degeneratePercent = analysis.TriangleCount > 0 
                    ? (float)analysis.DegenerateTriangleCount / analysis.TriangleCount * 100 
                    : 0f;

                // Log metrics with threshold comparisons
                UnityEngine.Debug.Log($"  Degenerate Triangles: {analysis.DegenerateTriangleCount} ({degeneratePercent:F1}%) [threshold: {acceptableDegeneratePercent:F1}%]");
                UnityEngine.Debug.Log($"  Max Aspect Ratio: {analysis.MaxAspectRatio:F2} [threshold: {acceptableMaxAspectRatio:F2}]");
                UnityEngine.Debug.Log($"  Avg Triangle Area: {analysis.AvgTriangleArea:F6}");
                UnityEngine.Debug.Log($"  Min Triangle Area: {analysis.MinTriangleArea:F6}");
                UnityEngine.Debug.Log($"  Y-Range: [{analysis.MinY:F3}, {analysis.MaxY:F3}]");

                if (degeneratePercent > acceptableDegeneratePercent)
                {
                    UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H3 POTENTIAL ISSUE: High degenerate triangle percentage ({degeneratePercent:F1}%)");
                }

                if (analysis.MaxAspectRatio > acceptableMaxAspectRatio)
                {
                    UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H3 POTENTIAL ISSUE: Very high aspect ratio detected ({analysis.MaxAspectRatio:F2})");
                }

                if (degeneratePercent <= acceptableDegeneratePercent && analysis.MaxAspectRatio <= acceptableMaxAspectRatio)
                {
                    UnityEngine.Debug.Log($"[BANDING_DIAG] H3 RULED OUT: Triangle quality is acceptable across mesh");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H3 CONFIRMED: Triangle quality issues detected");
                }

                Assert.Pass("Diagnostic test completed");
            }
            finally
            {
                vertices.Dispose();
                indices.Dispose();
            }
        }

        /// <summary>
        /// H4 Test: Determine if banding is caused by SDF function complexity.
        /// Uses a flat plane SDF (density = p.y - midHeight) to isolate Surface Nets algorithm behavior.
        /// Runs SurfaceNetsJob directly — no ECS pipeline needed.
        /// </summary>
        [UnityTest]
        public IEnumerator FlatPlaneSDF_HasNoBanding()
        {
            var resolution = new int3(TestResolution, TestResolution, TestResolution);
            var voxelSize = TestVoxelSize;
            // Offset by 0.3 voxels so the surface sits between grid rows (Y=8.3).
            // Without offset, midHeight=8.0 lands exactly on a sample point where density=0,
            // and ProcessCell's strict check (minDensity < 0 && maxDensity > 0) misses it.
            var midHeight = (resolution.y - 1) * voxelSize * 0.5f + voxelSize * 0.3f;

            // Fill density with flat plane: density = worldY - midHeight
            var densityRes = new int3(resolution.x + 1, resolution.y + 1, resolution.z + 1);
            var densityCount = densityRes.x * densityRes.y * densityRes.z;
            var densities = new NativeArray<float>(densityCount, Allocator.TempJob);

            for (int z = 0; z < densityRes.z; z++)
            {
                for (int y = 0; y < densityRes.y; y++)
                {
                    for (int x = 0; x < densityRes.x; x++)
                    {
                        var index = x + densityRes.x * (y + densityRes.y * z);
                        var worldY = y * voxelSize;
                        densities[index] = worldY - midHeight;
                    }
                }
            }

            var result = RunSurfaceNetsDirectly(densities, densityRes, resolution, voxelSize);

            try
            {
                UnityEngine.Debug.Log($"[BANDING_DIAG] H4 Test - Flat Plane SDF Analysis:");
                UnityEngine.Debug.Log($"  Surface at Y={midHeight:F2}, VoxelSize={voxelSize}");
                UnityEngine.Debug.Log($"  Vertex Count: {result.vertices.Length}");
                UnityEngine.Debug.Log($"  Triangle Count: {result.indices.Length / 3}");

                if (result.vertices.Length == 0)
                {
                    UnityEngine.Debug.LogError("[BANDING_DIAG] H4 FAILED: No vertices generated for flat plane");
                    Assert.Fail("Flat plane SDF produced no vertices");
                }

                // Analyze Y distribution
                var analysis = TerrainMeshAnalyzer.Analyze(result.vertices, result.indices, HistogramBuckets);
                var spacing = TerrainMeshAnalyzer.AnalyzeVertexYSpacing(result.vertices);
                int peaks = TerrainMeshAnalyzer.FindClusteringPeaks(analysis.YHistogram, ClusteringThreshold);

                UnityEngine.Debug.Log($"  Y Range: [{analysis.MinY:F3}, {analysis.MaxY:F3}]");
                UnityEngine.Debug.Log($"  Y Spread: {analysis.MaxY - analysis.MinY:F6} (should be ~0 for flat plane)");
                UnityEngine.Debug.Log($"  Y Spacing StdDev: {spacing.StandardDeviation:F6}");
                UnityEngine.Debug.Log($"  Clustering Peaks: {peaks}");
                UnityEngine.Debug.Log($"  Max Aspect Ratio: {analysis.MaxAspectRatio:F2}");

                // For a flat plane, all vertices should be at the same Y
                float ySpread = analysis.MaxY - analysis.MinY;
                bool isTrulyFlat = ySpread < voxelSize * 0.01f; // Within 1% of voxel size

                if (isTrulyFlat)
                {
                    UnityEngine.Debug.Log($"[BANDING_DIAG] H4 Result: Flat plane IS flat (spread={ySpread:F6})");
                    UnityEngine.Debug.Log($"  → Banding is caused by SDF complexity, NOT the Surface Nets algorithm");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H4 Result: Flat plane is NOT flat (spread={ySpread:F6})");
                    UnityEngine.Debug.LogWarning($"  → Surface Nets algorithm introduces banding even with trivial SDF");
                }

                Assert.Pass($"H4 diagnostic completed. Flat plane Y-spread: {ySpread:F6}");
            }
            finally
            {
                result.vertices.Dispose();
                result.indices.Dispose();
                densities.Dispose();
            }

            yield break;
        }

        /// <summary>
        /// H5 Test: Determine if the inverse-density weighting formula causes vertex snapping.
        /// Compares the current formula (1/|d|+eps) against a tilted plane to measure Y-spread.
        /// Since we can't modify SurfaceNetsJob weighting at runtime, we test with a tilted plane
        /// where the surface crosses voxel cells at non-integer Y positions.
        /// If banding exists on a tilted plane, it's the weighting formula; if flat, it's the SDF.
        /// </summary>
        [UnityTest]
        public IEnumerator DensityWeighting_TiltedPlane_ShowsBanding()
        {
            var resolution = new int3(TestResolution, TestResolution, TestResolution);
            var voxelSize = TestVoxelSize;
            var midHeight = (resolution.y - 1) * voxelSize * 0.5f;

            // Tilted plane: density = p.y - (midHeight + slope*p.x)
            // This forces the isosurface to cross at varying Y per-cell
            var slope = 0.5f; // 0.5 rise per unit X

            var densityRes = new int3(resolution.x + 1, resolution.y + 1, resolution.z + 1);
            var densityCount = densityRes.x * densityRes.y * densityRes.z;
            var densities = new NativeArray<float>(densityCount, Allocator.TempJob);

            for (int z = 0; z < densityRes.z; z++)
            {
                for (int y = 0; y < densityRes.y; y++)
                {
                    for (int x = 0; x < densityRes.x; x++)
                    {
                        var index = x + densityRes.x * (y + densityRes.y * z);
                        var worldX = x * voxelSize;
                        var worldY = y * voxelSize;
                        densities[index] = worldY - (midHeight + slope * worldX);
                    }
                }
            }

            var result = RunSurfaceNetsDirectly(densities, densityRes, resolution, voxelSize);

            try
            {
                UnityEngine.Debug.Log($"[BANDING_DIAG] H5 Test - Tilted Plane Weighting Analysis:");
                UnityEngine.Debug.Log($"  Surface: Y = {midHeight} + {slope}*X, VoxelSize={voxelSize}");
                UnityEngine.Debug.Log($"  Vertex Count: {result.vertices.Length}");

                if (result.vertices.Length == 0)
                {
                    UnityEngine.Debug.LogError("[BANDING_DIAG] H5 FAILED: No vertices generated for tilted plane");
                    Assert.Fail("Tilted plane produced no vertices");
                }

                // For a tilted plane, the ideal Y = midHeight + slope*X.
                // Measure how far each vertex deviates from the ideal surface.
                float maxDeviation = 0f;
                float sumDeviation = 0f;
                int deviationCount = 0;

                // Also check if deviations cluster at voxel boundaries
                var deviations = new NativeArray<float>(result.vertices.Length, Allocator.Temp);
                try
                {
                    for (int i = 0; i < result.vertices.Length; i++)
                    {
                        var v = result.vertices[i];
                        var idealY = midHeight + slope * v.x;
                        var deviation = v.y - idealY;
                        deviations[i] = deviation;
                        var absDev = math.abs(deviation);
                        maxDeviation = math.max(maxDeviation, absDev);
                        sumDeviation += absDev;
                        deviationCount++;
                    }

                    float avgDeviation = deviationCount > 0 ? sumDeviation / deviationCount : 0f;

                    // Analyze the Y positions for clustering
                    var analysis = TerrainMeshAnalyzer.Analyze(result.vertices, result.indices, HistogramBuckets);
                    var spacing = TerrainMeshAnalyzer.AnalyzeVertexYSpacing(result.vertices);
                    int peaks = TerrainMeshAnalyzer.FindClusteringPeaks(analysis.YHistogram, ClusteringThreshold);

                    UnityEngine.Debug.Log($"  Y Range: [{analysis.MinY:F3}, {analysis.MaxY:F3}]");
                    UnityEngine.Debug.Log($"  Deviation from ideal - Avg: {avgDeviation:F6}, Max: {maxDeviation:F6}");
                    UnityEngine.Debug.Log($"  Y Spacing - Mean: {spacing.MeanSpacing:F4}, StdDev: {spacing.StandardDeviation:F4}");
                    UnityEngine.Debug.Log($"  Y Spacing - Min: {spacing.MinSpacing:F6}, Max: {spacing.MaxSpacing:F4}");
                    UnityEngine.Debug.Log($"  Clustering Peaks: {peaks}");
                    UnityEngine.Debug.Log($"  Max Aspect Ratio: {analysis.MaxAspectRatio:F2}");

                    // Check if vertex Y positions cluster at voxel-layer boundaries
                    // With slope=0.5 and VoxelSize=1, ideal Y increments by 0.5 per voxel column.
                    // If weighting snaps to integer Y, we'll see clustering at integer values.
                    bool hasClustering = peaks > (HistogramBuckets * 0.1f);
                    bool hasHighDeviation = maxDeviation > voxelSize * 0.3f;

                    if (hasClustering || hasHighDeviation)
                    {
                        UnityEngine.Debug.LogWarning($"[BANDING_DIAG] H5 CONFIRMED: Inverse-density weighting causes vertex snapping");
                        UnityEngine.Debug.LogWarning($"  Clustering peaks: {peaks}, Max deviation: {maxDeviation:F4}");
                        UnityEngine.Debug.LogWarning($"  The formula weight=1/(|d|+eps) biases vertices toward corners with d≈0");
                    }
                    else
                    {
                        UnityEngine.Debug.Log($"[BANDING_DIAG] H5 RULED OUT: Tilted plane has smooth vertex placement");
                    }

                    Assert.Pass($"H5 diagnostic completed. Peaks: {peaks}, MaxDev: {maxDeviation:F4}");
                }
                finally
                {
                    deviations.Dispose();
                }
            }
            finally
            {
                result.vertices.Dispose();
                result.indices.Dispose();
                densities.Dispose();
            }

            yield break;
        }

        /// <summary>
        /// Runs SurfaceNetsJob directly without ECS pipeline.
        /// Returns vertices and indices that the CALLER must dispose.
        /// </summary>
        private struct DirectMeshResult
        {
            public NativeArray<float3> vertices;
            public NativeArray<int> indices;
        }

        private DirectMeshResult RunSurfaceNetsDirectly(
            NativeArray<float> densities, int3 densityResolution, int3 gridResolution, float voxelSize)
        {
            var baseCellResolution = new int3(
                math.max(gridResolution.x - 1, 0),
                math.max(gridResolution.y - 1, 0),
                math.max(gridResolution.z - 1, 0));

            var cellResolution = new int3(
                math.max(densityResolution.x - 1, 0),
                math.max(densityResolution.y - 1, 0),
                math.max(densityResolution.z - 1, 0));

            var cellCount = cellResolution.x * cellResolution.y * cellResolution.z;
            var vertexIndices = new NativeArray<int>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var cellSigns = new NativeArray<sbyte>(cellCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = densityResolution,
                    VoxelSize = voxelSize,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexIndices,
                    CellSigns = cellSigns,
                    CellResolution = cellResolution,
                    BaseCellResolution = baseCellResolution
                };

                job.Execute();

                // Copy to persistent arrays for caller
                var resultVerts = new NativeArray<float3>(vertices.Length, Allocator.Temp);
                var resultIndices = new NativeArray<int>(indices.Length, Allocator.Temp);
                for (int i = 0; i < vertices.Length; i++) resultVerts[i] = vertices[i];
                for (int i = 0; i < indices.Length; i++) resultIndices[i] = indices[i];

                return new DirectMeshResult { vertices = resultVerts, indices = resultIndices };
            }
            finally
            {
                vertexIndices.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }

        // Helper struct for voxel size correlation test
        private struct VoxelSizeTestResult
        {
            public float[] histogram;
            public float minY;
            public float maxY;
            public int vertexCount;
        }

        /// <summary>
        /// Helper method to run mesh generation with a specific voxel size and return results.
        /// </summary>
        private IEnumerator RunTestWithVoxelSize(float voxelSize, System.Action<VoxelSizeTestResult> onComplete)
        {
            // Setup test world
            var tempWorld = new World("TempDiagnosticWorld");
            var tempEntityManager = tempWorld.EntityManager;

            try
            {
                // Setup (same pattern as VertexYPositions test)
                var debugEntity = tempEntityManager.CreateEntity();
                var debugConfig = TerrainDebugConfig.Default;
                debugConfig.Enabled = true;
                debugConfig.FreezeStreaming = true;
                debugConfig.FixedCenterChunk = int2.zero;
                debugConfig.StreamingRadiusInChunks = 1;
                tempEntityManager.AddComponentData(debugEntity, debugConfig);

                var settingsEntity = tempEntityManager.CreateEntity();
                var settings = new SDFTerrainFieldSettings
                {
                    BaseHeight = TestBaseHeight,
                    Amplitude = TestAmplitude,
                    Frequency = TestFrequency,
                    NoiseValue = 0f
                };
                tempEntityManager.AddComponentData(settingsEntity, settings);

                var configEntity = tempEntityManager.CreateEntity();
                var featureConfig = new Streaming.ProjectFeatureConfigSingleton
                {
                    TerrainStreamingRadiusInChunks = 1
                };
                tempEntityManager.AddComponentData(configEntity, featureConfig);

                var resolution = new int3(TestResolution, TestResolution, TestResolution);
                var chunkVerticalSpan = math.max(0, resolution.y - 1) * voxelSize;
                var originY = settings.BaseHeight - (chunkVerticalSpan * 0.5f);

                var chunkEntity = tempEntityManager.CreateEntity();
                tempEntityManager.AddComponentData(chunkEntity, new TerrainChunk { ChunkCoord = new int3(0, 0, 0) });
                tempEntityManager.AddComponentData(chunkEntity, TerrainChunkGridInfo.Create(resolution, voxelSize));
                tempEntityManager.AddComponentData(chunkEntity, new TerrainChunkBounds { WorldOrigin = new float3(0, originY, 0) });
                tempEntityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunkEntity);

                var streamingSystem = tempWorld.CreateSystem<Streaming.TerrainChunkStreamingSystem>();
                var densitySystem = tempWorld.CreateSystem<TerrainChunkDensitySamplingSystem>();
                var meshBuildSystem = tempWorld.CreateSystem<TerrainChunkMeshBuildSystem>();

                streamingSystem.Update(tempWorld.Unmanaged);
                yield return null;

                for (int i = 0; i < 10; i++)
                {
                    densitySystem.Update(tempWorld.Unmanaged);
                    yield return null;
                }

                for (int i = 0; i < 10; i++)
                {
                    meshBuildSystem.Update(tempWorld.Unmanaged);
                    yield return null;
                }

                var meshData = tempEntityManager.GetComponentData<TerrainChunkMeshData>(chunkEntity);
                var vertices = new NativeArray<float3>(meshData.Mesh.Value.Vertices.Length, Allocator.Temp);

                try
                {
                    for (int i = 0; i < meshData.Mesh.Value.Vertices.Length; i++)
                    {
                        vertices[i] = meshData.Mesh.Value.Vertices[i];
                    }

                    var histogram = TerrainMeshAnalyzer.ComputeYHistogram(vertices, HistogramBuckets);

                    float minY = float.MaxValue;
                    float maxY = float.MinValue;
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        if (vertices[i].y < minY) minY = vertices[i].y;
                        if (vertices[i].y > maxY) maxY = vertices[i].y;
                    }

                    onComplete(new VoxelSizeTestResult
                    {
                        histogram = histogram,
                        minY = minY,
                        maxY = maxY,
                        vertexCount = vertices.Length
                    });
                }
                finally
                {
                    vertices.Dispose();
                }
            }
            finally
            {
                if (tempWorld.IsCreated)
                {
                    tempWorld.Dispose();
                }
            }
        }

        /// <summary>
        /// Calculates the average interval between peak Y positions.
        /// </summary>
        private float CalculateAveragePeakInterval(float[] peaks)
        {
            if (peaks == null || peaks.Length < 2)
            {
                return 0f;
            }

            System.Array.Sort(peaks);
            return (peaks[peaks.Length - 1] - peaks[0]) / (peaks.Length - 1);
        }
    }
}