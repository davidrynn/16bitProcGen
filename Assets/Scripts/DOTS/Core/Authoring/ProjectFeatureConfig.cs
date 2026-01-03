using UnityEngine;

[CreateAssetMenu(menuName = "Config/ProjectFeatureConfig")]
public class ProjectFeatureConfig : ScriptableObject
{
    [Header("Feature Toggles")]
    public bool EnablePlayerSystem = false;
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
    public bool EnableTerrainModificationSystem = false;
    public bool EnableTerrainGlobPhysicsSystem = false;

    [Header("Terrain SDF Systems")]
    public bool EnableTerrainChunkDensitySamplingSystem = false;
    public bool EnableTerrainEditInputSystem = false;

    [Header("Terrain Meshing Systems")]
    public bool EnableTerrainChunkMeshBuildSystem = true;
    public bool EnableTerrainChunkRenderPrepSystem = true;
    public bool EnableTerrainChunkMeshUploadSystem = true;
    public bool EnableTerrainChunkColliderBuildSystem = false;
    public bool EnableTerrainColliderSettingsBootstrapSystem = true;

    [Header("Dungeon Systems")]
    public bool EnableDungeonRenderingSystem = true;

    [Header("Weather Systems")]
    public bool EnableHybridWeatherSystem = true;
}
