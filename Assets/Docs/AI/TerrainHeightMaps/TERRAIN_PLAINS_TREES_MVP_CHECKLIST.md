# Plains + Trees MVP Implementation Checklist
_Status: PHASE C IN PROGRESS — visual validation pending_
_Last updated: 2026-04-10_

## Implementation Progress

| Phase | Status | Notes |
|---|---|---|
| A — Plains terrain shape | ✅ COMPLETE | All 4 EditMode tests passing |
| B — Tree placement records | ✅ COMPLETE | All 4 EditMode + 2 PlayMode tests passing |
| C — Visual rendering | 🔄 IN PROGRESS | Systems wired, instancing fix applied, visual validation pending |

**Implementation notes (deviations from spec):**
- `TreePlacementAlgorithm.cs` added as a separate static class (not in original spec). Burst 1.8.x in Unity 6 treats all `public static` methods on a `[BurstCompile]` struct as C-ABI entry points (BC1064/BC1067), even without `[BurstCompile]` on the method. Moving the algorithm out of the ISystem struct fixes this while preserving Burst inlining from `OnUpdate`.
- `TreeChunkRenderSystem.OnUpdate` auto-enables `material.enableInstancing` at runtime to avoid a required manual Inspector step (Unity requires GPU instancing enabled on the material for `DrawMeshInstanced`).

**Pending before Phase C acceptance:**
- Visual confirmation: trees appear in Play Mode at terrain surface positions
- Delete diagnostic scripts: `TerrainHeightDebugGizmos.cs`, `TreePlacementDebugGizmos.cs`

---

## 1. Purpose

Provide a step-by-step implementation guide scoped strictly to:

- **Plains terrain shape** using layered noise in the active SDF pipeline
- **Sparse plains tree placement records** (deterministic, seam-safe)
- **Visual-only tree rendering** via instanced mesh draw calls

This document is the implementation companion to:
- `TERRAIN_BIOME_NOISE_SCHEMA.md` — component definitions
- `TERRAIN_BIOME_NOISE_SPEC.md` — biome behavior targets
- `TERRAIN_MVP_PRIORITY_NOTE.md` — scope priority rationale
- `TERRAIN_PLAINS_NOISE_ALGORITHM.md` — noise function, seed strategy, and all starting values

---

## 2. Scope

### In scope

- Replace the current sine-wave `SdGround` with a deterministic layered-noise ground function
- Introduce `TerrainGenerationContext` and `TerrainFieldSettings` singletons
- Wire world seed through the sampling pipeline
- Implement sparse plains tree placement per chunk with seam-safe determinism
- Render accepted tree placements as instanced meshes (visuals only)

### Out of scope for this MVP

- Moisture field evaluation
- Biome blending between archetypes
- Ruggedness field (declare but leave zero)
- `TerrainChunkBiomeState` and `TerrainChunkGenerationStamp`
- `TerrainBiomeLookupSettings` and `TerrainBiomeRuleTable`
- Grass and fine ground cover
- Multiple biome archetypes beyond plains
- Tree interaction, harvesting, or damage
- Near-player interactive tree entity promotion
- Tree persistence and divergence tracking
- Per-biome tree rule schema (deferred — design post-MVP once visuals are working)
- Far-distance tree impostor rendering

---

## 3. Pre-conditions

Verify these exist before starting:

| Asset | Path | Status |
|---|---|---|
| `SDFMath` static class | `Assets/Scripts/DOTS/Terrain/SDF/SDFMath.cs` | exists |
| `SDFTerrainField` struct | `Assets/Scripts/DOTS/Terrain/SDF/SDFTerrainField.cs` | exists |
| `SDFTerrainFieldSettings` component | `Assets/Scripts/DOTS/Terrain/SDF/SDFTerrainFieldSettings.cs` | exists |
| `TerrainChunkDensitySamplingSystem` | `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs` | exists |
| `TerrainBootstrapAuthoring` MonoBehaviour | `Assets/Scripts/DOTS/Terrain/Bootstrap/TerrainBootstrapAuthoring.cs` | exists |
| `DotsSystemBootstrap` MonoBehaviour | `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` | exists |
| `ProjectFeatureConfig` ScriptableObject | `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` | exists |

