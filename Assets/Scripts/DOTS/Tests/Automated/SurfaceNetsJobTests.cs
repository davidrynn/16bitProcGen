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
        public void SurfaceNetsJob_GeneratesVertexForSingleSurface()
        {
            var densities = new NativeArray<float>(8, Allocator.TempJob);
            densities[0] = -1f;
            densities[1] = -0.5f;
            densities[2] = -0.25f;
            densities[3] = -0.1f;
            densities[4] = 0.1f;
            densities[5] = 0.25f;
            densities[6] = 0.5f;
            densities[7] = 1f;

            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = new int3(2, 2, 2),
                    VoxelSize = 1f,
                    ChunkOrigin = float3.zero,
                    Vertices = vertices,
                    Indices = indices
                };

                job.Run();

                Assert.AreEqual(1, vertices.Length);
                Assert.AreEqual(0, indices.Length);
                Assert.That(vertices[0].x, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(vertices[0].y, Is.EqualTo(0.5f).Within(1e-4f));
                Assert.That(vertices[0].z, Is.EqualTo(0.5f).Within(1e-4f));
            }
            finally
            {
                densities.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }

        [Test]
        public void SurfaceNetsJob_NoSurfaceWhenUniform()
        {
            var densities = new NativeArray<float>(8, Allocator.TempJob);
            for (int i = 0; i < densities.Length; i++)
            {
                densities[i] = 1f;
            }

            var vertices = new NativeList<float3>(Allocator.TempJob);
            var indices = new NativeList<int>(Allocator.TempJob);

            try
            {
                var job = new SurfaceNetsJob
                {
                    Densities = densities,
                    Resolution = new int3(2, 2, 2),
                    VoxelSize = 1f,
                    ChunkOrigin = float3.zero,
                    Vertices = vertices,
                    Indices = indices
                };

                job.Run();

                Assert.AreEqual(0, vertices.Length);
                Assert.AreEqual(0, indices.Length);
            }
            finally
            {
                densities.Dispose();
                vertices.Dispose();
                indices.Dispose();
            }
        }
    }
}
