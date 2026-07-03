# Migration Notes: MonoBehaviour → DOTS/ECS

This document outlines the 3-phase migration plan to adapt SmolbeanPlanet3D's procedural generation and terrain concepts for a DOTS-first architecture in 16bitProcGen.

---

## Phase Overview

| Phase | Timeline | Focus | Effort |
|-------|----------|-------|--------|
| **Phase 1: Extraction** | 1–2 weeks | Extract reusable concepts (WFC schema, proc gen pipeline, data structures) | Low-Med |
| **Phase 2: ECS Porting** | 3–6 weeks | Rewrite architecture for DOTS/Systems + integrated Jobs | Medium-High |
| **Phase 3: Productionization** | 6+ weeks | Optimize, add features (weighted WFC, chunking), stabilize | High |

---

## Phase 1: Extraction (1–2 Weeks)

**Goal**: Create standalone, architecture-agnostic reusable modules without full MonoBehaviour dependency.

### Key Deliverables

#### 1.1 Height Field Generator (Pure Utility)
**File**: `Procedural/HeightFieldGenerator.cs`

```csharp
public static class HeightFieldGenerator
{
    public static float[,] GenerateHeightMap(HeightFieldConfig config)
    {
        // Seed Xorshift PRNG
        var prng = new Xorshift128((uint)config.Seed);
        
        // Perlin noise layers (use table-based or seeded function, NOT Mathf.PerlinNoise)
        var heights = new float[config.MapHeight, config.MapWidth];
        
        // ... generate with deterministic hash-based noise ...
        return heights;
    }
}
```

**Rationale**: No MonoBehaviour dependency. Can be directly moved to Jobs in Phase 2.

#### 1.2 WFC Constraint Solver (Pure Utility)
**File**: `Procedural/WaveFunctionCollapseSolver.cs`

Extract from `WaveFunctionCollapse.cs`:
- Decouple from `MapData` ScriptableObject
- Make structs serializable (remove GameObjects, references)
- Implement as static solver with injected config

```csharp
public static class WaveFunctionCollapseSolver
{
    public static WfcResult Solve(
        NativeArray<WfcModuleDefinition> modules,
        NativeArray<WfcConstraintRule> constraints,
        WfcRunConfig config)
    {
        // Pure constraint solving logic
        // No Unity engine calls except debugging
        var result = new WfcResult { /* ... */ };
        return result;
    }
}
```

**Rationale**: Solver is deterministic logic, easily portable to Burst jobs.

#### 1.3 Data Schema Definitions
**File**: `Data/WfcSchema.cs`

Define as pure C# structs:
```csharp
public struct WfcModuleDefinition { /* ... */ }
public struct WfcSocketDefinition { /* ... */ }
public struct WfcConstraintRule { /* ... */ }
public struct WfcRunConfig { /* ... */ }
public struct HeightFieldConfig { /* ... */ }
```

**Rationale**: Schemas must be serializable and DOTS-compatible.

#### 1.4 Terrain Mesh Compatibility Library
**File**: `Data/TerrainMeshRegistry.cs`

Pre-compute module definitions for your mesh library:

```csharp
[CreateAssetMenu(menuName = "Procedural/Terrain Mesh Registry")]
public class TerrainMeshRegistry : ScriptableObject
{
    [SerializeField] public WfcModuleDefinition[] Modules;
    [SerializeField] public WfcConstraintRule[] Constraints;
    
    // At edit time: Use TerrainDataEditor logic to derive sockets from mesh boundaries
    // At runtime: Load and use as-is
}
```

**Rationale**: Delegate mesh boundary analysis to editor tool (leverage existing `TerrainDataEditor.cs`).

#### 1.5 Deterministic PRNG Utility
**File**: `Util/Xorshift128.cs`

```csharp
public struct Xorshift128
{
    private uint x, y, z, w;
    
    public Xorshift128(uint seed) { /* ... */ }
    
    public int Range(int min, int max)
    {
        // ... true xorshift, NOT Mathf.Random
    }
    
    public float Range(float min, float max)
    {
        // ... deterministic float in range
    }
}
```

**Why**: Original code uses global `Random` (non-deterministic across domain reloads). Must replace.

#### 1.6 Identified Bugs (Fix Now)