---

## 4. Validation Approach

This checklist uses two complementary validation methods. Both are required — tests alone cannot catch visual artifacts, and visual inspection alone cannot assert determinism or seam correctness.

### 4.1 Unity MCP Test Runner

Use the `unity-mcp-skill` to execute EditMode and PlayMode tests from the CLI after each phase.

Workflow:
1. Implement the steps in a phase.
2. Run the phase's tests via Unity MCP test runner.
3. Read the results before proceeding. Do not start the next phase until all tests in the current phase pass.
4. If a test fails, fix the root cause — do not skip or comment out the failing assertion.

### 4.2 Throw-Away Diagnostic Scripts

Each phase includes a short-lived diagnostic MonoBehaviour. These are Scene-view gizmo drawers that let you observe terrain shape and tree placement visually without running the full pipeline.

Rules for diagnostic scripts:
- Create them as described in the step. Attach to any empty GameObject in the active scene.
- Use them to validate that the feature looks correct before running formal tests.
- Delete them (or move to `Archives/Diagnostics/`) once the phase is accepted.
- Do not commit diagnostic scripts to the main codebase.

### 4.3 Tuning Loop (Phase A specific)

The starting values in `TERRAIN_PLAINS_NOISE_ALGORITHM.md` are calibrated starting points, not final values. Expect one or two tuning passes:

```
implement → run diagnostic → observe terrain shape
  → adjust ElevationLowAmplitude or ElevationExponent in inspector
  → observe again → run tests → if flatness assertion fails, tighten amplitude
  → accept and move to Phase B
```

### 4.4 Inline Code Documentation

The terrain noise implementation contains non-obvious math. The following code requires inline comments even under the project's general "only comment non-obvious logic" rule:

- `SDFMath.SdLayeredGround` — comment the redistribution formula explaining what `sign(x) * pow(abs(x), exp)` does to the elevation distribution and reference `TERRAIN_PLAINS_NOISE_ALGORITHM.md §4`
- `SDFMath.SeedLayerOffset` — comment the hash multiplier choices and why the 500-unit scale prevents cross-seed correlation
- `TerrainFieldSettings.ElevationExponent` — XML summary comment stating `> 1 flattens plains, < 1 sharpens peaks, 1.0 = no redistribution`
- `TreePlacementGenerationSystem` candidate loop — comment the probability noise layer index reservation (layer 3 reserved for trees, never reuse for terrain)

All other code in this implementation follows the standard rule: no comments unless the logic is non-obvious.

---

## 5. Phase A — Plains Terrain Shape

Implement and validate all Phase A steps before beginning Phase B.

---

### Step A1 — Add `TerrainGenerationContext` component

**File to create:** `Assets/Scripts/DOTS/Terrain/SDF/TerrainGenerationContext.cs`

```csharp
using Unity.Entities;

namespace DOTS.Terrain
{
    public struct TerrainGenerationContext : IComponentData
    {
        public uint WorldSeed;
        public uint GenerationVersion;
        public float GlobalHeightOffset;
    }
}
```

Notes:
- `WorldSeed` must flow through to all sampling jobs for determinism.
- `GenerationVersion` allows intentional algorithm changes without ambiguity. Set to `1` initially.
- `GlobalHeightOffset` replaces the old semantic role of `NoiseValue` as a constant vertical bias.

---

### Step A2 — Add `TerrainFieldSettings` component (plains subset)

**File to create:** `Assets/Scripts/DOTS/Terrain/SDF/TerrainFieldSettings.cs`

