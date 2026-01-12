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
        public IEnumerator SurfaceNetsJob_Winding_NegZ_ForVerticalXYFace()
        {
            // Vertical wall at z = 1.5, expect normals to point -Z (matches XY face orientation in SurfaceNets).
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
                    Assert.Less(normal.z, 0f, "Vertical XY face should face -Z");
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
