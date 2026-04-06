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
                var editDistance = ComputeEditDistance(worldPos, in edit);

                if (edit.Operation == SDFEditOperation.Subtract)
                {
                    density = SDFMath.OpSubtraction(density, editDistance);
                }
                else
                {
                    density = SDFMath.OpUnion(density, editDistance);
                }
            }

            return density;
        }

        private static float ComputeEditDistance(float3 worldPos, in SDFEdit edit)
        {
            var offset = worldPos - edit.Center;
            if (edit.Shape == SDFEditShape.Box)
            {
                var halfExtents = math.max(edit.HalfExtents, new float3(1e-5f));
                return SDFMath.SdBox(offset, halfExtents);
            }

            return SDFMath.SdSphere(offset, edit.Radius);
        }
    }
}
