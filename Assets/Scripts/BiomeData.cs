using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Biomes/Create New Biome")]
public class BiomeData : ScriptableObject
{
    public BiomeType biomeType;

    // Noise properties for this biome
    public float noiseScale = 0.1f;
    public float heightMultiplier = 5f;
    public float noiseOffsetX = 0f;
    public float noiseOffsetZ = 0f;

    [System.Serializable]
    public struct TerrainProbability
    {
        public TerrainType terrainType;
        public float minHeight;
        public float maxHeight;
        public float probability;
    }

    public List<TerrainProbability> terrainChances = new List<TerrainProbability>();

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
        return terrainChances[0].terrainType;
    }
}
