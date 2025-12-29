using DOTS.Player.Bootstrap;
using DOTS.Player.Systems;
using DOTS.Terrain;
using DOTS.Terrain.Generation;
using DOTS.Terrain.Meshing;
using DOTS.Terrain.Modification;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Weather;
using Unity.Entities;
using UnityEngine;
 
public class DotsSystemBootstrap : MonoBehaviour
{
    public ProjectFeatureConfig config;

    void Awake()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (config == null)
        {
            Debug.LogWarning("[DOTS Bootstrap] ProjectFeatureConfig not assigned. No systems will be enabled.");
            return;
        }

        if (config.EnableTerrainSystem)
        {
            world.CreateSystem<TerrainSystem>();
            Debug.Log("[DOTS Bootstrap] TerrainSystem enabled via config.");

            if (config.EnableHybridTerrainGenerationSystem)
            {
                world.CreateSystem<HybridTerrainGenerationSystem>();
                Debug.Log("[DOTS Bootstrap] HybridTerrainGenerationSystem enabled via config.");
            }

            world.CreateSystem<TerrainGenerationSystem>();
            Debug.Log("[DOTS Bootstrap] TerrainGenerationSystem enabled via config.");

            if (config.EnableTerrainCleanupSystem)
            {
                world.CreateSystem<TerrainCleanupSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainCleanupSystem enabled via config.");
            }

            if (config.EnableChunkProcessor)
            {
                world.CreateSystem<ChunkProcessor>();
                Debug.Log("[DOTS Bootstrap] ChunkProcessor enabled via config.");
            }

            if (config.EnableTerrainModificationSystem)
            {
                world.CreateSystem<TerrainModificationSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainModificationSystem enabled via config.");
            }

            if (config.EnableTerrainGlobPhysicsSystem)
            {
                world.CreateSystem<TerrainGlobPhysicsSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainGlobPhysicsSystem enabled via config.");
            }

            if (config.EnableTerrainChunkDensitySamplingSystem)
            {
                world.CreateSystem<TerrainChunkDensitySamplingSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainChunkDensitySamplingSystem enabled via config.");
            }

            if (config.EnableTerrainEditInputSystem)
            {
                world.CreateSystem<TerrainEditInputSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainEditInputSystem enabled via config.");
            }

            if (config.EnableTerrainChunkMeshBuildSystem)
            {
                world.CreateSystem<TerrainChunkMeshBuildSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainChunkMeshBuildSystem enabled via config.");
            }

            if (config.EnableTerrainChunkRenderPrepSystem)
            {
                world.CreateSystem<TerrainChunkRenderPrepSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainChunkRenderPrepSystem enabled via config.");
            }

            if (config.EnableTerrainChunkMeshUploadSystem)
            {
                world.CreateSystem<TerrainChunkMeshUploadSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainChunkMeshUploadSystem enabled via config.");
            }
        }

        if (config.EnablePlayerSystem)
        {
            if (config.EnablePlayerBootstrapFixedRateInstaller)
            {
                world.CreateSystem<PlayerBootstrapFixedRateInstaller>();
                Debug.Log("[DOTS Bootstrap] PlayerBootstrapFixedRateInstaller enabled via config.");
            }

            if (config.EnablePlayerEntityBootstrap)
            {
                world.CreateSystem<PlayerEntityBootstrap>();
                Debug.Log("[DOTS Bootstrap] PlayerEntityBootstrap enabled via config.");
            }

            if (config.EnablePlayerEntityBootstrapPureEcs)
            {
                world.CreateSystem<PlayerEntityBootstrap_PureECS>();
                Debug.Log("[DOTS Bootstrap] PlayerEntityBootstrap_PureECS enabled via config.");
            }

            if (config.EnablePlayerInputSystem)
            {
                world.CreateSystem<PlayerInputSystem>();
                Debug.Log("[DOTS Bootstrap] PlayerInputSystem enabled via config.");
            }

            if (config.EnablePlayerLookSystem)
            {
                world.CreateSystem<PlayerLookSystem>();
                Debug.Log("[DOTS Bootstrap] PlayerLookSystem enabled via config.");
            }

            if (config.EnablePlayerMovementSystem)
            {
                world.CreateSystem<PlayerMovementSystem>();
                Debug.Log("[DOTS Bootstrap] PlayerMovementSystem enabled via config.");
            }

            if (config.EnablePlayerGroundingSystem)
            {
                world.CreateSystem<PlayerGroundingSystem>();
                Debug.Log("[DOTS Bootstrap] PlayerGroundingSystem enabled via config.");
            }

            if (config.EnablePlayerCameraSystem)
            {
                world.CreateSystem<PlayerCameraSystem>();
                Debug.Log("[DOTS Bootstrap] PlayerCameraSystem enabled via config.");
            }

            if (config.EnablePlayerCinemachineCameraSystem)
            {
                world.CreateSystem<PlayerCinemachineCameraSystem>();
                Debug.Log("[DOTS Bootstrap] PlayerCinemachineCameraSystem enabled via config.");
            }

            if (config.EnableCameraFollowSystem)
            {
                world.CreateSystem<CameraFollowSystem>();
                Debug.Log("[DOTS Bootstrap] CameraFollowSystem enabled via config.");
            }

#if SIMPLE_PLAYER_MOVEMENT_ENABLED
            if (config.EnableSimplePlayerMovementSystem)
            {
                world.CreateSystem<SimplePlayerMovementSystem>();
                Debug.Log("[DOTS Bootstrap] SimplePlayerMovementSystem enabled via config.");
            }
#endif
        }

        if (config.EnableDungeonSystem)
        {
            if (config.EnableDungeonRenderingSystem)
            {
                world.CreateSystem<DungeonRenderingSystem>();
                Debug.Log("[DOTS Bootstrap] DungeonRenderingSystem enabled via config.");
            }

            world.CreateSystem<DungeonVisualizationSystem>();
            Debug.Log("[DOTS Bootstrap] DungeonVisualizationSystem enabled via config.");
        }

        if (config.EnableWeatherSystem)
        {
            world.CreateSystem<WeatherSystem>();
            Debug.Log("[DOTS Bootstrap] WeatherSystem enabled via config.");

            if (config.EnableHybridWeatherSystem)
            {
                world.CreateSystem<HybridWeatherSystem>();
                Debug.Log("[DOTS Bootstrap] HybridWeatherSystem enabled via config.");
            }
        }
    }
}
