using DOTS.Terrain;
using UnityEngine;
using UnityEngine.Serialization;

namespace DOTS.Core.Authoring
{
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
        private const float HighRenderDistance = 300f;
    
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
    
        [Header("Player Gameplay Systems")]
        public bool EnablePlayerInputSystem = true;
        public bool EnablePlayerLookSystem = true;
        public bool EnablePlayerMovementSystem = true;
        public bool EnablePlayerGroundingSystem = true;
        public bool EnablePlayerTerrainSafetySystem = true;
    
        [Header("Player Movement MVP Systems")]
        public bool EnableSlingshotChargeSystem = true;
        public bool EnableSlingshotLaunchSystem = true;
        public bool EnableChainWindowSystem = true;
        public bool EnableMovementStateBookkeepingSystem = true;
        public bool EnableLandingDetectionSystem = true;
        public bool EnableCameraEffectResolverSystem = true;
        public bool EnableCameraChargeFeedbackSystem = true;
        public bool EnableCameraSpeedFeedbackSystem = true;
        public bool EnableCameraLandingFeedbackSystem = true;
        public bool EnableCameraGlideFeedbackSystem = true;
        public bool EnableGlideSystem = true;
        public bool EnableScreenEffectResolverSystem = true;
    
        [Header("Terrain Core Systems")]
        [FormerlySerializedAs("EnableHybridTerrainGenerationSystem")]
        public bool EnableLegacyHeightmapTerrainGenerationSystem = true;
        public bool EnableTerrainCleanupSystem = false;
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
    
        [Header("Rock Systems")]
        public bool EnableRockPlacementSystem = true;
        public bool EnableRockRenderSystem = true;
    
        [Header("Pebble Systems")]
        public bool EnablePebblePlacementSystem = true;
        public bool EnablePebbleRenderSystem = true;
    
        [Header("Terrain Debug Systems")]
        [FormerlySerializedAs("EnableTerrainSeamValidatorSystem")]
        public bool EnableTerrainDensitySeamValidatorSystem = false;
        public bool EnableTerrainMeshSeamValidatorSystem = false;
        public bool EnableTerrainMeshBorderDebugSystem = false;
        public bool EnablePlayerFallThroughDiagnosticSystem = false;
        public bool EnableTerrainColliderTimingSystem = false;
    
        [Header("World Impostors")]
        [Tooltip("Render a terrain-coloured flat disc beyond the SDF chunk radius so the world " +
                 "appears to extend to the horizon during sky-drop and high-altitude camera views.")]
        public bool EnableGroundPlaneImpostor = true;
    
        [Header("Sky-Drop Intro")]
        [Tooltip("Spawn the player at SkyDropSpawnHeight instead of the default ground-level position. " +
                 "The readiness gate holds gravity until terrain colliders are ready or the timeout fires, " +
                 "then the player falls through the disc onto the terrain below.")]
        public bool EnableSkyDropSpawn = false;
        [Tooltip("World-space Y position for the sky-drop spawn. 400 gives ~10–15 s of freefall before landing.")]
        [Min(50f)]
        public float SkyDropSpawnHeight = 400f;
        [Tooltip("Seconds to hold gravity before releasing for the sky-drop. Gives terrain chunks time to " +
                 "build colliders at ground level before the player arrives. Ignored when EnableSkyDropSpawn is false.")]
        [Range(1f, 20f)]
        public float SkyDropGravityHoldSeconds = 8f;
        [Tooltip("V14 meteor-interior loading shell (METEOR_ARRIVAL_SEQUENCE_SPEC.md): full-screen meteor " +
                 "interior overlay covering the sky-drop readiness hold; breaks open when the gate actually " +
                 "releases. Diegetic loading screen — the player never watches the world assemble. " +
                 "Only active when EnableSkyDropSpawn is true.")]
        public bool EnableMeteorArrivalShell = true;
        [Tooltip("Minimum seconds the meteor shell stays closed even if the readiness gate is ready sooner, " +
                 "so a fast load doesn't flash-open. Enforced inside the gate itself so gravity release and " +
                 "shell break-open stay one beat. Ignored when the shell is disabled.")]
        [Range(0f, 5f)]
        public float MeteorShellMinHoldSeconds = 1.75f;
        [Tooltip("V13 burning-descent VFX (METEOR_ARRIVAL_SEQUENCE_SPEC.md): first-person screen-edge " +
                 "flames + embers that ignite when the readiness gate releases and burn off by altitude " +
                 "before landing. Arrival-sequence only — ordinary falls never show meteor effects. " +
                 "Only active when EnableSkyDropSpawn is true.")]
        public bool EnableMeteorDescentVfx = true;

