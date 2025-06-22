using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LODSystem : MonoBehaviour
{
    public static LODSystem Instance { get; private set; }
    
    [Header("LOD Configuration")]
    public LODSettings lodSettings;
    public Transform player;
    
    [Header("Debug")]
    public bool showDebugInfo = false;
    public bool showLODBorders = false;
    
    [Header("Camera-Based LOD")]
    public Camera mainCamera;
    public bool useCameraBasedLOD = true;
    public float screenSpaceLODThreshold = 0.1f; // Minimum screen space size for high LOD
    
    private Dictionary<Vector2, LODTerrainChunk> lodChunks = new Dictionary<Vector2, LODTerrainChunk>();
    private Vector2 currentPlayerChunkCoord;
    private Vector2 playerLastChunkCoord;
    private float lastUpdateTime;
    
    // Cached LOD level data
    private Dictionary<int, Mesh> cachedMeshes = new Dictionary<int, Mesh>();
    private Dictionary<int, Material> cachedMaterials = new Dictionary<int, Material>();

    // Add frame skipping for performance
    private int updateFrameSkip = 2; // Update LOD every 2 frames

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
        if (lodSettings == null)
        {
            Debug.LogError("LODSettings not assigned!");
            return;
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("Player not found! Please assign player transform or tag the player object.");
                return;
            }
        }

        InitializeLODSystem();
    }

    void Update()
    {
        if (!lodSettings.enableLOD || player == null)
            return;

        // Throttle updates for performance
        if (Time.time - lastUpdateTime < lodSettings.updateInterval)
            return;

        UpdatePlayerChunkPosition();
        UpdateLODLevels();
        lastUpdateTime = Time.time;
    }

    private void InitializeLODSystem()
    {
        // Validate LOD settings
        if (lodSettings.lodLevels.Length == 0)
        {
            Debug.LogError("No LOD levels defined!");
            return;
        }

        // Sort LOD levels by distance (closest first)
        System.Array.Sort(lodSettings.lodLevels, (a, b) => a.distance.CompareTo(b.distance));

        Debug.Log($"LOD System initialized with {lodSettings.lodLevels.Length} levels");
    }

    private void UpdatePlayerChunkPosition()
    {
        Vector2 newChunkCoord = GetChunkCoordinate(player.position);
        
        if (newChunkCoord != currentPlayerChunkCoord)
        {
            playerLastChunkCoord = currentPlayerChunkCoord;
            currentPlayerChunkCoord = newChunkCoord;
        }
    }

    private void UpdateLODLevels()
    {
        // Skip frames for performance
        if (Time.frameCount % updateFrameSkip != 0)
            return;
        
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }
        
        if (mainCamera == null)
        {
            Debug.LogWarning("No camera found for LOD system!");
            return;
        }

        Vector3 cameraPos = mainCamera.transform.position;
        
        foreach (var kvp in lodChunks)
        {
            Vector2 chunkCoord = kvp.Key;
            LODTerrainChunk chunk = kvp.Value;
            
            if (chunk != null)
            {
                LODLevel appropriateLOD;
                
                if (useCameraBasedLOD)
                {
                    appropriateLOD = GetCameraBasedLODLevel(chunk, cameraPos);
                }
                else
                {
                    // Fallback to distance-based
                    Vector3 chunkCenter = chunk.transform.position + new Vector3(chunk.width / 2f, 0, chunk.depth / 2f);
                    float distance = Vector3.Distance(cameraPos, chunkCenter);
                    appropriateLOD = GetAppropriateLODLevel(distance);
                }
                
                chunk.UpdateLODLevel(appropriateLOD);
            }
        }
    }

    private LODLevel GetCameraBasedLODLevel(LODTerrainChunk chunk, Vector3 cameraPos)
    {
        // Calculate chunk bounds in world space
        Vector3 chunkCenter = chunk.transform.position + new Vector3(chunk.width / 2f, 0, chunk.depth / 2f);
        
        // Calculate screen space size
        float screenSpaceSize = CalculateScreenSpaceSize(chunkCenter, chunk.width, cameraPos);
        
        // Calculate distance as fallback
        float distance = Vector3.Distance(cameraPos, chunkCenter);
        
        // Determine LOD based on screen space size with distance fallback
        if (screenSpaceSize > 0.8f || distance < 100f) // Very large on screen or very close
        {
            return GetLODLevelByName("High");
        }
        else if (screenSpaceSize > 0.4f || distance < 300f) // Large on screen or close
        {
            return GetLODLevelByName("Medium");
        }
        else if (screenSpaceSize > 0.15f || distance < 800f) // Medium on screen or moderate distance
        {
            return GetLODLevelByName("Low");
        }
        else // Small on screen or far away
        {
            return GetLODLevelByName("Ultra Low");
        }
    }

    private float CalculateScreenSpaceSize(Vector3 worldPos, float worldSize, Vector3 cameraPos)
    {
        // Project world position to screen space
        Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPos);
        
        // If behind camera, return 0
        if (screenPos.z < 0)
            return 0f;
        
        // Calculate distance from camera
        float distance = Vector3.Distance(cameraPos, worldPos);
        
        // Calculate screen space size using proper perspective projection
        float fov = mainCamera.fieldOfView * Mathf.Deg2Rad;
        float screenSpaceSize = (worldSize / distance) / Mathf.Tan(fov * 0.5f);
        
        // Normalize to screen height (0-1)
        screenSpaceSize = screenSpaceSize / Screen.height;
        
        // Clamp to reasonable range
        return Mathf.Clamp01(screenSpaceSize);
    }

    private LODLevel GetLODLevelByName(string name)
    {
        if (lodSettings == null || lodSettings.lodLevels == null)
            return null;
        
        foreach (var level in lodSettings.lodLevels)
        {
            if (level.name == name)
                return level;
        }
        
        return lodSettings.lodLevels[0]; // Fallback to first level
    }

    public LODLevel GetAppropriateLODLevel(float distance)
    {
        if (lodSettings == null || lodSettings.lodLevels == null || lodSettings.lodLevels.Length == 0)
        {
            Debug.LogWarning("LODSettings not properly configured!");
            return null;
        }

        // Find the appropriate LOD level based on distance
        // We want the highest quality LOD level where distance <= threshold
        for (int i = 0; i < lodSettings.lodLevels.Length; i++)
        {
            if (distance <= lodSettings.lodLevels[i].distance)
            {
                return lodSettings.lodLevels[i];
            }
        }
        
        // Return the lowest LOD level if distance exceeds all thresholds
        return lodSettings.lodLevels[lodSettings.lodLevels.Length - 1];
    }

    public Vector2 GetChunkCoordinate(Vector3 worldPosition)
    {
        int chunkSize = TerrainManager.Instance?.chunkSize ?? 16;
        return new Vector2(
            Mathf.FloorToInt(worldPosition.x / chunkSize),
            Mathf.FloorToInt(worldPosition.z / chunkSize)
        );
    }

    public void RegisterChunk(Vector2 chunkCoord, LODTerrainChunk chunk)
    {
        if (!lodChunks.ContainsKey(chunkCoord))
        {
            lodChunks[chunkCoord] = chunk;
            
            // Set initial LOD level
            if (player != null)
            {
                // Calculate distance to chunk center, not corner
                Vector3 chunkCenter = chunk.transform.position + new Vector3(chunk.width / 2f, 0, chunk.depth / 2f);
                float distance = Vector3.Distance(player.position, chunkCenter);
                LODLevel initialLOD = GetAppropriateLODLevel(distance);
                Debug.Log($"LODSystem: Initial LOD for chunk {chunkCoord}: Distance={distance:F1}m, LOD={initialLOD?.name ?? "None"}");
                chunk.UpdateLODLevel(initialLOD);
            }
        }
    }

    public void UnregisterChunk(Vector2 chunkCoord)
    {
        if (lodChunks.ContainsKey(chunkCoord))
        {
            lodChunks.Remove(chunkCoord);
        }
    }

    public Mesh GetCachedMesh(int resolution)
    {
        if (cachedMeshes.ContainsKey(resolution))
        {
            return cachedMeshes[resolution];
        }
        
        // Create new mesh for this resolution
        Mesh mesh = GenerateLODMesh(resolution);
        cachedMeshes[resolution] = mesh;
        return mesh;
    }

    public Material GetCachedMaterial(LODLevel lodLevel)
    {
        if (lodLevel == null)
        {
            Debug.LogWarning("LODLevel is null in GetCachedMaterial!");
            return null;
        }

        int key = lodLevel.GetHashCode();
        
        if (cachedMaterials.ContainsKey(key))
        {
            return cachedMaterials[key];
        }
        
        // Create new material for this LOD level
        Material material = CreateLODMaterial(lodLevel);
        if (material != null)
        {
            cachedMaterials[key] = material;
        }
        else
        {
            Debug.LogError($"Failed to create material for LOD level {lodLevel.name}");
        }
        
        return material;
    }

    private Mesh GenerateLODMesh(int resolution)
    {
        Mesh mesh = new Mesh();
        mesh.name = $"LOD_Mesh_{resolution}";
        
        int vertexCount = (resolution + 1) * (resolution + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[resolution * resolution * 6];
        
        // Generate vertices
        for (int z = 0, i = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++, i++)
            {
                vertices[i] = new Vector3(x, 0, z);
                uvs[i] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }
        
        // Generate triangles
        int tris = 0;
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int vert = z * (resolution + 1) + x;
                
                triangles[tris + 0] = vert;
                triangles[tris + 1] = vert + resolution + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + resolution + 1;
                triangles[tris + 5] = vert + resolution + 2;
                
                tris += 6;
            }
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

    private Material CreateLODMaterial(LODLevel lodLevel)
    {
        Material material = null;
        
        // Try to use the LOD level's material first
        if (lodLevel.material != null)
        {
            material = new Material(lodLevel.material);
        }
        else
        {
            // Try to find the Universal Render Pipeline/Lit shader
            Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                material = new Material(urpShader);
            }
            else
            {
                // Fallback to loading the TerrainMat from Resources or Assets
                Material terrainMat = Resources.Load<Material>("TerrainMat");
                if (terrainMat == null)
                {
                    // Try to find it in the Materials folder
                    terrainMat = Resources.Load<Material>("Materials/TerrainMat");
                }
                
                if (terrainMat != null)
                {
                    material = new Material(terrainMat);
                }
                else
                {
                    // Last resort: try to find any available shader
                    Shader fallbackShader = Shader.Find("Standard");
                    if (fallbackShader == null)
                    {
                        fallbackShader = Shader.Find("Unlit/Color");
                    }
                    
                    if (fallbackShader != null)
                    {
                        material = new Material(fallbackShader);
                        Debug.LogWarning($"Using fallback shader {fallbackShader.name} for LOD material");
                    }
                    else
                    {
                        Debug.LogError("No suitable shader found for LOD material!");
                        return null;
                    }
                }
            }
        }
        
        // Apply LOD-specific settings if material was created successfully
        if (material != null)
        {
            material.SetFloat("_Smoothness", lodLevel.textureQuality);
        }
        
        return material;
    }

    void OnDrawGizmos()
    {
        // Add null checks to prevent errors
        if (lodSettings == null)
            return;
            
        if (!lodSettings.showLODGizmos || !Application.isPlaying)
            return;

        if (player == null)
            return;

        Vector3 playerPos = player.position;
        
        foreach (var lodLevel in lodSettings.lodLevels)
        {
            if (lodLevel == null)
                continue;
                
            Color gizmoColor = Color.Lerp(Color.red, Color.green, lodLevel.textureQuality);
            gizmoColor.a = 0.3f;
            Gizmos.color = gizmoColor;
            
            Gizmos.DrawWireSphere(playerPos, lodLevel.distance);
        }
    }

    void OnGUI()
    {
        if (!showDebugInfo || !Application.isPlaying)
            return;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label("LOD System Debug Info", GUI.skin.box);
        
        if (player != null)
        {
            GUILayout.Label($"Player Position: {player.position}");
            GUILayout.Label($"Current Chunk: {currentPlayerChunkCoord}");
            GUILayout.Label($"Active Chunks: {lodChunks.Count}");
            
            if (lodSettings != null && lodSettings.lodLevels != null)
            {
                Vector3 playerPos = player.position;
                foreach (var lodLevel in lodSettings.lodLevels)
                {
                    if (lodLevel == null)
                        continue;
                        
                    int chunksInLevel = 0;
                    foreach (var chunk in lodChunks.Values)
                    {
                        if (chunk != null)
                        {
                            float distance = Vector3.Distance(playerPos, chunk.transform.position);
                            if (distance <= lodLevel.distance)
                                chunksInLevel++;
                        }
                    }
                    GUILayout.Label($"{lodLevel.name}: {chunksInLevel} chunks");
                }
            }
        }
        
        GUILayout.EndArea();
    }
} 