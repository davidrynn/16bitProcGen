#if UNITY_EDITOR
using DOTS.Terrain;
using Unity.Mathematics;
using UnityEngine;

/// Throw-away Phase A diagnostic. Draws height-coloured spheres in the Scene view.
/// DELETE after Phase A is accepted.
[ExecuteAlways]
public class TerrainHeightDebugGizmos : MonoBehaviour
{
    [Header("Sampling Grid")]
    public int   GridSize    = 20;
    public float GridSpacing = 3f;
    public float BaseHeight  = 0f;

    [Header("Noise Settings — match TerrainBootstrapAuthoring inspector")]
    public uint  WorldSeed              = 12345u;
    public float ElevationLowFrequency  = 0.004f;
    public float ElevationLowAmplitude  = 5.0f;
    public float ElevationMidFrequency  = 0.018f;
    public float ElevationMidAmplitude  = 1.2f;
    public float ElevationHighFrequency = 0.07f;
    public float ElevationHighAmplitude = 0.25f;
    public float ElevationExponent      = 1.6f;

    private void OnDrawGizmos()
    {
        var settings = new TerrainFieldSettings
        {
            BaseHeight             = BaseHeight,
            ElevationLowFrequency  = ElevationLowFrequency,
            ElevationLowAmplitude  = ElevationLowAmplitude,
            ElevationMidFrequency  = ElevationMidFrequency,
            ElevationMidAmplitude  = ElevationMidAmplitude,
            ElevationHighFrequency = ElevationHighFrequency,
            ElevationHighAmplitude = ElevationHighAmplitude,
            ElevationExponent      = ElevationExponent,
        };

        float maxAmp = ElevationLowAmplitude + ElevationMidAmplitude + ElevationHighAmplitude;
        var origin = (float3)transform.position;

        for (int z = 0; z < GridSize; z++)
        for (int x = 0; x < GridSize; x++)
        {
            var worldXZ = origin + new float3(x * GridSpacing, 0f, z * GridSpacing);
            var height = SampleHeight(worldXZ.x, worldXZ.z, settings);

            float t = math.saturate((height - BaseHeight + maxAmp) / (2f * maxAmp));
            Gizmos.color = Color.Lerp(Color.blue, Color.Lerp(Color.green, Color.white, t), t);
            Gizmos.DrawSphere(new Vector3(worldXZ.x, height, worldXZ.z), 0.3f);
        }
    }

    private float SampleHeight(float x, float z, TerrainFieldSettings s)
    {
        float yLow = s.BaseHeight - 20f, yHigh = s.BaseHeight + 20f;
        for (int i = 0; i < 16; i++)
        {
            float yMid = (yLow + yHigh) * 0.5f;
            if (SDFMath.SdLayeredGround(new float3(x, yMid, z), s, WorldSeed) < 0f)
                yLow = yMid;
            else
                yHigh = yMid;
        }
        return (yLow + yHigh) * 0.5f;
    }
}
#endif
