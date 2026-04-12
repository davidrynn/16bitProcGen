using NUnit.Framework;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class SDFMathTests
    {
        [Test]
        public void SdSphere_ReturnsExpectedSignedDistances()
        {
            var radius = 2f;
            var inside = SDFMath.SdSphere(float3.zero, radius);
            var surface = SDFMath.SdSphere(new float3(radius, 0f, 0f), radius);
            var outside = SDFMath.SdSphere(new float3(radius + 1f, 0f, 0f), radius);

            Assert.Less(inside, 0f, "Points inside the sphere should report negative distance");
            Assert.AreEqual(0f, surface, 1e-5f, "Points on the surface should report zero distance");
            Assert.Greater(outside, 0f, "Points outside the sphere should report positive distance");
        }

        [Test]
        public void SdBox_HandlesInteriorAndExteriorPoints()
        {
            var halfExtents = new float3(1f, 2f, 3f);

            var inside = SDFMath.SdBox(new float3(0.5f, 1f, 1.5f), halfExtents);
            var surface = SDFMath.SdBox(new float3(1f, 2f, 3f), halfExtents);
            var outside = SDFMath.SdBox(new float3(2f, 4f, 6f), halfExtents);

            Assert.Less(inside, 0f, "Inside sample should be negative");
            Assert.AreEqual(0f, surface, 1e-5f, "Surface sample should be zero");
            Assert.Greater(outside, 0f, "Outside sample should be positive");
        }

        [Test]
        public void SdGround_ComputesSignedHeightRelativeToSurface()
        {
            const float amplitude = 5f;
            const float frequency = 0.25f;
            const float baseHeight = 10f;
            const float noiseValue = 0.2f;

            var sampleBelow = SDFMath.SdGround(new float3(0f, 8f, 0f), amplitude, frequency, baseHeight, noiseValue);
            var sampleAbove = SDFMath.SdGround(new float3(0f, 15f, 0f), amplitude, frequency, baseHeight, noiseValue);

            Assert.Less(sampleBelow, 0f, "Points below the ground plane should be negative");
            Assert.Greater(sampleAbove, 0f, "Points above the ground plane should be positive");
        }

        [Test]
        public void BooleanOps_FollowSpec()
        {
            Assert.AreEqual(-2f, SDFMath.OpUnion(-2f, 1f));
            Assert.AreEqual(3f, SDFMath.OpUnion(3f, 5f));

            Assert.AreEqual(2f, SDFMath.OpSubtraction(2f, -5f));
            Assert.AreEqual(4f, SDFMath.OpSubtraction(4f, 2f));
        }
    }
}