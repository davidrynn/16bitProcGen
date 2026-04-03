using NUnit.Framework;
using DOTS.Terrain.Rendering;

namespace DOTS.Terrain.Tests
{
    [TestFixture]
    public class TerrainChunkGrassSurfaceTests
    {
        [Test]
        public void Default_HasFullDensity()
        {
            Assert.AreEqual(1f, TerrainChunkGrassSurface.Default.Density);
        }

        [Test]
        public void Default_BiomeTypeIdIsZero()
        {
            Assert.AreEqual(0, TerrainChunkGrassSurface.Default.BiomeTypeId);
        }

        [Test]
        public void Default_GrassTypeIsStandardBlades()
        {
            Assert.AreEqual(0, TerrainChunkGrassSurface.Default.GrassType,
                "GrassType 0 = standard instanced blades; other values are reserved.");
        }
    }
}
