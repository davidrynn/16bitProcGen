using UnityEngine;

public class GameManager : MonoBehaviour
{
    private TerrainManager terrainManager;
    private BiomeManager biomeManager;
    private LegacyWeatherSystem weatherSystem;

    void Start()
    {
        // Find systems
        biomeManager = FindAnyObjectByType<BiomeManager>();
        terrainManager = FindAnyObjectByType<TerrainManager>();
        weatherSystem = FindAnyObjectByType<LegacyWeatherSystem>();

        // Initialize terrain manager with biome manager
        if (terrainManager != null && biomeManager != null)
        {
            terrainManager.Initialize(biomeManager);
        }
        else
        {
            Debug.LogError("TerrainManager or BiomeManager not found!");
        }

        // Initialize weather system if present
        if (weatherSystem != null && biomeManager != null)
        {
            // Set initial biome for weather
            BiomeData defaultBiome = biomeManager.GetBiome(BiomeType.Plains);
            if (defaultBiome != null)
            {
                weatherSystem.SetBiome(defaultBiome);
            }
        }
    }
}
