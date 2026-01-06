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

            // Systems with [DisableAutoCreation] must be manually added to a system group to run
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();

            if (config.EnableTerrainChunkDensitySamplingSystem)
            {
                var densitySamplingHandle = world.CreateSystem<TerrainChunkDensitySamplingSystem>();
                simGroup.AddSystemToUpdateList(densitySamplingHandle);
                Debug.Log("[DOTS Bootstrap] TerrainChunkDensitySamplingSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainEditInputSystem)
            {
                var editInputHandle = world.CreateSystem<TerrainEditInputSystem>();
                simGroup.AddSystemToUpdateList(editInputHandle);
                Debug.Log("[DOTS Bootstrap] TerrainEditInputSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkMeshBuildSystem)
            {
                var meshBuildHandle = world.CreateSystem<TerrainChunkMeshBuildSystem>();
                simGroup.AddSystemToUpdateList(meshBuildHandle);
                Debug.Log("[DOTS Bootstrap] TerrainChunkMeshBuildSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkRenderPrepSystem)
            {
                var renderPrepHandle = world.CreateSystem<TerrainChunkRenderPrepSystem>();
                simGroup.AddSystemToUpdateList(renderPrepHandle);
                Debug.Log("[DOTS Bootstrap] TerrainChunkRenderPrepSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkMeshUploadSystem)
            {
                var meshUploadHandle = world.CreateSystem<TerrainChunkMeshUploadSystem>();
                simGroup.AddSystemToUpdateList(meshUploadHandle);
                Debug.Log("[DOTS Bootstrap] TerrainChunkMeshUploadSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainColliderSettingsBootstrapSystem)
            {
                world.CreateSystem<TerrainColliderSettingsBootstrapSystem>();
                Debug.Log("[DOTS Bootstrap] TerrainColliderSettingsBootstrapSystem enabled via config.");
            }

            if (config.EnableTerrainChunkColliderBuildSystem)
            {
                var handle = world.CreateSystem<TerrainChunkColliderBuildSystem>();
                simGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] TerrainChunkColliderBuildSystem enabled and added to SimulationSystemGroup.");
            }
        }

        if (config.EnablePlayerSystem)
        {
            var initGroup = world.GetExistingSystemManaged<InitializationSystemGroup>();
            
            if (config.EnablePlayerBootstrapFixedRateInstaller)
            {
                var handle = world.CreateSystem<PlayerBootstrapFixedRateInstaller>();
                initGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerBootstrapFixedRateInstaller enabled and added to InitializationSystemGroup.");
            }

            if (config.EnablePlayerEntityBootstrap)
            {
                var handle = world.CreateSystem<PlayerEntityBootstrap>();
                initGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerEntityBootstrap enabled and added to InitializationSystemGroup.");
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