        [Header("Structure Placement")]
        public bool EnableStructurePlacementSystem = true;
        public bool EnableRelicRealizationSystem = true;
        public bool EnableRelicLodSelectionSystem = true;
    
        [Header("Dungeon Systems")]
        [FormerlySerializedAs("EnableDungeonRenderingSystem")]
        public bool EnableDungeonEntitySpawningSystem = false;
    
        [Header("Weather Systems")]
        [FormerlySerializedAs("EnableHybridWeatherSystem")]
        public bool EnableWeatherGpuEffectsSystem = false;
    
        // ── Vista / Camera ──
    
        [Header("Vista / Camera")]
        [Tooltip("The WORLD REFERENCE DISTANCE — where the ordinary world visually ends. Drives the aerial " +
                 "haze/fog ramps (_AtmoFarFade) and the disc→skirt handoff. When > 0, overrides the " +
                 "terrain-derived value. Since R6 this is no longer necessarily the camera far plane — " +
                 "see LandmarkDrawDistance.")]
        [Min(0f)]
        public float VistaCameraFarClip = 600f;

        [Tooltip("Landmark draw distance (R6, LANDMARK_DRAW_DISTANCE_SPEC.md): the camera far plane is " +
                 "raised to max(world reference, this) so hero relics never cull, while the world's visual " +
                 "edge stays at VistaCameraFarClip. Cheap: terrain/scatter only exist ≤180u, so the far " +
                 "plane wasn't protecting perf. 0 = disabled (camera far plane = world reference, as before R6).")]
        [Min(0f)]
        public float LandmarkDrawDistance = 2000f;
    
        // ── Distance Fog ──
    
        [Header("Distance Fog")]
        [Tooltip("Enable distance fog that fades geometry into a haze before the camera far clip plane.")]
        public bool EnableDistanceFog = true;
    
        [Tooltip("Fog rendering mode. ExponentialSquared keeps near objects crisp and builds haze toward the horizon. " +
                 "Linear is a flat ramp useful mainly for hard far-clip masking.")]
        public FogMode FogMode = FogMode.ExponentialSquared;
    
        [Tooltip("Fog colour — should match the sky horizon tint so haze blends naturally rather than creating a stripe.")]
        public Color FogColor = new Color(0.80f, 0.68f, 0.60f, 1f);
    
        [Tooltip("Density for Exponential / ExponentialSquared fog modes. " +
                 "ExponentialSquared at 0.007 keeps nearby objects crisp and builds visible haze at 150–300 world units.")]
        [Range(0.001f, 0.05f)]
        public float FogDensity = 0.007f;
    
        [Tooltip("(Linear mode only) Fog start as a fraction of the camera far clip.")]
        [Range(0f, 1f)]
        public float FogStartRatio = 0.4f;
    
        [Tooltip("(Linear mode only) Fog end as a fraction of the camera far clip. " +
                 "0.95 means fully opaque just before the far clip plane.")]
        [Range(0.1f, 1f)]
        public float FogEndRatio = 0.95f;
    
        /// <summary>
        /// Camera far clip distance. Returns <see cref="VistaCameraFarClip"/> when set (> 0),
        /// otherwise derives from terrain render distance with 1.5× headroom.
        /// The vista override decouples visual range from terrain streaming radius.
        /// </summary>
        public float DerivedCameraFarClip =>
            VistaCameraFarClip > 0f
                ? VistaCameraFarClip
                : Mathf.Max(100f, TerrainRenderDistance * 1.5f);

        /// <summary>
        /// The far clip plane cameras should actually use (R6): the world reference distance,
        /// raised to <see cref="LandmarkDrawDistance"/> when that is larger so hero relics never
        /// cull. With <see cref="LandmarkDrawDistance"/> = 0 this collapses to
        /// <see cref="DerivedCameraFarClip"/> — pre-R6 behavior.
        /// </summary>
        public float DerivedLandmarkFarClip =>
            Mathf.Max(DerivedCameraFarClip, LandmarkDrawDistance);
    
        /// <summary>Absolute fog start distance (Linear mode). Derived from <see cref="DerivedCameraFarClip"/> × <see cref="FogStartRatio"/>.</summary>
        public float DerivedFogStartDistance => DerivedCameraFarClip * FogStartRatio;
    
        /// <summary>Absolute fog end distance (Linear mode). Derived from <see cref="DerivedCameraFarClip"/> × <see cref="FogEndRatio"/>.</summary>
        public float DerivedFogEndDistance => DerivedCameraFarClip * FogEndRatio;
    }
}
