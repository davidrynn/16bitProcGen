using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

public class LODTerrainManager : MonoBehaviour
{
    public static LODTerrainManager Instance { get; private set; }
    
    [Header("Terrain Configuration")]
    public Transform player;
    public GameObject terrainChunkPrefab;
    public int chunkSize = 16;
    public int viewDistance = 40;
    public int chunksX = 5;
    public int chunksZ = 5;
    public float worldScale = 2f;
    public BiomeData defaultBiome;
    
    [Header("LOD Integration")]
    public bool useLODSystem = true;
    public LODSystem lodSystem;
    public bool forceCollidersEnabled = true; // Always keep colliders enabled for gameplay
    
    [Header("Performance")]
    public bool enableChunkCulling = true;
    public float cullingDistance = 8000f;
    public bool enableChunkLoadingOptimization = false;
    public int maxChunksToKeep = 10000;
    public float chunkUpdateInterval = 0.1f; // Update chunks every 0.1 seconds
    
    private Dictionary<Vector2, LODTerrainChunk> chunks = new Dictionary<Vector2, LODTerrainChunk>();
    private List<Biome> biomes;
    private Dictionary<TerrainType, Texture2D> terrainTextures = new Dictionary<TerrainType, Texture2D>();
    private Vector2 currentPlayerChunkCoord;
    private Vector2 playerLastChunkCoord;
    private BiomeManager biomeManager;
    private float lastChunkUpdateTime = 0f;

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

        // Find LOD system
        lodSystem = FindFirstObjectByType<LODSystem>();
        
        // Initialize LOD system if enabled
        if (useLODSystem)
        {
            if (lodSystem == null)
            {
                Debug.LogWarning("LOD System not found! Creating one...");
                GameObject lodSystemGO = new GameObject("LOD System");
                lodSystem = lodSystemGO.AddComponent<LODSystem>();
                
                // Try to load LODSettings from ScriptableObjects
                LODSettings settings = null;
                
                // Try to load from Resources first
                settings = Resources.Load<LODSettings>("LODSettings");
                if (settings == null)
                {
                    settings = Resources.Load<LODSettings>("ScriptableObjects/LODSettings");
                }
                
                // If still not found, try to find it in the project assets
                if (settings == null)
                {
                    // This is a fallback - in a real scenario, you'd want to assign this in the inspector
                    Debug.LogWarning("LODSettings not found in Resources! Please assign LODSettings in the inspector or place it in a Resources folder.");
                }
                
                if (settings != null)
                {
                    lodSystem.lodSettings = settings;
                }
                else
                {
                    Debug.LogWarning("LODSettings not found! Creating default settings...");
                    lodSystem.lodSettings = ScriptableObject.CreateInstance<LODSettings>();
                }
            }
            
            // Ensure LODSystem has a player reference
            if (lodSystem.player == null && player != null)
            {
                lodSystem.player = player;
            }
        }

        GenerateTerrainTextures();
        CreateChunk(new Vector2(0, 0));
        GenerateTerrain();
        
        // Ensure colliders are enabled for gameplay
        if (forceCollidersEnabled)
        {
            EnsureCollidersEnabled();
        }
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

