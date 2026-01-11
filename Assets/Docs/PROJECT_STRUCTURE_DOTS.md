# Project Structure – DOTS-First Overview

## Purpose
This document describes the high-level structure of the 16bitProcGen project as it relates to Unity DOTS (Entities/ECS), including folder layout, assembly definitions, and best practices for modular, scalable DOTS development.

---

## Folder Structure (Key Areas)

```
Assets/
  Docs/                # Project documentation (this file, migration plans, specs)
  Scripts/
    Authoring/         # MonoBehaviours, ScriptableObjects, and DOTS bootstraps
    DOTS/              # Pure DOTS systems, components, jobs, and ECS logic
      Core/            # Core ECS utilities, debug, and shared logic
      WFC/             # Wave Function Collapse systems and data
      Terrain/         # Terrain generation, SDF, and mesh systems
      ...              # Other DOTS feature areas
    Player/            # Player systems, input, camera, and bootstrap logic
    ...                # Other gameplay or feature modules
  ScriptableObjects/   # (Legacy) ScriptableObject assets (migrating to Authoring/)
  ...
```

---

## Assembly Definitions (.asmdef)
- **Modular assemblies** for each major feature (e.g., DOTS.Terrain, DOTS.Player.Bootstrap, Core, etc.)
- **Authoring**: MonoBehaviours and ScriptableObjects for scene setup and configuration
- **DOTS**: Pure ECS systems and components, grouped by feature
- **Tests**: Separate assemblies for PlayMode and EditMode tests

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

**Legacy Terrain Systems (⚠️ Deprecated):**
These systems use the legacy `DOTS.Terrain.TerrainData` component and are maintained for backward compatibility only. New development should use the SDF terrain pipeline.
- `TerrainSystem` - [LEGACY] Core terrain validation system for legacy TerrainData component
- `HybridTerrainGenerationSystem` - [LEGACY] Terrain generation using compute shaders with legacy TerrainData (gated by `EnableHybridTerrainGenerationSystem`)
- `TerrainGenerationSystem` - [LEGACY] Legacy terrain generation system (currently disabled internally)
- `TerrainModificationSystem` - [LEGACY] Terrain modification for legacy TerrainData component (gated by `EnableTerrainModificationSystem`)
- `TerrainCleanupSystem` - Blob asset cleanup (gated by `EnableTerrainCleanupSystem`)
- `ChunkProcessor` - Chunk processing (gated by `EnableChunkProcessor`)
- `TerrainGlobPhysicsSystem` - Terrain glob physics (gated by `EnableTerrainGlobPhysicsSystem`)

**For new terrain development:** Use `TerrainBootstrapAuthoring` or create entities with SDF components (`DOTS.Terrain.TerrainChunk`, `TerrainChunkGridInfo`, `TerrainChunkBounds`, etc.). See `Assets/Docs/SDF_SurfaceNets_ECS_Overview.md` for details.

#### Player Systems (gated by `EnablePlayerSystem`)
- `PlayerBootstrapFixedRateInstaller` - Fixed rate installer (gated by `EnablePlayerBootstrapFixedRateInstaller`)
- `PlayerEntityBootstrap` - Entity bootstrap (gated by `EnablePlayerEntityBootstrap`)
- `PlayerEntityBootstrap_PureECS` - Pure ECS bootstrap (gated by `EnablePlayerEntityBootstrapPureEcs`)
- `PlayerInputSystem` - Player input (gated by `EnablePlayerInputSystem`)
- `PlayerLookSystem` - Player look (gated by `EnablePlayerLookSystem`)
- `PlayerMovementSystem` - Player movement (gated by `EnablePlayerMovementSystem`)
- `PlayerGroundingSystem` - Player grounding (gated by `EnablePlayerGroundingSystem`)
- `PlayerCameraSystem` - Player camera (gated by `EnablePlayerCameraSystem`)
- `PlayerCinemachineCameraSystem` - Cinemachine camera (gated by `EnablePlayerCinemachineCameraSystem`)
- `CameraFollowSystem` - Camera follow (gated by `EnableCameraFollowSystem`)
- `SimplePlayerMovementSystem` - Simple movement (gated by `EnableSimplePlayerMovementSystem`, conditional compile)

#### Dungeon Systems (gated by `EnableDungeonSystem`)
- `DungeonRenderingSystem` - Dungeon rendering (gated by `EnableDungeonRenderingSystem`)
- `DungeonVisualizationSystem` - Dungeon visualization

#### Weather Systems (gated by `EnableWeatherSystem`)
- `WeatherSystem` - Weather effects system

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
