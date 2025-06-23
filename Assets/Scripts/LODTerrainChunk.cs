using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class LODTerrainChunk : MonoBehaviour
{
    [Header("LOD Configuration")]
    public int width = 16;
    public int depth = 16;
    
    [Header("Debug")]
    public bool showLODInfo = false;
    public bool showChunkDebug = false;
    
    // Add static control for all chunks - separate controls for different debug types
    public static bool GlobalDebugEnabled = false;
    public static bool GlobalChunkDebugEnabled = false;
    public static bool GlobalGizmosEnabled = false;
    public static bool GlobalTextOverlayEnabled = false;
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private MeshCollider meshCollider;
    private bool isGenerated = false;
    
    // LOD-specific data
    private LODLevel currentLODLevel;
    private Vector2 chunkPosition;
    private BiomeData biomeData;
    private Dictionary<TerrainType, Texture2D> terrainTextures;
    
    // Cached mesh data for different LOD levels
    private Dictionary<int, Mesh> lodMeshes = new Dictionary<int, Mesh>();
    private Dictionary<int, Vector3[]> lodVertices = new Dictionary<int, Vector3[]>();
    private Dictionary<int, int[]> lodTriangles = new Dictionary<int, int[]>();
    private Dictionary<int, Vector2[]> lodUVs = new Dictionary<int, Vector2[]>();

    private void OnDrawGizmos()
    {
        // Only show gizmos if both local and global gizmo debug are enabled
        if (!GlobalGizmosEnabled || !showChunkDebug)
            return;
            
        Gizmos.color = Color.red;
        Vector3 pos = transform.position;
        Gizmos.DrawWireCube(
            new Vector3(pos.x + width / 2f, 0, pos.z + depth / 2f),
            new Vector3(width, 0, depth)
        );
        
        // Show LOD level info
        if (currentLODLevel != null)
        {
            Gizmos.color = Color.Lerp(Color.red, Color.green, currentLODLevel.textureQuality);
            Gizmos.DrawWireSphere(transform.position, 2f);
        }
    }

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        meshCollider = GetComponent<MeshCollider>();
        
        if (meshCollider == null)
        {
            meshCollider = gameObject.AddComponent<MeshCollider>();
        }
    }

    public void SetVisible(bool visible)
    {
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
        }
        
        // CRITICAL: Don't disable the mesh or collider when setting invisible
        // The mesh and collider must remain active for collision detection
        // Only the renderer should be disabled for performance
    }

    public void GenerateChunk(Vector2 chunkPos, Dictionary<TerrainType, Texture2D> terrainTextures, BiomeData biome)
    {
        this.chunkPosition = chunkPos;
        this.biomeData = biome;
        this.terrainTextures = terrainTextures;
        
        // Ensure we have the required components
        if (meshFilter == null)
        {
            meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                meshFilter = gameObject.AddComponent<MeshFilter>();
            }
        }
        
        if (meshRenderer == null)
        {
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }
        }
        
        if (meshCollider == null)
        {
            meshCollider = GetComponent<MeshCollider>();
            if (meshCollider == null)
            {
                meshCollider = gameObject.AddComponent<MeshCollider>();
            }
        }
        
        if (biome == null)
        {
            Debug.LogError($"No biome provided for chunk at {chunkPos}");
            return;
        }

        if (terrainTextures == null || terrainTextures.Count == 0)
        {
            Debug.LogError($"No terrain textures provided for chunk at {chunkPos}");
            return;
        }

        // Register with LOD system
        if (LODSystem.Instance != null)
        {
            LODSystem.Instance.RegisterChunk(chunkPos, this);
        }
        else
        {
            Debug.LogWarning($"LODTerrainChunk: LODSystem.Instance is null for chunk {chunkPos} - LOD will not work!");
        }

        // Generate initial mesh
        GenerateMeshForLODLevel(16); // Start with highest detail
        isGenerated = true;
    }

    public void UpdateLODLevel(LODLevel newLODLevel)
    {
        if (currentLODLevel == newLODLevel)
            return;

        currentLODLevel = newLODLevel;
        
        if (isGenerated)
        {
            ApplyLODLevel(newLODLevel);
        }
    }

    private void ApplyLODLevel(LODLevel lodLevel)
    {
        if (lodLevel == null)
            return;

        // Generate or get cached mesh for this LOD level
        Mesh mesh = GetOrGenerateLODMesh(lodLevel.meshResolution);
        
        if (mesh != null)
        {
            // Apply mesh
            if (meshFilter != null)
            {
                meshFilter.mesh = mesh;
            }
            
            // Update collider - match original TerrainChunk.cs exactly
            if (meshCollider != null && lodLevel.useCollider)
            {
                meshCollider.sharedMesh = null; // Clear old mesh - THIS WAS MISSING!
                meshCollider.sharedMesh = mesh; // Set new mesh
                meshCollider.enabled = true;
            }
            else if (meshCollider != null)
            {
                meshCollider.enabled = false;
            }
            
            // Update material (only if needed)
            if (meshRenderer != null && terrainTextures != null)
            {
                TerrainType dominantType = GetDominantTerrainType();
                if (terrainTextures.ContainsKey(dominantType))
                {
                    // Use existing material from prefab
                    Material mat = meshRenderer.material;
                    if (mat != null)
                    {
                        mat.SetTexture("_BaseMap", terrainTextures[dominantType]); // URP Shader
                        mat.SetTexture("_MainTex", terrainTextures[dominantType]); // Standard Shader
                        
                        // Update shadow settings
                        meshRenderer.shadowCastingMode = lodLevel.useShadows ? 
                            ShadowCastingMode.On : ShadowCastingMode.Off;
                    }
                    else
                    {
                        Debug.LogError($"Chunk {name}: Material is null in ApplyLODLevel - check prefab material assignment!");
                    }
                }
                else
                {
                    Debug.LogError($"Chunk {name}: No texture found for terrain type {dominantType} in ApplyLODLevel");
                }
            }
        }
        else
        {
            Debug.LogError($"Failed to generate mesh for LOD level {lodLevel.name} with resolution {lodLevel.meshResolution}");
        }
    }

    private Mesh GetOrGenerateLODMesh(int resolution)
    {
        if (lodMeshes.ContainsKey(resolution))
        {
            return lodMeshes[resolution];
        }

        // Generate new mesh for this resolution
        Mesh mesh = GenerateMeshForResolution(resolution);
        lodMeshes[resolution] = mesh;
        
        return mesh;
    }

    private Mesh GenerateMeshForResolution(int resolution)
    {
        Mesh mesh = new Mesh();
        mesh.name = $"LOD_Chunk_{chunkPosition.x}_{chunkPosition.y}_Res_{resolution}";

        // Generate or get cached data
        Vector3[] vertices = GetOrGenerateVertices(resolution);
        int[] triangles = GetOrGenerateTriangles(resolution);
        Vector2[] uvs = GetOrGenerateUVs(resolution);

        // Validate mesh data
        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError($"Generated mesh has no vertices for resolution {resolution}");
            return null;
        }
        
        if (triangles == null || triangles.Length == 0)
        {
            Debug.LogError($"Generated mesh has no triangles for resolution {resolution}");
            return null;
        }

        // Apply data to mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Vector3[] GetOrGenerateVertices(int resolution)
    {
        if (lodVertices.ContainsKey(resolution))
        {
            return lodVertices[resolution];
        }

        Vector3[] vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        
        for (int z = 0, i = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++, i++)
            {
                // Calculate global position
                float globalX = chunkPosition.x * width + (x * width / resolution);
                float globalZ = chunkPosition.y * depth + (z * depth / resolution);
                
                // Get height from biome
                float height = biomeData != null ? biomeData.GenerateHeight(globalX, globalZ) : 0f;
                
                // Scale to resolution
                float localX = (float)x / resolution * width;
                float localZ = (float)z / resolution * depth;
                
                vertices[i] = new Vector3(localX, height, localZ);
            }
        }

        lodVertices[resolution] = vertices;
        return vertices;
    }

    private int[] GetOrGenerateTriangles(int resolution)
    {
        if (lodTriangles.ContainsKey(resolution))
        {
            return lodTriangles[resolution];
        }

        int[] triangles = new int[resolution * resolution * 6];
        int tris = 0, vert = 0;

        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                triangles[tris + 0] = vert;
                triangles[tris + 1] = vert + resolution + 1;
                triangles[tris + 2] = vert + 1;
                triangles[tris + 3] = vert + 1;
                triangles[tris + 4] = vert + resolution + 1;
                triangles[tris + 5] = vert + resolution + 2;

                vert++;
                tris += 6;
            }
            vert++; // Correcting vertex offset for next row - THIS WAS MISSING!
        }

        lodTriangles[resolution] = triangles;
        return triangles;
    }

    private Vector2[] GetOrGenerateUVs(int resolution)
    {
        if (lodUVs.ContainsKey(resolution))
        {
            return lodUVs[resolution];
        }

        Vector2[] uvs = new Vector2[(resolution + 1) * (resolution + 1)];
        
        for (int z = 0, i = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++, i++)
            {
                uvs[i] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }

        lodUVs[resolution] = uvs;
        return uvs;
    }

    private TerrainType GetDominantTerrainType()
    {
        if (biomeData == null)
            return TerrainType.Default;

        // Sample a few points to determine dominant terrain type
        float totalHeight = 0f;
        int sampleCount = 0;
        
        for (int x = 0; x < width; x += 4)
        {
            for (int z = 0; z < depth; z += 4)
            {
                float globalX = chunkPosition.x * width + x;
                float globalZ = chunkPosition.y * depth + z;
                totalHeight += biomeData.GenerateHeight(globalX, globalZ);
                sampleCount++;
            }
        }

        float avgHeight = totalHeight / Mathf.Max(1, sampleCount);
        return biomeData.GetTerrainType(avgHeight);
    }

    private void GenerateMeshForLODLevel(int resolution)
    {
        Mesh mesh = GetOrGenerateLODMesh(resolution);
        
        if (mesh != null)
        {
            meshFilter.mesh = mesh;
            
            // Set the collider immediately - match original TerrainChunk.cs exactly
            if (meshCollider != null)
            {
                meshCollider.sharedMesh = null; // Clear old mesh - THIS WAS MISSING!
                meshCollider.sharedMesh = mesh; // Set new mesh
                meshCollider.enabled = true;
                Debug.Log($"Set collider for chunk {name} with mesh {mesh.name}");
            }
            else
            {
                Debug.LogError($"MeshCollider is null for chunk {name}!");
            }
            
            // Apply material like the original TerrainChunk - use existing material from prefab
            if (meshRenderer != null && terrainTextures != null)
            {
                TerrainType dominantType = GetDominantTerrainType();
                Debug.Log($"Chunk {name}: Dominant terrain type = {dominantType}");
                
                if (terrainTextures.ContainsKey(dominantType))
                {
                    // Use existing material from prefab like original TerrainChunk
                    Material mat = meshRenderer.material;
                    if (mat != null)
                    {
                        mat.SetTexture("_BaseMap", terrainTextures[dominantType]); // URP Shader
                        mat.SetTexture("_MainTex", terrainTextures[dominantType]); // Standard Shader
                        Debug.Log($"Chunk {name}: Applied texture for {dominantType} to existing material");
                    }
                    else
                    {
                        Debug.LogError($"Chunk {name}: Material is null - check if prefab has a material assigned!");
                    }
                }
                else
                {
                    Debug.LogError($"Chunk {name}: No texture found for terrain type {dominantType}");
                }
            }
            else
            {
                Debug.LogError($"Chunk {name}: MeshRenderer or terrainTextures is null! Renderer={meshRenderer != null}, Textures={terrainTextures != null}");
            }
        }
        else
        {
            Debug.LogError($"Failed to generate mesh for resolution {resolution}");
        }
    }

    public bool IsGenerated()
    {
        return isGenerated;
    }

    public LODLevel GetCurrentLODLevel()
    {
        return currentLODLevel;
    }

    void OnDestroy()
    {
        // Unregister from LOD system
        if (LODSystem.Instance != null)
        {
            LODSystem.Instance.UnregisterChunk(chunkPosition);
        }
    }

    void OnGUI()
    {
        // Only show GUI text if both local and global text overlay debug are enabled
        if (!GlobalTextOverlayEnabled || !showChunkDebug)
            return;
            
        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0)
        {
            string chunkInfo = $"Chunk: {name}\n";
            
            if (currentLODLevel != null)
            {
                chunkInfo += $"LOD: {currentLODLevel.name}\n";
                chunkInfo += $"Res: {currentLODLevel.meshResolution}\n";
                chunkInfo += $"Dist: {currentLODLevel.distance:F0}\n";
                chunkInfo += $"Collider: {(currentLODLevel.useCollider ? "On" : "Off")}\n";
                chunkInfo += $"Shadows: {(currentLODLevel.useShadows ? "On" : "Off")}";
            }
            else
            {
                chunkInfo += "LOD: None\n";
                chunkInfo += $"Mesh: {(meshFilter?.mesh != null ? "Yes" : "No")}\n";
                chunkInfo += $"Collider: {(meshCollider?.enabled == true ? "On" : "Off")}\n";
                chunkInfo += $"Renderer: {(meshRenderer?.enabled == true ? "On" : "Off")}";
            }
            
            GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 200, 100), chunkInfo);
        }
    }
} 