using DOTS.Terrain.Meshing;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SurfaceNetsJobTests
    {
        [Test]
        public void SurfaceNetsJob_GeneratesVerticesAndIndices()
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
        }

        [Test]
        public void SurfaceNetsJob_NoSurfaceWhenUniform()
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