**Bug 1: Bootstrap Dimension Swap**
- **File**: `MapGeneratorManager.cs` line ~X
- **Current**: `GameMapHeight = mapData.GameMapWidth`
- **Fix**: `GameMapHeight = mapData.GameMapHeight`

**Bug 2: WFC Restrict Guard**
- **File**: `GridManager.cs` or `MapSquareOptions.cs` line ~Y
- **Current**: `if (options[module]) { /* restrict */ }`
  - Checks *before* restriction; skips guard on valid modules
- **Fix**: `if (options[module] && options.Length > 1) { /* restrict */ }`
  - Check cardinality *after* restriction

**Bug 3: Missing Height Delta Validation**
- **Issue**: WFC doesn't validate height compatibility across tile boundaries
- **Fix**: Pre-filter module possibilities based on height map before WFC solve

### Phase 1 Deliverables Checklist

- [ ] Extract HEIGHT_FIELD_GENERATOR as pure utility
- [ ] Extract WFC_SOLVER as pure utility (no MonoBehaviour deps)
- [ ] Create WFC_SCHEMA.cs (all 5 data structures)
- [ ] Create TERRAIN_MESH_REGISTRY ScriptableObject + editor authoring tool
- [ ] Implement XORSHIFT128 deterministic PRNG
- [ ] Fix dimension swap bug in bootstrap
- [ ] Fix WFC restrict guard bug
- [ ] Add pre-filtering height delta check before WFC collapse
- [ ] Create unit tests for Phase 1 utilities
- [ ] Document parameter ranges (height scales, noise frequencies, WFC backtrack depth)

### Estimated Effort
- **Code writing**: 4–6 hours
- **Testing + bug fixes**: 3–4 hours
- **Documentation**: 2–3 hours
- **Total**: ~10 hours (1.5 days)

---

## Phase 2: ECS Porting (3–6 Weeks)

**Goal**: Migrate procedural generation from MonoBehaviour/singleton architecture to DOTS/Systems with integrated Burst jobs.

### Key Changes

#### 2.1 Create ECS Components (Replace Managers)

**Instead of**: `GameStateManager` singleton with callbacks

**Create**: Data components:

```csharp
public struct WorldGenerationState : IComponentData
{
    public int MapWidth;
    public int MapHeight;
    public int Seed;
    public WorldGenerationPhase Phase;       // Uninitialized, HeightField, WFC, Geometry, Features, Polish
}

public enum WorldGenerationPhase { Uninitialized, HeightField, WFC, Geometry, Features, Polish, Complete }

public struct TerrainHeightMap : IComponentData
{
    public NativeArray<float> Heights;
    public int Width;
    public int Height;
}

public struct WfcResult : IComponentData
{
    public NativeArray<int> TileModules;
    public int Width;
    public int Height;
}
```

#### 2.2 Create Generation Systems (Replace Monobehaviours)

**Phase 1 System** (Height Field):
```csharp
public partial class HeightFieldGenerationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // var query = SystemAPI.QueryBuilder().WithAll<WorldGenerationState>().Build();
        // var config = query.GetSingleton<WorldGenerationState>();
        
        // Job: Burst-compiled height field gen
        new HeightFieldGenerationJob { /* ... */ }.ScheduleParallel();
    }
}

[BurstCompile]
public partial struct HeightFieldGenerationJob : IJobEntity
{
    public void Execute(ref TerrainHeightMap heightMap, in WorldGenerationState state)
    {
        // Uses Xorshift128 PRNG (deterministic)
        var heights = HeightFieldGenerator.GenerateHeightMap(/* ... */);
    }
}
```

**Phase 2 System** (WFC Solve):
```csharp
public partial class WfcSolveSystem : SystemBase
{
    protected override void OnUpdate()
    {
        new WfcSolveJob { /* ... */ }.Schedule();
    }
}

[BurstCompile]
public partial struct WfcSolveJob : IJob
{
    public void Execute()
    {
        var result = WaveFunctionCollapseSolver.Solve(/* ... */);
    }
}
```

**Phase 3 System** (Geometry Realization):
```csharp
public partial class GeometryRealizationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Create tile entities from WFC result
        // Use EntityCommandBuffer (StructuralChanges not allowed in jobs)
        
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        new GeometryRealizationJob { CommandBuffer = ecb }.Schedule().Complete();
        ecb.Playback(EntityManager);
    }
}
```

