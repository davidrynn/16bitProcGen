using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Biomes/Create New Biome")]
public class BiomeData : ScriptableObject
{
    public BiomeType biomeType;
    [Tooltip("Controls the biome size on the terrain. Larger values = larger biomes.")]
    public float biomeScale = 0.5f;

    [Header("Biome Properties")]
    [Tooltip("Base temperature of the biome in Celsius")]
    public float baseTemperature = 20f;
    [Tooltip("How much the temperature can vary from the base temperature")]
    public float temperatureVariation = 10f;
    [Tooltip("Humidity level of the biome (0-1)")]
    [Range(0f, 1f)]
    public float humidity = 0.5f;
    [Tooltip("Base wind speed in the biome")]
    public float windSpeed = 5f;
    [Tooltip("How quickly weather can change in this biome")]
    public float weatherChangeFrequency = 1f;
    [Tooltip("Ambient light color for this biome")]
    public Color ambientLight = Color.white;
    [Tooltip("Base fog density for this biome")]
    [Range(0f, 1f)]
    public float fogDensity = 0.1f;

    [Header("Weather Settings")]
    [Tooltip("Possible weather types that can occur in this biome")]
    public List<WeatherType> possibleWeatherTypes = new List<WeatherType>();
    [Tooltip("Probability weights for each weather type (should match possibleWeatherTypes)")]
    public List<float> weatherProbabilities = new List<float>();

    [Header("Noise Settings")]
    public NoiseType noiseType;
    [Range(0.001f, 0.5f)]
    public float noiseScale = 0.1f;
    /* Scale/Height Multiplier for
     * 
    * Perlin
    * 1-5	    Subtle, gently rolling hills	Plains, deserts, gentle slopes
    * 5-15	    Noticeable elevation changes	Forests, foothills, mild mountains
    * 15-30	Large mountains, steep terrain	Mountain ranges, rugged terrain
    * 30-50+	Extreme terrain, sharp cliffs	Alien worlds, fantasy landscapes
    * 
    * Cellular
    * noiseScale	0.02–0.2	Controls cell size:
    * Small values (0.02–0.05) = large, visible cells.
    * Larger values (0.1–0.2) = smaller, more frequent cells.
    * heightMultiplier	2–25	Controls vertical exaggeration:
    *  Lower values = subtle cells (like gentle undulations).
    *  Higher values = pronounced, stark edges.
    *  Swamp Biome (subtle cells):

       noiseScale: 0.03
       heightMultiplier: 3–7

   Alien/Fantasy Biome (dramatic cell edges):
    * 
    * Voronai
    * 
       noiseScale: 0.08–0.15
       heightMultiplier: 15–25
     Recommended Ranges for Voronoi Noise:
Parameter	Practical Range	Typical Appearance
noiseScale	0.02–0.3	Controls cell size:
Smaller values (0.02–0.08) = larger polygonal shapes.
Higher values (0.1–0.3) = finer polygonal details.
heightMultiplier	5–40	Controls vertical sharpness:
Low values = subtle polygonal hills.
High values = dramatic cliffs, sharp polygonal peaks.
🔹 Recommended Example Values:

    Crystalline or Rocky Biome (sharp polygons):
        noiseScale: 0.1–0.2
        heightMultiplier: 20–40
    Desert/Mesa Biome (subtle polygonal mesas):
        noiseScale: 0.03–0.06
        heightMultiplier: 5–15
    */
    [Range(1f, 50f)]
    public float heightMultiplier = 5f;
    public Vector2 noiseOffset;

    [Header("Biome Transition")]
    [Tooltip("How smoothly this biome blends with others (0-1)")]
    [Range(0f, 1f)]
    public float transitionSmoothness = 0.5f;
    [Tooltip("Additional noise scale for biome transitions")]
    public float transitionNoiseScale = 0.2f;

    [System.Serializable]
    public struct TerrainProbability
    {
        public TerrainType terrainType;
        public float minHeight;
        public float maxHeight;
        [Range(0f, 1f)]
        public float probability;
    }

    public List<TerrainProbability> terrainChances = new List<TerrainProbability>();

    // Generate height based on noise type
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
                INoiseFunction voronaiNoise = new VoronoiNoise(noiseScale);
                height = voronaiNoise.Generate(adjustedX, adjustedZ);
                break;

            case NoiseType.Test:
                INoiseFunction testNoise = new TestNoise(noiseScale);
                height = testNoise.Generate(adjustedX, adjustedZ);
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
        return terrainChances[0].terrainType;
    }

    // Get current temperature with variation
    public float GetCurrentTemperature()
    {
        float timeBasedNoise = Mathf.PerlinNoise(Time.time * 0.1f, 0) * 2 - 1;
        return baseTemperature + (timeBasedNoise * temperatureVariation);
    }

    // Get current humidity with variation
    public float GetCurrentHumidity()
    {
        float timeBasedNoise = Mathf.PerlinNoise(Time.time * 0.05f, 100) * 0.2f;
        return Mathf.Clamp01(humidity + timeBasedNoise);
    }

    // Get current wind speed with variation
    public float GetCurrentWindSpeed()
    {
        float timeBasedNoise = Mathf.PerlinNoise(Time.time * 0.2f, 200) * 2 - 1;
        return Mathf.Max(0, windSpeed + (timeBasedNoise * windSpeed * 0.3f));
    }

    // Get transition factor between this biome and another
    public float GetTransitionFactor(Vector2 position, BiomeData otherBiome)
    {
        float transitionNoise = Mathf.PerlinNoise(
            position.x * transitionNoiseScale,
            position.y * transitionNoiseScale
        );
        return Mathf.Lerp(0, 1, transitionNoise * transitionSmoothness);
    }

    // Get weather probability for a specific weather type
    public float GetWeatherProbability(WeatherType weatherType)
    {
        int index = possibleWeatherTypes.IndexOf(weatherType);
        if (index >= 0 && index < weatherProbabilities.Count)
        {
            return weatherProbabilities[index];
        }
        return 0f;
    }

    // Validate the biome data
    private void OnValidate()
    {
        // Ensure weather probabilities match possible weather types
        while (weatherProbabilities.Count < possibleWeatherTypes.Count)
        {
            weatherProbabilities.Add(1f);
        }
        while (weatherProbabilities.Count > possibleWeatherTypes.Count)
        {
            weatherProbabilities.RemoveAt(weatherProbabilities.Count - 1);
        }

        // Normalize weather probabilities
        float total = 0f;
        foreach (float prob in weatherProbabilities)
        {
            total += prob;
        }
        if (total > 0)
        {
            for (int i = 0; i < weatherProbabilities.Count; i++)
            {
                weatherProbabilities[i] /= total;
            }
        }
    }
}
