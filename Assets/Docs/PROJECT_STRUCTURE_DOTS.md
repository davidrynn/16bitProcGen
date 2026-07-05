# Project Structure – DOTS-First Overview

**Status:** ACTIVE
**Last Updated:** 2026-07-02 (rewritten to post-cleanup-round-1 reality; see `AI/CODEBASE_SIMPLIFICATION_PLAN.md`)

## Purpose
This document describes the high-level structure of the 16bitProcGen project as it relates to Unity DOTS (Entities/ECS), including folder layout, assembly definitions, and best practices for modular, scalable DOTS development.

---

## Folder Structure (Key Areas)

```
Assets/
  Docs/                # Project documentation (DOCUMENT_INDEX.md is the entry point)
  Scripts/
    DOTS/
      Core/            # DebugSettings + debug controllers (namespace DOTS.Core)
        Authoring/     # DotsSystemBootstrap + ProjectFeatureConfig (own asmdef, ns DOTS.Core.Authoring)
      Compute/         # ComputeShaderManager (ns DOTS.Compute)
      Impostors/       # Ground-plane horizon impostor (ns DOTS.Impostors)
      Structures/      # Relic/structure placement (ns DOTS.Structures)
      Modification/    # Live glob physics (ns DOTS.Terrain.Modification)
      Weather/         # WeatherSimulationSystem + WeatherGpuEffectsSystem (ns DOTS.Terrain.Weather)
      WFC/             # Dungeon WFC — paused, resuming (ns DOTS.Terrain.WFC)
      Terrain/         # SDF terrain (ns DOTS.Terrain.*)
        SDF/           # Density field, edits, sampling systems
        Meshing/       # Surface Nets mesh build/upload/prep
        Physics/       # Collider build
        Streaming/     # Chunk streaming window
        LOD/           # Chunk LOD selection/apply
        Grass/         # GPU grass (ns DOTS.Terrain.Grass)
        Trees/ Rocks/ Pebbles/   # Scatter families
        SurfaceScatter/          # Shared scatter math/render/delta utilities
        Legacy/        # Quarantined heightmap pipeline (ns DOTS.Terrain.Legacy) — do not extend
        Debug/         # Seam validators, diagnostics
      Tests/           # ALL NUnit tests: EditMode/ + PlayMode/, one asmdef each (ns DOTS.Tests.*; plan R48)
    Player/            # ns DOTS.Player.* — documented exception: folder lacks the DOTS/ segment
    Rendering/Sky/     # ns DOTS.Rendering.Sky — documented exception, compiled into Core.asmdef
    Terrain/Rendering/ # TerrainChunkRenderSettings only (Core.asmdef; relocation blocked by asmdef cycle, plan S10)
  ScriptableObjects/   # ProjectFeatureConfig.asset and friends
  Resources/           # Runtime-loaded settings + compute shaders
```

---

## Assembly Definitions (.asmdef) — actual state
Reality check (2026-07-03): assemblies are **not** per-feature. The bulk of `Assets/Scripts/DOTS/**` compiles into one monolithic `DOTS.Terrain` assembly (Structures, WFC, Weather, Impostors, Compute included). The other production assemblies: `Core` (root loose scripts + Rendering/Sky + Terrain/Rendering), `DOTS.Core.Authoring`, `DOTS.Terrain.Bootstrap`, `DOTS.Terrain.SurfaceScatter.Editor`, `DOTS.Player.Components`, `DOTS.Player.Bootstrap`, `Player`. Reference direction that bites: `DOTS.Terrain` → `Core` (so nothing in `Core` can reference DOTS types — see plan row S10 for the modularization follow-up).
- **Tests**: exactly two test assemblies (round-2 consolidation, plan R48): `DOTS.Tests.EditMode` and `DOTS.Tests.PlayMode`, both under `Assets/Scripts/DOTS/Tests/`

---

## System Bootstrap Pattern

The project uses a centralized bootstrap system (`DotsSystemBootstrap`) to conditionally enable DOTS systems marked with `[DisableAutoCreation]` based on `ProjectFeatureConfig` settings.

### Assembly Reference Requirements

**Important**: Unity's assembly definition references are **not transitive**. When the bootstrap creates a system via `world.CreateSystem<SomeSystem>()`, the bootstrap's assembly must directly reference all Unity packages and assemblies that `SomeSystem` uses, even if those packages are already referenced by the system's own assembly.

For example, if `TerrainGlobPhysicsSystem` uses `Unity.Transforms.LocalTransform`, and `DOTS.Core.Authoring` (the bootstrap assembly) creates that system, then `DOTS.Core.Authoring.asmdef` must include `"Unity.Transforms"` in its references, even though `DOTS.Terrain.asmdef` also references it.

Similarly, when creating player systems that use `PlayerInputComponent` or `PlayerMovementConfig`, the bootstrap assembly must reference `"DOTS.Player.Components"` directly.

### Current Pattern

- `DotsSystemBootstrap` (in `DOTS.Core.Authoring` assembly) reads `ProjectFeatureConfig` and conditionally creates systems
- The bootstrap assembly accumulates Unity package and assembly references as systems are added
- This is an **intentional design choice** for centralized, configurable system registration

### When Adding New Systems

1. Add the system class with `[DisableAutoCreation]` attribute
2. Add conditional creation logic to `DotsSystemBootstrap.cs`
3. Add any required Unity package or assembly references to `DOTS.Core.Authoring.asmdef` if the new system introduces new dependencies

