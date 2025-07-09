using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Unity.Collections;

/// <summary>
/// Simple Visual Debug Test for DOTS Terrain System
/// Creates terrain entities and displays them as simple meshes
/// Demonstrates DOTS → GPU → Visual data flow
/// </summary>
public class SimpleVisualDebugTest : MonoBehaviour
{
    [Header("Visual Test Settings")]
    public bool runVisualTestOnStart = true;
    public int testChunkCount = 3;
    public int chunkResolution = 32;
    public float chunkSize = 10f;
    public float heightScale = 5f;
    
    [Header("Visualization")]
    public Material terrainMaterial;
    public bool showWireframe = true;
    public bool showHeightColors = true;
    
    [Header("Debug Info")]
    [SerializeField] private int activeChunkCount;
    [SerializeField] private float lastGenerationTime;
    
    private TerrainEntityManager entityManager;
    private TerrainComputeBufferManager bufferManager;
    private ComputeShaderManager computeManager;
    
    // Visual representation
    private GameObject[] chunkVisuals;
    private Mesh[] chunkMeshes;
    private Material[] chunkMaterials;
    
    private void Start()
    {
        if (runVisualTestOnStart)
        {
            RunVisualDebugTest();
        }
    }
    
    /// <summary>
    /// Runs the complete visual debug test
    /// </summary>
    public void RunVisualDebugTest()
    {
        Debug.Log("=== SIMPLE VISUAL DEBUG TEST ===");
        
        InitializeManagers();
        CreateTestChunks();
        GenerateVisualRepresentation();
        DisplayDebugInfo();
        
        Debug.Log("=== VISUAL DEBUG TEST COMPLETE ===");
    }
    
    /// <summary>
    /// Initializes all required managers
    /// </summary>
    private void InitializeManagers()
    {
        Debug.Log("Initializing managers...");
        
        // Get or create entity manager
        entityManager = FindFirstObjectByType<TerrainEntityManager>();
        if (entityManager == null)
        {
            Debug.LogWarning("Creating TerrainEntityManager for visual test");
            var go = new GameObject("VisualTestEntityManager");
            entityManager = go.AddComponent<TerrainEntityManager>();
        }
        
        // Get or create buffer manager
        bufferManager = FindFirstObjectByType<TerrainComputeBufferManager>();
        if (bufferManager == null)
        {
            Debug.LogWarning("Creating TerrainComputeBufferManager for visual test");
            var go = new GameObject("VisualTestBufferManager");
            bufferManager = go.AddComponent<TerrainComputeBufferManager>();
        }
        
        // Get compute manager singleton
        try
        {
            computeManager = ComputeShaderManager.Instance;
            Debug.Log("✓ ComputeShaderManager singleton initialized");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to get ComputeShaderManager instance: {e.Message}");
            computeManager = null;
        }
        
        Debug.Log("✓ Managers initialized");
    }
    
    /// <summary>
    /// Creates test terrain chunks with DOTS entities
    /// </summary>
    private void CreateTestChunks()
    {
        Debug.Log($"Creating {testChunkCount} test chunks...");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        // Create chunks in a grid pattern
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(testChunkCount));
        
        for (int i = 0; i < testChunkCount; i++)
        {
            int x = i % gridSize;
            int z = i / gridSize;
            var chunkPos = new int2(x, z);
            
            // Create terrain entity with DOTS data
            var entity = entityManager.CreateTerrainEntity(chunkPos, BiomeType.Forest);
            
            if (entity == Entity.Null)
            {
                Debug.LogError($"Failed to create terrain entity at {chunkPos}");
                continue;
            }
            
            // Generate basic height data for visualization
            GenerateBasicHeightData(chunkPos);
            
            Debug.Log($"✓ Created terrain chunk at {chunkPos}");
        }
        
        stopwatch.Stop();
        lastGenerationTime = stopwatch.ElapsedMilliseconds;
        activeChunkCount = entityManager.GetTerrainEntityCount();
        
