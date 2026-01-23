using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain
{
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
        public int Operation;

        public static SDFEdit Create(float3 center, float radius, int operation)
        {
            return new SDFEdit
            {
                Center = center,
                Radius = radius,
                Operation = operation
            };
        }
    }
}