```csharp
using Unity.Entities;

namespace DOTS.Terrain
{
    public struct TerrainFieldSettings : IComponentData
    {
        public float BaseHeight;

        // Elevation layers — all three used for plains
        public float ElevationLowFrequency;
        public float ElevationLowAmplitude;
        public float ElevationMidFrequency;
        public float ElevationMidAmplitude;
        public float ElevationHighFrequency;
        public float ElevationHighAmplitude;

        /// <summary>
        /// Redistribution exponent applied to the combined elevation signal.
        /// Greater than 1 flattens plains and widens valleys.
        /// Less than 1 sharpens peaks for mountains.
        /// 1.0 applies no redistribution.
        /// </summary>
        public float ElevationExponent;

        // Moisture and ruggedness — declared for schema compatibility, unused in plains MVP
        public float MoistureFrequency;
        public float MoistureAmplitude;
        public float RuggednessFrequency;
        public float RuggednessAmplitude;
    }
}
```

Plains starting values (from `TERRAIN_PLAINS_NOISE_ALGORITHM.md` §5):

```csharp
new TerrainFieldSettings
{
    BaseHeight             = 0f,
    ElevationLowFrequency  = 0.004f,
    ElevationLowAmplitude  = 5.0f,
    ElevationMidFrequency  = 0.018f,
    ElevationMidAmplitude  = 1.2f,
    ElevationHighFrequency = 0.07f,
    ElevationHighAmplitude = 0.25f,
    ElevationExponent      = 1.6f,
}
```

---

### Step A3 — Add `SdLayeredGround` to `SDFMath`

**File to modify:** `Assets/Scripts/DOTS/Terrain/SDF/SDFMath.cs`

Copy the complete implementation from `TERRAIN_PLAINS_NOISE_ALGORITHM.md` §3 and §4:

- Add `using static Unity.Mathematics.noise;` to the file-level usings.
- Add the private `SeedLayerOffset(uint seed, uint layer)` helper with the hash comment described in §4.4 above.
- Add the public `SdLayeredGround(float3 worldPos, in TerrainFieldSettings settings, uint seed)` method with the redistribution comment described in §4.4 above.

Do NOT remove `SdGround` — it is still used by `SDFTerrainField` during Stage 1 migration.

All sampling in `SdLayeredGround` must be in world space. Never reference chunk origin or restart at a local boundary.

---

### Step A4 — Update `SDFTerrainField` to consume new settings

**File to modify:** `Assets/Scripts/DOTS/Terrain/SDF/SDFTerrainField.cs`

```csharp
public struct SDFTerrainField
{
    // Legacy fields — keep during Stage 1 migration, remove in Stage 3
    public float BaseHeight;
    public float Amplitude;
    public float Frequency;
    public float NoiseValue;

    // New fields — populated when TerrainFieldSettings singleton is available
    public bool UseLayeredNoise;
    public uint WorldSeed;
    public TerrainFieldSettings LayeredSettings;

    public float Sample(float3 worldPos, NativeArray<SDFEdit> edits)
    {
        var density = UseLayeredNoise
            ? SDFMath.SdLayeredGround(worldPos, in LayeredSettings, WorldSeed)
            : SDFMath.SdGround(worldPos, Amplitude, Frequency, BaseHeight, NoiseValue);

        if (!edits.IsCreated || edits.Length == 0) return density;
        for (var i = 0; i < edits.Length; i++)
        {
            var edit = edits[i];
            var editDistance = ComputeEditDistance(worldPos, in edit);
            density = edit.Operation == SDFEditOperation.Subtract
                ? SDFMath.OpSubtraction(density, editDistance)
                : SDFMath.OpUnion(density, editDistance);
        }
        return density;
    }
    // ... ComputeEditDistance unchanged
}
```

---

### Step A5 — Update `TerrainBootstrapAuthoring` to create new singletons

**File to modify:** `Assets/Scripts/DOTS/Terrain/Bootstrap/TerrainBootstrapAuthoring.cs`

Add inspector fields:

```csharp
[Header("World Generation")]
[SerializeField] private uint worldSeed = 12345u;
[SerializeField] private uint generationVersion = 1u;

[Header("Elevation Layers")]
[SerializeField] private float elevationLowFrequency  = 0.004f;
[SerializeField] private float elevationLowAmplitude  = 5.0f;
[SerializeField] private float elevationMidFrequency  = 0.018f;
[SerializeField] private float elevationMidAmplitude  = 1.2f;
[SerializeField] private float elevationHighFrequency = 0.07f;
[SerializeField] private float elevationHighAmplitude = 0.25f;
[SerializeField] private float elevationExponent      = 1.6f;
```