        Debug.Log($"✓ Created {activeChunkCount} chunks in {lastGenerationTime}ms");
    }
    
    /// <summary>
    /// Generates basic height data for a chunk
    /// </summary>
    private void GenerateBasicHeightData(int2 chunkPos)
    {
        // Get GPU buffers for this chunk
        var heightBuffer = bufferManager.GetHeightBuffer(chunkPos, chunkResolution);
        var biomeBuffer = bufferManager.GetBiomeBuffer(chunkPos, chunkResolution);
        
        if (heightBuffer == null || biomeBuffer == null)
        {
            Debug.LogError($"Failed to get buffers for chunk {chunkPos}");
            return;
        }
        
        // Generate simple height data (sine wave pattern for testing)
        var heightData = new float[chunkResolution * chunkResolution];
        var biomeData = new float[chunkResolution * chunkResolution];
        
        for (int z = 0; z < chunkResolution; z++)
        {
            for (int x = 0; x < chunkResolution; x++)
            {
                int index = z * chunkResolution + x;
                
                // Generate simple height pattern
                float xNorm = (float)x / chunkResolution;
                float zNorm = (float)z / chunkResolution;
                
                // Create a simple sine wave pattern
                float height = Mathf.Sin(xNorm * Mathf.PI * 2) * Mathf.Cos(zNorm * Mathf.PI * 2) * 0.5f + 0.5f;
                height += Mathf.Sin(xNorm * Mathf.PI * 4) * 0.25f;
                height += Mathf.Cos(zNorm * Mathf.PI * 4) * 0.25f;
                
                // Add some variation based on chunk position
                height += Mathf.Sin(chunkPos.x * 0.5f + xNorm * Mathf.PI) * 0.1f;
                height += Mathf.Cos(chunkPos.y * 0.5f + zNorm * Mathf.PI) * 0.1f;
                
                heightData[index] = height;
                biomeData[index] = height; // Use height as biome value for now
            }
        }
        
        // Upload to GPU buffers
        heightBuffer.SetData(heightData);
        biomeBuffer.SetData(biomeData);
        
        Debug.Log($"✓ Generated height data for chunk {chunkPos}");
    }
    
    /// <summary>
    /// Creates visual representation of the terrain chunks
    /// </summary>
    private void GenerateVisualRepresentation()
    {
        Debug.Log("Generating visual representation...");
        
        // Initialize arrays
        chunkVisuals = new GameObject[testChunkCount];
        chunkMeshes = new Mesh[testChunkCount];
        chunkMaterials = new Material[testChunkCount];
        
        // Create material if not assigned
        if (terrainMaterial == null)
        {
            terrainMaterial = CreateDefaultTerrainMaterial();
        }
        
        // Create visual representation for each chunk
        int gridSize = Mathf.CeilToInt(Mathf.Sqrt(testChunkCount));
        
        for (int i = 0; i < testChunkCount; i++)
        {
            int x = i % gridSize;
            int z = i / gridSize;
            var chunkPos = new int2(x, z);
            
            // Create chunk GameObject
            var chunkGO = new GameObject($"TerrainChunk_{chunkPos.x}_{chunkPos.y}");
            chunkGO.transform.SetParent(transform);
            chunkGO.transform.position = new Vector3(x * chunkSize, 0, z * chunkSize);
            
            // Create mesh
            var mesh = CreateTerrainMesh(chunkPos);
            
            // Create material instance
            var material = new Material(terrainMaterial);
            if (showHeightColors)
            {
                material.color = GetHeightColor(chunkPos);
            }
            
            // Add mesh renderer
            var meshRenderer = chunkGO.AddComponent<MeshRenderer>();
            meshRenderer.material = material;
            
            // Add mesh filter
            var meshFilter = chunkGO.AddComponent<MeshFilter>();
            meshFilter.mesh = mesh;
            
            // Store references
            chunkVisuals[i] = chunkGO;
            chunkMeshes[i] = mesh;
            chunkMaterials[i] = material;
            
            Debug.Log($"✓ Created visual for chunk {chunkPos}");
        }
        
        Debug.Log("✓ Visual representation complete");
    }
    
    /// <summary>
    /// Creates a terrain mesh for a chunk
    /// </summary>
    private Mesh CreateTerrainMesh(int2 chunkPos)
    {
        var mesh = new Mesh();
        mesh.name = $"TerrainMesh_{chunkPos.x}_{chunkPos.y}";
        
        // Get height data from GPU buffer
        var heightBuffer = bufferManager.GetHeightBuffer(chunkPos, chunkResolution);
        if (heightBuffer == null)
        {
            Debug.LogError($"No height buffer for chunk {chunkPos}");
            return mesh;
        }
        
        var heightData = new float[chunkResolution * chunkResolution];
        heightBuffer.GetData(heightData);
        
        // Create vertices and triangles
        var vertices = new Vector3[chunkResolution * chunkResolution];
        var triangles = new int[(chunkResolution - 1) * (chunkResolution - 1) * 6];
        var uvs = new Vector2[chunkResolution * chunkResolution];
        
        // Generate vertices
        for (int z = 0; z < chunkResolution; z++)
        {
            for (int x = 0; x < chunkResolution; x++)
            {
                int index = z * chunkResolution + x;
                
                float xPos = (float)x / (chunkResolution - 1) * chunkSize;
                float zPos = (float)z / (chunkResolution - 1) * chunkSize;
                float yPos = heightData[index] * heightScale;
                
                vertices[index] = new Vector3(xPos, yPos, zPos);
                uvs[index] = new Vector2((float)x / chunkResolution, (float)z / chunkResolution);
            }
        }
        
        // Generate triangles
        int triangleIndex = 0;
        for (int z = 0; z < chunkResolution - 1; z++)
        {
            for (int x = 0; x < chunkResolution - 1; x++)
            {
                int vertexIndex = z * chunkResolution + x;
                
                // First triangle
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + chunkResolution;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                
                // Second triangle
                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + chunkResolution;
                triangles[triangleIndex + 5] = vertexIndex + chunkResolution + 1;
                
                triangleIndex += 6;
            }
        }
        
        // Apply to mesh
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Creates a default terrain material
    /// </summary>
    private Material CreateDefaultTerrainMaterial()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }
        
        var material = new Material(shader);
        material.color = Color.green;
        
        if (showWireframe)
        {
            material.SetFloat("_Wireframe", 1.0f);
        }
        
        return material;
    }
    
    /// <summary>
    /// Gets a color based on chunk position for visual variety
    /// </summary>
    private Color GetHeightColor(int2 chunkPos)
    {
        // Simple color variation based on chunk position
        float hue = (chunkPos.x + chunkPos.y) * 0.1f % 1.0f;
        return Color.HSVToRGB(hue, 0.7f, 0.8f);
    }
    
    /// <summary>
    /// Displays debug information
    /// </summary>
    private void DisplayDebugInfo()
    {
        Debug.Log("=== VISUAL DEBUG INFO ===");
        Debug.Log($"Active Chunks: {activeChunkCount}");
        Debug.Log($"Generation Time: {lastGenerationTime}ms");
        Debug.Log($"Chunk Resolution: {chunkResolution}");
        Debug.Log($"Chunk Size: {chunkSize}");
        Debug.Log($"Height Scale: {heightScale}");
        Debug.Log($"Show Wireframe: {showWireframe}");
        Debug.Log($"Show Height Colors: {showHeightColors}");
        Debug.Log("========================");
    }
    
    /// <summary>
    /// Updates the visual representation (called from Update)
    /// </summary>
    private void UpdateVisuals()
    {
        if (chunkVisuals == null) return;
        
        // Update materials if needed
        if (showHeightColors && chunkMaterials != null)
        {
            int gridSize = Mathf.CeilToInt(Mathf.Sqrt(testChunkCount));
            for (int i = 0; i < testChunkCount; i++)
            {
                int x = i % gridSize;
                int z = i / gridSize;
                var chunkPos = new int2(x, z);
                
                if (chunkMaterials[i] != null)
                {
                    chunkMaterials[i].color = GetHeightColor(chunkPos);
                }
            }
        }
    }
    
    private void Update()
    {
        UpdateVisuals();
    }
    
    /// <summary>
    /// Cleanup method
    /// </summary>
    private void OnDestroy()
    {
        // Clean up visual objects
        if (chunkVisuals != null)
        {
            foreach (var chunk in chunkVisuals)
            {
                if (chunk != null)
                {
                    DestroyImmediate(chunk);
                }
            }
        }
        
        // Clean up meshes
        if (chunkMeshes != null)
        {
            foreach (var mesh in chunkMeshes)
            {
                if (mesh != null)
                {
                    DestroyImmediate(mesh);
                }
            }
        }
        
        // Clean up materials
        if (chunkMaterials != null)
        {
            foreach (var material in chunkMaterials)
            {
                if (material != null)
                {
                    DestroyImmediate(material);
                }
            }
        }
        
        // Clean up terrain entities
        if (entityManager != null)
        {
            var entities = entityManager.GetAllTerrainEntities();
            foreach (var entity in entities)
            {
                entityManager.DestroyTerrainEntity(entity);
            }
        }
    }
    
    /// <summary>
    /// Context menu for running the test
    /// </summary>
    [ContextMenu("Run Visual Debug Test")]
    private void RunTest()
    {
        RunVisualDebugTest();
    }
    
    /// <summary>
    /// Context menu for cleaning up
    /// </summary>
    [ContextMenu("Cleanup Visual Test")]
    private void CleanupTest()
    {
        OnDestroy();
    }
} 