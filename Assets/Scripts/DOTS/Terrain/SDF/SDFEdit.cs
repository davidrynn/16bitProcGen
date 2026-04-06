using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    public enum SDFEditShape : byte
    {
        Sphere = 0,
        Box = 1
    }

    public static class SDFEditOperation
    {
        public const int Add = 1;
        public const int Subtract = -1;
    }

    /// <summary>
    /// Describes a single additive or subtractive SDF brush application.
    /// </summary>
    public struct SDFEdit : IBufferElementData
    {
        public float3 Center;
        public float Radius;
        public float3 HalfExtents;
        public SDFEditShape Shape;
        public int Operation;

        public static SDFEdit Create(float3 center, float radius, int operation)
        {
            return new SDFEdit
            {
                Center = center,
                Radius = radius,
                HalfExtents = new float3(math.max(radius, 1e-5f)),
                Shape = SDFEditShape.Sphere,
                Operation = operation
            };
        }

        public static SDFEdit CreateBox(float3 center, float3 halfExtents, int operation)
        {
            var safeHalfExtents = math.max(new float3(1e-5f), halfExtents);

            return new SDFEdit
            {
                Center = center,
                Radius = math.cmax(safeHalfExtents),
                HalfExtents = safeHalfExtents,
                Shape = SDFEditShape.Box,
                Operation = operation
            };
        }
    }
}
