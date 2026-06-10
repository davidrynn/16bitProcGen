using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Quick Visual Test - Run this to immediately see DOTS terrain working
/// This is a simplified version for quick testing
/// </summary>
public class QuickVisualTest : MonoBehaviour
{
    [Header("Quick Test Settings")]
    public bool runOnStart = true;
    public int chunkCount = 2; // Small test
    public float heightScale = 3f;
    
    private void Start()
    {
        if (runOnStart)
        {
            RunQuickTest();
        }
    }
    
    /// <summary>
    /// Runs a quick visual test
    /// </summary>
    public void RunQuickTest()
    {
        Debug.Log("=== QUICK VISUAL TEST ===");
        
        // Setup camera if needed
        SetupCamera();
        
        // Create a simple terrain chunk
        CreateSimpleTerrain();
        
        Debug.Log("=== QUICK TEST COMPLETE ===");
    }
    
    /// <summary>
    /// Sets up a basic camera
    /// </summary>
    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var cameraGO = new GameObject("Main Camera");
            cam = cameraGO.AddComponent<Camera>();
            cameraGO.tag = "MainCamera";
        }
        
        cam.transform.position = new Vector3(0, 8, -8);
        cam.transform.rotation = Quaternion.Euler(45, 0, 0);
        cam.fieldOfView = 60f;
        
        Debug.Log("✓ Camera setup complete");
    }
    
    /// <summary>
    /// Creates a simple terrain chunk for testing
    /// </summary>
    private void CreateSimpleTerrain()
    {
        Debug.Log("Creating simple terrain...");
        
        // Create a simple mesh
        var mesh = CreateSimpleMesh();
        
        // Create GameObject
        var terrainGO = new GameObject("SimpleTerrain");
        terrainGO.transform.position = Vector3.zero;
        
        // Add mesh components
        var meshFilter = terrainGO.AddComponent<MeshFilter>();
        meshFilter.mesh = mesh;
        
        var meshRenderer = terrainGO.AddComponent<MeshRenderer>();
        
        // Try to load the existing TerrainMat material
        Material material = LoadTerrainMaterial();
        meshRenderer.material = material;
        
        // Add some lighting to make the material visible
        SetupBasicLighting();
        
        Debug.Log("✓ Simple terrain created");
    }
    
    /// <summary>
    /// Sets up basic lighting to make materials visible
    /// </summary>
    private void SetupBasicLighting()
    {
        // Check if we already have a light
        Light existingLight = FindFirstObjectByType<Light>();
        if (existingLight == null)
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45, 45, 0);
            light.intensity = 1.0f;
            light.color = Color.white;
            Debug.Log("✓ Added directional light");
        }
        else
        {
            Debug.Log("✓ Found existing light");
        }
    }
    
    /// <summary>
    /// Loads the existing TerrainMat material or creates a fallback
    /// </summary>
    private Material LoadTerrainMaterial()
    {
        // Try to load the existing TerrainMat by GUID
        var terrainMat = Resources.Load<Material>("Materials/TerrainMat");
        if (terrainMat != null)
        {
            Debug.Log("✓ Loaded existing TerrainMat from Resources");
            var material = new Material(terrainMat); // Create instance
            LogMaterialInfo(material);
            
            // Check if the material has a valid URP shader
            if (IsValidURPMaterial(material))
            {
                return material;
            }
            else
            {
                Debug.LogWarning("TerrainMat has invalid shader, creating URP replacement");
            }
        }
        
        // Try to load by GUID directly (Editor only)
        #if UNITY_EDITOR
        // Try multiple possible GUIDs for TerrainMat
        string[] possibleGuids = {
            "9bbf02c57c169f44facade859a4fc2da", // From prefab reference
            "2de723160d493304fb54f9454c886408", // ColorQuantizeMaterial (wrong one)
            // Add more if needed
        };
        
        foreach (var guid in possibleGuids)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
            {
                var loadedMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if (loadedMat != null && loadedMat.name == "TerrainMat")
                {
                    Debug.Log($"✓ Loaded TerrainMat by GUID from path: {path}");
                    var material = new Material(loadedMat); // Create instance
                    LogMaterialInfo(material);
                    
                    if (IsValidURPMaterial(material))
                    {
                        return material;
                    }
                    else
                    {
                        Debug.LogWarning("TerrainMat has invalid shader, creating URP replacement");
                    }
                }
            }
        }
        
        // Try to find TerrainMat by name
        var allMaterials = UnityEditor.AssetDatabase.FindAssets("t:Material");
        foreach (var guid in allMaterials)
        {
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            var loadedMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
            if (loadedMat != null && loadedMat.name == "TerrainMat")
            {
                Debug.Log($"✓ Found TerrainMat by name from path: {path}");
                var material = new Material(loadedMat); // Create instance
                LogMaterialInfo(material);
                
                if (IsValidURPMaterial(material))
                {
                    return material;
                }
                else
                {
                    Debug.LogWarning("TerrainMat has invalid shader, creating URP replacement");
                }
            }
        }
        #endif
        
        // Create a proper URP material
        Debug.Log("Creating URP terrain material");
        return CreateURPTerrainMaterial();
    }
    
    /// <summary>
    /// Checks if a material is using a valid URP shader
    /// </summary>
    private bool IsValidURPMaterial(Material material)
    {
        if (material == null || material.shader == null)
            return false;
            
        string shaderName = material.shader.name.ToLower();
        return shaderName.Contains("universal") || shaderName.Contains("urp");
    }
    
    /// <summary>
    /// Creates a proper URP terrain material
    /// </summary>
    private Material CreateURPTerrainMaterial()
    {
        // Try URP shaders in order of preference
        Shader[] urpShaders = {
            Shader.Find("Universal Render Pipeline/Lit"),
            Shader.Find("Universal Render Pipeline/Unlit"),
            Shader.Find("Universal Render Pipeline/Simple Lit"),
            Shader.Find("Universal Render Pipeline/Baked Lit")
        };
        
        Shader selectedShader = null;
        foreach (var shader in urpShaders)
        {
            if (shader != null)
            {
                selectedShader = shader;
                Debug.Log($"✓ Using URP shader: {shader.name}");
                break;
            }
        }
        
        if (selectedShader == null)
        {
            Debug.LogWarning("No URP shaders found, falling back to Standard");
            selectedShader = Shader.Find("Standard");
        }
        
        if (selectedShader == null)
        {
            Debug.LogError("No shaders found! Creating error material");
            var errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            errorMaterial.color = Color.red;
            return errorMaterial;
        }
        
        var material = new Material(selectedShader);
        material.name = "URP_TerrainMaterial";
        
        // Set a nice green color for terrain
        Color terrainColor = new Color(0.2f, 0.8f, 0.3f, 1.0f);
        
        // Set color based on shader type
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", terrainColor);
        }
        else if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", terrainColor);
        }
        else
        {
            material.color = terrainColor;
        }
        
        // Set some basic properties for better appearance
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 0.1f);
        }
        
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", 0.0f);
        }
        
        Debug.Log($"✓ Created URP terrain material with color: {terrainColor}");
        return material;
    }
    
    /// <summary>
    /// Logs material information for debugging
    /// </summary>
    private void LogMaterialInfo(Material material)
    {
        Debug.Log($"Material: {material.name}, Shader: {material.shader.name}");
        Debug.Log($"Material color: {material.color}");
        
        // Try to get BaseColor (URP) or Color (Standard)
        try
        {
            if (material.HasProperty("_BaseColor"))
            {
                var baseColor = material.GetColor("_BaseColor");
                Debug.Log($"BaseColor: {baseColor}");
            }
            else if (material.HasProperty("_Color"))
            {
                var color = material.GetColor("_Color");
                Debug.Log($"Color: {color}");
            }
            else
            {
                Debug.LogWarning("Material has neither _BaseColor nor _Color property");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Error accessing material color property: {e.Message}");
        }
    }
    
    /// <summary>
    /// Creates a simple terrain mesh
    /// </summary>
    private Mesh CreateSimpleMesh()
    {
        var mesh = new Mesh();
        mesh.name = "SimpleTerrainMesh";
        
        int resolution = 16;
        var vertices = new Vector3[resolution * resolution];
        var triangles = new int[(resolution - 1) * (resolution - 1) * 6];
        var uvs = new Vector2[resolution * resolution];
        
        // Generate vertices
        for (int z = 0; z < resolution; z++)
        {
            for (int x = 0; x < resolution; x++)
            {
                int index = z * resolution + x;
                
                float xPos = (float)x / (resolution - 1) * 10f;
                float zPos = (float)z / (resolution - 1) * 10f;
                
                // Simple height function
                float xNorm = (float)x / resolution;
                float zNorm = (float)z / resolution;
                float height = Mathf.Sin(xNorm * Mathf.PI * 2) * Mathf.Cos(zNorm * Mathf.PI * 2) * heightScale;
                
                vertices[index] = new Vector3(xPos, height, zPos);
                uvs[index] = new Vector2(xNorm, zNorm);
            }
        }
        
        // Generate triangles
        int triangleIndex = 0;
        for (int z = 0; z < resolution - 1; z++)
        {
            for (int x = 0; x < resolution - 1; x++)
            {
                int vertexIndex = z * resolution + x;
                
                triangles[triangleIndex] = vertexIndex;
                triangles[triangleIndex + 1] = vertexIndex + resolution;
                triangles[triangleIndex + 2] = vertexIndex + 1;
                
                triangles[triangleIndex + 3] = vertexIndex + 1;
                triangles[triangleIndex + 4] = vertexIndex + resolution;
                triangles[triangleIndex + 5] = vertexIndex + resolution + 1;
                
                triangleIndex += 6;
            }
        }
        
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }
    
    /// <summary>
    /// Context menu for running the test
    /// </summary>
    [ContextMenu("Run Quick Test")]
    private void RunTest()
    {
        RunQuickTest();
    }
    
    /// <summary>
    /// Simple test method that can be called from Unity Editor
    /// </summary>
    [ContextMenu("Test Material Only")]
    private void TestMaterialOnly()
    {
        Debug.Log("=== TESTING MATERIAL CREATION ===");
        
        var material = LoadTerrainMaterial();
        if (material != null)
        {
            Debug.Log($"✓ Material created successfully: {material.name}");
            Debug.Log($"Shader: {material.shader.name}");
            Debug.Log($"Color: {material.color}");
            
            // Test if it's a valid URP material
            if (IsValidURPMaterial(material))
            {
                Debug.Log("✓ Valid URP material");
            }
            else
            {
                Debug.LogWarning("⚠ Not a valid URP material");
            }
        }
        else
        {
            Debug.LogError("✗ Failed to create material");
        }
        
        Debug.Log("=== MATERIAL TEST COMPLETE ===");
    }
} 