### Systems Managed by Bootstrap

The following systems are controlled by `DotsSystemBootstrap` via `ProjectFeatureConfig`:

#### Terrain Systems

**Current Active System: SDF (Signed Distance Fields) Terrain Pipeline**
The project uses an SDF-based terrain system as the primary terrain generation approach. This system uses components in the `DOTS.Terrain` namespace and Surface Nets meshing.

**Active SDF Systems:**
- `TerrainChunkDensitySamplingSystem` - SDF density sampling (gated by `EnableTerrainChunkDensitySamplingSystem`)
- `TerrainChunkStreamingSystem` - Streams chunk entities around the player (gated by `EnableTerrainChunkStreamingSystem`; radius via `TerrainStreamingRadiusInChunks`)
- `TerrainEditInputSystem` - Terrain edit input for SDF edits (gated by `EnableTerrainEditInputSystem`)
- `TerrainChunkMeshBuildSystem` - Surface Nets mesh building (gated by `EnableTerrainChunkMeshBuildSystem`)
- `TerrainChunkRenderPrepSystem` - Render preparation (gated by `EnableTerrainChunkRenderPrepSystem`)
- `TerrainChunkMeshUploadSystem` - Mesh upload (gated by `EnableTerrainChunkMeshUploadSystem`)

Note: Streaming radius is mirrored into an ECS singleton (`ProjectFeatureConfigSingleton`) by `DotsSystemBootstrap` so unmanaged systems can read it.

**Legacy Terrain Systems (⚠️ quarantined in `DOTS/Terrain/Legacy`, namespace `DOTS.Terrain.Legacy`):**
These use the legacy heightmap `TerrainData` component; kept compiling for reference, do not extend (retirement tracked as plan row S11).
- `TerrainDataValidationSystem` - [LEGACY] validates legacy TerrainData (created unconditionally; survivor of the ChunkProcessor/TerrainSystem merge)
- `LegacyHeightmapTerrainGenerationSystem` - [LEGACY] compute-shader heightmap generation (gated by `EnableLegacyHeightmapTerrainGenerationSystem`)
- `TerrainCleanupSystem` - Blob asset cleanup for legacy components (gated by `EnableTerrainCleanupSystem`)

**Live but heightmap-adjacent:**
- `TerrainGlobPhysicsSystem` - Terrain glob physics (gated by `EnableTerrainGlobPhysicsSystem`; in `DOTS.Terrain.Modification`)

**For new terrain development:** Use `TerrainBootstrapAuthoring` or create entities with SDF components (`DOTS.Terrain.TerrainChunk`, `TerrainChunkGridInfo`, `TerrainChunkBounds`, etc.). See `Assets/Docs/SDF_SurfaceNets_ECS_Overview.md` for details.

#### Player Systems (gated by `EnablePlayerSystem`)
- `PlayerBootstrapFixedRateInstaller` - Fixed rate installer (gated by `EnablePlayerBootstrapFixedRateInstaller`)
- `PlayerEntityBootstrap` - Entity bootstrap (gated by `EnablePlayerEntityBootstrap`)
- `PlayerInputSystem` / `PlayerLookSystem` / `PlayerMovementSystem` / `PlayerGroundingSystem` / `PlayerTerrainSafetySystem` - core loop (individually gated)
- Movement MVP: `SlingshotChargeSystem`, `SlingshotLaunchSystem`, `ChainWindowSystem`, `GlideSystem`, `LandingDetectionSystem`, `MovementStateBookkeepingSystem`
- Camera: `CameraEffectResolverSystem` is the **only** camera driver (superseded variants were removed in cleanup round 1), plus the Camera*FeedbackSystem writers and `ScreenEffectResolverSystem`

#### Dungeon Systems (gated by `EnableDungeonSystem`) — paused, resuming
- `DungeonEntitySpawningSystem` - spawns prefab entities from collapsed WFC cells (gated by `EnableDungeonEntitySpawningSystem`)
- `DungeonDebugVisualizationSystem` - editor-only debug visualization
- `WFCCollapseSystem` - the WFC solver; **known gap:** not yet created by the bootstrap (`Assets/Docs/Tickets/backlog.md`, ticket W1)

#### Weather Systems (gated by `EnableWeatherSystem`)
- `WeatherSimulationSystem` - per-chunk weather state simulation
- `WeatherGpuEffectsSystem` - applies weather via compute shaders (gated by `EnableWeatherGpuEffectsSystem`)

**Note**: All systems listed above have `[DisableAutoCreation]` to prevent Unity's automatic system discovery. They are only created when explicitly enabled via the bootstrap and config.

---

## DOTS-First Principles
- **All runtime logic in ECS systems/components**
- **MonoBehaviours only for authoring, bootstrapping, or hybrid visualization**
- **ScriptableObjects for config, referenced by bootstraps/authoring**
- **No cross-feature dependencies except via Core or explicit references**
- **[DisableAutoCreation]** on all systems not meant to run by default; enable via bootstrap/config

---

## Refactoring Guidance
- As you refactor, update this document to reflect new assemblies, folders, or architectural changes.
- Keep DOTS systems and authoring/config separate for clarity and modularity.
- Document any new feature area with a short section here.

---

_Last updated: 2026-01-10 (Added terrain streaming system + config singleton mirror)_
