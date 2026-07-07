# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A 16-bit retro-style procedural terrain/dungeon sandbox game built in **Unity 6.2** using **DOTS/ECS architecture**. Features SDF-based terrain with Surface Nets meshing, Wave Function Collapse dungeons, and GPU compute shader integration.

## Precedence

If instructions conflict: **safety > architecture rules > performance > style > logging verbosity**.

## Documentation Navigation

- Start documentation discovery with `Assets/Docs/DOCUMENT_INDEX.md` before broad doc searches.
- Prefer active/current docs over `Assets/Docs/Archives/` unless historical context is explicitly needed.
- Documentation structure, metadata, and canonical-doc rules live in `Assets/Docs/DOCUMENTATION_SYSTEM_SPEC.md`.
- For documentation creation, reorganization, indexing, or canonicalization tasks, use the `documentation-governance` skill in `.agents/skills/documentation-governance/`.

## Core Architecture Principles

### DOTS-First Development (Mandatory)

1. **Runtime Spawning** - All gameplay entities are created at runtime by systems, never placed in the editor hierarchy
2. **Minimal Bootstrap** - One small MonoBehaviour entry point, then pure ECS
3. **No MonoBehaviour Logic** - MonoBehaviours only for bootstrap and configuration, never gameplay
4. **No Editor Scene Dependencies** - Tests use empty scenes + programmatic entity creation
5. **BlobAssets for Shared Data** - Immutable data stored in blob references; dispose before reassigning: `if (oldBlob.IsCreated) oldBlob.Dispose();`

### Dual Terrain Pipelines

**Heightmap Path (Legacy — quarantined in `Assets/Scripts/DOTS/Terrain/Legacy/`, namespace `DOTS.Terrain.Legacy`):**
`TerrainEntityManager → TerrainDataBuilder → LegacyHeightmapTerrainGenerationSystem → GPU compute → BlobAsset → Mesh`
Do not extend this pipeline; retirement is tracked in `Assets/Docs/Process/CODEBASE_SIMPLIFICATION_PLAN.md` §6.7 (S11).

**SDF / Surface Nets Path (Primary for destructible terrain):**
Follow `Assets/Docs/Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md`. Keep heightmap and SDF pipelines decoupled.

## Build & Test Commands

```bash
# Unity Test Runner (from Unity Editor)
# Window > General > Test Runner

# Run EditMode tests (fast, no scene required)
# Test Runner > EditMode tab > Run All

# Run PlayMode tests (integration tests)
# Test Runner > PlayMode tab > Run All

# Build validation
# File > Build Settings > Build

# After major refactors, clear and restart Unity:
# Delete: Library/, Temp/, obj/
```

### Test Locations

All NUnit tests live under `Assets/Scripts/DOTS/Tests/` in exactly two assemblies:

- `EditMode/` (`DOTS.Tests.EditMode`, Editor-only) - fast unit/integration tests
- `PlayMode/` (`DOTS.Tests.PlayMode`) - frame/physics/scene tests incl. smoke tests and the `TestSystemBootstrap` helper

Namespaces mirror the folders (`DOTS.Tests.EditMode` / `DOTS.Tests.PlayMode`). See the folder's `README.md`.

## Code Conventions

### System Authoring (Required Pattern)

```csharp
using Unity.Burst;
using Unity.Entities;

namespace DOTS.Player.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(PhysicsSimulationGroup))]  // Explicit ordering
    public partial struct MySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RequiredComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Implementation
        }
    }
}
```

### Key Requirements

- All systems must be `partial` (for source generators)
- Use `[BurstCompile]` for performance
- Use `[DisableAutoCreation]` for conditionally-created systems
- One class per file, filename matches class name
- Prefer struct-based `ISystem` over class-based `SystemBase`
- Use unique class names (no namespace-only disambiguation)
- Use `EntityCommandBuffer` from `EndSimulationEntityCommandBufferSystem.Singleton` for structural changes in jobs

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Systems | PascalCase + `System` suffix | `PlayerMovementSystem` |
| Components | PascalCase, optional suffix | `PlayerInputComponent`, `TerrainData` |
| Enums | PascalCase with `: byte` for ECS | `PlayerMovementMode : byte` |
| Private fields | camelCase with underscore | `_cachedQuery` |

