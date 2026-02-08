using UnityEngine;

[CreateAssetMenu(menuName = "Config/ProjectFeatureConfig")]
public class ProjectFeatureConfig : ScriptableObject
{
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

    [Header("Terrain Streaming")]
    public bool EnableTerrainChunkStreamingSystem = true;
    [Min(0)]
    public int TerrainStreamingRadiusInChunks = 2;

    [Header("Terrain Meshing Systems")]
    public bool EnableTerrainChunkMeshBuildSystem = true;
    public bool EnableTerrainChunkRenderPrepSystem = true;
    public bool EnableTerrainChunkMeshUploadSystem = true;
    public bool EnableTerrainChunkColliderBuildSystem = true;
    public bool EnableTerrainColliderSettingsBootstrapSystem = true;

    [Header("Terrain Debug Systems")]
    public bool EnableTerrainSeamValidatorSystem = false;
    public bool EnableTerrainMeshSeamValidatorSystem = false;
    public bool EnableTerrainMeshBorderDebugSystem = false;

    [Header("Dungeon Systems")]
    public bool EnableDungeonRenderingSystem = false;

    [Header("Weather Systems")]
    public bool EnableHybridWeatherSystem = false;
}