        // Only update chunks at intervals to improve performance
        if (Time.time - lastChunkUpdateTime >= chunkUpdateInterval)
        {
            if (currentPlayerChunkCoord != playerLastChunkCoord)
            {
                playerLastChunkCoord = currentPlayerChunkCoord;
                UpdateVisibleChunks();
            }
            
            lastChunkUpdateTime = Time.time;
        }
    }

    void UpdateVisibleChunks()
    {
        HashSet<Vector2> alreadyUpdatedChunks = new HashSet<Vector2>();
        int chunksCreated = 0;
        int chunksMadeVisible = 0;

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
                    chunksCreated++;
                }
                else
                {
                    chunks[viewedChunkCoord].SetVisible(true);
                    chunksMadeVisible++;
                }
            }
        }

        Debug.Log($"UpdateVisibleChunks: Created {chunksCreated} new chunks, Made {chunksMadeVisible} visible. Total chunks: {chunks.Count}");

        // Hide chunks outside view distance
        foreach (var coord in new List<Vector2>(chunks.Keys))
        {
            if (!alreadyUpdatedChunks.Contains(coord))
            {
                chunks[coord].SetVisible(false);
            }
        }
        
        // Optimize chunk loading to prevent performance issues
        OptimizeChunkLoading();
    }

    void UpdateChunkCulling()
    {
        Vector3 playerPos = player.position;
        
        foreach (var kvp in chunks)
        {
            Vector2 coord = kvp.Key;
            LODTerrainChunk chunk = kvp.Value;
            
            if (chunk != null)
            {
                // Calculate chunk distance from player in chunk coordinates
                Vector2 playerChunkCoord = new Vector2(
                    Mathf.FloorToInt(playerPos.x / chunkSize),
                    Mathf.FloorToInt(playerPos.z / chunkSize)
                );
                
                float chunkDistance = Vector2.Distance(coord, playerChunkCoord);
                
                // Cull chunks that are too far away
                if (chunkDistance > viewDistance)
                {
                    chunk.SetVisible(false);
                }
                else
                {
                    chunk.SetVisible(true);
                }
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
        Color[] pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = color;
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private void GenerateTerrain()
    {
        // Use viewDistance instead of chunksX/chunksZ for initial generation
        for (int z = -viewDistance; z <= viewDistance; z++)
        {
            for (int x = -viewDistance; x <= viewDistance; x++)
            {
                Vector2 chunkCoord = new Vector2(x, z);
                if (!chunks.ContainsKey(chunkCoord))
                {
                    CreateChunk(chunkCoord);
                }
            }
        }
    }

    void CreateChunk(Vector2 chunkCoord)
    {
        if (terrainChunkPrefab == null)
        {
            Debug.LogError("TerrainChunkPrefab is not assigned!");
            return;
        }

        Vector3 chunkPosition = new Vector3(
            chunkCoord.x * chunkSize,
            0,
            chunkCoord.y * chunkSize
        );

        // Create chunk as child of this LODTerrainManager
        GameObject chunkObject = Instantiate(terrainChunkPrefab, chunkPosition, Quaternion.identity, transform);
        chunkObject.name = $"LOD_Chunk_{chunkCoord.x}_{chunkCoord.y}";

        LODTerrainChunk chunk = chunkObject.GetComponent<LODTerrainChunk>();
        if (chunk == null)
        {
            chunk = chunkObject.AddComponent<LODTerrainChunk>();
        }

        // Ensure the chunk has the required components
        if (chunkObject.GetComponent<MeshFilter>() == null)
        {
            chunkObject.AddComponent<MeshFilter>();
        }
        
        if (chunkObject.GetComponent<MeshRenderer>() == null)
        {
            chunkObject.AddComponent<MeshRenderer>();
        }

        chunk.width = chunkSize;
        chunk.depth = chunkSize;

        BiomeData biome = DetermineBiome(chunkCoord);
        if (biome == null)
        {
            Debug.LogWarning($"No biome determined for chunk at {chunkCoord}, using default biome");
            biome = defaultBiome;
        }
        
        chunk.GenerateChunk(chunkCoord, terrainTextures, biome);

        chunks[chunkCoord] = chunk;
    }

    public Texture2D GenerateGradientTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float r = (float)x / width;
                float g = (float)y / height;
                float b = 0.5f;
                pixels[y * width + x] = new Color(r, g, b);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    public void SetLODSystemEnabled(bool enabled)
    {
        useLODSystem = enabled;
        
        if (lodSystem != null)
        {
            lodSystem.lodSettings.enableLOD = enabled;
        }
    }

    public void SetLODSettings(LODSettings settings)
    {
        if (lodSystem != null && settings != null)
        {
            lodSystem.lodSettings = settings;
        }
    }

    public void SetLODSystem(LODSystem system)
    {
        if (system != null)
        {
            lodSystem = system;
            useLODSystem = true;
        }
    }

    public void SetViewDistance(int newViewDistance)
    {
        viewDistance = newViewDistance;
        UpdateVisibleChunks();
    }

    public void SetCullingDistance(float newCullingDistance)
    {
        cullingDistance = newCullingDistance;
    }

    public int GetActiveChunkCount()
    {
        int count = 0;
        foreach (var chunk in chunks.Values)
        {
            if (chunk != null && chunk.gameObject.activeInHierarchy)
                count++;
        }
        return count;
    }

    public Dictionary<string, int> GetLODLevelStats()
    {
        Dictionary<string, int> stats = new Dictionary<string, int>();
        
        if (lodSystem != null && lodSystem.lodSettings != null)
        {
            foreach (var lodLevel in lodSystem.lodSettings.lodLevels)
            {
                stats[lodLevel.name] = 0;
            }
        }

        foreach (var chunk in chunks.Values)
        {
            if (chunk != null && chunk.GetCurrentLODLevel() != null)
            {
                string levelName = chunk.GetCurrentLODLevel().name;
                if (stats.ContainsKey(levelName))
                {
                    stats[levelName]++;
                }
            }
        }

        return stats;
    }

    public void EnsureCollidersEnabled()
    {
        if (lodSystem != null && lodSystem.lodSettings != null)
        {
            bool needsUpdate = false;
            
            foreach (var lodLevel in lodSystem.lodSettings.lodLevels)
            {
                if (!lodLevel.useCollider)
                {
                    lodLevel.useCollider = true;
                    needsUpdate = true;
                    Debug.Log($"Enabled collider for LOD level: {lodLevel.name}");
                }
            }
            
            if (needsUpdate)
            {
                Debug.Log("Updated LODSettings to ensure all levels have colliders enabled");
            }
        }
    }

    private void OptimizeChunkLoading()
    {
        if (!enableChunkLoadingOptimization)
            return;
        
        // Limit total chunks to prevent performance issues
        if (chunks.Count > maxChunksToKeep)
        {
            // Calculate distances from player to chunk centers
            var chunksWithDistances = chunks.Select(kvp => new
            {
                Coord = kvp.Key,
                Chunk = kvp.Value,
                Distance = Vector3.Distance(
                    player.position, 
                    kvp.Value.transform.position + new Vector3(chunkSize / 2f, 0, chunkSize / 2f) // Use chunk center
                )
            }).ToList();
            
            // Sort by distance (furthest first)
            chunksWithDistances.Sort((a, b) => b.Distance.CompareTo(a.Distance));
            
            // Remove the furthest chunks
            int chunksToRemove = chunks.Count - maxChunksToKeep;
            for (int i = 0; i < chunksToRemove; i++)
            {
                var chunkData = chunksWithDistances[i];
                
                // Don't remove chunks that are within view distance
                if (chunkData.Distance <= viewDistance * chunkSize)
                {
                    continue; // Skip chunks that are still in view
                }
                
                chunks.Remove(chunkData.Coord);
                
                // Unregister from LOD system before destroying
                if (lodSystem != null)
                {
                    lodSystem.UnregisterChunk(chunkData.Coord);
                }
                
                Destroy(chunkData.Chunk.gameObject);
            }
            
            Debug.Log($"Optimized chunk loading: Removed {chunksToRemove} distant chunks. Total chunks: {chunks.Count}");
        }
    }
} 