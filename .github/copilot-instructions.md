# Copilot Instructions for 16bitProcGen

## Project Snapshot
Unity Entities 1.x + GPU compute shaders power a retro 16-bit procedural terrain/dungeon sandbox. This is a **DOTS-first project**: runtime code lives in systems and components; MonoBehaviours are reserved for authoring, UI, and scene bootstrap only.

**Key Directories:**
- `Assets/Scripts/` - All gameplay code (DOTS systems, components, managers)
- `Assets/Scripts/DOTS/` - Core ECS systems (terrain, WFC, weather, modification)
- `Assets/Scripts/Player/` - Player systems (movement, camera, input, bootstrap)
- `Assets/Resources/Shaders/` - Compute shaders (TerrainNoise, WFCGeneration, etc.)
- `Assets/Docs/` - Specifications and architecture docs
- `Assets/.cursor/plans/` - Production roadmap and feature plans

**Production Roadmap:** `Assets/.cursor/plans/game-production-plan-7ea46cb6.plan.md` drives development priorities. Review "Immediate Next Steps" before starting work.

---

## Core Architecture

### Terrain Generation Pipeline
**Flow:** `TerrainEntityManager` → `TerrainDataBuilder` → `HybridTerrainGenerationSystem` → GPU compute → BlobAsset → Mesh

1. **Entity Spawning:** `TerrainEntityManager` (MonoBehaviour) creates terrain chunk entities using `TerrainDataBuilder`
2. **GPU Compute:** `HybridTerrainGenerationSystem` orchestrates compute shader execution via `ComputeShaderManager`
3. **Data Storage:** Results stored in `TerrainHeightData` BlobAssets (reference-counted, dispose properly)
4. **Mesh Rebuild:** Gated on `needsGeneration` and `needsMeshUpdate` flags in `TerrainData` component
5. **Transform Sync:** `TerrainTransformSystem` synchronizes `TerrainData.worldPosition/rotation/scale` → `LocalTransform`

**Key Classes:**
- `TerrainEntityManager` - Entity lifecycle and spawning (MonoBehaviour singleton)
- `TerrainDataBuilder` - Static factory for terrain component creation
- `HybridTerrainGenerationSystem` - Orchestrates GPU generation pipeline
- `ComputeShaderManager` - Singleton managing compute shader loading and dispatch

### SDF / Surface Nets Terrain Rule

For any work related to SDF-based terrain, Surface Nets, or chunked destructible terrain:

- Treat `Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md` as the **authoritative, up-to-date plan**.
- Prefer the SDF + Surface Nets ECS pipeline described there over any older or heightmap-only terrain implementations.
- Do not extend `HybridTerrainGenerationSystem` or the existing compute-based terrain pipeline for SDF work unless the SPEC explicitly instructs otherwise.

### WFC Dungeon Generation
**Flow:** WFC collapse state → compute shader → prefab instantiation → rendering

1. **Collapse:** `HybridWFCSystem` manages Wave Function Collapse with deterministic seeding
   - Control via `DebugSettings.UseFixedWFCSeed` (default: `12345`)
   - Uses `Unity.Mathematics.Random` for Burst compatibility
2. **Prefabs:** `DungeonPrefabRegistry` singleton provides baked entity prefabs (floor, wall, door)
3. **Rendering:** `DungeonRenderingSystem` and `DungeonVisualizationSystem` instantiate prefabs from collapse results
4. **Data:** Collapse patterns stored in `WFCPatternData` BlobAssets

### GPU Compute Integration
**Requirements:**
- Shaders must live in `Assets/Resources/Shaders/` with exact names matching `Resources.Load()` calls
- Shader list: `TerrainNoise`, `WFCGeneration`, `TerrainModification`, `WeatherEffects`, `StructureGeneration`, `TerrainErosion`, `TerrainGlobRemoval`
- Kernel names and constants must mirror between C# and `.compute` files
- Mismatches surface as `FindKernel` failures during `ComputeShaderManager.InitializeKernels()`

