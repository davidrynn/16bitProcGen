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
        if (biomeManager == null)
        {
            Debug.LogError("BiomeManager not initialized!");
            return;
        }

        GenerateTerrainTextures();
        CreateChunk(new Vector2(0, 0));
        GenerateTerrain();
    }

    void Update()
    {
        if (player == null)
        {
            Debug.LogError("Player reference is missing!");
            return;
        }

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
                    currentPlayerChunkCoord.x + xOffset,
                    currentPlayerChunkCoord.y + zOffset
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
        terrainTextures[TerrainType.Default] = GenerateGradientTexture(16, 16);

        foreach (TerrainType terrainType in Enum.GetValues(typeof(TerrainType)))
        {
            if (!terrainTextures.ContainsKey(terrainType))
            {
                Debug.LogError($"Missing texture for terrain type: {terrainType}");
            }
        }
    }

    private BiomeData DetermineBiome(Vector2 position)
    {
        if (biomeManager == null)
        {
            Debug.LogError("BiomeManager is null in DetermineBiome");
            return defaultBiome;
        }

        float highestWeight = 0f;
        BiomeData selectedBiome = defaultBiome;

        foreach (BiomeData biome in biomeManager.GetAllBiomes())
        {
            float biomeNoise = Mathf.PerlinNoise(
                position.x * biome.biomeScale / worldScale,
                position.y * biome.biomeScale / worldScale
            );

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
        if (biomeManager == null)
        {
            Debug.LogError("BiomeManager is null in DetermineBiomeWeights");
            return new Dictionary<BiomeData, float> { { defaultBiome, 1.0f } };
        }

        Dictionary<BiomeData, float> biomeWeights = new Dictionary<BiomeData, float>();
        float totalWeight = 0f;

        foreach (BiomeData biome in biomeManager.GetAllBiomes())
        {
            float noiseValue = Mathf.PerlinNoise(
                position.x * biome.biomeScale / worldScale,
                position.y * biome.biomeScale / worldScale
            );
            biomeWeights.Add(biome, noiseValue);
            totalWeight += noiseValue;
        }

        if (totalWeight > 0)
        {
            foreach (BiomeData biome in biomeManager.GetAllBiomes())
            {
                biomeWeights[biome] /= totalWeight;
            }
        }
        else
        {
            biomeWeights[defaultBiome] = 1.0f;
        }

        return biomeWeights;
    }

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
                Vector2 chunkPos = new Vector2(x, z);
                CreateChunk(chunkPos);
            }
        }
    }

    void CreateChunk(Vector2 chunkCoord)
    {
        if (terrainChunkPrefab == null)
        {
            Debug.LogError("TerrainChunk prefab is missing!");
            return;
        }

        Vector3 chunkPosition = new Vector3(
            chunkCoord.x * chunkSize,
            0,
            chunkCoord.y * chunkSize
        );

        GameObject chunkObj = Instantiate(terrainChunkPrefab, chunkPosition, Quaternion.identity);
        chunkObj.transform.parent = transform;

        BiomeData biome = DetermineBiome(chunkPosition);
        if (biome == null)
        {
            Debug.LogError($"No biome found for chunk at {chunkCoord}");
            biome = defaultBiome;
        }

        TerrainChunk chunk = chunkObj.GetComponent<TerrainChunk>();
        if (chunk != null)
        {
            chunk.width = chunkSize;
            chunk.depth = chunkSize;
            chunk.GenerateChunk(chunkCoord, terrainTextures, biome);
            chunks[chunkCoord] = chunk;
        }
        else
        {
            Debug.LogError($"TerrainChunk component missing on prefab at {chunkCoord}");
            Destroy(chunkObj);
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
}