#### 2.3 Rewrite Serialization (Replace SaveGameManager)

**Instead of**: JSON serialization of entire MonoBehaviour graph

**Create**: Component-based persistence:

```csharp
public struct TileData : IComponentData
{
    public int ModuleId;
    public float Height;
    public byte WearState;
}

public struct WorldSaveData
{
    public int Seed;
    public int MapWidth;
    public int MapHeight;
    public NativeArray<TileData> Tiles;    // Binary blob
}

public static class WorldSerializer
{
    public static void SaveWorld(ref SystemState state, WorldSaveData data)
    {
        // Binary serialization: Seed + dims + tile array
        using var file = File.OpenWrite("world.save");
        file.Write(BitConverter.GetBytes(data.Seed));
        // ... etc
    }
}
```

**Rationale**: No type metadata overhead; deterministic serialization.

#### 2.4 Adapt Spatial Components

**Instead of**: Per-tile GameObject + GridManager singleton

**Create**: Hierarchy:
```
Entity: WorldRoot
├─ Component: WorldGenerationState
├─ Component: TerrainHeightMap
├─ Component: WfcResult
└─ Child: TerrainChunk[0..N]
   └─ Component: ChunkTransform (position, rotation, scale)
   └─ Component: TerrainChunkData (meshId, materialIndex)
   └─ Component: Entities.Graphics rendering components
```

**Rendering**: Use Entities Graphics + GPU instancing instead of per-tile GameObjects.

#### 2.5 Integrate Existing Tools

**Keep**:
- Mesh assets (TerrainMeshesAutoGenerated)
- Artisan shaders (GrassShader, GroundWithWearShader)
- Animal/building prefabs (will convert to entity templates during Phase 2)

**Adapt**:
- TerrainDataEditor → mesh registry generation tool (editor only)
- TreeGenerator → job-based placement (Burst compiled)
- GrassInstancer → Entities Graphics compatible batching

### Phase 2 Deliverables Checklist

- [ ] Define ECS components (WorldGenerationState, TerrainHeightMap, WfcResult, etc.)
- [ ] Implement HeightFieldGenerationSystem + job
- [ ] Implement WfcSolveSystem + job
- [ ] Implement GeometryRealizationSystem + entity spawning
- [ ] Implement secondary feature placement systems (trees, rocks)
- [ ] Rewrite serialization as binary ComponentData export
- [ ] Convert terrain meshes to Entities Graphics compatible format
- [ ] Adapt grass instancing to Entities Graphics (GPU buffers)
- [ ] Remove MonoBehaviour singletons (GameStateManager, GridManager, SaveGameManager)
- [ ] Validate determinism (same seed → same world across domain reloads)
- [ ] Profile frame time per stage
- [ ] Document system order and dependencies

### Estimated Effort
- **Component design**: 1–2 weeks
- **System implementation + job ports**: 2–3 weeks
- **Serialization rewrite**: 1 week
- **Testing + debugging**: 1–2 weeks
- **Total**: 3–6 weeks

---

## Phase 3: Productionization (6+ Weeks)

**Goal**: Polish, optimize, add advanced features, and stabilize for production use.

### Key Features

#### 3.1 Weighted WFC Entropy
**Current limitation**: Uniform random collapse (all valid tiles equally likely)

**Upgrade**:
```csharp
public float SpawnWeight;         // Already in WfcModuleDefinition (Phase 1)

// In WFC solve job:
float totalWeight = 0;
for (int module in validModules)
    totalWeight += modules[module].SpawnWeight;

float pick = prng.Range(0, totalWeight);
for (int module in validModules)
{
    pick -= modules[module].SpawnWeight;
    if (pick <= 0) { chosenModule = module; break; }
}
```

**Benefit**: Control biome density, rarity, feature clustering.

#### 3.2 Deep Backtracking for WFC
**Current limitation**: Shallow backtracking (may fail on constrained maps)

**Upgrade**:
```csharp
// Use NativeQueue<WfcBacktrackPoint> for breadth-first state stack
// If collapse fails → pop most recent choice, try next valid option
// Tunable depth limit to prevent infinite loops
```

**Benefit**: Higher success rate on complex constraint networks.

