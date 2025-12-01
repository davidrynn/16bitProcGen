using Unity.Burst;
using Unity.Mathematics;

namespace DOTS.Terrain.SDF
{
    /// <summary>
    /// Burst-safe collection of signed distance helpers used by the SDF terrain backend.
    /// </summary>
    [BurstCompile]
    public static class SDFMath
    {
        public static float SdSphere(float3 p, float radius)
        {
            var safeRadius = math.max(radius, 1e-5f);
            return math.length(p) - safeRadius;
        }

        public static float SdBox(float3 p, float3 halfExtents)
        {
            var q = math.abs(p) - halfExtents;
            var outside = math.max(q, float3.zero);
            var inside = math.min(math.max(q.x, math.max(q.y, q.z)), 0f);
            return math.length(outside) + inside;
        }

        public static float SdGround(float3 p, float amplitude, float frequency, float baseHeight, float noiseValue)
        {
            // Combine a sine-based undulation with the provided noise sample to form a simple height field.
            var wave = (math.sin(p.x * frequency) + math.sin(p.z * frequency)) * 0.5f;
            var height = baseHeight + amplitude * (wave + noiseValue);
            return p.y - height;
        }

        public static float OpUnion(float a, float b) => math.min(a, b);

        /// <summary>
        /// Spec-compliant subtraction that carves interior points but never pushes outside samples farther away.
        /// Exterior points keep their original base distance, while interior points use max(base, -edit).
        /// </summary>
        public static float OpSubtraction(float baseDistance, float editDistance)
        {
            var subtraction = math.max(baseDistance, -editDistance);
            return baseDistance > 0f ? math.min(baseDistance, subtraction) : subtraction;
        }
    }
}
