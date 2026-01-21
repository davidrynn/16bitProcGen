using DOTS.Player.Bootstrap;
using DOTS.Player.Systems;
using DOTS.Terrain;
using DOTS.Terrain.Generation;
using DOTS.Terrain.Meshing;
using DOTS.Terrain.Modification;
using DOTS.Terrain.Streaming;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Weather;
using Unity.Entities;
using Unity.Physics.Systems;
using UnityEngine;
 
public class DotsSystemBootstrap : MonoBehaviour
{
    public ProjectFeatureConfig config;

    void Awake()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        // Ensure a usable default world exists before touching EntityManager or creating systems
        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[DOTS Bootstrap] Default world missing. Initializing a default world before system creation.");
            DefaultWorldInitialization.Initialize("Default World", false);
            world = World.DefaultGameObjectInjectionWorld;
        }

        if (world == null || !world.IsCreated)
        {
            Debug.LogError("[DOTS Bootstrap] Failed to initialize default world. Aborting DOTS bootstrap.");
            return;
        }

        if (config == null)
        {
            Debug.LogWarning("[DOTS Bootstrap] ProjectFeatureConfig not assigned. No systems will be enabled.");
            return;
        }

        if (config.EnableTerrainSystem)
        {
            EnsureConfigSingleton(world);

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

            if (config.EnableTerrainChunkStreamingSystem)
            {
                var handle = world.CreateSystem<TerrainChunkStreamingSystem>();
                simGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] TerrainChunkStreamingSystem enabled and added to SimulationSystemGroup.");
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

            if (config.EnableTerrainSeamValidatorSystem)
            {
                var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainSeamValidatorSystem>();
                simGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] TerrainSeamValidatorSystem enabled and added to SimulationSystemGroup.");
            }
        }

        if (config.EnablePlayerSystem)
        {
            var initGroup = world.GetExistingSystemManaged<InitializationSystemGroup>();
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var physicsGroup = world.GetExistingSystemManaged<PhysicsSystemGroup>();
            var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
            
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
                var handle = world.CreateSystem<PlayerInputSystem>();
                initGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerInputSystem enabled and added to InitializationSystemGroup.");
            }

            if (config.EnablePlayerLookSystem)
            {
                var handle = world.CreateSystem<PlayerLookSystem>();
                simGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerLookSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnablePlayerMovementSystem)
            {
                var handle = world.CreateSystem<PlayerMovementSystem>();
                physicsGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerMovementSystem enabled and added to PhysicsSystemGroup.");
            }

            if (config.EnablePlayerGroundingSystem)
            {
                var handle = world.CreateSystem<PlayerGroundingSystem>();
                physicsGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerGroundingSystem enabled and added to PhysicsSystemGroup.");
            }

            if (config.EnablePlayerCameraSystem)
            {
                var handle = world.CreateSystem<PlayerCameraSystem>();
                presentationGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerCameraSystem enabled and added to PresentationSystemGroup.");
            }

            if (config.EnablePlayerCinemachineCameraSystem)
            {
                var handle = world.CreateSystem<PlayerCinemachineCameraSystem>();
                presentationGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] PlayerCinemachineCameraSystem enabled and added to PresentationSystemGroup.");
            }

            if (config.EnableCameraFollowSystem)
            {
                var handle = world.CreateSystem<CameraFollowSystem>();
                simGroup.AddSystemToUpdateList(handle);
                Debug.Log("[DOTS Bootstrap] CameraFollowSystem enabled and added to SimulationSystemGroup.");
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

    private void EnsureConfigSingleton(World world)
    {
        if (world == null || !world.IsCreated)
        {
            Debug.LogWarning("[DOTS Bootstrap] Cannot create config singleton because the world is not available.");
            return;
        }

        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ProjectFeatureConfigSingleton>());
        if (query.CalculateEntityCount() == 0)
        {
            var entity = entityManager.CreateEntity(typeof(ProjectFeatureConfigSingleton));
            entityManager.SetComponentData(entity, new ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = config != null ? config.TerrainStreamingRadiusInChunks : 0
            });
        }
        query.Dispose();
    }
}
