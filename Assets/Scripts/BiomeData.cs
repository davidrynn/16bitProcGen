using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NewBiome", menuName = "Biomes/Create New Biome")]
public class BiomeData : ScriptableObject
{
    public BiomeType biomeType;

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

                height = voronaiNoise.Generate(adjustedX, adjustedZ); // Custom implementation
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
}
