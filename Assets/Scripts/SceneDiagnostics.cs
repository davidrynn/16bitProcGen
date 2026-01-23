using System.Collections;
using System.Collections.Generic;
using System.Text;
using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;
using DOTS.Terrain;
using DOTS.Terrain.Rendering;
using Unity.Collections;
using Unity.Rendering;

/// <summary>
/// Comprehensive diagnostic helper for basic scene setup.
/// Checks player, camera, and terrain setup and logs detailed information.
/// Add this to any GameObject in your scene to get diagnostic reports.
/// </summary>
public class SceneDiagnostics : MonoBehaviour
{
    [Header("Diagnostic Settings")]
    [Tooltip("Run diagnostics automatically on Start")]
    public bool runOnStart = true;
    
    [Tooltip("Run diagnostics periodically during play")]
    public bool runPeriodically = false;
    
    [Tooltip("Interval between periodic checks (seconds)")]
    public float periodicInterval = 5f;
    
    [Tooltip("Show diagnostic results on screen")]
    public bool showOnScreen = true;
    
    [Tooltip("Show detailed component information")]
    public bool showDetailedInfo = true;
    
    [Header("Filter Options")]
    [Tooltip("Check player setup")]
    public bool checkPlayer = true;
    
    [Tooltip("Check camera setup")]
    public bool checkCamera = true;
    
    [Tooltip("Check terrain setup")]
    public bool checkTerrain = true;
    
    [Tooltip("Check DOTS terrain entities")]
    public bool checkDOTSTerrain = true;
    
    [Tooltip("Check lighting setup")]
    public bool checkLighting = true;

    [Tooltip("Include Surface Nets mesh stats and winding sampling (costly if many chunks)")]
    public bool checkSurfaceNetsDebug = true;
    
    private float lastPeriodicCheck = 0f;
    private StringBuilder reportBuilder = new StringBuilder();
    private int diagnosticRunCount = 0;
    
    void Start()
    {
        if (runOnStart)
        {
            StartCoroutine(RunDiagnosticsDelayed(0.5f)); // Small delay to let systems initialize
        }
    }
    
    void Update()
    {
        if (runPeriodically && Time.time - lastPeriodicCheck >= periodicInterval)
        {
            lastPeriodicCheck = Time.time;
            RunDiagnostics();
        }
    }
    
    [ContextMenu("Run Diagnostics Now")]
    public void RunDiagnosticsNow()
    {
        RunDiagnostics();
    }
    
    private IEnumerator RunDiagnosticsDelayed(float delay)
    {
        yield return new WaitForSeconds(delay);
        RunDiagnostics();
    }
    
    public void RunDiagnostics()
    {
        diagnosticRunCount++;
        reportBuilder.Clear();
        
        reportBuilder.AppendLine("========================================");
        reportBuilder.AppendLine($"SCENE DIAGNOSTICS REPORT #{diagnosticRunCount}");
        reportBuilder.AppendLine($"Time: {Time.time:F2}s | Frame: {Time.frameCount}");
        reportBuilder.AppendLine("========================================");
        
        if (checkPlayer)
        {
            CheckPlayerSetup();
        }
        
        if (checkCamera)
        {
            CheckCameraSetup();
        }
        
        if (checkTerrain)
        {
            CheckTerrainSetup();
        }
        
        if (checkDOTSTerrain)
        {
            CheckDOTSTerrainSetup();
        }
        
        if (checkLighting)
        {
            CheckLightingSetup();
        }
        
        // Summary
        reportBuilder.AppendLine("========================================");
        reportBuilder.AppendLine("END OF DIAGNOSTICS REPORT");
        reportBuilder.AppendLine("========================================");
        
        Debug.Log(reportBuilder.ToString());
    }
    
