using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Biome
{
    public BiomeType biomeType;

    // Noise properties for this biome
    public float noiseScale = 0.1f;
    public float heightMultiplier = 5f;
    public float noiseOffsetX = 0f;
    public float noiseOffsetZ = 0f;

    // Terrain probabilities based on height
    [Serializable]
    public struct TerrainProbability
    {
        public TerrainType terrainType;
        public float minHeight;
        public float maxHeight;
        public float probability; // Chance of appearing at this height range
    }

    public List<TerrainProbability> terrainChances = new List<TerrainProbability>();

    public TerrainType GetTerrainType(float height)
    {
        float roll = UnityEngine.Random.value; // 0 to 1 random chance

        foreach (var entry in terrainChances)
        {
            if (height >= entry.minHeight && height <= entry.maxHeight)
            {
                if (roll <= entry.probability)
                    return entry.terrainType;
            }
        }

        return terrainChances[0].terrainType; // Default to first terrain type if no match
    }
}
