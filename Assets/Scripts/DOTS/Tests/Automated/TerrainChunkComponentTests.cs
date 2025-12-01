using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Terrain.SDF;

using TerrainChunkComponent = DOTS.Terrain.SDF.TerrainChunk;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkComponentTests
    {
        [Test]
        public void TerrainChunk_StoresChunkCoord()
        {
            var chunk = new TerrainChunkComponent { ChunkCoord = new int3(2, 0, -1) };
            Assert.AreEqual(new int3(2, 0, -1), chunk.ChunkCoord);
        }

        [Test]
        public void TerrainChunkGridInfo_ComputesVoxelCount()
        {
            var grid = TerrainChunkGridInfo.Create(new int3(4, 3, 2), 1.25f);
            Assert.AreEqual(new int3(4, 3, 2), grid.Resolution);
            Assert.AreEqual(1.25f, grid.VoxelSize);
            Assert.AreEqual(24, grid.VoxelCount);
        }

        [Test]
        public void TerrainChunkBounds_StoresWorldOrigin()
        {
            var bounds = new TerrainChunkBounds { WorldOrigin = new float3(10f, 5f, -3f) };
            Assert.AreEqual(new float3(10f, 5f, -3f), bounds.WorldOrigin);
        }

        [Test]
        public void TerrainChunkDensity_HoldsBlobReference()
        {
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<TerrainChunkDensityBlob>();
            var values = builder.Allocate(ref root.Values, 4);
            values[0] = -1f;
            values[1] = 0f;
            values[2] = 1f;
            values[3] = 2f;

            var blob = builder.CreateBlobAssetReference<TerrainChunkDensityBlob>(Allocator.Persistent);
            builder.Dispose();

            var density = TerrainChunkDensity.FromBlob(blob);
            try
            {
                Assert.IsTrue(density.IsCreated);
                Assert.AreEqual(4, density.Length);
                Assert.AreEqual(1f, density.Data.Value.Values[2]);
            }
            finally
            {
                density.Dispose();
            }
        }
    }
}
