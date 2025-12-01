using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DOTS.Terrain.SDF;
using Unity.Jobs;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkDensitySamplingSystemTests
    {
        [Test]
        public void ChunkDensityJob_SamplesGroundSDF()
        {
            var field = new SDFTerrainField
            {
                BaseHeight = 0f,
                Amplitude = 0f,
                Frequency = 0f,
                NoiseValue = 0f
            };

            var densities = new NativeArray<float>(8, Allocator.TempJob);
            var edits = new NativeArray<SDFEdit>(0, Allocator.TempJob);
            try
            {
                var job = new TerrainChunkDensitySamplingJob
                {
                    Resolution = new int3(2, 2, 2),
                    VoxelSize = 1f,
                    ChunkOrigin = float3.zero,
                    Field = field,
                    Edits = edits,
                    Densities = densities
                };

                job.Run();

                Assert.AreEqual(0f, densities[0], 1e-5f);
                Assert.AreEqual(1f, densities[2], 1e-5f);
                Assert.AreEqual(1f, densities[7], 1e-5f);
            }
            finally
            {
                densities.Dispose();
                edits.Dispose();
            }
        }

        [Test]
        public void ChunkDensityJob_RespectsSubtractEdit()
        {
            var field = new SDFTerrainField
            {
                BaseHeight = 1f,
                Amplitude = 0f,
                Frequency = 0f,
                NoiseValue = 0f
            };

            var edits = new NativeArray<SDFEdit>(1, Allocator.TempJob);
            try
            {
                edits[0] = SDFEdit.Create(new float3(0.5f, 0.5f, 0.5f), 1.5f, SDFEditOperation.Subtract);

                var densities = new NativeArray<float>(8, Allocator.TempJob);
                try
                {
                    var job = new TerrainChunkDensitySamplingJob
                    {
                        Resolution = new int3(2, 2, 2),
                        VoxelSize = 1f,
                        ChunkOrigin = float3.zero,
                        Field = field,
                        Edits = edits,
                        Densities = densities
                    };

                    job.Run();

                    Assert.Greater(densities[1], 0f, "Subtract edit should carve solid voxels into empty space");
                    Assert.Greater(densities[7], 0f, "Far voxels remain positive (air)");
                }
                finally
                {
                    densities.Dispose();
                }
            }
            finally
            {
                edits.Dispose();
            }
        }
    }
}