In `EnsureFieldSettings()`, after the existing `SDFTerrainFieldSettings` block, add:

```csharp
var contextQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainGenerationContext>());
if (contextQuery.CalculateEntityCount() == 0)
{
    var entity = entityManager.CreateEntity(typeof(TerrainGenerationContext));
    entityManager.SetComponentData(entity, new TerrainGenerationContext
    {
        WorldSeed = worldSeed,
        GenerationVersion = generationVersion,
        GlobalHeightOffset = 0f
    });
}
contextQuery.Dispose();

var fieldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainFieldSettings>());
if (fieldQuery.CalculateEntityCount() == 0)
{
    var entity = entityManager.CreateEntity(typeof(TerrainFieldSettings));
    entityManager.SetComponentData(entity, new TerrainFieldSettings
    {
        BaseHeight             = baseHeight,
        ElevationLowFrequency  = elevationLowFrequency,
        ElevationLowAmplitude  = elevationLowAmplitude,
        ElevationMidFrequency  = elevationMidFrequency,
        ElevationMidAmplitude  = elevationMidAmplitude,
        ElevationHighFrequency = elevationHighFrequency,
        ElevationHighAmplitude = elevationHighAmplitude,
        ElevationExponent      = elevationExponent,
    });
}
fieldQuery.Dispose();
```

---

### Step A6 — Update `TerrainChunkDensitySamplingSystem` to read new singletons

**File to modify:** `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs`

After the existing `SDFTerrainFieldSettings` guard (line 32):

```csharp
var useLayeredNoise = false;
TerrainFieldSettings layeredSettings = default;
uint worldSeed = 0;

if (SystemAPI.HasSingleton<TerrainFieldSettings>() && SystemAPI.HasSingleton<TerrainGenerationContext>())
{
    layeredSettings = SystemAPI.GetSingleton<TerrainFieldSettings>();
    worldSeed = SystemAPI.GetSingleton<TerrainGenerationContext>().WorldSeed;
    useLayeredNoise = true;
}
```

Update `SDFTerrainField` construction (lines 44–50):

```csharp
var field = new SDFTerrainField
{
    BaseHeight      = settings.BaseHeight,
    Amplitude       = settings.Amplitude,
    Frequency       = settings.Frequency,
    NoiseValue      = settings.NoiseValue,
    UseLayeredNoise = useLayeredNoise,
    WorldSeed       = worldSeed,
    LayeredSettings = layeredSettings
};
```

No other changes to this system.

---

### Step A7 — Phase A diagnostic script (throw-away)

**File to create (throw-away):** `Assets/Scripts/DOTS/Terrain/SDF/TerrainHeightDebugGizmos.cs`

Attach to any empty GameObject in the active scene. Delete after Phase A is accepted.

