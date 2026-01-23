using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace DOTS.Terrain
{
    /// <summary>
    /// Burst-friendly terrain sampler that evaluates the ground SDF and applies runtime edits.
    /// </summary>
    [BurstCompile]
    public struct SDFTerrainField
    {
        public float BaseHeight;
        public float Amplitude;
        public float Frequency;
        public float NoiseValue;

        public float Sample(float3 worldPos, NativeArray<SDFEdit> edits)
        {
            var density = SDFMath.SdGround(worldPos, Amplitude, Frequency, BaseHeight, NoiseValue);

            if (!edits.IsCreated || edits.Length == 0)
            {
                return density;
            }

            for (var i = 0; i < edits.Length; i++)
            {
                var edit = edits[i];
                var sphere = SDFMath.SdSphere(worldPos - edit.Center, edit.Radius);

                if (edit.Operation == SDFEditOperation.Subtract)
                {
                    density = SDFMath.OpSubtraction(density, sphere);
                }
                else
                {
                    density = SDFMath.OpUnion(density, sphere);
                }
            }

            return density;
        }
    }
}
