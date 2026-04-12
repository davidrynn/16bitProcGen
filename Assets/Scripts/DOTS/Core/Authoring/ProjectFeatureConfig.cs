using DOTS.Terrain;
using UnityEngine;

public enum TerrainRenderDistancePreset
{
    Low = 0,
    Medium = 1,
    High = 2,
}

[CreateAssetMenu(menuName = "Config/ProjectFeatureConfig")]
public class ProjectFeatureConfig : ScriptableObject
{
    private const float LowRenderDistance = 120f;
    private const float MediumRenderDistance = 180f;
    private const float HighRenderDistance = 240f;

    [Header("Terrain Render Distance")]
    [Tooltip("Preset render-distance quality. Drives streaming radius, LOD thresholds, and camera far clip automatically.")]
    public TerrainRenderDistancePreset TerrainRenderDistancePreset = TerrainRenderDistancePreset.Medium;

    public float TerrainRenderDistance => TerrainRenderDistancePreset switch
    {
        TerrainRenderDistancePreset.Low => LowRenderDistance,
        TerrainRenderDistancePreset.Medium => MediumRenderDistance,
        TerrainRenderDistancePreset.High => HighRenderDistance,
        _ => MediumRenderDistance,
    };

    [Header("Feature Toggles")]
    public bool EnablePlayerSystem = true;
    public bool EnableTerrainSystem = true;
    public bool EnableDungeonSystem = false;
    public bool EnableWeatherSystem = false;
    public bool EnableRenderingSystem = true;
    public bool EnableTestSystems = false;

    [Header("Player Bootstrap Systems")]
    public bool EnablePlayerBootstrapFixedRateInstaller = true;
    public bool EnablePlayerEntityBootstrap = true;
    public bool EnablePlayerEntityBootstrapPureEcs = false;

    [Header("Player Gameplay Systems")]
    public bool EnablePlayerInputSystem = true;
    public bool EnablePlayerLookSystem = true;
    public bool EnablePlayerMovementSystem = true;
    public bool EnablePlayerGroundingSystem = true;
    public bool EnablePlayerTerrainSafetySystem = true;
    public bool EnablePlayerCameraSystem = true;
    public bool EnablePlayerCinemachineCameraSystem = true;
    public bool EnableCameraFollowSystem = true;

    [Header("Player Legacy/Test Systems")]
    public bool EnableSimplePlayerMovementSystem = false;

    [Header("Terrain Core Systems")]
    public bool EnableHybridTerrainGenerationSystem = true;
    public bool EnableTerrainCleanupSystem = false;
    public bool EnableChunkProcessor = false;
    public bool EnableTerrainModificationSystem = true;
    public bool EnableTerrainGlobPhysicsSystem = true;

    [Header("Terrain SDF Systems")]
    public bool EnableTerrainChunkDensitySamplingSystem = true;
    public bool EnableTerrainEditInputSystem = true;

    [Header("Terrain Edit Settings")]
    public TerrainEditPlacementMode TerrainEditPlacementMode = TerrainEditPlacementMode.SnappedCube;
    public TerrainEditSnapSpace TerrainEditSnapSpace = TerrainEditSnapSpace.ChunkLocal;
    [Range(0.25f, 1f)]
    public float TerrainEditCellFraction = 0.25f;
    public Vector3 TerrainEditGlobalSnapAnchor = Vector3.zero;
    [Min(1)]
    public int TerrainEditCubeDepthCells = 1;
    public bool TerrainEditEnablePlayerOverlapGuard = true;
    [Min(0f)]
    public float TerrainEditPlayerClearance = 0.15f;
    public bool TerrainEditLockChunkLocalSnap = true;

    [Header("Terrain Streaming")]
    public bool EnableTerrainChunkStreamingSystem = true;

    [Header("Terrain Meshing Systems")]
    public bool EnableTerrainChunkMeshBuildSystem = true;
    public bool EnableTerrainChunkRenderPrepSystem = true;
    public bool EnableTerrainChunkMeshUploadSystem = true;
    public bool EnableTerrainChunkColliderBuildSystem = true;
    public bool EnableTerrainColliderSettingsBootstrapSystem = true;

    [Header("Terrain LOD Systems")]
    [Tooltip("LOD systems are auto-enabled when terrain render distance > 0. Override to force off.")]
    public bool EnableTerrainLodSelectionSystem = true;
    public bool EnableTerrainLodApplySystem = true;

    // ── Derived values from TerrainRenderDistance ──

    /// <summary>Default chunk stride: (16-1) * 1 = 15 world units.</summary>
    public const float DefaultChunkStride = 15f;

    /// <summary>Streaming radius in chunks, derived from TerrainRenderDistance.</summary>
    public int DerivedStreamingRadiusInChunks =>
        Mathf.Max(1, Mathf.CeilToInt(TerrainRenderDistance / DefaultChunkStride));

    /// <summary>Camera far clip plane, derived from TerrainRenderDistance with headroom.</summary>
    public float DerivedCameraFarClip =>
        Mathf.Max(100f, TerrainRenderDistance * 1.5f);

    /// <summary>LOD0 ring radius in chunk units (~1/3 of streaming radius).</summary>
    public float DerivedLod0MaxDist =>
        Mathf.Max(1f, DerivedStreamingRadiusInChunks / 3f);

    /// <summary>LOD1 ring radius in chunk units (~2/3 of streaming radius).</summary>
    public float DerivedLod1MaxDist =>
        Mathf.Max(2f, DerivedStreamingRadiusInChunks * 2f / 3f);

    /// <summary>LOD2 ring radius in chunk units (matches streaming radius by default).</summary>
    public float DerivedLod2MaxDist =>
        Mathf.Max(DerivedLod1MaxDist + 1f, DerivedStreamingRadiusInChunks);

    [Header("Tree Systems")]
    public bool EnableTreePlacementSystem = true;
    public bool EnableTreeRenderSystem = true;

    [Header("Terrain Debug Systems")]
    public bool EnableTerrainSeamValidatorSystem = false;
    public bool EnableTerrainMeshSeamValidatorSystem = false;
    public bool EnableTerrainMeshBorderDebugSystem = false;
    public bool EnablePlayerFallThroughDiagnosticSystem = false;
    public bool EnableTerrainColliderTimingSystem = false;

    [Header("Dungeon Systems")]
    public bool EnableDungeonRenderingSystem = false;

    [Header("Weather Systems")]
    public bool EnableHybridWeatherSystem = false;
}