```csharp
#if UNITY_EDITOR
using DOTS.Terrain;
using Unity.Mathematics;
using UnityEngine;

/// Throw-away Phase A diagnostic. Draws height-coloured spheres in the Scene view.
/// DELETE after Phase A is accepted.
[ExecuteAlways]
public class TerrainHeightDebugGizmos : MonoBehaviour
{
    [Header("Sampling Grid")]
    public int   GridSize    = 20;
    public float GridSpacing = 3f;
    public float BaseHeight  = 0f;

    [Header("Noise Settings — match TerrainBootstrapAuthoring inspector")]
    public uint  WorldSeed              = 12345u;
    public float ElevationLowFrequency  = 0.004f;
    public float ElevationLowAmplitude  = 5.0f;
    public float ElevationMidFrequency  = 0.018f;
    public float ElevationMidAmplitude  = 1.2f;
    public float ElevationHighFrequency = 0.07f;
    public float ElevationHighAmplitude = 0.25f;
    public float ElevationExponent      = 1.6f;

    private void OnDrawGizmos()
    {
        var settings = new TerrainFieldSettings
        {
            BaseHeight             = BaseHeight,
            ElevationLowFrequency  = ElevationLowFrequency,
            ElevationLowAmplitude  = ElevationLowAmplitude,
            ElevationMidFrequency  = ElevationMidFrequency,
            ElevationMidAmplitude  = ElevationMidAmplitude,
            ElevationHighFrequency = ElevationHighFrequency,
            ElevationHighAmplitude = ElevationHighAmplitude,
            ElevationExponent      = ElevationExponent,
        };

        float maxAmp = ElevationLowAmplitude + ElevationMidAmplitude + ElevationHighAmplitude;
        var origin = (float3)transform.position;

        for (int z = 0; z < GridSize; z++)
        for (int x = 0; x < GridSize; x++)
        {
            var worldXZ = origin + new float3(x * GridSpacing, 0f, z * GridSpacing);
            var height = SampleHeight(worldXZ.x, worldXZ.z, settings);

            float t = math.saturate((height - BaseHeight + maxAmp) / (2f * maxAmp));
            Gizmos.color = Color.Lerp(Color.blue, Color.Lerp(Color.green, Color.white, t), t);
            Gizmos.DrawSphere(new Vector3(worldXZ.x, height, worldXZ.z), 0.3f);
        }
    }

    private float SampleHeight(float x, float z, TerrainFieldSettings s)
    {
        float yLow = s.BaseHeight - 20f, yHigh = s.BaseHeight + 20f;
        for (int i = 0; i < 16; i++)
        {
            float yMid = (yLow + yHigh) * 0.5f;
            if (SDFMath.SdLayeredGround(new float3(x, yMid, z), s, WorldSeed) < 0f)
                yLow = yMid;
            else
                yHigh = yMid;
        }
        return (yLow + yHigh) * 0.5f;
    }
}
#endif
```

**What to look for:**
- Wide flat green bands with gradual blue/white transitions → plains character confirmed
- Rapid colour alternation → amplitude too high, reduce `ElevationLowAmplitude`
- Uniform single colour → amplitude too low
- Visible axis-aligned stripes → `SeedLayerOffset` offsets may be identical across layers

---

### Step A8 — Run Phase A tests via Unity MCP

**File to create:** `Assets/Scripts/DOTS/Tests/Automated/TerrainLayeredNoiseTests.cs`

Required EditMode tests:

1. **Determinism** — same seed + same world-space position → same `SdLayeredGround` result.
2. **Seam continuity** — same world-space position sampled from two different chunk origins returns values within `1e-4f`.
3. **Plains flatness** — standard deviation of 100 sampled heights across a 45×45-unit area < `3.5f`. (See `TERRAIN_PLAINS_NOISE_ALGORITHM.md` §7.)
4. **Legacy fallback** — `UseLayeredNoise = false` produces the same result as unmodified `SdGround`.

**Execute via Unity MCP, then tune if needed:**

| Failure | Likely cause | Fix |
|---|---|---|
| Determinism | Seed offset not applied or layers share same offset | Check `SeedLayerOffset` uses distinct multipliers per layer |
| Seam continuity | Chunk-local offset creeping in | Verify `SdLayeredGround` uses only `worldPos`, never `worldPos - chunkOrigin` |
| Flatness | `ElevationLowAmplitude` too high | Reduce toward 3.5f, re-run diagnostic, then re-run tests |
| Legacy fallback | `SdGround` signature changed | Check `SDFMath.SdGround` is unmodified |

Do not proceed to Phase B until all four pass.

---

## 6. Phase B — Plains Tree Placement

Begin Phase B only after all Phase A tests pass and the Step A7 diagnostic looks correct.

---

### Step B1 — Define tree placement data structures

**File to create:** `Assets/Scripts/DOTS/Terrain/Trees/TreePlacementRecord.cs`

```csharp
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// One accepted tree site on a terrain chunk. Only valid placements are written —
    /// rejected candidates are discarded, not stored.
    /// </summary>
    public struct TreePlacementRecord : IBufferElementData
    {
        public float3 WorldPosition;
        public float  GroundNormalY;  // dot(surface normal, up) — retained for visual tilt later
        public byte   TreeTypeId;     // 0 = generic plains tree (MVP)
    }
}
```