### Code Documentation

- Add XML `<summary>` comments to all new public types, components, and non-trivial public methods.
- Prefer **why** comments over **what** comments — the code shows what; comments should explain constraints, trade-offs, ordering decisions, or intentional simplifications that aren't derivable from reading the code alone.
- Always comment non-obvious DOTS/ECS constraints:
  - System update ordering rationale (`[UpdateBefore]` / `[UpdateAfter]` choices)
  - `WithoutBurst()` usage — explain why Burst doesn't apply
  - Intentional `SystemBase` over `ISystem` (e.g. managed API required)
  - `[DisableAutoCreation]` rationale
  - BlobAsset lifecycle notes at disposal sites
- Don't comment what the code obviously does.
- Don't add comments to code you didn't change.

### Namespace Structure

Namespaces mirror folders under `Assets/Scripts/DOTS/` except the two documented exceptions at the bottom.

```
DOTS.
├── Core                    # DebugSettings + debug controllers (DOTS/Core)
├── Core.Authoring          # DotsSystemBootstrap, ProjectFeatureConfig (own asmdef)
├── Compute                 # ComputeShaderManager
├── Impostors               # Ground-plane horizon impostor
├── Structures              # Relic/structure placement
├── Terrain                 # SDF terrain core (SDF/, chunk components, physics)
│   ├── .Meshing / .Streaming / .LOD    # Surface Nets, chunk window, LOD
│   ├── .Grass / .Trees / .Rocks / .Pebbles   # Scatter families
│   ├── .SurfaceScatter     # Shared scatter math/render/delta utilities
│   ├── .Modification       # Glob physics (live)
│   ├── .Weather            # WeatherSimulationSystem + WeatherGpuEffectsSystem
│   ├── .WFC                # Dungeon generation (paused, resuming)
│   ├── .Debug              # Seam validators, diagnostics
│   └── .Legacy             # Quarantined heightmap pipeline — do not extend
├── Player.Systems / .Components / .Bootstrap   # Folder: Assets/Scripts/Player (exception: no DOTS/ segment)
└── Rendering.Sky           # Folder: Assets/Scripts/Rendering/Sky (exception; Core.asmdef)
```

## Debug Logging

**Never use `Debug.Log` directly in systems.** Use the centralized debug system:

```csharp
using DOTS.Core;

DebugSettings.LogTerrain("Terrain message");
DebugSettings.LogWFC("WFC message");
DebugSettings.LogWeather("Weather message");
DebugSettings.LogRendering("Rendering message");
DebugSettings.LogSeam("Seam stitching message");
DebugSettings.LogTest("Test message");
```

Debug flags controlled via `DebugSettings` static class (all default to `false`). Add new flags to DebugSettings instead of preprocessor directives.

## Configuration

### ScriptableObject Settings

- **Location**: `Assets/Resources/TerrainGenerationSettings.asset`
- Access via: `Resources.Load<TerrainGenerationSettings>("TerrainGenerationSettings")`
- Contains: performance knobs, noise params, mesh height scale, debug toggles, terrain thresholds

### Feature Flags

- `ProjectFeatureConfig` - Controls which systems are enabled
- `DebugSettings.EnableTestSystems` - Gates test-only systems

### Dev Determinism Pins

Dynamic/randomized systems each expose a **pin** in their existing config so debugging and visual
validation run against stable data — added case-by-case when a system first causes pain, not as a
central framework. Existing pins: `TimeOfDayController.pinTimeOfDay` (+ `pinnedNormalizedTime`) holds
the day/night cycle; `TimeOfDayController.disableAtmosphereHaze` broadcasts zero V9 atmosphere haze so
converted surfaces render clean (`ProjectFeatureConfig.EnableDistanceFog` no longer governs the visible
haze — it only drives built-in fog on unconverted terrain/scatter); `DebugSettings.UseFixedWFCSeed`
fixes WFC seeding. Relic/structure placement is
already seed-deterministic (hash-jittered stable anchor IDs) and needs no pin; *authored* placement
(quests, hero relics) is a product feature, not a pin — see ticket V12.