### Configuration System
**`TerrainGenerationSettings`** (ScriptableObject at `Resources/TerrainGenerationSettings.asset`):
- Performance: `maxChunksPerFrame`, buffer sizes
- Noise: scale, height multiplier, biome scale, offset
- Visual: mesh height scale
- Debug: logging toggles
- Terrain types: height thresholds

Prefer inspector-based configuration over hardcoded values.

---

## DOTS Architecture Standards

### System Design Rules
1. **All systems must be `partial`** - Allows source generators to extend them
2. **One class per file** - Filename must match class name exactly
3. **Use `ISystem` for new systems** - Prefer struct-based `ISystem` over `SystemBase` (unless needing inheritance)
4. **Burst-friendly paths** - Avoid managed references in jobs; use `EntityCommandBuffer` for structural changes
5. **Unique class names** - No namespace-only disambiguation; rename conflicting classes

**Example Pattern:**
```csharp
namespace DOTS.Player.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnUpdate(ref SystemState state) 
        {
            // Use EntityCommandBuffer for structural changes
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
        }
    }
}
```

### BlobAsset Management
BlobAssets are **reference-counted** - improper disposal causes memory leaks:
- Always dispose old blobs before reassigning: `if (oldBlob.IsCreated) oldBlob.Dispose();`
- Types: `TerrainHeightData`, `TerrainModificationData`, `WFCPatternData`
- Creation pattern: `BlobBuilder` → `CreateBlobAssetReference` → assign to component

### Debug Logging Pattern
**Never use `Debug.Log` directly** - use `DOTS.Terrain.Core.DebugSettings`:

```csharp
using DOTS.Terrain.Core;

// Instead of: Debug.Log("WFC collapsed");
DebugSettings.LogWFC("Collapsed to wall tile");

// Other loggers:
DebugSettings.LogTerrain("Height generation complete");
DebugSettings.LogWeather("Weather changed to rain");
DebugSettings.LogRendering("Mesh rebuilt");
DebugSettings.LogTest("Test entity created");
```

**Toggles** (default `false`):
- `EnableDebugLogging`, `EnableWFCDebug`, `EnableTerrainDebug`
- `EnableWeatherDebug`, `EnableRenderingDebug`, `EnableTestDebug`
- `EnableTestSystems` - Gates test-only systems

Add new flags to `DebugSettings` rather than using preprocessor directives.

---

## Developer Workflows

### Play Mode Testing (Primary Loop)
1. **Quick terrain check:** Attach `HybridGenerationTest` to GameObject in scene
   - Press Space to force regeneration (`HybridTerrainGenerationSystem` hook)
   - Adjust `maxChunksPerFrame` in `TerrainGenerationSettings` asset
2. **WFC validation:** Use `WFCTestSetup` or `WFCDungeonRenderingTest` MonoBehaviours
   - Verify deterministic collapse (seed `12345`)
   - Check prefab placement and rendering
3. **Bootstrap patterns:** See `Assets/Scripts/Player/Bootstrap/` for pure-code DOTS scene setup
   - `PlayerCameraBootstrap_WithVisuals` - Complete player + camera spawn (no authoring needed)
   - Example of runtime entity spawning without subscenes

### Compute Shader Testing
**Smoke tests:** `Scripts/DOTS/Test/BasicComputeShaderTest.cs` and `SimpleComputeTest.cs`
- Validates kernel names match shader definitions
- Tests buffer allocation and dispatch
- Checks readback data correctness

**When shaders fail:**
1. Verify shader exists in `Assets/Resources/Shaders/`
2. Check kernel name spelling (case-sensitive)
3. Validate buffer sizes match shader expectations
4. Review `ComputeShaderManager.InitializeKernels()` logs

### Test Scene Setup
**Test MonoBehaviours** (attach to GameObject):
- `AutoTestSetup` - Basic environment setup
- `HybridTestSetup` - Full DOTS + compute environment
- `QuickTerrainEntityCreator` - Grid of terrain chunks
- `WeatherTestSetup` - Weather system integration
- See `Assets/Scripts/DOTS/Test/Testing_Documentation.md` for comprehensive test catalog

### Build Hygiene
After large refactors, clear generated code:
```powershell
Remove-Item -Recurse -Force Library, Temp, obj
# Restart Unity
```

