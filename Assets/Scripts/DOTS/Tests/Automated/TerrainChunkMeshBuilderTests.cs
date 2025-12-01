 using DOTS.Terrain.Meshing;
using DOTS.Terrain.SDF;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Entities;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkMeshBuilderTests
    {
        [Test]
        public void BuildMeshBlob_GeneratesVertexWhenSurfaceExists()
        {
            var density = CreateDensityBlob(new[] { -1f, -0.5f, -0.25f, -0.1f, 0.1f, 0.25f, 0.5f, 1f });
            try
            {
                var grid = TerrainChunkGridInfo.Create(new int3(2, 2, 2), 1f);
                var bounds = new TerrainChunkBounds { WorldOrigin = float3.zero };

                var blob = TerrainChunkMeshBuilder.BuildMeshBlob(ref density, in grid, in bounds);

                Assert.IsTrue(blob.IsCreated);
                Assert.AreEqual(1, blob.Value.Vertices.Length);
            }
            finally
            {
                density.Dispose();
            }
        }

        [Test]
        public void BuildMeshBlob_NoVerticesForUniformDensity()
        {
            var density = CreateDensityBlob(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f });
            try
            {
                var grid = TerrainChunkGridInfo.Create(new int3(2, 2, 2), 1f);
                var bounds = new TerrainChunkBounds { WorldOrigin = float3.zero };

                var blob = TerrainChunkMeshBuilder.BuildMeshBlob(ref density, in grid, in bounds);

                Assert.IsTrue(blob.IsCreated);
                Assert.AreEqual(0, blob.Value.Vertices.Length);
            }
            finally
            {
                density.Dispose();
            }
        }

        private static TerrainChunkDensity CreateDensityBlob(float[] values)
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
            var data = builder.Allocate(ref root.Values, values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                data[i] = values[i];
            }

            var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Persistent);
            builder.Dispose();
            return TerrainChunkDensity.FromBlob(blob);
        }
    }
}
