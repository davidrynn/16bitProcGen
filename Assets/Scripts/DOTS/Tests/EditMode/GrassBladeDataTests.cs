using System.Runtime.InteropServices;
using NUnit.Framework;
using DOTS.Terrain.Rendering;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Validates the memory layout of <see cref="GrassBladeData"/> against the layout
    /// declared in GrassBlades.shader's <c>GrassBladeData</c> HLSL struct.
    ///
    /// If these tests fail after a struct change, update the shader struct to match
    /// BEFORE rebuilding blade buffers — a mismatch causes silent GPU data corruption.
    /// </summary>
    [TestFixture]
    public class GrassBladeDataTests
    {
        [Test]
        public void GrassBladeData_StrideIs32Bytes()
        {
            int stride = Marshal.SizeOf<GrassBladeData>();
            Assert.AreEqual(32, stride,
                "Shader StructuredBuffer stride must be 32 bytes. Update GrassBlades.shader if the struct changes.");
        }

        [Test]
        public void GrassBladeData_WorldPosition_AtOffset0()
        {
            int offset = (int)Marshal.OffsetOf<GrassBladeData>(nameof(GrassBladeData.WorldPosition));
            Assert.AreEqual(0, offset);
        }

        [Test]
        public void GrassBladeData_Height_AtOffset12()
        {
            int offset = (int)Marshal.OffsetOf<GrassBladeData>(nameof(GrassBladeData.Height));
            Assert.AreEqual(12, offset);
        }

        [Test]
        public void GrassBladeData_ColorTint_AtOffset16()
        {
            int offset = (int)Marshal.OffsetOf<GrassBladeData>(nameof(GrassBladeData.ColorTint));
            Assert.AreEqual(16, offset);
        }

        [Test]
        public void GrassBladeData_FacingAngle_AtOffset28()
        {
            int offset = (int)Marshal.OffsetOf<GrassBladeData>(nameof(GrassBladeData.FacingAngle));
            Assert.AreEqual(28, offset);
        }
    }
}
