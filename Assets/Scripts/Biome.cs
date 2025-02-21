using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Biome
{
    public string name;
    public float minHeight;
    public float maxHeight;
    public float noiseThreshold;

    public Dictionary<TerrainType, Texture2D> textures = new Dictionary<TerrainType, Texture2D>();

    // Define which terrain types exist in this biome
    public TerrainType[] availableTerrainTypes;
}
