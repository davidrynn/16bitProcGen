using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

/// <summary>
/// [LEGACY] Biome component for the legacy DOTS terrain system.
/// Defines biome properties and generation parameters.
/// 
/// ⚠️ LEGACY COMPONENT: This component is part of the legacy terrain system using DOTS.Terrain.TerrainData.
/// The current active terrain system uses SDF (Signed Distance Fields) with SDFTerrainFieldSettings for terrain parameters,
/// and does not use BiomeComponent.
/// 
/// This component is maintained for backward compatibility with existing tests and legacy code.
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