using DOTS.Player.Bootstrap;
using DOTS.Player.Systems;
using DOTS.Terrain.Core;
using DOTS.Terrain;
using DOTS.Terrain.Generation;
using DOTS.Terrain.LOD;
using DOTS.Terrain.Meshing;
using DOTS.Terrain.Modification;
using DOTS.Terrain.Rendering;
using DOTS.Terrain.Streaming;
using DOTS.Terrain.Trees;
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
            DebugSettings.LogWarning("Bootstrap: Default world missing. Initializing a default world before system creation.");
            DefaultWorldInitialization.Initialize("Default World", false);
            world = World.DefaultGameObjectInjectionWorld;
        }

        if (world == null || !world.IsCreated)
        {
            DebugSettings.LogError("Bootstrap: Failed to initialize default world. Aborting DOTS bootstrap.");
            return;
        }

        if (config == null)
        {
            DebugSettings.LogWarning("Bootstrap: ProjectFeatureConfig not assigned. No systems will be enabled.");
            return;
        }

        // Diagnostic systems rely on fall-through/pipeline debug channels.
        // Wire them from config so hypothesis testing does not require code changes.
        if (config.EnablePlayerFallThroughDiagnosticSystem || config.EnableTerrainColliderTimingSystem)
        {
            DebugSettings.EnableFallThroughDebug = true;
        }

        if (config.EnableTerrainColliderTimingSystem)
        {
            DebugSettings.EnableTerrainColliderPipelineDebug = true;
        }

        if (config.EnableTerrainSystem)
        {
            EnsureConfigSingleton(world);

            world.CreateSystem<TerrainSystem>();
            DebugSettings.Log("Bootstrap: TerrainSystem enabled via config.");

            if (config.EnableHybridTerrainGenerationSystem)
            {
                world.CreateSystem<HybridTerrainGenerationSystem>();
                DebugSettings.Log("Bootstrap: HybridTerrainGenerationSystem enabled via config.");
            }

            world.CreateSystem<TerrainGenerationSystem>();
            DebugSettings.Log("Bootstrap: TerrainGenerationSystem enabled via config.");

            if (config.EnableTerrainCleanupSystem)
            {
                world.CreateSystem<TerrainCleanupSystem>();
                DebugSettings.Log("Bootstrap: TerrainCleanupSystem enabled via config.");
            }

            if (config.EnableChunkProcessor)
            {
                world.CreateSystem<ChunkProcessor>();
                DebugSettings.Log("Bootstrap: ChunkProcessor enabled via config.");
            }

            if (config.EnableTerrainModificationSystem)
            {
                world.CreateSystem<TerrainModificationSystem>();
                DebugSettings.Log("Bootstrap: TerrainModificationSystem enabled via config.");
            }

            if (config.EnableTerrainGlobPhysicsSystem)
            {
                world.CreateSystem<TerrainGlobPhysicsSystem>();
                DebugSettings.Log("Bootstrap: TerrainGlobPhysicsSystem enabled via config.");
            }

            // Systems with [DisableAutoCreation] must be manually added to a system group to run
            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();

            if (config.EnableTerrainChunkStreamingSystem)
            {
                var handle = world.CreateSystem<TerrainChunkStreamingSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainChunkStreamingSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainLodSelectionSystem)
            {
                var handle = world.CreateSystem<TerrainChunkLodSelectionSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainChunkLodSelectionSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainLodApplySystem)
            {
                var handle = world.CreateSystem<TerrainChunkLodApplySystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainChunkLodApplySystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainEditInputSystem)
            {
                var editInputHandle = world.CreateSystem<TerrainEditInputSystem>();
                simGroup.AddSystemToUpdateList(editInputHandle);
                DebugSettings.Log("Bootstrap: TerrainEditInputSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkDensitySamplingSystem)
            {
                var densitySamplingHandle = world.CreateSystem<TerrainChunkDensitySamplingSystem>();
                simGroup.AddSystemToUpdateList(densitySamplingHandle);
                DebugSettings.Log("Bootstrap: TerrainChunkDensitySamplingSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkMeshBuildSystem)
            {
                var meshBuildHandle = world.CreateSystem<TerrainChunkMeshBuildSystem>();
                simGroup.AddSystemToUpdateList(meshBuildHandle);
                DebugSettings.Log("Bootstrap: TerrainChunkMeshBuildSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkRenderPrepSystem)
            {
                var renderPrepHandle = world.CreateSystem<TerrainChunkRenderPrepSystem>();
                simGroup.AddSystemToUpdateList(renderPrepHandle);
                DebugSettings.Log("Bootstrap: TerrainChunkRenderPrepSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainChunkMeshUploadSystem)
            {
                var meshUploadHandle = world.CreateSystem<TerrainChunkMeshUploadSystem>();
                simGroup.AddSystemToUpdateList(meshUploadHandle);
                DebugSettings.Log("Bootstrap: TerrainChunkMeshUploadSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainColliderSettingsBootstrapSystem)
            {
                world.CreateSystem<TerrainColliderSettingsBootstrapSystem>();
                DebugSettings.Log("Bootstrap: TerrainColliderSettingsBootstrapSystem enabled via config.");
            }

            if (config.EnableTerrainChunkColliderBuildSystem)
            {
                var handle = world.CreateSystem<TerrainChunkColliderBuildSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainChunkColliderBuildSystem enabled and added to SimulationSystemGroup.");
            }

            var grassGenerationHandle = world.CreateSystem<GrassChunkGenerationSystem>();
            simGroup.AddSystemToUpdateList(grassGenerationHandle);
            DebugSettings.Log("Bootstrap: GrassChunkGenerationSystem enabled and added to SimulationSystemGroup.");

            if (config.EnableTreePlacementSystem)
            {
                var invalidationHandle = world.CreateSystem<TreePlacementInvalidationSystem>();
                simGroup.AddSystemToUpdateList(invalidationHandle);
                DebugSettings.Log("Bootstrap: TreePlacementInvalidationSystem enabled and added to SimulationSystemGroup.");

                var handle = world.CreateSystem<TreePlacementGenerationSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TreePlacementGenerationSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTreeRenderSystem)
            {
                var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
                var handle = world.CreateSystem<TreeChunkRenderSystem>();
                presentationGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TreeChunkRenderSystem enabled and added to PresentationSystemGroup.");
            }

            if (config.EnableTerrainSeamValidatorSystem)
            {
                var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainSeamValidatorSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainSeamValidatorSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainMeshSeamValidatorSystem)
            {
                var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainMeshSeamValidatorSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainMeshSeamValidatorSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnableTerrainMeshBorderDebugSystem)
            {
                var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainMeshBorderDebugSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainMeshBorderDebugSystem enabled and added to SimulationSystemGroup.");
            }

            // Logs per-frame snapshots (position, velocity, grounding, nearby chunk collider status)
            // when the player unexpectedly loses ground or drops below the SDF surface.
            // Enable alongside DebugSettings.EnableFallThroughDebug to diagnose fall-through bugs.
            if (config.EnablePlayerFallThroughDiagnosticSystem)
            {
                var handle = world.CreateSystem<DOTS.Terrain.Debug.PlayerFallThroughDiagnosticSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerFallThroughDiagnosticSystem enabled and added to SimulationSystemGroup.");
            }

            // Measures frames/time between chunk spawn and collider completion.
            // Useful for tuning MaxCollidersPerFrame or verifying that colliders build
            // before the player reaches newly streamed chunks.
            // Requires DebugSettings.EnableFallThroughDebug for spawn timestamps.
            if (config.EnableTerrainColliderTimingSystem)
            {
                var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainColliderTimingSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: TerrainColliderTimingSystem enabled and added to SimulationSystemGroup.");
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
                DebugSettings.Log("Bootstrap: PlayerBootstrapFixedRateInstaller enabled and added to InitializationSystemGroup.");
            }

            if (config.EnablePlayerEntityBootstrap)
            {
                var handle = world.CreateSystem<PlayerEntityBootstrap>();
                initGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerEntityBootstrap enabled and added to InitializationSystemGroup.");
            }

            if (config.EnablePlayerEntityBootstrapPureEcs)
            {
                world.CreateSystem<PlayerEntityBootstrap_PureECS>();
                DebugSettings.Log("Bootstrap: PlayerEntityBootstrap_PureECS enabled via config.");
            }

            if (config.EnablePlayerInputSystem)
            {
                var handle = world.CreateSystem<PlayerInputSystem>();
                initGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerInputSystem enabled and added to InitializationSystemGroup.");
            }

            if (config.EnablePlayerLookSystem)
            {
                var handle = world.CreateSystem<PlayerLookSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerLookSystem enabled and added to SimulationSystemGroup.");
            }

            if (config.EnablePlayerMovementSystem)
            {
                var handle = world.CreateSystem<PlayerMovementSystem>();
                physicsGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerMovementSystem enabled and added to PhysicsSystemGroup.");
            }

            // Holds player physics until nearby terrain colliders are ready to prevent startup fall-through.
            var startupReadinessHandle = world.CreateSystem<PlayerStartupReadinessSystem>();
            physicsGroup.AddSystemToUpdateList(startupReadinessHandle);
            DebugSettings.Log("Bootstrap: PlayerStartupReadinessSystem enabled and added to PhysicsSystemGroup.");

            // Safety net: raycasts from the player's previous position to current position each
            // frame. If a collider is hit between the two, the player tunneled through a surface
            // and is snapped back. Works for any geometry (terrain, dungeons, caves).
            if (config.EnablePlayerTerrainSafetySystem)
            {
                var handle = world.CreateSystem<PlayerTerrainSafetySystem>();
                physicsGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerTerrainSafetySystem enabled and added to PhysicsSystemGroup.");
            }

            if (config.EnablePlayerGroundingSystem)
            {
                var handle = world.CreateSystem<PlayerGroundingSystem>();
                physicsGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerGroundingSystem enabled and added to PhysicsSystemGroup.");
            }

            if (config.EnablePlayerCameraSystem)
            {
                var handle = world.CreateSystem<PlayerCameraSystem>();
                presentationGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerCameraSystem enabled and added to PresentationSystemGroup.");
            }

            if (config.EnablePlayerCinemachineCameraSystem)
            {
                var handle = world.CreateSystem<PlayerCinemachineCameraSystem>();
                presentationGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: PlayerCinemachineCameraSystem enabled and added to PresentationSystemGroup.");
            }

            if (config.EnableCameraFollowSystem)
            {
                var handle = world.CreateSystem<CameraFollowSystem>();
                simGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: CameraFollowSystem enabled and added to SimulationSystemGroup.");
            }

#if SIMPLE_PLAYER_MOVEMENT_ENABLED
            if (config.EnableSimplePlayerMovementSystem)
            {
                world.CreateSystem<SimplePlayerMovementSystem>();
                DebugSettings.Log("Bootstrap: SimplePlayerMovementSystem enabled via config.");
            }
#endif
        }

        if (config.EnableDungeonSystem)
        {
            if (config.EnableDungeonRenderingSystem)
            {
                world.CreateSystem<DungeonRenderingSystem>();
                DebugSettings.Log("Bootstrap: DungeonRenderingSystem enabled via config.");
            }

            world.CreateSystem<DungeonVisualizationSystem>();
            DebugSettings.Log("Bootstrap: DungeonVisualizationSystem enabled via config.");
        }

        if (config.EnableWeatherSystem)
        {
            world.CreateSystem<WeatherSystem>();
            DebugSettings.Log("Bootstrap: WeatherSystem enabled via config.");

            if (config.EnableHybridWeatherSystem)
            {
                world.CreateSystem<HybridWeatherSystem>();
                DebugSettings.Log("Bootstrap: HybridWeatherSystem enabled via config.");
            }
        }
    }

    private void EnsureConfigSingleton(World world)
    {
        if (world == null || !world.IsCreated)
        {
            DebugSettings.LogWarning("Bootstrap: Cannot create config singleton because the world is not available.");
            return;
        }

        var entityManager = world.EntityManager;
        var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ProjectFeatureConfigSingleton>());
        if (query.CalculateEntityCount() == 0)
        {
            var entity = entityManager.CreateEntity(typeof(ProjectFeatureConfigSingleton));
            entityManager.SetComponentData(entity, new ProjectFeatureConfigSingleton
            {
                TerrainStreamingRadiusInChunks = config != null ? config.DerivedStreamingRadiusInChunks : 0,
                CameraFarClipPlane = config != null ? config.DerivedCameraFarClip : 300f,
                TerrainStreamingEnabled = config != null && config.EnableTerrainChunkStreamingSystem
            });
        }
        query.Dispose();

        var lodQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainLodSettings>());
        if (lodQuery.CalculateEntityCount() == 0)
        {
            var lodEntity = entityManager.CreateEntity(typeof(TerrainLodSettings));
            if (config != null)
            {
                var lodDefaults = TerrainLodSettings.Default;
                lodDefaults.Lod0MaxDist = config.DerivedLod0MaxDist;
                lodDefaults.Lod1MaxDist = config.DerivedLod1MaxDist;
                lodDefaults.Lod2MaxDist = config.DerivedLod2MaxDist;
                lodDefaults.UseStreamingAsCullBoundary = true;
                entityManager.SetComponentData(lodEntity, lodDefaults);
            }
            else
            {
                entityManager.SetComponentData(lodEntity, TerrainLodSettings.Default);
            }
        }
        lodQuery.Dispose();

        var editSettings = TerrainEditSettings.Default;
        if (config != null)
        {
            editSettings = TerrainEditSettings.FromValues(
                config.TerrainEditPlacementMode,
                config.TerrainEditSnapSpace,
                config.TerrainEditCellFraction,
                config.TerrainEditGlobalSnapAnchor.x,
                config.TerrainEditGlobalSnapAnchor.y,
                config.TerrainEditGlobalSnapAnchor.z,
                config.TerrainEditCubeDepthCells,
                config.TerrainEditEnablePlayerOverlapGuard,
                config.TerrainEditPlayerClearance,
                config.TerrainEditLockChunkLocalSnap);
        }

        var editQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainEditSettings>());
        if (editQuery.CalculateEntityCount() == 0)
        {
            var settingsEntity = entityManager.CreateEntity(typeof(TerrainEditSettings));
            entityManager.SetComponentData(settingsEntity, editSettings);
        }
        else
        {
            var settingsEntity = editQuery.GetSingletonEntity();
            entityManager.SetComponentData(settingsEntity, editSettings);
        }
        editQuery.Dispose();
    }
}
