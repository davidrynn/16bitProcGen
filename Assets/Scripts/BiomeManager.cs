using System.Collections.Generic;
using UnityEngine;

public class BiomeManager : MonoBehaviour
{
    [SerializeField] private List<BiomeData> biomes;

    public BiomeData GetBiome(BiomeType type)
    {
        return biomes.Find(b => b.biomeType == type);
    }

    public List<BiomeData> GetAllBiomes()
    {
        return biomes;
    }
}
