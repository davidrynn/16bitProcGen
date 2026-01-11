using System;
using DOTS.Terrain.Meshing;
using DOTS.Terrain;
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
        public void BuildMeshBlob_GeneratesGeometryWhenSurfaceExists()
        {
            var resolution = new int3(3, 3, 3);
            var density = CreateDensityBlob(resolution, (x, y, z) => x - 0.5f);
            try
            {
                var grid = TerrainChunkGridInfo.Create(resolution, 1f);
                var densityGrid = new TerrainChunkDensityGridInfo { Resolution = resolution };
                var bounds = new TerrainChunkBounds { WorldOrigin = float3.zero };

                var blob = TerrainChunkMeshBuilder.BuildMeshBlob(ref density, in grid, in densityGrid, in bounds);

                Assert.IsTrue(blob.IsCreated);
                Assert.Greater(blob.Value.Vertices.Length, 0);
                Assert.Greater(blob.Value.Indices.Length, 0);
            }
            finally
            {
                density.Dispose();
            }
        }

        [Test]
        public void BuildMeshBlob_NoVerticesForUniformDensity()
        {
            var resolution = new int3(3, 3, 3);
            var density = CreateDensityBlob(resolution, (_, _, _) => 1f);
            try
            {
                var grid = TerrainChunkGridInfo.Create(resolution, 1f);
                var densityGrid = new TerrainChunkDensityGridInfo { Resolution = resolution };
                var bounds = new TerrainChunkBounds { WorldOrigin = float3.zero };

                var blob = TerrainChunkMeshBuilder.BuildMeshBlob(ref density, in grid, in densityGrid, in bounds);

                Assert.IsTrue(blob.IsCreated);
                Assert.AreEqual(0, blob.Value.Vertices.Length);
                Assert.AreEqual(0, blob.Value.Indices.Length);
            }
            finally
            {
                density.Dispose();
            }
        }

        private static TerrainChunkDensity CreateDensityBlob(int3 resolution, Func<int, int, int, float> sampler)
        {
            var total = resolution.x * resolution.y * resolution.z;
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
            var data = builder.Allocate(ref root.Values, total);

            var index = 0;
            for (int z = 0; z < resolution.z; z++)
            {
                for (int y = 0; y < resolution.y; y++)
                {
                    for (int x = 0; x < resolution.x; x++)
                    {
                        data[index++] = sampler(x, y, z);
                    }
                }
            }

            var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Persistent);
            builder.Dispose();
            return TerrainChunkDensity.FromBlob(blob);
        }
    }
}
