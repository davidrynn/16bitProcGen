using DOTS.Terrain.Legacy;
using DOTS.Player.Bootstrap;
using DOTS.Player.Systems;
using DOTS.Core;
using DOTS.Terrain;
using DOTS.Terrain.LOD;
using DOTS.Terrain.Meshing;
using DOTS.Terrain.Modification;
using DOTS.Terrain.Rocks;
using DOTS.Terrain.Grass;
using DOTS.Terrain.Streaming;
using DOTS.Terrain.Trees;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Weather;
using DOTS.Rendering.Sky;
using Unity.Entities;
using Unity.Physics.Systems;
using UnityEngine;
 
namespace DOTS.Core.Authoring
{
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

            // Seed the atmosphere's world reference + landmark distances from config
            // (LANDMARK_DRAW_DISTANCE_SPEC.md P2/P1). Pushed from here because Core
            // (Rendering.Sky) cannot reference this assembly.
            AtmosphereBroadcast.WorldReferenceDistance = config.DerivedCameraFarClip;
            AtmosphereBroadcast.LandmarkDistance = config.LandmarkDrawDistance;
    
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
    
                // Survivor of the ChunkProcessor/TerrainSystem duplicate-validator merge (plan C5);
                // takes the unconditional slot the deleted TerrainSystem occupied.
                world.CreateSystem<TerrainDataValidationSystem>();
                DebugSettings.Log("Bootstrap: TerrainDataValidationSystem enabled via config.");
    
                if (config.EnableLegacyHeightmapTerrainGenerationSystem)
                {
                    world.CreateSystem<LegacyHeightmapTerrainGenerationSystem>();
                    DebugSettings.Log("Bootstrap: LegacyHeightmapTerrainGenerationSystem enabled via config.");
                }
    
