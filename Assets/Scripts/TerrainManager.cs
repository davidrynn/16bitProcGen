using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainManager : MonoBehaviour
{
    public static TerrainManager Instance { get; private set; }
    public GameObject terrainChunkPrefab;
    public int chunkSize = 16;
    public int chunksX = 5;
    public int chunksZ = 5;
    public float worldScale = 2f;

    private Dictionary<Vector2, TerrainChunk> chunks = new Dictionary<Vector2, TerrainChunk>();
    private float[,] heightMap;
    private List<Biome> biomes;
    private Dictionary<TerrainType, Texture2D> terrainTextures = new Dictionary<TerrainType, Texture2D>();
    private BiomeManager biomeManager;

    public void Initialize(BiomeManager biomeManager)
    {
        this.biomeManager = biomeManager;
        GenerateHeightMap();
        GenerateTerrain();
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
        GenerateHeightMap();
        GenerateTerrain();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R)) // Press 'R' to regenerate
        {
            GenerateHeightMap();
            GenerateTerrain();
        }
    }

    private void GenerateHeightMap()
    {
        heightMap = new float[chunksX * chunkSize + 1, chunksZ * chunkSize + 1];

        for (int z = 0; z <= chunksZ * chunkSize; z++)
        {
            for (int x = 0; x <= chunksX * chunkSize; x++)
            {
                Vector2 worldPos = new Vector2(x, z);
                BiomeData biome = DetermineBiome(worldPos);
                float noiseValue = Mathf.PerlinNoise(
                    (x + biome.noiseOffsetX) * biome.noiseScale,
                    (z + biome.noiseOffsetZ) * biome.noiseScale
                );

                heightMap[x, z] = noiseValue * biome.heightMultiplier;
            }
        }
    }

    private void GenerateTerrainTextures()
    {
        terrainTextures[TerrainType.Grass] = GenerateSolidColorTexture(Color.green, 16, 16);
        terrainTextures[TerrainType.Snow] = GenerateSolidColorTexture(Color.white, 16, 16);
        terrainTextures[TerrainType.Ice] = GenerateSolidColorTexture(new Color(0.7f, 0.9f, 1f), 16, 16);
        terrainTextures[TerrainType.Water] = GenerateSolidColorTexture(Color.blue, 16, 16);
        terrainTextures[TerrainType.Sand] = GenerateSolidColorTexture(new Color(0.9f, 0.8f, 0.5f), 16, 16);
        terrainTextures[TerrainType.Rock] = GenerateSolidColorTexture(Color.gray, 16, 16);
        terrainTextures[TerrainType.Mud] = GenerateSolidColorTexture(new Color(0.5f, 0.3f, 0.1f), 16, 16);
        terrainTextures[TerrainType.Lava] = GenerateSolidColorTexture(Color.red, 16, 16);
    }

    private BiomeData DetermineBiome(Vector2 position)
    {
        float noiseValue = Mathf.PerlinNoise(position.x * 0.01f, position.y * 0.01f);

        if (noiseValue < 0.2f) return biomeManager.GetBiome(BiomeType.Swamp);
        if (noiseValue < 0.4f) return biomeManager.GetBiome(BiomeType.Desert);
        if (noiseValue < 0.7f) return biomeManager.GetBiome(BiomeType.Forest);
        return biomeManager.GetBiome(BiomeType.Mountains);
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
                Vector2 chunkPos = new Vector2(x * chunkSize, z * chunkSize);
                BiomeData biome = DetermineBiome(chunkPos);

                GameObject newChunk = Instantiate(terrainChunkPrefab, new Vector3(chunkPos.x, 0, chunkPos.y), Quaternion.identity);
                TerrainChunk terrainChunk = newChunk.GetComponent<TerrainChunk>();

                if (terrainChunk != null)
                {
                    terrainChunk.width = chunkSize;
                    terrainChunk.depth = chunkSize;
                    terrainChunk.GenerateChunk(chunkPos, heightMap, terrainTextures, biome);
                    chunks[chunkPos] = terrainChunk;
                }
            }
        }
    }
}
