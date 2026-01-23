# CLAUDE.md - 16bitProcGen

## Project Overview

A 16-bit retro-style procedural terrain/dungeon sandbox game built in **Unity 6.2** using **DOTS/ECS architecture**. Features SDF-based terrain with Surface Nets meshing, Wave Function Collapse dungeons, and GPU compute shader integration.

## Core Architecture Principles

### DOTS-First Development (Mandatory)

1. **Runtime Spawning** - All gameplay entities are created at runtime by systems, never placed in the editor hierarchy
2. **Minimal Bootstrap** - One small MonoBehaviour entry point, then pure ECS
3. **No MonoBehaviour Logic** - MonoBehaviours only for bootstrap and configuration, never gameplay
4. **No Editor Scene Dependencies** - Tests use empty scenes + programmatic entity creation
5. **BlobAssets for Shared Data** - Immutable data stored in blob references

### SOLID Principles in ECS Context

- **Single Responsibility**: Each system handles one concern
- **Open/Closed**: Extend via new components/systems, don't modify existing
- **Interface Segregation**: Components are small, focused data containers
- **Dependency Inversion**: Systems query for components, not concrete entities

## Project Structure

```
Assets/
├── Scripts/
│   ├── DOTS/                      # Pure ECS systems & components
│   │   ├── Core/                  # Debug utilities, DebugSettings
│   │   ├── Terrain/               # SDF terrain, meshing, physics
│   │   ├── Modification/          # Terrain editing/destruction
│   │   ├── Biome/                 # Biome system
│   │   ├── WFC/                   # Wave Function Collapse dungeons
│   │   ├── Generation/            # Terrain generation pipeline
│   │   ├── Weather/               # Weather system
│   │   ├── Compute/               # GPU shader management
│   │   └── Test/                  # Test harnesses
│   └── Player/                    # Player systems, bootstrap
│       ├── Bootstrap/             # Entity spawning patterns
│       ├── Systems/               # Movement, camera, input
│       ├── Components/            # Player data structures
│       └── Authoring/             # MonoBehaviour setup
├── Docs/                          # Architecture documentation
│   ├── AI/                        # AI-focused specs
│   └── Archives/                  # Historical docs
└── Resources/
    └── Shaders/                   # GPU compute shaders
```

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

### Naming Conventions

| Type | Convention | Example |
|------|------------|---------|
| Systems | PascalCase + `System` suffix | `PlayerMovementSystem` |
| Components | PascalCase, optional suffix | `PlayerInputComponent`, `TerrainData` |
| Enums | PascalCase with `: byte` for ECS | `PlayerMovementMode : byte` |
| Methods | PascalCase | `OnCreate`, `ApplyModification` |
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
DebugSettings.LogPlayer("Player message");
```

Debug flags are controlled via `DebugController` MonoBehaviour or `DebugSettings` static class.

## Testing Requirements

### Test Categories

1. **EditMode Tests** - Unit tests for pure ECS logic (fast, no scene)
2. **PlayMode Tests** - Integration tests with runtime systems

### Test Patterns

```csharp
[Test]
public void MySystem_WhenCondition_ShouldBehavior()
{
    // Arrange - Create fresh World
    using var world = new World("TestWorld");
    var em = world.EntityManager;

    // Act - Create entities, run systems
    var entity = em.CreateEntity();
    em.AddComponentData(entity, new MyComponent { Value = 1 });

    // Assert
    Assert.AreEqual(expected, actual);
}
```

### Test Locations

- `Assets/Scripts/DOTS/Test/` - Manual/hybrid tests
- `Assets/Scripts/DOTS/Tests/Automated/` - NUnit automated tests
- `Assets/Scripts/Player/Bootstrap/Tests/` - Player bootstrap tests

### Test Principles

- No hand-placed test entities; all spawned by code
- Tests spawn entities programmatically
- Empty scene + runtime spawning pattern
- Avoid editor scene dependencies

## Configuration

### ScriptableObject Settings

- **Location**: `Assets/Resources/TerrainGenerationSettings.asset`
- Access via: `Resources.Load<TerrainGenerationSettings>("TerrainGenerationSettings")`

### Feature Flags

- `ProjectFeatureConfig` - Controls which systems are enabled
- Read by `DotsSystemBootstrap` for conditional system creation

### Compute Shaders

- **Location**: `Assets/Resources/Shaders/`
- Access: `Resources.Load("Shaders/ShaderName")` (exact name match required)
- Managed by `ComputeShaderManager.InitializeKernels()`

## Development Workflow

### SPEC → TEST → CODE

1. **Spec First**: Define behavior in documentation
2. **Test Second**: Write failing tests
3. **Code Third**: Implement until tests pass

### Common Commands

```bash
# Unity Test Runner (from Unity Editor)
# Window > General > Test Runner

# Build validation
# File > Build Settings > Build
```

### After Major Refactors

Clear these directories and restart Unity:
- `Library/`
- `Temp/`
- `obj/`

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

## Package Dependencies

- `com.unity.entities`: 1.4.2
- `com.unity.entities.graphics`: 1.4.16
- `com.unity.physics`: 1.4.2
- `com.unity.burst`: 1.8.25
- `com.unity.inputsystem`: 1.16.0
- `com.unity.cinemachine`: 3.1.5
- `com.unity.render-pipelines.universal`: 17.2.0

## What NOT to Do

- Place gameplay entities in scene hierarchy
- Create GameObject visualization systems for production code
- Use SubScenes for world layout (this is a procedural game)
- Hardcode configuration values
- Use `Debug.Log` directly in systems
- Store managed references in components (breaks Burst)
- Use `SystemBase` when `ISystem` would work

## What TO Do

- Spawn everything at runtime via systems
- Use baking for prefabs/assets only
- Test with empty scenes + programmatic spawning
- Use DOTS debugging tools (Entity Debugger, Systems window)
- Configure via ScriptableObjects or ProjectFeatureConfig
- Dispose BlobAssets before reassigning: `if (oldBlob.IsCreated) oldBlob.Dispose();`

## Related Documentation

- `Assets/Docs/AI_Instructions.md` - Detailed AI assistant standards
- `Assets/Docs/PROJECT_STRUCTURE_DOTS.md` - Full project structure
- `Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md` - Bootstrap pattern guide
- `Assets/Scripts/DOTS/Test/Testing_Documentation.md` - Complete test catalog
- `.github/copilot-instructions.md` - Additional context for AI assistants