                if (config.EnableTerrainCleanupSystem)
                {
                    world.CreateSystem<TerrainCleanupSystem>();
                    DebugSettings.Log("Bootstrap: TerrainCleanupSystem enabled via config.");
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
    
                if (config.EnableRockPlacementSystem)
                {
                    var invalidationHandle = world.CreateSystem<RockPlacementInvalidationSystem>();
                    simGroup.AddSystemToUpdateList(invalidationHandle);
                    DebugSettings.Log("Bootstrap: RockPlacementInvalidationSystem enabled and added to SimulationSystemGroup.");
    
                    var handle = world.CreateSystem<RockPlacementGenerationSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: RockPlacementGenerationSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableRockRenderSystem)
                {
                    var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
                    var handle = world.CreateSystem<RockChunkRenderSystem>();
                    presentationGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: RockChunkRenderSystem enabled and added to PresentationSystemGroup.");
                }
    
                if (config.EnablePebblePlacementSystem)
                {
                    var invalidationHandle = world.CreateSystem<DOTS.Terrain.Pebbles.PebblePlacementInvalidationSystem>();
                    simGroup.AddSystemToUpdateList(invalidationHandle);
                    DebugSettings.Log("Bootstrap: PebblePlacementInvalidationSystem enabled and added to SimulationSystemGroup.");
    
                    var handle = world.CreateSystem<DOTS.Terrain.Pebbles.PebblePlacementGenerationSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: PebblePlacementGenerationSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnablePebbleRenderSystem)
                {
                    var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
                    var handle = world.CreateSystem<DOTS.Terrain.Pebbles.PebbleChunkRenderSystem>();
                    presentationGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: PebbleChunkRenderSystem enabled and added to PresentationSystemGroup.");
                }
    
                if (config.EnableStructurePlacementSystem)
                {
                    var handle = world.CreateSystem<DOTS.Structures.StructureAnchorPlanningSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: StructureAnchorPlanningSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableRelicRealizationSystem)
                {
                    var handle = world.CreateSystem<DOTS.Structures.RelicRealizationSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    // R6 P4 spawn fade rides the same flag: it only advances the
                    // RelicSpawnFade component that realization spawns — one feature.
                    var fadeHandle = world.CreateSystem<DOTS.Structures.RelicSpawnFadeSystem>();
                    simGroup.AddSystemToUpdateList(fadeHandle);
                    DebugSettings.Log("Bootstrap: RelicRealizationSystem + RelicSpawnFadeSystem enabled and added to SimulationSystemGroup.");
                }
    
                // RelicLodSelectionSystem runs in PresentationSystemGroup so LocalToWorldSystem
                // has already flushed transform writes before Entities Graphics submits draws.
                // Disabling the flag leaves all relics in LOD 0 (useful for debugging/repro).
                if (config.EnableRelicLodSelectionSystem)
                {
                    var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
                    var handle = world.CreateSystem<DOTS.Structures.RelicLodSelectionSystem>();
                    presentationGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: RelicLodSelectionSystem enabled and added to PresentationSystemGroup.");
                }
    
                if (config.EnableTerrainDensitySeamValidatorSystem)
                {
                    var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainDensitySeamValidatorSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: TerrainDensitySeamValidatorSystem enabled and added to SimulationSystemGroup.");
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
                    // Write sky-drop params directly — avoids TryGetSingleton timing issues.
                    ref var bootstrap = ref world.Unmanaged.GetUnsafeSystemRef<PlayerEntityBootstrap>(handle);
                    bootstrap.SkyDropEnabled = config.EnableSkyDropSpawn;
                    bootstrap.SkyDropSpawnHeight = config.SkyDropSpawnHeight;
                    bootstrap.SkyDropGravityHoldSeconds = config.SkyDropGravityHoldSeconds;
                    initGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: PlayerEntityBootstrap enabled and added to InitializationSystemGroup.");
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
    
                // ── Movement MVP systems ──
    
                if (config.EnableSlingshotChargeSystem)
                {
                    var handle = world.CreateSystem<SlingshotChargeSystem>();
                    physicsGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: SlingshotChargeSystem enabled and added to PhysicsSystemGroup.");
                }
    
                if (config.EnableSlingshotLaunchSystem)
                {
                    var handle = world.CreateSystem<SlingshotLaunchSystem>();
                    physicsGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: SlingshotLaunchSystem enabled and added to PhysicsSystemGroup.");
                }
    
                if (config.EnableGlideSystem)
                {
                    var handle = world.CreateSystem<GlideSystem>();
                    physicsGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: GlideSystem enabled and added to PhysicsSystemGroup.");
                }
    
                if (config.EnableChainWindowSystem)
                {
                    var handle = world.CreateSystem<ChainWindowSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: ChainWindowSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableMovementStateBookkeepingSystem)
                {
                    var handle = world.CreateSystem<MovementStateBookkeepingSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: MovementStateBookkeepingSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableLandingDetectionSystem)
                {
                    var handle = world.CreateSystem<LandingDetectionSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: LandingDetectionSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableCameraChargeFeedbackSystem)
                {
                    var handle = world.CreateSystem<CameraChargeFeedbackSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: CameraChargeFeedbackSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableCameraSpeedFeedbackSystem)
                {
                    var handle = world.CreateSystem<CameraSpeedFeedbackSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: CameraSpeedFeedbackSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableCameraLandingFeedbackSystem)
                {
                    var handle = world.CreateSystem<CameraLandingFeedbackSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: CameraLandingFeedbackSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableCameraGlideFeedbackSystem)
                {
                    var handle = world.CreateSystem<CameraGlideFeedbackSystem>();
                    simGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: CameraGlideFeedbackSystem enabled and added to SimulationSystemGroup.");
                }
    
                if (config.EnableCameraEffectResolverSystem)
                {
                    var handle = world.CreateSystem<CameraEffectResolverSystem>();
                    presentationGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: CameraEffectResolverSystem enabled and added to PresentationSystemGroup.");
                }
    
                if (config.EnableScreenEffectResolverSystem)
                {
                    var handle = world.CreateSystem<ScreenEffectResolverSystem>();
                    presentationGroup.AddSystemToUpdateList(handle);
                    DebugSettings.Log("Bootstrap: ScreenEffectResolverSystem enabled and added to PresentationSystemGroup.");
                }
    
            }
    
            if (config.EnableDungeonSystem)
            {
                if (config.EnableDungeonEntitySpawningSystem)
                {
                    world.CreateSystem<DungeonEntitySpawningSystem>();
                    DebugSettings.Log("Bootstrap: DungeonEntitySpawningSystem enabled via config.");
                }
    
                world.CreateSystem<DungeonDebugVisualizationSystem>();
                DebugSettings.Log("Bootstrap: DungeonDebugVisualizationSystem enabled via config.");
            }
    
            if (config.EnableWeatherSystem)
            {
                world.CreateSystem<WeatherSimulationSystem>();
                DebugSettings.Log("Bootstrap: WeatherSimulationSystem enabled via config.");
    
                if (config.EnableWeatherGpuEffectsSystem)
                {
                    world.CreateSystem<WeatherGpuEffectsSystem>();
                    DebugSettings.Log("Bootstrap: WeatherGpuEffectsSystem enabled via config.");
                }
            }
    
            // Ground-plane impostor is independent of the terrain pipeline — it reads
            // only the player transform, so it registers outside the terrain block.
            if (config.EnableGroundPlaneImpostor)
            {
                var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
                var handle = world.CreateSystem<DOTS.Impostors.GroundPlaneImpostorSystem>();
                presentationGroup.AddSystemToUpdateList(handle);
                DebugSettings.Log("Bootstrap: GroundPlaneImpostorSystem enabled and added to PresentationSystemGroup.");
            }
    
            ApplyDistanceFog();
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
                    // The landmark-raised far plane (R6) — camera bootstraps consume this field
                    // directly. The un-raised world reference distance reaches shaders via
                    // AtmosphereBroadcast.WorldReferenceDistance instead.
                    CameraFarClipPlane = config != null ? config.DerivedLandmarkFarClip : 300f,
                    TerrainStreamingEnabled = config != null && config.EnableTerrainChunkStreamingSystem,
                    SkyDropEnabled = config != null && config.EnableSkyDropSpawn,
                    SkyDropSpawnHeight = config != null ? config.SkyDropSpawnHeight : 400f,
                    SkyDropGravityHoldSeconds = config != null ? config.SkyDropGravityHoldSeconds : 8f,
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
    
        /// <summary>
        /// Applies distance fog via <see cref="RenderSettings"/> so geometry fades into atmospheric
        /// haze before the camera far clip plane. Exponential mode (default) produces the natural
        /// depth-haze feel required for the vista discovery moment; Linear is a fallback for hard
        /// far-clip masking when exponential feel isn't wanted.
        /// </summary>
        private void ApplyDistanceFog()
        {
            if (!config.EnableDistanceFog)
            {
                RenderSettings.fog = false;
                DebugSettings.Log("Bootstrap: Distance fog disabled via config.");
                return;
            }
    
            RenderSettings.fog = true;
            RenderSettings.fogMode = config.FogMode;
            RenderSettings.fogColor = config.FogColor;
    
            if (config.FogMode == FogMode.Linear)
            {
                RenderSettings.fogStartDistance = config.DerivedFogStartDistance;
                RenderSettings.fogEndDistance = config.DerivedFogEndDistance;
            }
            else
            {
                RenderSettings.fogDensity = config.FogDensity;
            }
    
            string fogDetail = config.FogMode == FogMode.Linear
                ? $"start={config.DerivedFogStartDistance:0.0}, end={config.DerivedFogEndDistance:0.0}"
                : $"density={config.FogDensity:0.4f}";
    
            DebugSettings.LogRendering(
                $"Bootstrap: Distance fog enabled — mode={config.FogMode}, {fogDetail}, farClip={config.DerivedCameraFarClip:0.0}",
                forceLog: true);
        }
    }
}
