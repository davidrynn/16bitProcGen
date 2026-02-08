# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A 16-bit retro-style procedural terrain/dungeon sandbox game built in **Unity 6.2** using **DOTS/ECS architecture**. Features SDF-based terrain with Surface Nets meshing, Wave Function Collapse dungeons, and GPU compute shader integration.

## Precedence

If instructions conflict: **safety > architecture rules > performance > style > logging verbosity**.

## Core Architecture Principles

### DOTS-First Development (Mandatory)

1. **Runtime Spawning** - All gameplay entities are created at runtime by systems, never placed in the editor hierarchy
2. **Minimal Bootstrap** - One small MonoBehaviour entry point, then pure ECS
3. **No MonoBehaviour Logic** - MonoBehaviours only for bootstrap and configuration, never gameplay
4. **No Editor Scene Dependencies** - Tests use empty scenes + programmatic entity creation
5. **BlobAssets for Shared Data** - Immutable data stored in blob references; dispose before reassigning: `if (oldBlob.IsCreated) oldBlob.Dispose();`

### Dual Terrain Pipelines

**Heightmap Path (Legacy/Parallel):**
`TerrainEntityManager → TerrainDataBuilder → HybridTerrainGenerationSystem → GPU compute → BlobAsset → Mesh`

**SDF / Surface Nets Path (Primary for destructible terrain):**
Follow `Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md`. Do not extend HybridTerrainGenerationSystem for SDF unless the spec explicitly instructs. Keep heightmap and SDF pipelines decoupled.

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

- `Assets/Scripts/DOTS/Test/` - Manual/hybrid test MonoBehaviours
- `Assets/Scripts/DOTS/Tests/Automated/` - NUnit automated tests
- `Assets/Scripts/Player/Bootstrap/Tests/` - Player bootstrap tests

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

### Namespace Structure

```
DOTS.
├── Terrain           # SDF terrain systems
├── Modification      # Destruction/editing
├── Player.Systems    # Player ISystem implementations
├── Player.Components # Player IComponentData
├── Player.Bootstrap  # Entity spawning
├── Biome             # Biome system
├── Generation        # Procedural generation
├── Weather           # Environmental effects
├── WFC               # Dungeon generation
└── Compute           # GPU management
```

## Debug Logging

**Never use `Debug.Log` directly in systems.** Use the centralized debug system:

```csharp
using DOTS.Terrain.Core;

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
4. `CameraFollowSystem` - Third-person camera

### WFC Dungeon Flow

`Collapse state → compute shader → prefab instantiation → rendering`
- `HybridWFCSystem` with deterministic seeding (`DebugSettings.UseFixedWFCSeed`, default seed: 12345)
- `DungeonPrefabRegistry` supplies baked entity prefabs
- `DungeonRenderingSystem` / `DungeonVisualizationSystem` handle instantiation

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
- Key systems to extend: `HybridTerrainGenerationSystem`, `HybridWFCSystem`, `DungeonRenderingSystem`, `TerrainTransformSystem`, `TerrainModificationSystem`

## Common Pitfalls

- Missing `partial` on systems → source generators fail
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

- `Assets/Docs/AI_Instructions.md` - Detailed AI assistant standards and DOTS-first workflow
- `Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md` - SDF terrain implementation roadmap
- `Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md` - Bootstrap pattern guide with physics setup
- `Assets/Scripts/DOTS/Test/Testing_Documentation.md` - Complete test catalog
- `.github/copilot-instructions.md` - Additional context including current development focus