**File to create:** `Assets/Scripts/DOTS/Terrain/Trees/ChunkTreePlacementTag.cs`

```csharp
using Unity.Entities;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Present on a chunk whose tree placement records are current.
    /// Remove to trigger regeneration.
    /// </summary>
    public struct ChunkTreePlacementTag : IComponentData
    {
        public uint GenerationVersion;
    }
}
```

---

### Step B2 — Create `TreePlacementGenerationSystem`

**File to create:** `Assets/Scripts/DOTS/Terrain/Trees/TreePlacementGenerationSystem.cs`

Placement parameters (from `TERRAIN_PLAINS_NOISE_ALGORITHM.md` §6):

```csharp
const float MinTreeSpacing        = 5.0f;
const float CellJitterRadius      = 1.5f;
const float PlainsSlopeMinNormalY = 0.85f;
const float PlainsProbability     = 0.35f;
```

Algorithm per chunk:

```
1. Generate 3×3 jittered candidate grid in world space
   - Cell size = MinTreeSpacing
   - Jitter from CandidateJitter(worldSeed, chunkCoord, cellX, cellZ) — see algorithm doc §6

2. For each candidate:
   a. Binary-search chunk density blob along Y to find surface height
   b. Reject if no solid surface found
   c. Compute surface normal from central-difference on blob values
   d. Reject if GroundNormalY < PlainsSlopeMinNormalY
   e. Sample probability noise using snoise at layer-3 seed offset
      // Layer index 3 is reserved for tree probability — never reuse for terrain layers 0–2
      Reject if normalised probability > PlainsProbability
   f. Reject if closer than MinTreeSpacing to any already-accepted candidate

3. Write accepted candidates as TreePlacementRecord buffer entries
4. Add ChunkTreePlacementTag { GenerationVersion }
```

System framework:

```csharp
using Unity.Burst;
using Unity.Entities;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Trees
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct TreePlacementGenerationSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainGenerationContext>();
            state.RequireForUpdate<TerrainFieldSettings>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Implement algorithm above
        }
    }
}
```

Notes:
- All candidate generation must use world-space coordinates — never chunk-local restarts.
- Blob access: `blob.Value.Values` with `TerrainChunkDensityGridInfo.Resolution` and `VoxelSize`.
- Use `EntityCommandBuffer` from `EndSimulationEntityCommandBufferSystem.Singleton`.

---

### Step B3 — Register in bootstrap

**File to modify:** `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`

Add `EnableTreePlacementSystem` bool to `ProjectFeatureConfig`. Then inside `if (config.EnableTerrainSystem)`:

```csharp
if (config.EnableTreePlacementSystem)
{
    var handle = world.CreateSystem<TreePlacementGenerationSystem>();
    simGroup.AddSystemToUpdateList(handle);
    DebugSettings.Log("Bootstrap: TreePlacementGenerationSystem enabled.");
}
```

---

### Step B4 — Phase B diagnostic script (throw-away)

**File to create (throw-away):** `Assets/Scripts/DOTS/Terrain/Trees/TreePlacementDebugGizmos.cs`

```csharp
#if UNITY_EDITOR
using DOTS.Terrain.Trees;
using Unity.Entities;
using UnityEngine;

/// Throw-away Phase B diagnostic. Draws spheres at accepted tree positions.
/// DELETE after Phase B is accepted.
[ExecuteAlways]
public class TreePlacementDebugGizmos : MonoBehaviour
{
    public float SphereRadius = 0.4f;

    private void OnDrawGizmos()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>(),
            ComponentType.ReadOnly<TreePlacementRecord>());

        Gizmos.color = Color.green;
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            var buffer = em.GetBuffer<TreePlacementRecord>(entity, true);
            foreach (var record in buffer)
                Gizmos.DrawSphere(record.WorldPosition, SphereRadius);
        }
        query.Dispose();
    }
}
#endif
```