---

## Making Changes

### Extending Existing Systems
**Prefer augmenting over duplicating** - these systems already have perf tracking and scheduling:
- `HybridTerrainGenerationSystem` - Terrain compute orchestration
- `HybridWFCSystem` - Dungeon WFC collapse
- `DungeonRenderingSystem` - Prefab instantiation
- `TerrainTransformSystem` - Transform synchronization
- `TerrainModificationSystem` - Terrain editing/destruction
- `TerrainGlobPhysicsSystem` - Glob physics and collection

**Pattern:** Extend via partial class in new file, don't create overlapping systems.

### Structural Changes in Systems
**Use `EntityCommandBuffer` for Burst safety:**
```csharp
var ecbSingleton = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>();
var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

foreach (var entity in SystemAPI.Query<RefRO<TerrainData>>())
{
    ecb.AddComponent<NewComponent>(entity);  // Deferred structural change
}
```

**Reserve direct `EntityManager` for MonoBehaviours** like `TerrainEntityManager`.

### Adding Compute Kernels
1. Add kernel to shader file in `Assets/Resources/Shaders/`
2. Mirror constants/buffer layouts in C# code
3. Update `ComputeShaderManager` kernel caching if needed
4. Add smoke test validating kernel loads
5. Test with debug logging enabled

### Adding Debug Features
1. Add toggle to `DebugSettings` (default `false`)
2. Gate logging: `DebugSettings.LogYourSystem("message")`
3. Add static helper: `public static void LogYourSystem(string msg, bool force = false)`
4. Update `DebugController` inspector if UI control needed

---

## Current Development Focus

**Phase 1: Core Player Experience** (per production plan)
1. **Magic Hand System** - Raycast targeting, charge mechanic, destruction integration
   - New namespace: `Scripts/Player/MagicHand/`
   - Components: `MagicHandComponent`, `MagicHandInputSystem`, `MagicHandVisualizationSystem`
2. **Slingshot Movement** - Replace FPS controls with grip + launch mechanics
   - New namespace: `Scripts/Player/Movement/`
   - Components: `SlingshotMovementComponent`, `SlingshotInputSystem`, `SlingshotTrajectorySystem`
3. **Resource Collection** - Auto-collect terrain globs, inventory storage
   - New namespace: `Scripts/Resources/`
   - Extend: `TerrainGlobComponent`, integrate with `TerrainGlobPhysicsSystem`
4. **Basic HUD** - Resource counters, hand charge indicator

**Short-term World Work:**
- Biome blending and transitions
- Structure placement beyond dungeons
- Reuse existing blob/component patterns for data storage

**Architecture Principle:** If legacy code conflicts with DOTS-first principles, prioritize refactoring to pure DOTS unless Unity tooling blocks it.

---

## Reference Documentation

- **`Assets/Scripts/DOTS/Test/Testing_Documentation.md`** - Complete test harness catalog with setup instructions
- **`Assets/Docs/AI_Instructions.md`** - Cursor-specific automation rules and SPEC → TEST → CODE workflow
- **`Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md`** - Pure-code DOTS scene setup patterns
- **`Assets/README.md`** - DOTS system authoring checklist (partial classes, cleanup procedures)
- **`Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md`** - SDF terrain integration roadmap

---

## Common Pitfalls

1. **Forgetting `partial` on systems** - Source generators will fail
2. **Direct `Debug.Log` in systems** - Use `DebugSettings` loggers
3. **BlobAsset leaks** - Always dispose before reassigning
4. **Compute shader name mismatches** - Check `Resources.Load()` spelling
5. **Structural changes without ECB** - Breaks Burst compilation in jobs
6. **Creating new systems for existing logic** - Extend via partial classes instead
7. **Hardcoded configuration** - Use `TerrainGenerationSettings` ScriptableObject

---

## Version Control Notes

- **Unity version:** 2022 LTS+ or Unity 6+
- **Required packages:** `com.unity.entities`, `com.unity.entities.graphics`, `com.unity.mathematics`, `com.unity.collections`, `com.unity.jobs`, `com.unity.burst`
- **Render pipeline:** Built-in or URP (project-configurable)
