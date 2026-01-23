using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SDFTerrainFieldTests
    {
        [Test]
        public void Sample_NoEdits_RespectsGroundHeight()
        {
            var field = new SDFTerrainField
            {
                BaseHeight = 5f,
                Amplitude = 1f,
                Frequency = 0f,
                NoiseValue = 0f
            };

            var edits = new NativeArray<SDFEdit>(0, Allocator.Temp);
            try
            {
                var below = field.Sample(new float3(0f, 4f, 0f), edits);
                var above = field.Sample(new float3(0f, 6f, 0f), edits);

                Assert.Less(below, 0f, "Points below the ground should be solid (negative)");
                Assert.Greater(above, 0f, "Points above the ground should be air (positive)");
            }
            finally
            {
                edits.Dispose();
            }
        }

        [Test]
        public void Sample_SubtractEdit_CreatesHollowRegion()
        {
            var field = new SDFTerrainField
            {
                BaseHeight = 5f,
                Amplitude = 0f,
                Frequency = 0f,
                NoiseValue = 0f
            };

            var edits = new NativeArray<SDFEdit>(1, Allocator.Temp);
            try
            {
                edits[0] = SDFEdit.Create(new float3(0f, 5f, 0f), 2f, SDFEditOperation.Subtract);

                var density = field.Sample(new float3(0f, 5f, 0f), edits);
                Assert.Greater(density, 0f, "Subtract edit should carve empty space");
            }
            finally
            {
                edits.Dispose();
            }
        }

        [Test]
        public void Sample_AddEdit_CreatesBump()
        {
            var field = new SDFTerrainField
            {
                BaseHeight = 5f,
                Amplitude = 0f,
                Frequency = 0f,
                NoiseValue = 0f
            };

            var edits = new NativeArray<SDFEdit>(1, Allocator.Temp);
            try
            {
                edits[0] = SDFEdit.Create(new float3(0f, 5f, 0f), 2f, SDFEditOperation.Add);

                var density = field.Sample(new float3(0f, 6f, 0f), edits);
                Assert.Less(density, 0f, "Add edit should extend solid terrain");
            }
            finally
            {
                edits.Dispose();
            }
        }
    }
}
