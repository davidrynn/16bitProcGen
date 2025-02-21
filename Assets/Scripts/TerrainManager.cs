using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainManager : MonoBehaviour
{
    public GameObject terrainChunkPrefab;
    public int chunkSize = 16;
    public int chunksX = 5;
    public int chunksZ = 5;
    public float noiseScale = 0.1f;
    public float heightScale = 5f;

    private Dictionary<Vector2, TerrainChunk> chunks = new Dictionary<Vector2, TerrainChunk>();
    private float[,] heightMap;
    public Dictionary<TerrainType, Texture2D> terrainTextures = new Dictionary<TerrainType, Texture2D>();
    public Dictionary<BiomeType, TerrainType[]> biomeTerrainMapping = new Dictionary<BiomeType, TerrainType[]>();

    void Start()
    {
        GenerateHeightMap();
        GenerateBiomeTerrainMapping();
        GenerateTerrainTextures();
        GenerateTerrain();
    }

    void GenerateHeightMap()
    {
        heightMap = new float[chunksX * chunkSize + 1, chunksZ * chunkSize + 1];
        for (int z = 0; z <= chunksZ * chunkSize; z++)
        {
            for (int x = 0; x <= chunksX * chunkSize; x++)
            {
                heightMap[x, z] = Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * heightScale;
            }
        }
    }

    void GenerateBiomeTerrainMapping()
    {
        biomeTerrainMapping[BiomeType.Desert] = new TerrainType[] { TerrainType.Sand, TerrainType.Rock };
        biomeTerrainMapping[BiomeType.Forest] = new TerrainType[] { TerrainType.Grass, TerrainType.Dirt, TerrainType.Flora };
        biomeTerrainMapping[BiomeType.Mountains] = new TerrainType[] { TerrainType.Rock, TerrainType.Snow };
        biomeTerrainMapping[BiomeType.Volcanic] = new TerrainType[] { TerrainType.Lava, TerrainType.Rock };
        biomeTerrainMapping[BiomeType.Plains] = new TerrainType[] { TerrainType.Grass, TerrainType.Dirt };
        biomeTerrainMapping[BiomeType.Swamp] = new TerrainType[] { TerrainType.Mud, TerrainType.Water, TerrainType.Flora };
        biomeTerrainMapping[BiomeType.Arctic] = new TerrainType[] { TerrainType.Snow, TerrainType.Ice };
    }

    void GenerateTerrainTextures()
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

    Texture2D GenerateSolidColorTexture(Color color, int width, int height)
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

    void GenerateTerrain()
    {
        for (int x = 0; x < chunksX; x++)
        {
            for (int z = 0; z < chunksZ; z++)
            {
                Vector2 chunkPos = new Vector2(x * chunkSize, z * chunkSize);
                GameObject newChunk = Instantiate(terrainChunkPrefab, new Vector3(chunkPos.x, 0, chunkPos.y), Quaternion.identity);
                TerrainChunk terrainChunk = newChunk.GetComponent<TerrainChunk>();

                if (terrainChunk != null)
                {
                    terrainChunk.width = chunkSize;
                    terrainChunk.depth = chunkSize;
                    terrainChunk.GenerateChunk(chunkPos, heightMap, terrainTextures);
                    chunks[chunkPos] = terrainChunk;
                }
            }
        }
    }
}
