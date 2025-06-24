using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class TerrainChunk : MonoBehaviour
{
    public int width = 16;
    public int depth = 16;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private MeshCollider meshCollider;
    private bool isGenerated = false;

    // LOD Settings
    private int[] lodResolutions = { 16, 8, 4 };
    private float[] lodDistances = { 100f, 300f, 800f };
    public int currentLODLevel = 0;

    // Store references for LOD regeneration
    public Vector2 chunkPosition;
    private BiomeData currentBiome;
    private Dictionary<TerrainType, Texture2D> currentTerrainTextures;

    // Add this to TerrainChunk for inspector debugging
    [Header("LOD Debug Info")]
    [SerializeField] private string currentLODInfo = "Not initialized";

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;

        Vector3 pos = transform.position;
        Gizmos.DrawWireCube(
            new Vector3(pos.x + width / 2f, 0, pos.z + depth / 2f),
            new Vector3(width, 0, depth)
        );
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
    }

    // Encapsulated mesh generation
    private void GenerateMesh(int resolution)
    {
        Vector3[] vertices;
        Vector2[] uvs;
        GenerateVerticesAndUVs(chunkPosition, currentBiome, resolution, out vertices, out uvs);
        int[] triangles = GenerateTriangles(resolution);
        ApplyMeshData(vertices, triangles, uvs);
        
        // Assign texture using the generated vertices
        AssignTerrainTexture(vertices, currentTerrainTextures, currentBiome);
    }

    // Initial chunk generation
    public void GenerateChunk(Vector2 chunkPos, Dictionary<TerrainType, Texture2D> terrainTextures, BiomeData biome)
    {
        // Store references for LOD regeneration
        chunkPosition = chunkPos;
        currentBiome = biome;
        currentTerrainTextures = terrainTextures;

        mesh = new Mesh();
        mesh.name = $"Chunk_{chunkPos.x}_{chunkPos.y}";
        meshFilter.mesh = mesh;

        // Generate initial mesh (includes texture assignment)
        GenerateMesh(width); // Full resolution
        
        // Initialize LOD info
        currentLODInfo = $"LOD: {currentLODLevel}, Res: {lodResolutions[currentLODLevel]}, Dist: Initial";
        
        isGenerated = true;
    }

    private void GenerateVerticesAndUVs(Vector2 chunkPos, BiomeData biome, int resolution, out Vector3[] vertices, out Vector2[] uvs)
    {
        vertices = new Vector3[(resolution + 1) * (resolution + 1)];
        uvs = new Vector2[vertices.Length];

        for (int z = 0, i = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++, i++)
            {
                // Calculate global position based on chunk position and resolution
                float globalX = chunkPos.x * width + (x * width / resolution);
                float globalZ = chunkPos.y * depth + (z * depth / resolution);
                float height = biome.GenerateHeight(globalX, globalZ);

                vertices[i] = new Vector3(x * width / resolution, height, z * depth / resolution);
                uvs[i] = new Vector2((float)x / resolution, (float)z / resolution);
            }
        }
    }

    private int[] GenerateTriangles(int resolution)
    {
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
            vert++; // Correcting vertex offset for next row
        }

        return triangles;
    }

    private void ApplyMeshData(Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        if (mesh == null)
        {
            Debug.LogError("Mesh is null in ApplyMeshData");
            return;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // Update collider
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null; // Clear old mesh
            meshCollider.sharedMesh = mesh; // Set new mesh
        }
        else
        {
            Debug.LogError("MeshCollider is null in ApplyMeshData");
        }
    }

    private void AssignTerrainTexture(Vector3[] vertices, Dictionary<TerrainType, Texture2D> terrainTextures, BiomeData biome)
    {
        if (vertices == null || vertices.Length == 0)
        {
            Debug.LogError("No vertices provided for texture assignment");
            return;
        }

        float totalHeight = 0f;
        foreach (var vertex in vertices)
        {
            totalHeight += vertex.y;
        }

        float avgHeight = totalHeight / Mathf.Max(1, vertices.Length);
        TerrainType dominantTerrainType = biome.GetTerrainType(avgHeight);

        if (terrainTextures.ContainsKey(dominantTerrainType))
        {
            Material mat = meshRenderer.material;
            if (mat != null)
            {
                mat.SetTexture("_BaseMap", terrainTextures[dominantTerrainType]); // URP Shader
                mat.SetTexture("_MainTex", terrainTextures[dominantTerrainType]); // Standard Shader
            }
            else
            {
                Debug.LogError("Material is null in AssignTerrainTexture");
            }
        }
        else
        {
            Debug.LogWarning($"No texture found for terrain type {dominantTerrainType} for height: {avgHeight}");
            if (terrainTextures.ContainsKey(TerrainType.Default))
            {
                meshRenderer.material.mainTexture = terrainTextures[TerrainType.Default];
            }
            else
            {
                Debug.LogError("Default texture not found!");
            }
        }
    }

    public bool IsGenerated()
    {
        return isGenerated;
    }

    // LOD updates
    public void UpdateLOD(float distanceToPlayer)
    {
         int newLODLevel = 0;
        for (int i = 0; i < lodDistances.Length; i++)
        {
            if (distanceToPlayer > lodDistances[i])
            {
                newLODLevel = i + 1;
            }
        }
        
        // Always update the debug info, even if LOD level doesn't change
        currentLODInfo = $"LOD: {newLODLevel}, Res: {lodResolutions[Mathf.Min(newLODLevel, lodResolutions.Length - 1)]}, Dist: {distanceToPlayer:F1}";
        
        if (newLODLevel != currentLODLevel)
        {
            currentLODLevel = newLODLevel;
            int resolution = lodResolutions[Mathf.Min(currentLODLevel, lodResolutions.Length - 1)];
            
            GenerateMesh(resolution);
        }
    }

    // Add this method to show current LOD info
    public string GetLODInfo()
    {
        return $"LOD: {currentLODLevel}, Resolution: {lodResolutions[Mathf.Min(currentLODLevel, lodResolutions.Length - 1)]}";
    }

    // Add this method to manually print LOD info
    public void PrintLODInfo()
    {
        Debug.Log($"Chunk {chunkPosition}: {currentLODInfo}");
    }
}