#### 3.3 Chunked Mesh Generation
**Current limitation**: Per-tile GameObject (65K+ GameObjects for 256×256 map)

**Upgrade**:
```csharp
// Group 16×16 tiles per chunk
// Mesh.CombineMeshes() for each chunk
// Output: 256 chunk meshes instead of 65K tile GameObjects
```

**Benefit**: 99% memory reduction, enable larger worlds.

#### 3.4 Advanced Procedural Features
- **Biome-specific distributions** (forest, desert, tundra with distinct tile/feature patterns)
- **River/road generation** (post-WFC procedural carving)
- **Dungeon/cave networks** (separate constraint system, merged with terrain)
- **Landmark placement** (rare tile patterns, e.g., temples, stone circles)

#### 3.5 Performance Targets
- **256×256 height field**: <20ms
- **256×256 WFC solve**: <500ms (with weighted entropy + deep backtrack)
- **Geometry realization**: <300ms
- **Feature placement**: <200ms
- **Grass instancing**: <100ms
- **Total gen → playable**: ~2 seconds

#### 3.6 Advanced Determinism Testing
```csharp
[Test]
public void WorldSeedReproducesIdenticalLayout()
{
    // Generate world with seed=42
    var world1 = GenerateWorld(seed: 42);
    
    // Generate again with same seed
    var world2 = GenerateWorld(seed: 42);
    
    // Assert all tiles match
    Assert.That(world1.GetTileModules(), Is.EqualTo(world2.GetTileModules()));
}
```

**Rationale**: Critical for multiplayer, replays, streaming.

### Phase 3 Deliverables Checklist

- [ ] Implement weighted entropy collapse for WFC
- [ ] Implement deep backtracking + state queue
- [ ] Generate chunked meshes (16×16 grouping)
- [ ] Implement biome influence on tile distribution
- [ ] Add post-WFC river/road carving
- [ ] Benchmark: target <2s total generation for 256×256
- [ ] Establish determinism CI test suite
- [ ] Document parameter sweep ranges
- [ ] Add telemetry/diagnostics logging
- [ ] Create user-settable world config asset

### Estimated Effort
- **Weighted entropy + backtracking**: 2–3 weeks
- **Chunking + mesh optimization**: 2–3 weeks
- **Advanced features**: 2–4 weeks
- **Testing + optimization**: 2+ weeks
- **Total**: 6+ weeks

---

## Known Unknowns (Validation Gaps)

See [Tech-Analysis-For-16bitProcGen.md](../Tech-Analysis-For-16bitProcGen.md) Section 11 (Biggest Unknowns) for full list.

Key blockers for Phase 2:
1. Does current terrain mesh library scale to 50+ unique modules?
2. What's your target world size (256×256, 512×512, larger)?
3. Will NavMesh generation bottleneck on generation time?
4. Are prefabs properly socket-compatible for structure-level WFC?

---

## Relationship to Original Architecture

This migration intentionally **does not reuse** SmolbeanPlanet3D's MonoBehaviour-centric controllers:
- ❌ GameStateManager, BuildingController, AnimalController (domain-specific)
- ❌ SaveGameManager JSON layer (incompatible with ECS)
- ❌ Per-tile GameObject instantiation (performance bottleneck)

This migration **does reuse** algorithmic and data concepts:
- ✅ Height field Perlin noise pipeline
- ✅ Wave Function Collapse constraint solver + schema
- ✅ Mesh boundary compatibility tables (TerrainDataEditor tool)
- ✅ Wear/feedback texture system (adaptable to compute shaders)
- ✅ Instanced vegetation rendering approach

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| WFC fails to converge | Implement weighted entropy + deep backtrack; add height pre-filtering |
| Serialization bugs | Use binary format + determinism unit tests; no type metadata |
| Performance regression | Profile each system; use Burst + Jobs; benchmark against targets |
| Mesh compatibility issues | Create test suite for socket matching; visual debugging in editor |
| Multiplayer desync | Ensure deterministic PRNG + seed validation before sending |

---

**Reference**: [Tech-Analysis-For-16bitProcGen.md](../Tech-Analysis-For-16bitProcGen.md) Section 9 (Recommended 3-Phase Migration Plan)

**Generated**: April 9, 2026
