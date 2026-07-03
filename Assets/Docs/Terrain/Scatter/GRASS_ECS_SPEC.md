# GPU-Instanced Grass System — ECS Spec

**Status:** Planning — post-MVP / deferred behind core terrain and trees
**Replaces:** BruteForce GrassShader POC (`GrassChunkRenderSystem` + `TerrainChunkGrassMaterial`)
**Design Goal:** Highly configurable, edit-reactive grass rendered via `DrawMeshInstancedIndirect`, with multi-biome support staged after MVP.

**Priority Decision:** Grass is not part of the core terrain MVP. Core terrain and tree placement come first.

**When grass work starts:** first implementation should be one biome only (`Plains`, `BiomeTypeId = 0`). Multi-biome behavior is deferred.

---

## 1. Goals

| Goal | Notes |
|------|-------|
| Configurable density | 0 (bare rock) → 1 (dense meadow), per chunk |
| Biome color/style | MVP: Plains-only profile. Future: ScriptableObject settings per biome type |
| Edit reactivity | Grass removed wherever terrain SDF is modified |
| ECS-native | ISystem, no MonoBehaviour rendering logic |
| No geometry shader | Vertex shader only → GPU instancing viable |
| Sparse clumps | Deferred; defined as a separate grass type / shader variant — not in this phase |

This spec should be read as a deferred implementation design, not as the current terrain MVP priority.

---

## 2. Architecture Overview

```
TerrainChunkGrassSurface (IComponentData)
    Density, BiomeType, GrassType → drives blade generation

GrassChunkDirty (IComponentData, tag)
    Added when: chunk first tagged, terrain edited, settings change

GrassChunkBladeBuffer (managed IComponentData)
    Holds: GraphicsBuffer (blade instance data), args buffer, blade count

GrassChunkGenerationSystem (ISystem, SimulationSystemGroup)
    Reacts to: GrassChunkDirty
    Reads:  TerrainChunkMeshData.Mesh.Vertices/Indices + Density + BiomeType
    Writes: GrassChunkBladeBuffer via Burst job → NativeArray → GraphicsBuffer

GrassChunkRenderSystem (ISystem, PresentationSystemGroup)  ← replaces current
    Reads:  GrassChunkBladeBuffer, LocalToWorld
    Issues: Graphics.DrawMeshInstancedIndirect per chunk (1 draw call per chunk,
            thousands of blades per call — vs. geometry shader per-triangle cost)

GrassBiomeSettings (ScriptableObject)
    MVP: one Plains profile. Future: per-biome array

GrassSystemSettings (ScriptableObject singleton)
    Global: max blades per chunk, blade mesh ref, base material, fade distances
```

---

## 3. Data Model

### 3.1 `TerrainChunkGrassSurface` (extended from POC)

```csharp
namespace DOTS.Terrain.Rendering
{
    public struct TerrainChunkGrassSurface : IComponentData
    {
        public float   Density;       // 0..1. 0 = no grass, 1 = full density
        public int     BiomeTypeId;   // MVP: fixed to Plains (0). Future: index into GrassBiomeSettings[]
        public byte    GrassType;     // 0 = standard blades (this spec), 1 = sparse clumps (future)
        public bool    IsDirty;       // Set true to request buffer rebuild (avoids extra tag component)
    }
}
```

> **Note:** `GrassType == 1` (sparse clumps) is reserved. The generation and render systems skip
> any chunk with `GrassType != 0` until that variant is implemented.

### 3.2 `GrassBladeData` (shader-side struct, mirrored in C#)

```csharp
// Must match layout in GrassBlades.hlsl GrassBladeData struct
[StructLayout(LayoutKind.Sequential)]
public struct GrassBladeData
{
    public float3 WorldPosition;  // blade root in world space
    public float  Height;         // world-unit height (density + noise driven)
    public float3 ColorTint;      // biome base colour × per-blade noise
    public float  FacingAngle;    // random Y rotation (0..2π)
}
// Stride: 32 bytes
```

### 3.3 `GrassChunkBladeBuffer` (managed component)

```csharp
public class GrassChunkBladeBuffer : IComponentData, IDisposable
{
    public GraphicsBuffer BladeBuffer;   // structured, stride 32, one entry per blade
    public GraphicsBuffer ArgsBuffer;    // indirect draw args (5 × uint)
    public int            BladeCount;

    public void Dispose()
    {
        BladeBuffer?.Dispose();
        ArgsBuffer?.Dispose();
    }
}
```

