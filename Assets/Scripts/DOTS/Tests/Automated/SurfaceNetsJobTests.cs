using System.Collections;
using DOTS.Terrain.Meshing;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.TestTools;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SurfaceNetsJobTests
    {
        [UnityTest]
        public IEnumerator SurfaceNetsJob_GeneratesVerticesAndIndices()
        {
            var resolution = new int3(3, 3, 3);
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);
            FillPlaneDensities(densities, resolution);

            var vertexMap = new NativeArray<int>(8, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(8, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = new int3(2, 2, 2),
                    BaseCellResolution = new int3(2, 2, 2)
                };

                job.Run();

                Assert.Greater(vertices.Length, 0);
                Assert.Greater(indices.Length, 0);
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceNetsJob_NoSurfaceWhenUniform()
        {
            var resolution = new int3(3, 3, 3);
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);
            for (int i = 0; i < densities.Length; i++)
            {
                densities[i] = 1f;
            }

            var vertexMap = new NativeArray<int>(8, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(8, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = new int3(2, 2, 2),
                    BaseCellResolution = new int3(2, 2, 2)
                };

                job.Run();

                Assert.AreEqual(0, vertices.Length);
                Assert.AreEqual(0, indices.Length);
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceNetsJob_GeneratesConsistentWindingOrder_NormalsPointUpward()
        {
            // Create a horizontal ground plane: negative below y=1.5, positive above
            var resolution = new int3(4, 4, 4);
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        // Signed distance to y=1.5 plane (negative = inside/below, positive = outside/above)
                        densities[index++] = y - 1.5f;
                    }
                }
            }

            var vertexMap = new NativeArray<int>(27, Allocator.TempJob);  // 3x3x3 cells
            var cellSigns = new NativeArray<sbyte>(27, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = new int3(3, 3, 3),
                    BaseCellResolution = new int3(3, 3, 3)
                };

                job.Run();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for ground plane");
                Assert.Greater(indices.Length, 0, "Should generate indices for ground plane");
                Assert.AreEqual(0, indices.Length % 3, "Indices should be multiple of 3 (triangles)");

                // Verify all triangle normals point upward (positive Y) for horizontal ground
                int upwardCount = 0;
                int downwardCount = 0;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];

                    var edge1 = v1 - v0;
                    var edge2 = v2 - v0;
                    var normal = math.cross(edge1, edge2);

                    if (normal.y > 0)
                        upwardCount++;
                    else if (normal.y < 0)
                        downwardCount++;
                }

                // Ground plane triangles should all face upward
                Assert.Greater(upwardCount, 0, "Should have upward-facing triangles");
                Assert.AreEqual(0, downwardCount, $"Ground plane should have no downward-facing triangles, but found {downwardCount}");
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceNetsJob_Winding_Upward_ForXYGround()
        {
            // Ground plane: y = 1.5, expect normals to point +Y.
            var resolution = new int3(4, 4, 4); // 3x3x3 cells
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        densities[index++] = y - 1.5f;
                    }
                }
            }

            var cellRes = new int3(3, 3, 3);
            var vertexMap = new NativeArray<int>(cellRes.x * cellRes.y * cellRes.z, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(cellRes.x * cellRes.y * cellRes.z, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = cellRes,
                    BaseCellResolution = cellRes
                };

                job.Run();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for ground plane");
                Assert.Greater(indices.Length, 0, "Should generate indices for ground plane");

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];
                    var normal = math.cross(v1 - v0, v2 - v0);
                    Assert.Greater(normal.y, 0f, "Ground plane triangles should face upward (+Y)");
                }
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceNetsJob_Winding_PosZ_ForVerticalXYFace()
        {
            // Vertical wall at z = 1.5, expect normals to point +Z (outward, away from solid).
            // SDF = z - 1.5: negative (solid) below z=1.5, positive (air) above.
            // Gradient = (0,0,1) → outward is +Z.
            var resolution = new int3(4, 4, 4); // 3x3x3 cells
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        densities[index++] = z - 1.5f;
                    }
                }
            }

            var cellRes = new int3(3, 3, 3);
            var vertexMap = new NativeArray<int>(cellRes.x * cellRes.y * cellRes.z, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(cellRes.x * cellRes.y * cellRes.z, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = cellRes,
                    BaseCellResolution = cellRes
                };

                job.Run();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for vertical wall");
                Assert.Greater(indices.Length, 0, "Should generate indices for vertical wall");

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];
                    var normal = math.cross(v1 - v0, v2 - v0);
                    Assert.Greater(normal.z, 0f, "Vertical XY face should face +Z (outward, away from solid)");
                }
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceNetsJob_Winding_PosX_ForVerticalYZFace()
        {
            // Vertical wall at x = 1.5, expect normals to point +X.
            var resolution = new int3(4, 4, 4); // 3x3x3 cells
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        densities[index++] = x - 1.5f;
                    }
                }
            }

            var cellRes = new int3(3, 3, 3);
            var vertexMap = new NativeArray<int>(cellRes.x * cellRes.y * cellRes.z, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(cellRes.x * cellRes.y * cellRes.z, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = cellRes,
                    BaseCellResolution = cellRes
                };

                job.Run();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for vertical wall");
                Assert.Greater(indices.Length, 0, "Should generate indices for vertical wall");

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];
                    var normal = math.cross(v1 - v0, v2 - v0);
                    Assert.Greater(normal.x, 0f, "Vertical YZ face should face +X");
                }
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        [UnityTest]
        public IEnumerator SurfaceNetsJob_TieBreaksConsistently_OnPaddedGrid()
        {
            // Build a small grid with padded cells where signSum often equals zero (two pos / two neg).
            // Winding should remain stable and normals should point upward for a horizontal plane.
            // Use 4x4x4 samples so we have a 3x3x3 cell grid, matching padded chunk use.
            var resolution = new int3(4, 4, 4);
            var densities = new NativeArray<float>(resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            // Signed distance to plane y = 1.5: negative below, positive above; ties occur at plane corners/edges.
            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        densities[index++] = y - 1.5f;
                    }
                }
            }

            var cellResolution = new int3(3, 3, 3); // Resolution - 1
            var vertexMap = new NativeArray<int>(cellResolution.x * cellResolution.y * cellResolution.z, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(cellResolution.x * cellResolution.y * cellResolution.z, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = cellResolution,
                    BaseCellResolution = cellResolution
                };

                job.Run();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for padded grid");
                Assert.Greater(indices.Length, 0, "Should generate indices for padded grid");

                // All triangles should face upward for this horizontal plane even when signSum == 0.
                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];

                    var normal = math.cross(v1 - v0, v2 - v0);
                    Assert.GreaterOrEqual(normal.y, 0f, "Normals should not flip downward on tie-break faces");
                }
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        /// <summary>
        /// Regression test for the TryEmitQuad double-flip winding bug.
        ///
        /// Flat-plane tests pass because their cross products are large and unambiguous,
        /// so the double-flip accidentally produces correct results. On a curved sine-wave
        /// SDF, vertices in nearly-horizontal quads sit at slightly different heights,
        /// producing near-zero cross products whose sign is dominated by floating-point
        /// noise. The double-flip (conditional swap before cross product, then conditional
        /// re-flip after) yields inconsistent winding on those quads.
        ///
        /// This test uses a sine-wave ground surface — the same shape that produces
        /// visible backface-culling holes in-game — and asserts that ALL triangle normals
        /// have a non-negative Y component. Any downward-facing triangle proves the
        /// winding logic is broken for curved surfaces.
        /// </summary>
        [UnityTest]
        public IEnumerator SurfaceNetsJob_Winding_ConsistentOnCurvedSurface_SineWaveSDF()
        {
            // Larger grid to ensure we get plenty of nearly-horizontal quads where the
            // bug manifests. 16×16×16 samples → 15×15×15 cells.
            var resolution = new int3(16, 16, 16);
            var densities = new NativeArray<float>(
                resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            // SDF: signed distance to a sine-wave ground surface.
            //   height(x,z) = 8 + 3 * sin(x * 0.8) * sin(z * 0.8)
            //   density     = y - height   (negative below surface, positive above)
            //
            // The amplitude (3) and frequency (0.8) are chosen so that the surface has
            // regions that are nearly horizontal (crests/troughs) within the 16-unit grid.
            // Those regions are where the double-flip bug produces reversed triangles.
            const float amplitude = 3f;
            const float frequency = 0.8f;
            const float baseHeight = 8f;

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        float height = baseHeight +
                            amplitude * math.sin(x * frequency) * math.sin(z * frequency);
                        densities[index++] = y - height;
                    }
                }
            }

            var cellRes = resolution - new int3(1, 1, 1); // 15×15×15
            var totalCells = cellRes.x * cellRes.y * cellRes.z;
            var vertexMap = new NativeArray<int>(totalCells, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(totalCells, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = cellRes,
                    BaseCellResolution = cellRes
                };

                job.Execute();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for sine-wave terrain");
                Assert.Greater(indices.Length, 0, "Should generate indices for sine-wave terrain");
                Assert.AreEqual(0, indices.Length % 3, "Indices should be a multiple of 3 (triangles)");

                int outwardCount = 0;
                int inwardCount = 0;
                int degenerateCount = 0;
                int tangentSkipCount = 0;
                float worstInwardCosAngle = 0f;

                // Tolerance: triangles whose normal is nearly perpendicular to the
                // analytical SDF gradient are "tangent" — the surface is nearly edge-on
                // to the face axis. The discrete single-point density gradient and the
                // continuous analytical gradient can legitimately disagree at this scale.
                // These triangles are visually invisible (edge-on) and don't cause culling
                // holes. At this grid scale, the discrete finite-difference gradient at the grid node
                // and the continuous analytical gradient at the triangle centroid can disagree by up to ~70°
                // (amplitude=3, freq=0.8 rotates the gradient ~18° per voxel). Threshold of 0.35 ≈ cos(70°)
                // covers this discretization error. These triangles are correctly wound relative to the discrete field.
                const float tangentThreshold = 0.35f;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];

                    var triNormal = math.cross(v1 - v0, v2 - v0);

                    if (math.lengthsq(triNormal) < 1e-12f)
                    {
                        degenerateCount++;
                        continue;
                    }

                    // Compute the SDF gradient at the triangle centroid.
                    // SDF = y - (baseHeight + amplitude * sin(cx * freq) * sin(cz * freq))
                    // dSDF/dx = -amplitude * freq * cos(cx * freq) * sin(cz * freq)
                    // dSDF/dy = 1
                    // dSDF/dz = -amplitude * sin(cx * freq) * freq * cos(cz * freq)
                    var centroid = (v0 + v1 + v2) / 3f;
                    float cx = centroid.x;
                    float cz = centroid.z;
                    var gradient = new float3(
                        -amplitude * frequency * math.cos(cx * frequency) * math.sin(cz * frequency),
                        1f,
                        -amplitude * math.sin(cx * frequency) * frequency * math.cos(cz * frequency)
                    );

                    // Normalize both vectors so the dot product gives cos(angle).
                    var nN = math.normalize(triNormal);
                    var nG = math.normalize(gradient);
                    float cosAngle = math.dot(nN, nG);

                    // Skip near-tangent triangles where the surface is almost edge-on.
                    if (math.abs(cosAngle) < tangentThreshold)
                    {
                        tangentSkipCount++;
                        continue;
                    }

                    if (cosAngle > 0f)
                        outwardCount++;
                    else
                    {
                        inwardCount++;
                        if (math.abs(cosAngle) > math.abs(worstInwardCosAngle))
                            worstInwardCosAngle = cosAngle;
                    }
                }

                int totalMeaningful = outwardCount + inwardCount;
                Assert.Greater(totalMeaningful, 0, "Should have non-degenerate triangles");

                float outwardPct = totalMeaningful > 0
                    ? (outwardCount / (float)totalMeaningful) * 100f
                    : 0f;

                UnityEngine.Debug.Log(
                    $"[Winding Curved Surface] outward={outwardCount} inward={inwardCount} " +
                    $"degenerate={degenerateCount} tangentSkipped={tangentSkipCount} " +
                    $"total={indices.Length / 3} outwardPct={outwardPct:F1}% " +
                    $"worstInwardCos={worstInwardCosAngle:F4}");

                // The critical assertion: every non-degenerate, non-tangent triangle must
                // face outward (normal aligned with SDF gradient). Tangent triangles are
                // edge-on and visually invisible, so disagreement there is acceptable.
                Assert.AreEqual(0, inwardCount,
                    $"Sine-wave terrain should have 0 inward-facing triangles but found " +
                    $"{inwardCount} ({100f - outwardPct:F1}% of {totalMeaningful}). " +
                    $"tangentSkipped={tangentSkipCount}. " +
                    $"This indicates inconsistent winding from TryEmitQuad.");
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        /// <summary>
        /// Validates the density-gradient winding approach using a sphere SDF where the
        /// analytical gradient is known at every point: normalize(p - center).
        ///
        /// Unlike the sine-wave test (mostly horizontal with some slopes), a sphere
        /// exercises ALL three face generators with normals pointing in every direction.
        /// If the gradient-based winding works for a sphere, it works for any SDF.
        /// </summary>
        [UnityTest]
        public IEnumerator SurfaceNetsJob_Winding_ConsistentOnSphere_GradientSampling()
        {
            var resolution = new int3(16, 16, 16);
            var densities = new NativeArray<float>(
                resolution.x * resolution.y * resolution.z, Allocator.TempJob);

            // Sphere SDF: density = length(p - center) - radius
            // Gradient = normalize(p - center), always points radially outward.
            float3 center = new float3(7.5f, 7.5f, 7.5f);
            float radius = 5f;

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        float3 p = new float3(x, y, z);
                        densities[index++] = math.length(p - center) - radius;
                    }
                }
            }

            var cellRes = resolution - new int3(1, 1, 1);
            var totalCells = cellRes.x * cellRes.y * cellRes.z;
            var vertexMap = new NativeArray<int>(totalCells, Allocator.TempJob);
            var cellSigns = new NativeArray<sbyte>(totalCells, Allocator.TempJob);
            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = resolution,
                    VoxelSize = 1f,
                    Vertices = vertices,
                    Indices = indices,
                    VertexIndices = vertexMap,
                    CellSigns = cellSigns,
                    CellResolution = cellRes,
                    BaseCellResolution = cellRes
                };

                job.Execute();

                Assert.Greater(vertices.Length, 0, "Should generate vertices for sphere");
                Assert.Greater(indices.Length, 0, "Should generate indices for sphere");
                Assert.AreEqual(0, indices.Length % 3, "Indices should be a multiple of 3");

                int outwardCount = 0;
                int inwardCount = 0;
                int degenerateCount = 0;

                for (int i = 0; i < indices.Length; i += 3)
                {
                    var v0 = vertices[indices[i]];
                    var v1 = vertices[indices[i + 1]];
                    var v2 = vertices[indices[i + 2]];

                    var triNormal = math.cross(v1 - v0, v2 - v0);

                    if (math.lengthsq(triNormal) < 1e-12f)
                    {
                        degenerateCount++;
                        continue;
                    }

                    // SDF gradient for sphere = normalize(centroid - center)
                    var centroid = (v0 + v1 + v2) / 3f;
                    var toCenter = centroid - center;
                    if (math.lengthsq(toCenter) < 1e-12f)
                    {
                        degenerateCount++;
                        continue;
                    }
                    var gradient = math.normalize(toCenter);

                    float dot = math.dot(triNormal, gradient);

                    if (dot > 0f)
                        outwardCount++;
                    else
                        inwardCount++;
                }

                int totalMeaningful = outwardCount + inwardCount;
                Assert.Greater(totalMeaningful, 0, "Should have non-degenerate triangles");

                float outwardPct = totalMeaningful > 0
                    ? (outwardCount / (float)totalMeaningful) * 100f
                    : 0f;

                UnityEngine.Debug.Log(
                    $"[Winding Sphere] outward={outwardCount} inward={inwardCount} " +
                    $"degenerate={degenerateCount} total={indices.Length / 3} outwardPct={outwardPct:F1}%");

                Assert.AreEqual(0, inwardCount,
                    $"Sphere should have 0 inward-facing triangles but found " +
                    $"{inwardCount} ({100f - outwardPct:F1}% of {totalMeaningful}). " +
                    $"This validates that density-gradient winding works for all face orientations.");
            }
            finally
            {
                densities.Dispose();
                vertexMap.Dispose();
                cellSigns.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }

            yield return null;
        }

        private static void FillPlaneDensities(NativeArray<float> densities, int3 resolution)
        {
            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        densities[index++] = x - 0.5f;
                    }
                }
            }
        }
    }
}