    private void CheckPlayerSetup()
    {
        reportBuilder.AppendLine("\n--- PLAYER SETUP ---");
        
        // Check ECS Player Entity
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld != null && defaultWorld.IsCreated)
        {
            var entityManager = defaultWorld.EntityManager;
            using var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            int playerCount = playerQuery.CalculateEntityCount();
            
            if (playerCount > 0)
            {
                reportBuilder.AppendLine($"✓ Found {playerCount} player entity/entities (ECS)");
                
                if (showDetailedInfo)
                {
                    var playerEntity = playerQuery.GetSingletonEntity();
                    
                    // Check components
                    var hasTransform = entityManager.HasComponent<LocalTransform>(playerEntity);
                    var hasMovement = entityManager.HasComponent<PlayerMovementConfig>(playerEntity);
                    var hasInput = entityManager.HasComponent<PlayerInputComponent>(playerEntity);
                    var hasView = entityManager.HasComponent<PlayerViewComponent>(playerEntity);
                    
                    reportBuilder.AppendLine($"  Components: Transform={hasTransform}, Movement={hasMovement}, Input={hasInput}, View={hasView}");
                    
                    if (hasTransform)
                    {
                        var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
                        reportBuilder.AppendLine($"  Position: {transform.Position}");
                        reportBuilder.AppendLine($"  Rotation: {transform.Rotation.value}");
                    }
                }
            }
            else
            {
                reportBuilder.AppendLine("✗ No player entity found (ECS)");
            }
        }
        else
        {
            reportBuilder.AppendLine("✗ DOTS World not available");
        }
        
        // Check Player GameObject
        var playerVisual = GameObject.Find("Player Visual (ECS Synced)");
        if (playerVisual != null)
        {
            reportBuilder.AppendLine($"✓ Found player visual GameObject: {playerVisual.name}");
            reportBuilder.AppendLine($"  Position: {playerVisual.transform.position}");
            reportBuilder.AppendLine($"  Active: {playerVisual.activeInHierarchy}");
            
            if (showDetailedInfo)
            {
                // Check for PlayerVisualSync component by name (avoiding assembly reference)
                var components = playerVisual.GetComponents<MonoBehaviour>();
                bool hasSync = false;
                foreach (var comp in components)
                {
                    if (comp != null && comp.GetType().Name == "PlayerVisualSync")
                    {
                        hasSync = true;
                        break;
                    }
                }
                reportBuilder.AppendLine($"  Has PlayerVisualSync: {hasSync}");
            }
        }
        else
        {
            reportBuilder.AppendLine("✗ No player visual GameObject found");
        }
        