Managed components are stored outside the chunk memory so `GraphicsBuffer` (a GPU resource)
does not need to fit in a blittable IComponentData.

### 3.4 `GrassSystemSettings` (ScriptableObject singleton)

```
Assets/Resources/GrassSystemSettings.asset
```

```csharp
public class GrassSystemSettings : ScriptableObject
{
    [Header("Blade mesh & material")]
    public Mesh       BladeMesh;        // Simple 3-quad cross blade (~12 triangles)
    public Material   BaseMaterial;     // GrassBlade.shader instance

    [Header("Density")]
    public int        MaxBladesPerChunk = 4096;
    public float      BladesPerSqMeter  = 8f;   // multiplied by chunk area × Density

    [Header("Fade")]
    public float      FadeStartDistance = 60f;
    public float      FadeEndDistance   = 120f;

    [Header("Biomes")]
    public GrassBiomeSettings[] Biomes; // indexed by TerrainChunkGrassSurface.BiomeTypeId
}
```

### 3.5 `GrassBiomeSettings` (ScriptableObject, one per biome)

```csharp
public class GrassBiomeSettings : ScriptableObject
{
    public Color  BaseColor          = new Color(0.45f, 0.75f, 0.25f);
    public float  DensityMultiplier  = 1f;     // multiplied with chunk Density
    public float  MinBladeHeight     = 0.15f;  // world units
    public float  MaxBladeHeight     = 0.45f;
    public float  WindStrength       = 0.3f;   // passed to shader via MaterialPropertyBlock
    public float  ColorNoiseScale    = 0.15f;  // per-blade random tint variation ±amount
}
```

---

## 4. Custom Shader — `GrassBlades.shader`

**Location:** `Assets/Resources/Shaders/GrassBlades.shader`

### 4.1 Key design decisions

| Decision | Rationale |
|----------|-----------|
| No geometry shader | Enables GPU instancing; SRP Batcher not required but instancing works |
| `DrawMeshInstancedIndirect` | One call per chunk; blade count determined at buffer-fill time |
| Vertex shader billboarding | Each blade quad faces the camera; computed from `UNITY_MATRIX_V` |
| Alpha cutout | Simple `clip(albedo.a - 0.5)` from blade texture atlas |
| Per-instance data from `StructuredBuffer` | `BladeBuffer` read in vert shader via `unity_BaseInstanceID + SV_InstanceID` |
| Wind in vert shader | `sin(_Time.y * WindFreq + worldPos.x * WindScale)` — no texture sample |
| `MaterialPropertyBlock` for per-chunk params | Wind strength, fade distances written by render system; one MBP per chunk |

### 4.2 Shader properties (public surface)

```hlsl
Properties
{
    _MainTex         ("Blade Texture",  2D)    = "white" {}
    _AlphaCutoff     ("Alpha Cutoff",   Range(0,1)) = 0.5
    _WindFrequency   ("Wind Frequency", Float) = 1.4
    _WindScale       ("Wind XZ Scale",  Float) = 0.3
    _WindStrength    ("Wind Strength",  Float) = 0.3   // overridden per chunk via MPB
    _FadeStart       ("Fade Start",     Float) = 60
    _FadeEnd         ("Fade End",       Float) = 120
}
```

### 4.3 Per-instance data (via StructuredBuffer, not instancing macros)

```hlsl
// GrassBlades.hlsl (included by the shader)
struct GrassBladeData
{
    float3 WorldPosition;
    float  Height;
    float3 ColorTint;
    float  FacingAngle;
};

StructuredBuffer<GrassBladeData> _BladeBuffer;
```

In the vertex shader:
```hlsl
GrassBladeData blade = _BladeBuffer[instanceID];
// billboard the quad: rotate vertex offset by (FacingAngle + camera yaw)
// scale Y by blade.Height
// tint albedo by blade.ColorTint
```

### 4.4 Blade mesh

A simple cross of 3 intersecting quads (6 tris each = 18 total triangles).
Generated once procedurally by `GrassBladeMeshBuilder` (editor utility or at system startup).
Assigned to `GrassSystemSettings.BladeMesh`.

---

## 5. Systems

### 5.1 `GrassChunkGenerationSystem`

```
UpdateInGroup: SimulationSystemGroup
UpdateAfter:  TerrainChunkMeshUploadSystem   // mesh must be uploaded first
```