### Compute Shaders

- **Location**: `Assets/Resources/Shaders/`
- Access: `Resources.Load("Shaders/ShaderName")` (exact name match required)
- Managed by: `ComputeShaderManager.InitializeKernels()`
- Kernel/constant names must mirror C# ↔ `.compute` files exactly

## Key Systems Reference

### Terrain Pipeline

1. `TerrainChunkDensitySamplingSystem` - SDF density sampling
2. `TerrainChunkMeshBuildSystem` - Surface Nets meshing (Burst jobs)
3. `TerrainChunkMeshUploadSystem` - GPU mesh upload
4. `TerrainChunkRenderPrepSystem` - Rendering preparation
5. `TerrainChunkColliderBuildSystem` - Physics collider generation

### Player Systems

1. `PlayerInputSystem` - Input capture
2. `PlayerMovementSystem` - Physics-based movement
3. `PlayerGroundingSystem` - Ground detection
4. `CameraEffectResolverSystem` - The camera driver (third-person orbit + effects)

### WFC Dungeon Flow (paused — known bootstrap gap, `Assets/Docs/Tickets/TICKETS.md`)

`Collapse state → prefab instantiation → rendering`
- `WFCCollapseSystem` with deterministic seeding (`DebugSettings.UseFixedWFCSeed`, default seed: 12345); its compute-shader propagation path is an unimplemented stub
- `DungeonPrefabRegistry` supplies baked entity prefabs
- `DungeonEntitySpawningSystem` spawns entities; `DungeonDebugVisualizationSystem` is editor-only debug

### Bootstrap Pattern

```csharp
public class PlayerCameraBootstrap : MonoBehaviour
{
    private void Start()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        var em = world.EntityManager;

        var playerEntity = em.CreateEntity();
        em.AddComponent<PlayerTag>(playerEntity);
        em.AddComponentData(playerEntity, new LocalTransform { /* ... */ });
        // Systems automatically process when components exist
    }
}
```

## Development Workflow

### SPEC → TEST → CODE

1. **Spec First**: Define behavior in documentation
2. **Test Second**: Write failing tests
3. **Code Third**: Implement until tests pass

### Extending Systems

- Prefer augmenting existing systems via `partial class` in a new file
- Never extend anything in `DOTS.Terrain.Legacy` — that pipeline is quarantined pending retirement

## Common Pitfalls

- Missing `partial` on systems → source generators fail
- "namespace already contains a definition for X" after a rename/refactor → stale source-generator output; check `Temp/GeneratedCode/<Assembly>/` and clear `Library/`, `Temp/`, `obj/`
- `Debug.Log` in systems → use `DebugSettings.*` loggers
- BlobAsset leaks → dispose before reassigning
- Compute shader name/kernel mismatches → verify Resources.Load names and kernel strings
- Structural changes without ECB → breaks Burst in jobs
- Duplicating existing systems instead of extending
- Hardcoded config → use `TerrainGenerationSettings`
- Storing managed references in components → breaks Burst

## Package Dependencies

- `com.unity.entities`: 1.4.2
- `com.unity.entities.graphics`: 1.4.16
- `com.unity.physics`: 1.4.2
- `com.unity.burst`: 1.8.25
- `com.unity.inputsystem`: 1.16.0
- `com.unity.cinemachine`: 3.1.5
- `com.unity.render-pipelines.universal`: 17.2.0
- `com.unity.test-framework`: 1.6.0

## Related Documentation

- `Assets/Docs/DOCUMENT_INDEX.md` - Canonical entry point for project documentation
- `Assets/Docs/DOCUMENTATION_SYSTEM_SPEC.md` - Documentation structure and AI-discovery rules
- `Assets/Docs/Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md` - SDF terrain implementation roadmap
- `Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md` - Bootstrap pattern guide with physics setup
- NUnit suites under `Assets/Scripts/DOTS/Tests/` are the test surface (manual-harness catalog archived to `Assets/Docs/Archives/ManualTestScripts_2026/`)
- `.github/copilot-instructions.md` - Additional context including current development focus
