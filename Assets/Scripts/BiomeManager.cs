using System.Collections.Generic;
using UnityEngine;

public class BiomeManager : MonoBehaviour
{
    [SerializeField] private List<BiomeData> biomes;

    public BiomeData GetBiome(BiomeType type)
    {
        BiomeData biomeData = biomes.Find(b => b.biomeType == type);
        if (biomeData == null)
        {
            Debug.LogError("Biome not found: " + type);
        }
        return biomeData;
    }

    public List<BiomeData> GetAllBiomes()
    {
        return biomes;
    }
}