**Trigger:** Queries chunks with `TerrainChunkGrassSurface` where `IsDirty == true`
(or where `GrassChunkBladeBuffer` does not yet exist).

**Per-chunk algorithm (Burst job):**

```
1. Read TerrainChunkMeshData.Mesh.Vertices + Indices (BlobAsset)
2. Compute chunk surface area estimate from triangle areas
3. bladeCount = min(MaxBladesPerChunk,
                   surfaceArea × BladesPerSqMeter × Density × BiomeDensityMultiplier)
4. For each triangle in mesh:
     a. Compute triangle area weight
     b. Allocate proportional blade count
     c. For each blade in triangle:
          - Random barycentric coords (deterministic seed from chunk position)
          - WorldPosition = bary-interpolated vertex positions
          - FacingAngle   = random(seed)
          - Height        = lerp(MinBladeHeight, MaxBladeHeight, random(seed))
          - ColorTint     = BiomeColor ± ColorNoise * random(seed)
5. Write NativeArray<GrassBladeData> output
```

**After job:**
```
- Create / resize GrassChunkBladeBuffer.BladeBuffer
- Upload NativeArray → GraphicsBuffer via SetData
- Write args buffer: [indexCount, bladeCount, 0, 0, 0]
- Set IsDirty = false on TerrainChunkGrassSurface
```

**Notes:**
- Uses `Unity.Mathematics.Random` seeded by chunk grid position (deterministic, no drift)
- No camera dependency; all blades generated regardless of view (GPU culled at draw time)
- BlobAsset vertices are in world space for Surface Nets chunks; no transform needed

### 5.2 `GrassChunkRenderSystem` (replacement)

```
UpdateInGroup: PresentationSystemGroup
```

```csharp
foreach chunk with GrassChunkBladeBuffer + LocalToWorld:
    if buffer.BladeCount == 0: continue
    if distanceToCamera > FadeEnd: continue        // CPU cull: skip distant chunks

    mpb.SetBuffer("_BladeBuffer", buffer.BladeBuffer)
    mpb.SetFloat("_WindStrength",  biomeSettings.WindStrength)
    mpb.SetFloat("_FadeStart",     settings.FadeStartDistance)
    mpb.SetFloat("_FadeEnd",       settings.FadeEndDistance)

    Graphics.DrawMeshInstancedIndirect(
        settings.BladeMesh,
        submeshIndex: 0,
        settings.BaseMaterial,
        bounds,           // chunk world AABB, expanded by MaxBladeHeight
        buffer.ArgsBuffer,
        argsOffset: 0,
        mpb)
```

One `MaterialPropertyBlock` is created and reused per frame (not per chunk) to avoid GC.

### 5.3 Terrain Edit Reactivity

When `TerrainChunkEditUtility.MarkChunksDirty` runs (existing system, marks
`TerrainChunkNeedsDensityRebuild`), a second pass also needs to mark grass dirty:

```csharp
// In TerrainChunkEditUtility.MarkChunksDirty (or a sibling utility method):
if (em.HasComponent<TerrainChunkGrassSurface>(chunk))
{
    var surface = em.GetComponentData<TerrainChunkGrassSurface>(chunk);
    surface.IsDirty = true;
    em.SetComponentData(chunk, surface);
}
```

`GrassChunkGenerationSystem` detects `IsDirty == true`, rebuilds the blade buffer for that
chunk. Edited areas have their SDF geometry changed → mesh vertices shift → blade positions
regenerated from new vertices automatically. No separate "removal" logic needed.

---

## 6. Sparse Clumps Variant (Deferred)

`GrassType == 1` is reserved in `TerrainChunkGrassSurface` for a clump/tuft variant.

Design intent when implemented:
- Lower `BladesPerSqMeter` (e.g. 0.5–2) — isolated clusters rather than a carpet
- Larger `MaxBladeHeight` and `MinBladeHeight`
- May use a different `BladeMesh` (bushy cross with leaves) and separate material
- Could use a second ScriptableObject (`GrassBiomeSettings` for `GrassType == 1`)
- Render system dispatches separate `DrawMeshInstancedIndirect` call with clump material

Because the biome settings and chunk component already carry `GrassType` and `BiomeTypeId`,
no architectural changes are needed — only a new generation code path and clump mesh/material.

---

## 7. File Layout

