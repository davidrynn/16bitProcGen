using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainManager : MonoBehaviour
{
    public static TerrainManager Instance { get; private set; }
    public Transform player;
    public GameObject terrainChunkPrefab;
    public int chunkSize = 16;
    public int viewDistance = 4;
    public int chunksX = 5;
    public int chunksZ = 5;
    public float worldScale = 2f;
    public BiomeData defaultBiome;

    private Dictionary<Vector2, TerrainChunk> chunks = new Dictionary<Vector2, TerrainChunk>();
   // private float[,] heightMap;
    private List<Biome> biomes;
    private Dictionary<TerrainType, Texture2D> terrainTextures = new Dictionary<TerrainType, Texture2D>();
    private BiomeManager biomeManager;
    private Vector2 currentPlayerChunkCoord;
    private Vector2 playerLastChunkCoord;

    public void Initialize(BiomeManager biomeManager)
    {
        this.biomeManager = biomeManager;
        this.defaultBiome = biomeManager.GetBiome(BiomeType.Plains);
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        GenerateTerrainTextures();
        CreateChunk(new Vector2(0, 0));

        //   GenerateHeightMap();
        GenerateTerrain();
    }

    void Update()
    {
        currentPlayerChunkCoord = new Vector2(
            Mathf.FloorToInt(player.position.x / chunkSize),
            Mathf.FloorToInt(player.position.z / chunkSize)
            );
        if (currentPlayerChunkCoord != playerLastChunkCoord)
        {
            playerLastChunkCoord = currentPlayerChunkCoord;
            UpdateVisibleChunks();
        }

    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunks = new HashSet<Vector2>();

        for (int zOffset = -viewDistance; zOffset <= viewDistance; zOffset++)
        {
            for (int xOffset = -viewDistance; xOffset <= viewDistance; xOffset++)
            {
                Vector2 viewedChunkCoord = new Vector2(
                    Mathf.FloorToInt(currentPlayerChunkCoord.x + xOffset),
                    Mathf.FloorToInt(currentPlayerChunkCoord.y + zOffset)
                );

                alreadyUpdatedChunks.Add(viewedChunkCoord);

                if (!chunks.ContainsKey(viewedChunkCoord))
                {
                    CreateChunk(viewedChunkCoord);
                }
                else
                {
                    chunks[viewedChunkCoord].SetVisible(true);
                }
            }
        }

        // Hide chunks outside view distance
        foreach (Vector2 coord in new List<Vector2>(chunks.Keys))
        {
            if (!alreadyUpdatedChunks.Contains(coord))
            {
                chunks[coord].SetVisible(false);
            }
        }
    }

    //private void GenerateHeightMap()
    //{
    //    heightMap = new float[chunksX * chunkSize + 1, chunksZ * chunkSize + 1];

    //    for (int z = 0; z <= chunksZ * chunkSize; z++)
    //    {
    //        for (int x = 0; x <= chunksX * chunkSize; x++)
    //        {
    //            Vector2 worldPos = new Vector2(x, z);
    //            BiomeData biome = DetermineBiome(worldPos);
    //            if (biome == null)
    //            {
    //                Debug.LogError("No biome found for position " + worldPos);
    //                continue;
    //            };
    //            heightMap[x, z] = biome.GenerateHeight(worldPos.x, worldPos.y);
    //        }
    //    }
    //}


    private void GenerateTerrainTextures()
    {
        terrainTextures = new Dictionary<TerrainType, Texture2D>();

        terrainTextures[TerrainType.Grass] = GenerateSolidColorTexture(Color.green, 16, 16);
        terrainTextures[TerrainType.Sand] = GenerateSolidColorTexture(new Color(0.9f, 0.8f, 0.5f), 16, 16);
        terrainTextures[TerrainType.Rock] = GenerateSolidColorTexture(Color.gray, 16, 16);
        terrainTextures[TerrainType.Water] = GenerateSolidColorTexture(Color.blue, 16, 16);
        terrainTextures[TerrainType.Snow] = GenerateSolidColorTexture(Color.white, 16, 16);
        terrainTextures[TerrainType.Dirt] = GenerateSolidColorTexture(new Color(0.5f, 0.3f, 0.1f), 16, 16);
        terrainTextures[TerrainType.Ice] = GenerateSolidColorTexture(new Color(0.7f, 0.9f, 1f), 16, 16);
        terrainTextures[TerrainType.Mud] = GenerateSolidColorTexture(new Color(0.4f, 0.25f, 0.1f), 16, 16);
        terrainTextures[TerrainType.Lava] = GenerateSolidColorTexture(Color.red, 16, 16);
        terrainTextures[TerrainType.Flora] = GenerateSolidColorTexture(new Color(0f, 0.392f, 0f, 1f), 16, 16);
        terrainTextures[TerrainType.Chrystal] = GenerateSolidColorTexture(new Color(0.5f, 0f, 0.5f, 1f), 16, 16);
        // Set a default texture in case of missing assignment
        terrainTextures[TerrainType.Default] = GenerateGradientTexture(16, 16);

        // Debug check here:
        foreach (TerrainType terrainType in Enum.GetValues(typeof(TerrainType)))
        {
            if (!terrainTextures.ContainsKey(terrainType))
            {
                Debug.LogError($"Missing texture for terrain type: {terrainType}");
            }
            else
            {
                Debug.Log($"Texture assigned successfully for terrain type: {terrainType}");
            }
        }
    }

    public Texture2D GenerateGradientTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        Debug.Log("Generating gradient texture");
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                // ✅ Use both X and Y for the gradient effect
                float tX = (float)x / width;  // Normalized 0-1
                float tY = (float)y / height; // Normalized 0-1

                // ✅ Blend both axes for a smooth gradient
                float blendFactor = (tX + tY) / 2f; // Average both directions
                Color color = Color.Lerp(Color.blue, Color.green, blendFactor);

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return texture;
    }




    private BiomeData DetermineBiome(Vector2 position)
    {
        float highestWeight = 0f;
        BiomeData selectedBiome = null;

        foreach (BiomeData biome in biomeManager.GetAllBiomes())
        {
            //if(biome.biomeType == BiomeType.Test)
            //{
            //    selectedBiome = biome;
            //}
            float biomeNoise = Mathf.PerlinNoise(position.x * biome.biomeScale, position.y * biome.biomeScale);

            if (biomeNoise > highestWeight)
            {
                highestWeight = biomeNoise;
                selectedBiome = biome;
            }
        }

        return selectedBiome;
    }

    public Dictionary<BiomeData, float> DetermineBiomeWeights(Vector2 position)
    {
        Dictionary<BiomeData, float> biomeWeights = new Dictionary<BiomeData, float>();
        float totalWeight = 0f;

        foreach (BiomeData biome in biomeManager.GetAllBiomes())
        {
            float noiseValue = Mathf.PerlinNoise(position.x * biome.biomeScale, position.y * biome.biomeScale);
            biomeWeights.Add(biome, noiseValue);
            totalWeight += noiseValue;
        }

        // Normalize weights to sum up to 1
        foreach (BiomeData biome in biomeManager.GetAllBiomes())
        {
            biomeWeights[biome] /= totalWeight;
        }

        return biomeWeights;
    }

    //public Dictionary<BiomeData, float> DetermineBiomeWeights(Vector2 position)
    //{
    //    BiomeData testBiome = biomeManager.GetBiome(BiomeType.Test);
    //    return new Dictionary<BiomeData, float> { { testBiome, 1.0f } };
    //}





    private Texture2D GenerateSolidColorTexture(Color color, int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();
        return texture;
    }

    private void GenerateTerrain()
    {
        for (int x = 0; x < chunksX; x++)
        {
            for (int z = 0; z < chunksZ; z++)
            {
                Vector2 chunkPos = new Vector2(x * chunkSize, z * chunkSize);
                BiomeData biome = DetermineBiome(chunkPos);

                GameObject newChunk = Instantiate(terrainChunkPrefab, new Vector3(chunkPos.x, 0, chunkPos.y), Quaternion.identity);
                TerrainChunk terrainChunk = newChunk.GetComponent<TerrainChunk>();

                if (terrainChunk != null)
                {
                    terrainChunk.width = chunkSize;
                    terrainChunk.depth = chunkSize;
                    terrainChunk.GenerateChunk(chunkPos, terrainTextures, biome);
                    chunks[chunkPos] = terrainChunk;
                }
            }
        }
    }

    void CreateChunk(Vector2 chunkCoord)
    {
        Vector3 chunkPosition = new Vector3(chunkCoord.x * chunkSize, 0, chunkCoord.y * chunkSize);
        GameObject chunkObj = Instantiate(terrainChunkPrefab, chunkPosition, Quaternion.identity);
        BiomeData biome = DetermineBiome(chunkPosition);

        TerrainChunk chunk = chunkObj.GetComponent<TerrainChunk>();

        if (chunk != null)
        {
            chunk.width = chunkSize;
            chunk.depth = chunkSize;
            chunk.GenerateChunk(chunkCoord, terrainTextures, biome); // Pass chunkCoord correctly

            chunks.Add(chunkCoord, chunk);
        }
    }

}
