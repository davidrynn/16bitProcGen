using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace DOTS.Terrain.Rendering
{
    /// <summary>
    /// Per-blade instance data uploaded to a <see cref="UnityEngine.GraphicsBuffer"/> and read
    /// by the GrassBlades vertex shader via <c>StructuredBuffer&lt;GrassBladeData&gt;</c>.
    ///
    /// LAYOUT CONTRACT — must stay in sync with the GrassBladeData struct in GrassBlades.shader:
    ///   offset  0 : WorldPosition (float3, 12 bytes)
    ///   offset 12 : Height        (float,  4 bytes)
    ///   offset 16 : ColorTint     (float3, 12 bytes)
    ///   offset 28 : FacingAngle   (float,  4 bytes)
    ///   total     : 32 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct GrassBladeData
    {
        /// <summary>Root position of the blade in world space.</summary>
        public float3 WorldPosition;

        /// <summary>Blade height in world units. Scales the blade mesh Y axis in the vertex shader.</summary>
        public float Height;

        /// <summary>Per-blade colour tint multiplied with the blade texture in the fragment shader.</summary>
        public float3 ColorTint;

        /// <summary>Random Y-axis rotation added on top of camera-facing billboard (radians, 0..2π).</summary>
        public float FacingAngle;
    }
}