```
Assets/
├── Resources/
│   ├── Shaders/
│   │   └── GrassBlades.shader                  NEW
│   ├── GrassSystemSettings.asset               NEW (ScriptableObject)
│   └── Biomes/
│       └── GrassBiome_Plains.asset             NEW (MVP)
├── Scripts/DOTS/Terrain/Rendering/
│   ├── TerrainChunkGrassSurface.cs             MODIFY (add BiomeTypeId, GrassType, IsDirty)
│   ├── GrassChunkBladeBuffer.cs                NEW
│   ├── GrassChunkGenerationSystem.cs           NEW
│   ├── GrassChunkRenderSystem.cs               REPLACE
│   └── GrassBladeMeshBuilder.cs                NEW (editor utility for blade mesh asset)
├── Scripts/DOTS/Terrain/Settings/
│   ├── GrassSystemSettings.cs                  NEW
│   └── GrassBiomeSettings.cs                   NEW
└── Editor/
    └── TerrainGrassMaterialSetup.cs            REMOVE or repurpose as migration helper
```

---

## 8. Implementation Phases

### Phase 1 — Blade shader + static rendering
- [ ] `GrassBladeMeshBuilder` — generate blade cross mesh, save as asset
- [ ] `GrassBiomeSettings` + `GrassSystemSettings` ScriptableObjects
- [ ] `GrassBlades.shader` — vertex billboarding, alpha cutout, wind, StructuredBuffer read
- [ ] `GrassChunkBladeBuffer` managed component
- [ ] `GrassChunkGenerationSystem` — Burst job, uniform density, one biome
- [ ] `GrassChunkRenderSystem` — `DrawMeshInstancedIndirect`, replaces current system
- [ ] Validate: grass visible on surface, performance better than BruteForce POC

### Phase 2 — Configurability
- [ ] `TerrainChunkGrassSurface.Density` consumed (varies blade count per chunk)
- [ ] `BiomeTypeId` → `GrassBiomeSettings` lookup (MVP: fixed `BiomeTypeId=0` Plains)
- [ ] `MaterialPropertyBlock` per-chunk wind strength from Plains settings
- [ ] Fade in/out at `FadeStart / FadeEnd` distances (in vertex shader alpha)
- [ ] Editor: keep biome-tagging utility deferred until multi-biome phase

### Phase 3 — Terrain edit reactivity
- [ ] Extend `TerrainChunkEditUtility.MarkChunksDirty` to set `IsDirty = true` on grass surface
- [ ] `GrassChunkGenerationSystem` processes dirty chunks and rebuilds buffers
- [ ] Validate: carve terrain → grass disappears in carved region on next frame

### Phase 4 — Sparse clumps (future, separate spike)
- [ ] Define clump blade mesh + material
- [ ] Add `GrassType == 1` generation path to `GrassChunkGenerationSystem`
- [ ] Separate render path in `GrassChunkRenderSystem` or sibling system

---

## 9. Tests

Per the project's **SPEC → TEST → CODE** convention. All tests live under
`Assets/Scripts/DOTS/Tests/Automated/` unless noted.

### 9.1 EditMode tests (no scene, no Play mode)

**File:** `GrassBladeDataTests.cs`

| Test | What it verifies |
|------|-----------------|
| `GrassBladeData_StrideIs32Bytes` | `Marshal.SizeOf<GrassBladeData>() == 32` — must match shader `StructuredBuffer` stride |
| `GrassBladeData_FieldOffsets` | `WorldPosition` at offset 0, `Height` at 12, `ColorTint` at 16, `FacingAngle` at 28 via `Marshal.OffsetOf` |

**File:** `GrassChunkGenerationTests.cs`

| Test | What it verifies |
|------|-----------------|
| `BladeCount_RespectsMaxBladesPerChunk` | Given a large triangle and `Density=1`, output count ≤ `MaxBladesPerChunk` |
| `BladeCount_ScalesWithDensity` | `Density=0.5` produces ≈ half the blades of `Density=1` for same mesh (±5% for rounding) |
| `BladeCount_ZeroAtDensityZero` | `Density=0` → exactly 0 blades, no buffer allocated |
| `BladePositions_WithinTriangleBounds` | All `WorldPosition` values lie within AABB of the input triangle |
| `BladeGeneration_IsDeterministic` | Running twice with same chunk position seed produces identical `NativeArray<GrassBladeData>` |
| `PlainsDensityMultiplier_Applied` | `DensityMultiplier=0.5` on Plains settings halves blade count vs multiplier=1 |
| `BladeHeight_WithinPlainsRange` | All `Height` values satisfy `MinBladeHeight ≤ h ≤ MaxBladeHeight` in Plains settings |
| `ColorTint_WithinPlainsNoiseRange` | Each channel of `ColorTint` within `BaseColor ± ColorNoiseScale` in Plains settings |

