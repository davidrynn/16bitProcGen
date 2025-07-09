using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// Biome component for DOTS terrain system
/// Defines biome properties and generation parameters
/// </summary>
public struct BiomeComponent : IComponentData
{
    public BiomeType biomeType;
    public float biomeScale;
    public NoiseType noiseType;
    public float noiseScale;
    public float heightMultiplier;
    public float2 noiseOffset;
    public BlobAssetReference<BiomeTerrainData> terrainData;
}

/// <summary>
/// Terrain data specific to a biome
/// Contains terrain type probabilities and generation rules
/// </summary>
public struct BiomeTerrainData
{
    public BlobArray<TerrainProbability> terrainChances;
}

/// <summary>
/// Probability data for terrain types within a biome
/// </summary>
public struct TerrainProbability
{
    public TerrainType terrainType;
    public float minHeight;
    public float maxHeight;
    public float probability;
} 