        // Check for legacy player
        var legacyPlayer = GameObject.Find("Player");
        if (legacyPlayer != null)
        {
            reportBuilder.AppendLine($"⚠ Found legacy Player GameObject: {legacyPlayer.name}");
        }
    }
    
    private void CheckCameraSetup()
    {
        reportBuilder.AppendLine("\n--- CAMERA SETUP ---");
        
        // Check ECS Camera Entity
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld != null && defaultWorld.IsCreated)
        {
            var entityManager = defaultWorld.EntityManager;
            using var cameraQuery = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            int cameraCount = cameraQuery.CalculateEntityCount();
            
            if (cameraCount > 0)
            {
                reportBuilder.AppendLine($"✓ Found {cameraCount} camera entity/entities (ECS)");
                
                if (showDetailedInfo)
                {
                    var cameraEntity = cameraQuery.GetSingletonEntity();
                    var hasTransform = entityManager.HasComponent<LocalTransform>(cameraEntity);
                    
                    reportBuilder.AppendLine($"  Has Transform: {hasTransform}");
                    
                    if (hasTransform)
                    {
                        var transform = entityManager.GetComponentData<LocalTransform>(cameraEntity);
                        reportBuilder.AppendLine($"  Position: {transform.Position}");
                    }
                }
            }
            else
            {
                reportBuilder.AppendLine("✗ No camera entity found (ECS)");
            }
        }
        
        // Check Camera GameObject
        var cameraGO = GameObject.Find("Main Camera (ECS Player)");
        if (cameraGO == null)
        {
            cameraGO = Camera.main?.gameObject;
        }
        
        if (cameraGO != null)
        {
            reportBuilder.AppendLine($"✓ Found camera GameObject: {cameraGO.name}");
            
            var camera = cameraGO.GetComponent<Camera>();
            if (camera != null)
            {
                reportBuilder.AppendLine($"  Enabled: {camera.enabled}");
                reportBuilder.AppendLine($"  Tag: {cameraGO.tag}");
                reportBuilder.AppendLine($"  Position: {cameraGO.transform.position}");
                reportBuilder.AppendLine($"  Rotation: {cameraGO.transform.rotation.eulerAngles}");
                reportBuilder.AppendLine($"  FOV: {camera.fieldOfView}");
                reportBuilder.AppendLine($"  Near: {camera.nearClipPlane}, Far: {camera.farClipPlane}");
                
                if (showDetailedInfo)
                {
                    reportBuilder.AppendLine($"  Clear Flags: {camera.clearFlags}");
                    reportBuilder.AppendLine($"  Culling Mask: {camera.cullingMask}");
                }
            }
            else
            {
                reportBuilder.AppendLine("  ✗ Camera component missing!");
            }
        }
        else
        {
            reportBuilder.AppendLine("✗ No camera GameObject found");
        }
    }
    
    private void CheckTerrainSetup()
    {
        reportBuilder.AppendLine("\n--- TERRAIN SETUP (SDF DOTS Terrain) ---");
        
        World defaultWorld = World.DefaultGameObjectInjectionWorld;
        if (defaultWorld != null && defaultWorld.IsCreated)
        {
            var entityManager = defaultWorld.EntityManager;
            
            // Use reflection to get terrain chunk component type
            var terrainChunkType = System.Type.GetType("DOTS.Terrain.TerrainChunk, DOTS.Terrain");
            if (terrainChunkType != null)
            {
                using var terrainChunkQuery = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly(terrainChunkType)
                );
                int terrainChunkCount = terrainChunkQuery.CalculateEntityCount();
                
                if (terrainChunkCount > 0)
                {
                    reportBuilder.AppendLine($"✓ Found {terrainChunkCount} SDF terrain chunk entity/entities");
                    
                    if (showDetailedInfo)
                    {
                        // Get component types via reflection
                        var gridInfoType = System.Type.GetType("DOTS.Terrain.TerrainChunkGridInfo, DOTS.Terrain");
                        var boundsType = System.Type.GetType("DOTS.Terrain.TerrainChunkBounds, DOTS.Terrain");
                        var densityType = System.Type.GetType("DOTS.Terrain.TerrainChunkDensity, DOTS.Terrain");
                        var meshDataType = System.Type.GetType("DOTS.Terrain.TerrainChunkMeshData, DOTS.Terrain");
#if UNITY_ENTITIES_GRAPHICS
                        var renderFilterSettingsType = System.Type.GetType("Unity.Entities.Graphics.RenderFilterSettings, Unity.Entities.Graphics");
#endif
                        
                        int hasGridInfo = 0;
                        int hasBounds = 0;
                        int hasDensity = 0;
                        int hasMeshData = 0;
                        int hasRenderBounds = 0;
                        int hasMaterialMeshInfo = 0;
                        int hasWorldRenderBounds = 0;
                        int hasRenderFilterSettings = 0;
                        int hasRenderMeshArray = 0;
                        int hasTransform = 0;
                        
                        using var entities = terrainChunkQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                        int sampleCount = Mathf.Min(3, entities.Length);
                        
                        for (int i = 0; i < entities.Length; i++)
                        {
                            var entity = entities[i];
                            if (gridInfoType != null && entityManager.HasComponent(entity, gridInfoType)) hasGridInfo++;
                            if (boundsType != null && entityManager.HasComponent(entity, boundsType)) hasBounds++;
                            if (densityType != null && entityManager.HasComponent(entity, densityType)) hasDensity++;
                            if (meshDataType != null && entityManager.HasComponent(entity, meshDataType)) hasMeshData++;
                            if (entityManager.HasComponent<RenderBounds>(entity)) hasRenderBounds++;
                            if (entityManager.HasComponent<MaterialMeshInfo>(entity)) hasMaterialMeshInfo++;
#if UNITY_ENTITIES_GRAPHICS
                            if (entityManager.HasComponent<WorldRenderBounds>(entity)) hasWorldRenderBounds++;
                            if (renderFilterSettingsType != null && entityManager.HasComponent(entity, renderFilterSettingsType)) hasRenderFilterSettings++;
                            if (entityManager.HasComponent<RenderMeshArray>(entity)) hasRenderMeshArray++;
#endif
                            if (entityManager.HasComponent<LocalTransform>(entity)) hasTransform++;
                        }
                        
                        reportBuilder.AppendLine($"  Components:");
                        reportBuilder.AppendLine($"    TerrainChunkGridInfo: {hasGridInfo}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    TerrainChunkBounds: {hasBounds}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    TerrainChunkDensity: {hasDensity}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    TerrainChunkMeshData: {hasMeshData}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    RenderBounds: {hasRenderBounds}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    MaterialMeshInfo: {hasMaterialMeshInfo}/{terrainChunkCount}");
#if UNITY_ENTITIES_GRAPHICS
                        reportBuilder.AppendLine($"    WorldRenderBounds: {hasWorldRenderBounds}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    RenderFilterSettings: {hasRenderFilterSettings}/{terrainChunkCount}");
                        reportBuilder.AppendLine($"    RenderMeshArray: {hasRenderMeshArray}/{terrainChunkCount}");
#endif
                        reportBuilder.AppendLine($"    LocalTransform: {hasTransform}/{terrainChunkCount}");
                        
                        // Sample chunk details (simplified - can't access component data via reflection easily)
                        if (sampleCount > 0)
                        {
                            reportBuilder.AppendLine("  Sample chunks:");
                            for (int i = 0; i < sampleCount; i++)
                            {
                                var entity = entities[i];
                                reportBuilder.AppendLine($"    [{i}] Entity {entity.Index}");
                                
                                if (entityManager.HasComponent<LocalTransform>(entity))
                                {
                                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                                    reportBuilder.AppendLine($"        Position: {transform.Position}");
                                }
                            }
                        }

                        if (checkSurfaceNetsDebug)
                        {
                            LogTerrainPipelinePending(entityManager);
                            LogTerrainChunkMeshStats(entityManager);
                            LogSurfaceNetsWindingSample(entityManager);
                        }
                    }
                }
                else
                {
                    reportBuilder.AppendLine("  ⚠ No SDF terrain chunk entities found");
                }
            }
            else
            {
                reportBuilder.AppendLine("  ⚠ DOTS.Terrain assembly not available (terrain types not found)");
            }
            
            // Check for SDF terrain systems
            CheckSDFTerrainSystems(defaultWorld);
        }
        else
        {
            reportBuilder.AppendLine("  ✗ DOTS World not available");
        }
        
        // Check for legacy terrain (warn if found)
        CheckLegacyTerrain();
    }
    
    private void CheckSDFTerrainSystems(World world)
    {
        reportBuilder.AppendLine("\n  --- SDF Terrain Systems ---");

#if UNITY_ENTITIES_GRAPHICS
        reportBuilder.AppendLine("  Entities Graphics: Enabled (UNITY_ENTITIES_GRAPHICS)");
#else
        reportBuilder.AppendLine("  Entities Graphics: Disabled (UNITY_ENTITIES_GRAPHICS not defined)");
#endif

        try
        {
            var settings = TerrainChunkRenderSettingsProvider.GetOrLoad();
            reportBuilder.AppendLine($"  Render Settings: {(settings != null ? "Found" : "Missing")}, Material={(settings != null && settings.ChunkMaterial != null ? "Assigned" : "None")}");
        }
        catch
        {
            reportBuilder.AppendLine("  Render Settings: Error checking");
        }
        
        int activeSystemCount = 0;
        
        // Check for density sampling system using reflection
        try
        {
            var needsDensityRebuildType = System.Type.GetType("DOTS.Terrain.TerrainChunkNeedsDensityRebuild, DOTS.Terrain");
            if (needsDensityRebuildType != null)
            {
                using var densityQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly(needsDensityRebuildType)
                );
                int needsDensityCount = densityQuery.CalculateEntityCount();
                reportBuilder.AppendLine($"  Density Sampling: {(needsDensityCount > 0 ? "Entities need processing" : "No entities pending")}");
                activeSystemCount++;
            }
            else
            {
                reportBuilder.AppendLine($"  Density Sampling: Component type not available");
            }
        }
        catch
        {
            reportBuilder.AppendLine($"  Density Sampling: Error checking");
        }
        
        // Check for mesh build system using reflection
        try
        {
            var needsMeshBuildType = System.Type.GetType("DOTS.Terrain.TerrainChunkNeedsMeshBuild, DOTS.Terrain");
            if (needsMeshBuildType != null)
            {
                using var meshBuildQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly(needsMeshBuildType)
                );
                int needsMeshCount = meshBuildQuery.CalculateEntityCount();
                reportBuilder.AppendLine($"  Mesh Build: {(needsMeshCount > 0 ? "Entities need processing" : "No entities pending")}");
                activeSystemCount++;
            }
            else
            {
                reportBuilder.AppendLine($"  Mesh Build: Component type not available");
            }
        }
        catch
        {
            reportBuilder.AppendLine($"  Mesh Build: Error checking");
        }
        
        // Check for render prep (entities with RenderBounds)
        try
        {
            using var renderQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<RenderBounds>()
            );
            int renderableCount = renderQuery.CalculateEntityCount();
            reportBuilder.AppendLine($"  Render Prep: {renderableCount} entities with RenderBounds");
            if (renderableCount > 0) activeSystemCount++;
        }
        catch
        {
            reportBuilder.AppendLine($"  Render Prep: Error checking");
        }
        
        // Check for mesh upload (entities with MaterialMeshInfo)
        try
        {
            using var materialQuery = world.EntityManager.CreateEntityQuery(
                ComponentType.ReadOnly<MaterialMeshInfo>()
            );
            int materialCount = materialQuery.CalculateEntityCount();
            reportBuilder.AppendLine($"  Mesh Upload: {materialCount} entities with MaterialMeshInfo");
            if (materialCount > 0) activeSystemCount++;
        }
        catch
        {
            reportBuilder.AppendLine($"  Mesh Upload: Error checking");
        }
        
        reportBuilder.AppendLine($"  Summary: SDF terrain pipeline appears {(activeSystemCount > 0 ? "active" : "inactive")}");
    }
    
    private void CheckLegacyTerrain()
    {
        // Check for legacy GameObject-based terrain (warn if found)
        var legacyChunks = UnityEngine.Object.FindObjectsByType<global::TerrainChunk>(FindObjectsSortMode.None);
        if (legacyChunks.Length > 0)
        {
            reportBuilder.AppendLine($"\n  ⚠ LEGACY: Found {legacyChunks.Length} GameObject-based TerrainChunk component(s)");
            reportBuilder.AppendLine("    (These are from the legacy terrain system - consider migrating to SDF DOTS terrain)");
        }
        
        var terrainManager = TerrainManagerLegacy.Instance;
        if (terrainManager != null)
        {
            reportBuilder.AppendLine($"  ⚠ LEGACY: TerrainManager found (legacy GameObject-based terrain system)");
            reportBuilder.AppendLine("    (Consider migrating to SDF DOTS terrain system)");
        }
    }
    
    private void CheckDOTSTerrainSetup()
    {
        // This method is now integrated into CheckTerrainSetup()
        // Keeping for backward compatibility but redirecting
        CheckTerrainSetup();
    }
    
    private void CheckLightingSetup()
    {
        reportBuilder.AppendLine("\n--- LIGHTING SETUP ---");
        
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        reportBuilder.AppendLine($"Found {lights.Length} Light component(s)");
        
        if (lights.Length > 0)
        {
            int directionalCount = 0;
            int enabledCount = 0;
            
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional) directionalCount++;
                if (light.enabled) enabledCount++;
            }
            
            reportBuilder.AppendLine($"  Directional: {directionalCount}");
            reportBuilder.AppendLine($"  Enabled: {enabledCount}/{lights.Length}");
        }
        else
        {
            reportBuilder.AppendLine("  ⚠ No lights found");
        }
        
        reportBuilder.AppendLine($"  Ambient Intensity: {RenderSettings.ambientIntensity}");
        reportBuilder.AppendLine($"  Ambient Mode: {RenderSettings.ambientMode}");
    }
    
    void OnGUI()
    {
        if (showOnScreen && reportBuilder.Length > 0)
        {
            GUILayout.BeginArea(new Rect(10, 10, 500, Screen.height - 20));
            GUILayout.Box("Scene Diagnostics", GUILayout.Width(480));
            
            // Show last report (truncated for on-screen display)
            string displayText = reportBuilder.ToString();
            if (displayText.Length > 2000)
            {
                displayText = displayText.Substring(0, 2000) + "...\n(Full report in console)";
            }
            
            GUILayout.TextArea(displayText, GUILayout.ExpandHeight(true));
            
            if (GUILayout.Button("Run Diagnostics Now"))
            {
                RunDiagnostics();
            }
            
            GUILayout.EndArea();
        }
    }

    private void LogTerrainPipelinePending(EntityManager entityManager)
    {
        var densityType = System.Type.GetType("DOTS.Terrain.TerrainChunkNeedsDensityRebuild, DOTS.Terrain");
        var meshType = System.Type.GetType("DOTS.Terrain.TerrainChunkNeedsMeshBuild, DOTS.Terrain");
        var uploadType = System.Type.GetType("DOTS.Terrain.TerrainChunkNeedsRenderUpload, DOTS.Terrain");
        var colliderType = System.Type.GetType("DOTS.Terrain.TerrainChunkNeedsColliderBuild, DOTS.Terrain");

        int needsDensity = densityType != null ? entityManager.CreateEntityQuery(ComponentType.ReadOnly(densityType)).CalculateEntityCount() : -1;
        int needsMesh = meshType != null ? entityManager.CreateEntityQuery(ComponentType.ReadOnly(meshType)).CalculateEntityCount() : -1;
        int needsUpload = uploadType != null ? entityManager.CreateEntityQuery(ComponentType.ReadOnly(uploadType)).CalculateEntityCount() : -1;
        int needsCollider = colliderType != null ? entityManager.CreateEntityQuery(ComponentType.ReadOnly(colliderType)).CalculateEntityCount() : -1;

        reportBuilder.AppendLine("  Pipeline Pending:");
        reportBuilder.AppendLine($"    NeedsDensityRebuild: {(needsDensity >= 0 ? needsDensity.ToString() : "N/A")}");
        reportBuilder.AppendLine($"    NeedsMeshBuild: {(needsMesh >= 0 ? needsMesh.ToString() : "N/A")}");
        reportBuilder.AppendLine($"    NeedsRenderUpload: {(needsUpload >= 0 ? needsUpload.ToString() : "N/A")}");
        reportBuilder.AppendLine($"    NeedsColliderBuild: {(needsCollider >= 0 ? needsCollider.ToString() : "N/A")}");
    }

    private void LogTerrainChunkMeshStats(EntityManager entityManager)
    {
        var meshDataType = System.Type.GetType("DOTS.Terrain.TerrainChunkMeshData, DOTS.Terrain");
        var boundsType = System.Type.GetType("DOTS.Terrain.TerrainChunkBounds, DOTS.Terrain");

        if (meshDataType == null || boundsType == null)
        {
            reportBuilder.AppendLine("  Mesh Stats: Terrain types unavailable (DOTS.Terrain not referenced)");
            return;
        }

        using var meshQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly(meshDataType));
        var count = meshQuery.CalculateEntityCount();
        if (count == 0)
        {
            reportBuilder.AppendLine("  Mesh Stats: No chunks with mesh data");
            return;
        }

        reportBuilder.AppendLine($"  Mesh Stats: {count} chunk(s) have mesh data (detailed sampling disabled when using reflection)");
    }

    private void LogSurfaceNetsWindingSample(EntityManager entityManager, int maxChunks = 4, int maxTrianglesPerChunk = 256)
    {
        var diagType = Type.GetType("DOTS.Terrain.Meshing.SurfaceNetsDiagnostics, DOTS.Terrain");
        var meshDataType = Type.GetType("DOTS.Terrain.TerrainChunkMeshData, DOTS.Terrain");

        if (diagType == null || meshDataType == null)
        {
            reportBuilder.AppendLine("  Surface Nets Winding: Skipped (DOTS.Terrain not referenced)");
            return;
        }

        using var meshQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly(meshDataType));
        var count = meshQuery.CalculateEntityCount();
        if (count == 0)
        {
            reportBuilder.AppendLine("  Surface Nets Winding: No mesh data");
            return;
        }

        var method = diagType.GetMethod("TrySample", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (method == null)
        {
            reportBuilder.AppendLine("  Surface Nets Winding: Helper not found");
            return;
        }

        var args = new object[] { entityManager, maxChunks, maxTrianglesPerChunk, 0, 0, 0 };
        var ok = false;
        try
        {
            ok = (bool)method.Invoke(null, args);
        }
        catch
        {
            reportBuilder.AppendLine("  Surface Nets Winding: Invocation failed");
            return;
        }

        if (!ok)
        {
            reportBuilder.AppendLine("  Surface Nets Winding: No chunks sampled");
            return;
        }

        var sampledChunks = (int)args[3];
        var upward = (int)args[4];
        var downward = (int)args[5];

        reportBuilder.AppendLine("  Surface Nets Winding (sampled)");
        reportBuilder.AppendLine($"    Chunks Sampled: {sampledChunks}");
        reportBuilder.AppendLine($"    Upward Triangles: {upward}");
        reportBuilder.AppendLine($"    Downward Triangles: {downward} (should be 0; investigate if >0)");
    }
}

