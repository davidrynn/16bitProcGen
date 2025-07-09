using Unity.Mathematics;

/// <summary>
/// Data structure for passing terrain chunk information to compute shader
/// Shared between C# systems and compute shaders
/// </summary>
[System.Serializable]
public struct TerrainChunkData
{
    public float3 position;
    public int resolution;
    public float worldScale;
    public float time;
    public float biomeScale;
    public float noiseScale;
    public float heightMultiplier;
    public float2 noiseOffset;
}
