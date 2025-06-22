using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LODDebugger : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool showDebugInfo = true;
    public bool showPerformanceInfo = true;
    public bool showLODStats = true;
    
    [Header("Test Controls")]
    public bool testLODSystem = false;
    public bool testMaterialCreation = false;
    
    private LODTerrainManager lodTerrainManager;
    private LODSystem lodSystem;
    private float lastUpdateTime;
    private int frameCount;
    private float fps;
    
    void Start()
    {
        lodTerrainManager = FindFirstObjectByType<LODTerrainManager>();
        lodSystem = FindFirstObjectByType<LODSystem>();
        
        if (lodTerrainManager == null)
        {
            Debug.LogError("LODTerrainManager not found in scene!");
        }
        
        if (lodSystem == null)
        {
            Debug.LogWarning("LODSystem not found in scene!");
        }
    }
    
    void Update()
    {
        // Calculate FPS
        frameCount++;
        if (Time.time - lastUpdateTime >= 1.0f)
        {
            fps = frameCount / (Time.time - lastUpdateTime);
            frameCount = 0;
            lastUpdateTime = Time.time;
        }
        
        // Keyboard shortcuts for debug controls
        if (Input.GetKeyDown(KeyCode.F1))
        {
            TestLODSystem();
        }
        
        if (Input.GetKeyDown(KeyCode.F2))
        {
            TestMaterialCreation();
        }
        
        if (Input.GetKeyDown(KeyCode.F3))
        {
            TestClosestChunkCollider();
        }
        
        if (Input.GetKeyDown(KeyCode.F4))
        {
            CheckAllColliders();
        }
        
        if (Input.GetKeyDown(KeyCode.F5))
        {
            CheckMaterials();
        }
        
        if (Input.GetKeyDown(KeyCode.F6))
        {
            // Toggle LOD info display in debugger
            showLODStats = !showLODStats;
            Debug.Log($"LOD Stats display: {showLODStats}");
        }
        
        if (Input.GetKeyDown(KeyCode.F7))
        {
            // Toggle gizmos (scene view spheres/wire cubes)
            LODTerrainChunk.GlobalGizmosEnabled = !LODTerrainChunk.GlobalGizmosEnabled;
            Debug.Log($"Global gizmos: {LODTerrainChunk.GlobalGizmosEnabled}");
        }
        
        if (Input.GetKeyDown(KeyCode.F8))
        {
            // Toggle text overlays (game view text)
            LODTerrainChunk.GlobalTextOverlayEnabled = !LODTerrainChunk.GlobalTextOverlayEnabled;
            Debug.Log($"Global text overlays: {LODTerrainChunk.GlobalTextOverlayEnabled}");
        }

        // Test LOD system
        if (testLODSystem)
        {
            TestLODSystem();
            testLODSystem = false;
        }

        // Test material creation
        if (testMaterialCreation)
        {
            TestMaterialCreation();
            testMaterialCreation = false;
        }
    }
    
    void OnGUI()
    {
        if (!showDebugInfo)
            return;
            
        // Increase the GUI area to accommodate all buttons
        GUILayout.BeginArea(new Rect(10, 10, 400, 500)); // Changed from 300 to 500 height
        GUILayout.Label("LOD System Debug Info", GUI.skin.box);
        
        // Basic info
        GUILayout.Label($"FPS: {fps:F1}");
        GUILayout.Label($"Time: {Time.time:F1}s");
        
        if (showPerformanceInfo)
        {
            GUILayout.Space(10);
            GUILayout.Label("Performance Info", GUI.skin.box);
            
            if (lodTerrainManager != null)
            {
                GUILayout.Label($"Active Chunks: {lodTerrainManager.GetActiveChunkCount()}");
                GUILayout.Label($"Total Chunks: {lodTerrainManager.GetActiveChunkCount()}");
            }
            
            if (lodSystem != null && lodSystem.lodSettings != null)
            {
                GUILayout.Label($"LOD Enabled: {lodSystem.lodSettings.enableLOD}");
                GUILayout.Label($"Update Interval: {lodSystem.lodSettings.updateInterval}s");
            }
        }
        
        if (showLODStats && lodTerrainManager != null)
        {
            GUILayout.Space(10);
            GUILayout.Label("LOD Statistics", GUI.skin.box);
            
            Dictionary<string, int> stats = lodTerrainManager.GetLODLevelStats();
            foreach (var kvp in stats)
            {
                GUILayout.Label($"{kvp.Key}: {kvp.Value} chunks");
            }
        }
        
        // Test buttons
        GUILayout.Space(10);
        GUILayout.Label("Test Controls", GUI.skin.box);
        
        if (GUILayout.Button("Test LOD System"))
        {
            TestLODSystem();
        }
        
        if (GUILayout.Button("Test Material Creation"))
        {
            TestMaterialCreation();
        }
        
        if (GUILayout.Button("Toggle LOD System"))
        {
            if (lodTerrainManager != null)
            {
                bool currentState = lodSystem != null && lodSystem.lodSettings != null && lodSystem.lodSettings.enableLOD;
                lodTerrainManager.SetLODSystemEnabled(!currentState);
            }
        }
        
        if (GUILayout.Button("Force Enable Colliders"))
        {
            if (lodTerrainManager != null)
            {
                lodTerrainManager.EnsureCollidersEnabled();
            }
        }
        
        if (GUILayout.Button("Test Closest Chunk"))
        {
            TestClosestChunkCollider();
        }
        
        if (GUILayout.Button("Check All Colliders"))
        {
            CheckAllColliders();
        }
        
        if (GUILayout.Button("Check Materials"))
        {
            CheckMaterials();
        }
        
        // Add the new debug control buttons
        GUILayout.Space(5);
        GUILayout.Label("Debug Display Controls", GUI.skin.box);
        
        if (GUILayout.Button($"LOD Stats Display (F6) - {showLODStats}"))
        {
            showLODStats = !showLODStats;
            Debug.Log($"LOD Stats display: {showLODStats}");
        }
        
        if (GUILayout.Button($"Scene Gizmos (F7) - {LODTerrainChunk.GlobalGizmosEnabled}"))
        {
            LODTerrainChunk.GlobalGizmosEnabled = !LODTerrainChunk.GlobalGizmosEnabled;
            Debug.Log($"Global gizmos: {LODTerrainChunk.GlobalGizmosEnabled}");
        }
        
        if (GUILayout.Button($"Text Overlays (F8) - {LODTerrainChunk.GlobalTextOverlayEnabled}"))
        {
            LODTerrainChunk.GlobalTextOverlayEnabled = !LODTerrainChunk.GlobalTextOverlayEnabled;
            Debug.Log($"Global text overlays: {LODTerrainChunk.GlobalTextOverlayEnabled}");
        }
        
        // Quick toggle for all chunk debug
        if (GUILayout.Button("Toggle All Chunk Debug"))
        {
            bool newState = !LODTerrainChunk.GlobalGizmosEnabled;
            LODTerrainChunk.GlobalGizmosEnabled = newState;
            LODTerrainChunk.GlobalTextOverlayEnabled = newState;
            Debug.Log($"All chunk debug: {newState}");
        }

        GUILayout.EndArea();
    }
    
    void TestLODSystem()
    {
        Debug.Log("=== LOD System Test ===");

        if (lodTerrainManager == null)
        {
            Debug.LogError("LODTerrainManager is null!");
            return;
        }

        if (lodSystem == null)
        {
            Debug.LogError("LODSystem is null!");
            return;
        }

        if (lodSystem.lodSettings == null)
        {
            Debug.LogError("LODSettings is null!");
            return;
        }

        Debug.Log($"LOD System Status: {(lodSystem.lodSettings.enableLOD ? "Enabled" : "Disabled")}");
        Debug.Log($"LOD Levels: {lodSystem.lodSettings.lodLevels.Length}");
        Debug.Log($"Active Chunks: {lodTerrainManager.GetActiveChunkCount()}");

        // Test distance calculations
        if (lodSystem.player != null)
        {
            Vector3 playerPos = lodSystem.player.position;
            Debug.Log($"Player Position: {playerPos}");

            LODTerrainChunk[] chunks = FindObjectsByType<LODTerrainChunk>(FindObjectsSortMode.None);
            foreach (var chunk in chunks)
            {
                Vector3 chunkCenter = chunk.transform.position + new Vector3(chunk.width / 2f, 0, chunk.depth / 2f);
                float distance = Vector3.Distance(playerPos, chunkCenter);
                LODLevel currentLOD = chunk.GetCurrentLODLevel();

                Debug.Log($"Chunk {chunk.name}: Center={chunkCenter}, Distance={distance:F1}m, LOD={currentLOD?.name ?? "None"}");
            }
        }

        // Test LOD level stats
        Dictionary<string, int> stats = lodTerrainManager.GetLODLevelStats();
        foreach (var kvp in stats)
        {
            Debug.Log($"LOD Level {kvp.Key}: {kvp.Value} chunks");
        }

        Debug.Log("=== LOD System Test Complete ===");
    }
    
    void TestMaterialCreation()
    {
        Debug.Log("=== Material Creation Test ===");
        
        // Test shader finding
        Shader urpShader = Shader.Find("Universal Render Pipeline/Lit");
        Debug.Log($"URP Shader found: {urpShader != null}");
        
        Shader standardShader = Shader.Find("Standard");
        Debug.Log($"Standard Shader found: {standardShader != null}");
        
        Shader unlitShader = Shader.Find("Unlit/Color");
        Debug.Log($"Unlit Shader found: {unlitShader != null}");
        
        // Test material loading
        Material terrainMat = Resources.Load<Material>("TerrainMat");
        Debug.Log($"TerrainMat loaded from Resources: {terrainMat != null}");
        
        if (terrainMat == null)
        {
            terrainMat = Resources.Load<Material>("Materials/TerrainMat");
            Debug.Log($"TerrainMat loaded from Materials/Resources: {terrainMat != null}");
        }
        
        // Test material creation
        Material testMaterial = null;
        if (urpShader != null)
        {
            testMaterial = new Material(urpShader);
            Debug.Log("Successfully created URP material");
        }
        else if (standardShader != null)
        {
            testMaterial = new Material(standardShader);
            Debug.Log("Successfully created Standard material");
        }
        else if (unlitShader != null)
        {
            testMaterial = new Material(unlitShader);
            Debug.Log("Successfully created Unlit material");
        }
        else
        {
            Debug.LogError("No suitable shader found for material creation!");
        }
        
        if (testMaterial != null)
        {
            DestroyImmediate(testMaterial);
        }
        
        Debug.Log("=== Material Creation Test Complete ===");
    }
    
    void TestClosestChunkCollider()
    {
        Debug.Log("=== Closest Chunk Collider Test ===");

        if (lodSystem == null || lodSystem.player == null)
        {
            Debug.LogError("LODSystem or player is null!");
            return;
        }

        Vector3 playerPos = lodSystem.player.position;
        LODTerrainChunk[] chunks = FindObjectsByType<LODTerrainChunk>(FindObjectsSortMode.None);

        if (chunks.Length == 0)
        {
            Debug.LogError("No LODTerrainChunks found!");
            return;
        }

        // Find the closest chunk
        LODTerrainChunk closestChunk = null;
        float closestDistance = float.MaxValue;

        foreach (var chunk in chunks)
        {
            Vector3 chunkCenter = chunk.transform.position + new Vector3(chunk.width / 2f, 0, chunk.depth / 2f);
            float distance = Vector3.Distance(playerPos, chunkCenter);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestChunk = chunk;
            }
        }

        if (closestChunk != null)
        {
            Debug.Log($"Closest chunk: {closestChunk.name}");
            Debug.Log($"Distance to chunk center: {closestDistance:F1}m");

            // Check LOD level
            LODLevel currentLOD = closestChunk.GetCurrentLODLevel();
            Debug.Log($"Current LOD Level: {currentLOD?.name ?? "None"}");

            // Check collider
            MeshCollider collider = closestChunk.GetComponent<MeshCollider>();
            if (collider != null)
            {
                Debug.Log($"Collider enabled: {collider.enabled}");
                Debug.Log($"Collider has mesh: {collider.sharedMesh != null}");

                if (collider.enabled && collider.sharedMesh != null)
                {
                    Debug.Log("✅ Closest chunk has working collider!");
                }
                else
                {
                    Debug.LogError("❌ Closest chunk collider is not working properly!");
                }
            }
            else
            {
                Debug.LogError("❌ Closest chunk has no MeshCollider component!");
            }

            // Check if it should be High LOD
            if (closestDistance <= 5f)
            {
                if (currentLOD?.name == "High")
                {
                    Debug.Log("✅ Closest chunk is correctly using High LOD");
                }
                else
                {
                    Debug.LogWarning($"⚠️ Closest chunk should be High LOD but is using {currentLOD?.name ?? "None"}");
                }
            }
        }

        Debug.Log("=== Closest Chunk Collider Test Complete ===");
    }
    
    void CheckAllColliders()
    {
        Debug.Log("=== All Colliders Check ===");

        LODTerrainChunk[] chunks = FindObjectsByType<LODTerrainChunk>(FindObjectsSortMode.None);

        if (chunks.Length == 0)
        {
            Debug.LogError("No LODTerrainChunks found!");
            return;
        }

        int totalChunks = chunks.Length;
        int chunksWithColliders = 0;
        int chunksWithEnabledColliders = 0;
        int chunksWithMeshes = 0;
        int chunksWithWorkingColliders = 0;

        foreach (var chunk in chunks)
        {
            MeshCollider collider = chunk.GetComponent<MeshCollider>();

            if (collider != null)
            {
                chunksWithColliders++;

                if (collider.enabled)
                {
                    chunksWithEnabledColliders++;
                }

                if (collider.sharedMesh != null)
                {
                    chunksWithMeshes++;

                    if (collider.enabled && collider.sharedMesh != null)
                    {
                        chunksWithWorkingColliders++;
                    }
                }

                Debug.Log($"Chunk {chunk.name}: Collider={collider != null}, Enabled={collider.enabled}, HasMesh={collider.sharedMesh != null}, MeshName={collider.sharedMesh?.name ?? "None"}");
            }
            else
            {
                Debug.LogError($"Chunk {chunk.name}: No MeshCollider component!");
            }
        }

        Debug.Log($"=== Collider Summary ===");
        Debug.Log($"Total Chunks: {totalChunks}");
        Debug.Log($"Chunks with Colliders: {chunksWithColliders}");
        Debug.Log($"Chunks with Enabled Colliders: {chunksWithEnabledColliders}");
        Debug.Log($"Chunks with Meshes: {chunksWithMeshes}");
        Debug.Log($"Chunks with Working Colliders: {chunksWithWorkingColliders}");

        if (chunksWithWorkingColliders == totalChunks)
        {
            Debug.Log("✅ All chunks have working colliders!");
        }
        else
        {
            Debug.LogError($"❌ Only {chunksWithWorkingColliders}/{totalChunks} chunks have working colliders!");
        }

        Debug.Log("=== All Colliders Check Complete ===");
    }
    
    void CheckMaterials()
    {
        Debug.Log("=== Materials Check ===");

        LODTerrainChunk[] chunks = FindObjectsByType<LODTerrainChunk>(FindObjectsSortMode.None);

        if (chunks.Length == 0)
        {
            Debug.LogError("No LODTerrainChunks found!");
            return;
        }

        int totalChunks = chunks.Length;
        int chunksWithMaterials = 0;
        int chunksWithWorkingMaterials = 0;

        foreach (var chunk in chunks)
        {
            MeshRenderer renderer = chunk.GetComponent<MeshRenderer>();

            if (renderer != null)
            {
                Material material = renderer.material;

                if (material != null)
                {
                    chunksWithMaterials++;

                    // Check if material has a valid shader
                    if (material.shader != null)
                    {
                        chunksWithWorkingMaterials++;
                        Debug.Log($"Chunk {chunk.name}: Material={material.name}, Shader={material.shader.name}");
                    }
                    else
                    {
                        Debug.LogError($"Chunk {chunk.name}: Material has no shader!");
                    }
                }
                else
                {
                    Debug.LogError($"Chunk {chunk.name}: MeshRenderer has no material!");
                }
            }
            else
            {
                Debug.LogError($"Chunk {chunk.name}: No MeshRenderer component!");
            }
        }

        Debug.Log($"=== Material Summary ===");
        Debug.Log($"Total Chunks: {totalChunks}");
        Debug.Log($"Chunks with Materials: {chunksWithMaterials}");
        Debug.Log($"Chunks with Working Materials: {chunksWithWorkingMaterials}");

        if (chunksWithWorkingMaterials == totalChunks)
        {
            Debug.Log("✅ All chunks have working materials!");
        }
        else
        {
            Debug.LogError($"❌ Only {chunksWithWorkingMaterials}/{totalChunks} chunks have working materials!");
        }

        Debug.Log("=== Materials Check Complete ===");
    }
} 