**What to look for:**
- Sparse irregular green spheres on flat terrain → plains density confirmed
- No spheres → system not running; check `EnableTreePlacementSystem` and bootstrap
- Spheres clustered on chunk edges → world-space jitter not applied correctly
- Spheres on steep slopes → slope filter not working; check `GroundNormalY` threshold
- Dense carpet of spheres → `PlainsProbability` too high, reduce from 0.35f

---

### Step B5 — Run Phase B tests via Unity MCP

**File to create:** `Assets/Scripts/DOTS/Tests/Automated/TreePlacementTests.cs`

Required EditMode tests:

1. **Determinism** — same seed + same chunk coord → identical `TreePlacementRecord` list.
2. **Slope filter** — candidate with `GroundNormalY = 1.0f` accepted; candidate with `GroundNormalY = 0.5f` rejected.
3. **Plains sparsity** — accepted count per chunk in range `[0, 6]`.
4. **Spacing** — no two accepted candidates closer than `4.99f` world units.

Required PlayMode tests:

5. **Stream stability** — stream out / stream in produces identical records.
6. **No seam duplicates** — no two records from adjacent chunks within `0.01f` of each other.

**Execute via Unity MCP:**

| Failure | Likely cause | Fix |
|---|---|---|
| Determinism | Chunk-local random used | All candidate inputs must include world-space position and chunkCoord |
| Sparsity exceeded | `PlainsProbability` too high | Reduce from 0.35f |
| Spacing violation | Min-distance check missing or threshold wrong | Check inner loop uses `MinTreeSpacing` |
| Seam duplicates | Cell jitter seeded from local cell index only | Include `chunkCoord` in `CandidateJitter` hash |

---

## 7. Phase C — Visual Tree Rendering

Begin Phase C only after all Phase B tests pass and the Step B4 diagnostic shows correct placement.

---

### Step C1 — `TreeRenderConfig` managed component

**File to create:** `Assets/Scripts/DOTS/Terrain/Trees/TreeRenderConfig.cs`

Mesh and Material are managed types and cannot live in a Burst-compatible `IComponentData` struct. Use a managed component (class):

```csharp
using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Singleton managed component holding the mesh and material used to render all
    /// MVP plains trees via instanced draw calls. Not Burst-compatible by design —
    /// rendering is handled in a managed SystemBase.
    /// </summary>
    public class TreeRenderConfig : IComponentData
    {
        public Mesh     Mesh;
        public Material Material;
        public float    UniformScale;   // applied to all instances — tune for visual size
    }
}
```

---

### Step C2 — `TreeVisualBootstrap` MonoBehaviour

**File to create:** `Assets/Scripts/DOTS/Terrain/Trees/TreeVisualBootstrap.cs`

Follows the existing `TerrainBootstrapAuthoring` pattern.

```csharp
using DOTS.Terrain.Trees;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Bootstrap
{
    public class TreeVisualBootstrap : MonoBehaviour
    {
        [SerializeField] private Mesh     treeMesh;
        [SerializeField] private Material treeMaterial;
        [SerializeField] private float    treeScale = 1f;

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;
            var entity = em.CreateEntity();
            em.AddComponentObject(entity, new TreeRenderConfig
            {
                Mesh         = treeMesh,
                Material     = treeMaterial,
                UniformScale = treeScale,
            });
        }
    }
}
```

Assign a placeholder mesh (Unity capsule is fine for MVP) and an unlit material in the Inspector. Place this MonoBehaviour in the same scene as `TerrainBootstrapAuthoring`.

---

### Step C3 — `TreeChunkRenderSystem`

**File to create:** `Assets/Scripts/DOTS/Terrain/Trees/TreeChunkRenderSystem.cs`

Uses `Graphics.DrawMeshInstanced` — a managed Unity API — so this system must be `SystemBase`, not `ISystem`.