**File:** `TerrainChunkGrassSurfaceTests.cs`

| Test | What it verifies |
|------|-----------------|
| `Default_HasFullDensity` | `TerrainChunkGrassSurface.Default.Density == 1f` |
| `Default_IsNotDirty` | `TerrainChunkGrassSurface.Default.IsDirty == false` |
| `Default_GrassTypeIsStandard` | `GrassType == 0` |

### 9.2 PlayMode / integration tests

**File:** `GrassChunkIntegrationTests.cs`
(Uses empty scene + programmatic entity creation per project bootstrap pattern)

| Test | What it verifies |
|------|-----------------|
| `GenerationSystem_CreatesBufferOnDirtyChunk` | Add `TerrainChunkGrassSurface { IsDirty=true }` to entity with mesh → after one system update, entity has `GrassChunkBladeBuffer` with `BladeCount > 0` |
| `GenerationSystem_ClearsIsDirtyAfterRebuild` | After buffer generation, `IsDirty == false` |
| `GenerationSystem_SkipsCleanChunks` | `IsDirty=false` → generation system does not replace existing buffer |
| `GenerationSystem_SkipsFutureGrassType` | `GrassType=1` chunk → no buffer created (unimplemented type, not an error) |
| `EditDirty_RebuildsBladesFromNewMesh` | Mesh modified + `IsDirty=true` → blade positions reflect new vertex positions (old positions absent) |
| `BufferDisposed_OnEntityDestroy` | Entity destroyed → `GrassChunkBladeBuffer.Dispose()` called (no GraphicsBuffer leak) |
| `ZeroDensityChunk_NoDraw` | `Density=0` → `GrassChunkBladeBuffer.BladeCount == 0`, render system skips it |

### 9.3 Manual validation checklist (not automated)

Run these by eye in Play mode after Phase 1 and Phase 2 are complete:

- [ ] Grass blades visible on tagged surface chunks
- [ ] Blades face camera as player rotates around them (billboard)
- [ ] Wind animation visible and not "swirling" (uniform patches)
- [ ] Plains chunks render consistent color/height profile across streamed areas
- [ ] Low-density chunk visibly sparser than high-density neighbor
- [ ] Carving terrain → grass disappears in the carved region within one frame
- [ ] Frame time acceptable: Profiler shows grass draw calls < 5% of frame budget on 10-chunk view

---

## 10. Performance Targets

| Metric | Target |
|--------|--------|
| Draw calls (10 visible chunks) | 10 (one per chunk, same as BruteForce POC) |
| GPU geometry per chunk | `bladeCount × 18 tris` (vs geometry shader: `meshTris × stacks × 2`) |
| Buffer rebuild cost | One-time Burst job on dirty; zero cost on clean frames |
| Max blades per chunk | 4096 (tunable in `GrassSystemSettings`) |
| Memory per chunk | `4096 × 32 bytes = 128 KB` GraphicsBuffer |

At default density (4096 blades, 10 chunks): ~10 draw calls submitting ~740K triangles total.
Compare BruteForce POC (2000-tri Surface Nets chunk × 12 stacks × 10 chunks): ~480K triangles
with no batching and geometry shader amplification overhead.

---

## 11. Open Questions

1. **Blade mesh source** — procedural in `GrassBladeMeshBuilder` or import OBJ?
   Recommendation: procedural, keeps asset count low and parameters tweakable.

2. **Biome assignment** — how does a chunk know its biome at tagging time?
   For Phase 1: hardcoded to biome index 0. Phase 2: biome system query.

3. **Surface detection** — which chunks get `TerrainChunkGrassSurface`?
   For Phase 1: manual editor utility (same as POC). Phase 3: detect topmost solid chunk
   per XZ column during terrain generation.

4. **Blade texture atlas** — reuse BruteForce textures or create new?
   BruteForce `_GrassTex` (RGBA, blades on alpha) is reusable; sample by `UV.y` for blade
   tip-to-base gradient.
