using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Biomes/Create New Biome")]
public class BiomeData : ScriptableObject
{
    [Header("Biome Identity")]
    public BiomeType biomeType;
    [Tooltip("Controls the biome size on the terrain. Larger values = larger biomes.")]
    public float biomeScale = 0.5f;

    [Header("Terrain Generation")]
    public NoiseType noiseType = NoiseType.Perlin;
    [Range(0.001f, 0.5f)]
    public float noiseScale = 0.1f;
    [Range(1f, 50f)]
    public float heightMultiplier = 5f;
    public Vector2 noiseOffset;

    [System.Serializable]
    public struct TerrainProbability
    {
        public TerrainType terrainType;
        public float minHeight;
        public float maxHeight;
        [Range(0f, 1f)]
        public float probability;
    }

    [Header("Terrain Mapping")]
    public List<TerrainProbability> terrainChances = new List<TerrainProbability>();

    [Header("Weather (Optional)")]
    [Tooltip("Default weather for this biome")]
    public SimpleWeatherType defaultWeather = SimpleWeatherType.Clear;
    [Tooltip("Chance of weather changing (0-1)")]
    [Range(0f, 1f)]
    public float weatherChangeChance = 0.1f;

    // Core terrain generation
    public float GenerateHeight(float x, float z)
    {
        float height = 0f;
        float adjustedX = (x + noiseOffset.x) * noiseScale;
        float adjustedZ = (z + noiseOffset.y) * noiseScale;

        switch (noiseType)
        {
            case NoiseType.Perlin:
                height = Mathf.PerlinNoise(adjustedX, adjustedZ);
                break;
            case NoiseType.Cellular:
                INoiseFunction cellularNoise = new CellularNoise(noiseScale);
                height = cellularNoise.Generate(adjustedX, adjustedZ);
                break;
            case NoiseType.Voronoi:
                INoiseFunction voronoiNoise = new VoronoiNoise(noiseScale);
                height = voronoiNoise.Generate(adjustedX, adjustedZ);
                break;
            default:
                height = Mathf.PerlinNoise(adjustedX, adjustedZ);
                break;
        }

        return height * heightMultiplier;
    }

    public TerrainType GetTerrainType(float height)
    {
        float roll = Random.value;
        foreach (var entry in terrainChances)
        {
            if (height >= entry.minHeight && height <= entry.maxHeight)
            {
                if (roll <= entry.probability)
                    return entry.terrainType;
            }
        }
        
        // Fallback to first terrain type if no match
        return terrainChances.Count > 0 ? terrainChances[0].terrainType : TerrainType.Default;
    }

    // Validate the biome data
    private void OnValidate()
    {
        // Ensure we have at least one terrain chance
        if (terrainChances.Count == 0)
        {
            terrainChances.Add(new TerrainProbability
            {
                terrainType = TerrainType.Grass,
                minHeight = 0f,
                maxHeight = 10f,
                probability = 1f
            });
        }
    }
} 