```csharp
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Terrain.Trees
{
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TreeChunkRenderSystem : SystemBase
    {
        private static readonly Matrix4x4[] _instanceBuffer = new Matrix4x4[1023];

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<TreeRenderConfig>(out var config)) return;
            if (config.Mesh == null || config.Material == null) return;

            var batchCount = 0;

            Entities
                .WithAll<TerrainChunk>()
                .ForEach((DynamicBuffer<TreePlacementRecord> records) =>
                {
                    foreach (var record in records)
                    {
                        _instanceBuffer[batchCount++] = Matrix4x4.TRS(
                            record.WorldPosition,
                            Quaternion.identity,
                            Vector3.one * config.UniformScale);

                        if (batchCount == 1023)
                        {
                            Graphics.DrawMeshInstanced(config.Mesh, 0, config.Material, _instanceBuffer, batchCount);
                            batchCount = 0;
                        }
                    }
                }).WithoutBurst().Run();

            if (batchCount > 0)
                Graphics.DrawMeshInstanced(config.Mesh, 0, config.Material, _instanceBuffer, batchCount);
        }
    }
}
```

Notes:
- `Graphics.DrawMeshInstanced` max batch size is 1023 — the flush-at-1023 pattern handles any chunk count.
- `WithoutBurst()` is required because `DynamicBuffer` iteration with managed API calls cannot be Burst-compiled.
- This is an intentional MVP simplification. Replace with entities.graphics `RenderMeshArray` approach post-MVP if performance requires it.

---

### Step C4 — Register in bootstrap

**File to modify:** `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`

Add `EnableTreeRenderSystem` bool to `ProjectFeatureConfig`. Then inside `if (config.EnableTerrainSystem)`:

```csharp
if (config.EnableTreeRenderSystem)
{
    var handle = world.CreateSystem<TreeChunkRenderSystem>();
    simGroup.AddSystemToUpdateList(handle);
    DebugSettings.Log("Bootstrap: TreeChunkRenderSystem enabled.");
}
```

Note: `TreeChunkRenderSystem` updates in `PresentationSystemGroup`, not `SimulationSystemGroup`. Make sure to get the correct group handle:

```csharp
var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
var handle = world.CreateSystem<TreeChunkRenderSystem>();
presentationGroup.AddSystemToUpdateList(handle);
```

---

### Step C5 — Visual validation in Play Mode

No automated test is required for Phase C — visual correctness is validated by running the scene and observing:

- Trees appear at positions matching the Phase B diagnostic gizmos
- No trees floating above terrain or clipping below ground
- Scale and density look appropriate for plains at gameplay camera distance
- Frame rate is not significantly impacted (instanced draw calls are cheap at plains density)

**If trees are floating or underground:**
The surface-snap height from the placement system is offset. Check that `TreePlacementGenerationSystem` resolves surface height from the density blob, not from the raw world-space Y of the candidate.

**If no trees appear:**
- Confirm `TreeVisualBootstrap` is in the scene with a mesh and material assigned
- Confirm `EnableTreeRenderSystem` is enabled in `ProjectFeatureConfig`
- Confirm `TreeChunkRenderSystem` is added to `PresentationSystemGroup`, not `SimulationSystemGroup`

---

## 8. Acceptance Criteria

**Phase A complete when:**
- All Step A8 tests pass via Unity MCP
- Step A7 diagnostic shows wide flat terrain with gentle visible rolls
- No chunk seam discontinuities visible in diagnostic

**Phase B complete when:**
- All Step B5 tests pass via Unity MCP
- Step B4 diagnostic shows sparse, irregularly spaced green spheres
- No spheres on steep slopes

**Phase C complete when:**
- Trees visible in Play Mode at positions matching Phase B diagnostic
- No floating or underground trees
- `TerrainHeightDebugGizmos` and `TreePlacementDebugGizmos` deleted from project

---

## 9. Migration Clean-up (after MVP acceptance)

- Remove `SdGround` from `SDFMath.cs` and all call sites
- Remove legacy `Amplitude`, `Frequency`, `NoiseValue` fields from `SDFTerrainField`
- Remove `SDFTerrainFieldSettings` and all references
- Remove the `UseLayeredNoise` flag from `SDFTerrainField`
- Delete diagnostic scripts if not already removed
- Consider replacing `Graphics.DrawMeshInstanced` in `TreeChunkRenderSystem` with entities.graphics instancing if tree counts grow

Do not begin clean-up while the MVP checklist is still in progress.
