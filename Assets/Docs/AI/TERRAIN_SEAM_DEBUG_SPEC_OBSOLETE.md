# TERRAIN_SEAM_DEBUG_SPEC.md [OBSOLETE]

> **STATUS: OBSOLETE**
>
> This comprehensive spec has been replaced by two focused specs:
> - `TERRAIN_SEAM_DEBUG_SPEC_v1.md` - Density sampling validation (✅ Completed - density confirmed correct)
> - `TERRAIN_SEAM_DEBUG_MESH_SPEC.md` - Mesh/rendering validation (📋 Active)

---

**Project:** Unity 6 DOTS Terrain (SDF + Surface Nets)
**Goal:** Identify root cause of chunk seam artifacts (vertical 90° "walls", flipped-looking rendering, ring/arc patterns) using deterministic reproduction, debug visualization, numeric seam validation, and PlayMode invariant tests.
**Non-goal (for this SPEC):** Fix the seam bug. No algorithm changes beyond adding metadata, debug-only toggles, and diagnostics.

---

## 0) Constraints / Working Agreement (IMPORTANT)
1. **Instrumentation first.** Do not “fix” sampling, meshing, or sewing logic until diagnostics + tests exist.
2. **Small steps.** Each phase is a small PR-sized change.
3. **PlayMode-only tests** are acceptable for this project.
4. **Debug code must be gated** behind a singleton `TerrainDebugConfig` with `Enabled = true`.
5. If anything is unclear, prefer **adding logs/overlays** rather than guessing at the fix.

---

## 1) Entry Points (Given)
### 1. Chunk Loading / Streaming
- `Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs`

### 2. Height / Density Sampling
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs`
- uses `SDFTerrainFieldSettings` (BaseHeight, Amplitude, Frequency, etc.)
- produces `TerrainChunkDensityBlob`
- transitions to `TerrainChunkNeedsMeshBuild`

### 3. Mesh Building / Upload
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs`

### 4. LOD
- No LOD system exists (all chunks full res)

### 5. Bootstrap
- `Assets/Scripts/DOTS/Terrain/Bootstrap/TerrainBootstrapAuthoring.cs`

---

## 2) Hypothesis Tree (What diagnostics must disambiguate)
We want hard proof which bucket the bug falls into:

A. **Density mismatch at shared borders** (world→grid mapping mismatch, off-by-one, chunk origin math)  
B. **Surface Nets border handling** (needs apron/overlap samples, border clamping, neighbor dependency)  
C. **Streaming/pipeline state bands** (rings caused by chunks in different lifecycle stages: stale mesh vs new density, etc.)  
D. **Rendering/winding/normals/culling** (triangles sometimes flipped; appear missing with backface culling)

**Primary question:**  
> Do adjacent chunks produce identical density values at the same shared world positions?

If yes → look at meshing/rendering. If no → sampling/origin/off-by-one.

---

## 3) Phase Plan Overview
### Phase 1 — Determinism & Stable Repro
- Add `TerrainDebugConfig` singleton (debug-only behavior)
- Add chunk lifecycle tracking (`TerrainChunkDebugState`)

### Phase 2 — Visual Diagnostics
- Color chunks by coord or lifecycle stage
- Optional: chunk bounds visualization
- Optional: disable backface culling (debug only)

### Phase 3 — Numeric Seam Validator (runtime)
- Compare neighbor density borders and report max deltas
- Store/print “where mismatch occurs”

### Phase 4 — PlayMode Invariant Tests
- Border density continuity tests (2×2 or 3×3)
- Mesh validity tests
- Optional winding/normals sanity checks

**Stop after Phase 4.** Report findings. Only then consider fixes.

---

## 4) Phase 1: Determinism & Stable Repro

### 4.1 Add TerrainDebugConfig singleton
**Add file:**  
`Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfig.cs`

**Component:** `TerrainDebugConfig : IComponentData`
Suggested fields:
- `public bool Enabled;`
- `public int FixedSeed;` *(if seed exists elsewhere, store but do not rewire yet—just log)*
- `public bool FreezeStreaming;`
- `public int2 FixedCenterChunk;`
- `public int StreamingRadiusInChunks;` *(override streaming radius in debug mode)*
- `public bool ForceFixedCenterChunk;`
- `public bool ForceOnlyFixedRadiusSet;` *(when true, streaming system only manages chunks in radius set around FixedCenterChunk)*
- `public bool EnableSeamLogging;`
- `public float SeamEpsilon;` *(density continuity tolerance)*
- `public bool EnableDebugTintByState;`
- `public bool EnableDebugTintByCoord;`
- `public bool DebugDisableBackfaceCulling;` *(optional, see Phase 2)*

**Authoring (optional but recommended):**  
`Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfigAuthoring.cs` (MonoBehaviour)  
- Bakes/creates the singleton so you can toggle in scene.

**Acceptance Criteria**
- When `Enabled`, config exists as a singleton at runtime (verify via Entities debugger or log).

---

### 4.2 Add TerrainChunkDebugState component
**Add file:**  
`Assets/Scripts/DOTS/Terrain/Debug/TerrainChunkDebugState.cs`

**Components:**
- `public enum TerrainChunkLifecycleStage : byte { Spawned, NeedsDensity, DensityReady, NeedsMesh, MeshReady, Uploaded }`
- `public struct TerrainChunkDebugState : IComponentData`
  - `public int2 ChunkCoord;`
  - `public TerrainChunkLifecycleStage Stage;`
  - `public uint Version;` *(increment whenever chunk re-enters NeedsDensity)*

**Acceptance Criteria**
- Every spawned chunk entity gets a `TerrainChunkDebugState` with correct `ChunkCoord`.

---

### 4.3 Integrate into TerrainChunkStreamingSystem
**Edit file:**  
`Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs`

**Requirements:**
1. If `TerrainDebugConfig.Enabled && TerrainDebugConfig.FreezeStreaming`  
   - streaming system does not spawn/despawn due to player movement.
2. If `Enabled && ForceFixedCenterChunk`  
   - treat player chunk coord as `FixedCenterChunk`.
3. If `Enabled && ForceOnlyFixedRadiusSet`  
   - only maintain chunks in the radius set; do not consider other runtime heuristics.
4. Any chunk spawned should:
   - receive `TerrainChunkDebugState` with Stage=`Spawned`, Version=1
5. When a chunk is marked with `TerrainChunkNeedsDensityRebuild`, update Stage=`NeedsDensity` and increment Version.

**Acceptance Criteria**
- In debug mode, the same chunk set is spawned each run.
- Lifecycle stage changes appear consistent in logs (optional) and via debug tint (Phase 2).

---

### 4.4 Integrate lifecycle tracking into sampling / meshing / upload
**Edit files:**
- `TerrainChunkDensitySamplingSystem.cs`
- `TerrainChunkMeshBuildSystem.cs`
- `TerrainChunkMeshUploadSystem.cs`

**Rules:**
- When density blob is created/ready → set Stage=`DensityReady`
- When `TerrainChunkNeedsMeshBuild` is set → Stage=`NeedsMesh`
- When mesh blob created → Stage=`MeshReady`
- When uploaded/RenderMesh set → Stage=`Uploaded`

**Acceptance Criteria**
- Stage transitions align with pipeline execution.

---

## 5) Phase 2: Visual Diagnostics

### 5.1 Debug tinting by lifecycle stage / chunk coord
**Add file:**  
`Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugTintSystem.cs`

**Purpose:** Make “rings” explainable by showing chunk state bands.
- If `TerrainDebugConfig.EnableDebugTintByState` → color each chunk based on `TerrainChunkDebugState.Stage`
- If `EnableDebugTintByCoord` → color by hash of `ChunkCoord`

**Implementation options:**
- Preferred: set a per-entity material property via Entities Graphics (e.g., URP `MaterialProperty`/`URPMaterialPropertyBaseColor` depending on setup)
- If that’s too involved, temporary: use multiple debug materials assigned by stage (swap shared material when enabled)

**Acceptance Criteria**
- In scene view, you can visually see:
  - whether rings correspond to lifecycle stage differences
  - whether the “seam” aligns with a stage boundary

---

### 5.2 Optional: debug chunk bounds
**Goal:** confirm whether visible seams are actual chunk borders.
Options:
- Spawn thin line/edge meshes in debug
- Or spawn a wireframe rectangle decal-like mesh

**Acceptance Criteria**
- Chunk borders are visible and match (or do not match) the observed ring seams.

---

### 5.3 Optional: disable backface culling (debug only)
**Goal:** distinguish “missing triangles” from “flipped winding”.

Approach:
- If `DebugDisableBackfaceCulling`:
  - use a debug material with culling off
  - or override raster state if available in your pipeline

**Acceptance Criteria**
- When enabled, triangles that were previously invisible due to culling become visible (if winding is the problem).

---

## 6) Phase 3: Numeric Seam Validator (Runtime)

### 6.1 Ensure density metadata exists for border mapping
**Edit:** `TerrainChunkDensityBlob` definition (wherever it lives)

**Must include (or be accessible):**
- Grid resolution / dimensions (e.g., `int3 Size` or equivalent)
- Voxel size (float)
- Chunk origin in world space (`float3 OriginWS`)
- A deterministic mapping from grid index → world space position

If currently missing, add minimal metadata without changing sampling behavior.

**Acceptance Criteria**
- Seam validator can compute “matching world position” for border samples of adjacent chunks.

---

### 6.2 Add TerrainSeamValidatorSystem
**Add file:**  
`Assets/Scripts/DOTS/Terrain/Debug/TerrainSeamValidatorSystem.cs`

**System behavior:**
- Only runs when `TerrainDebugConfig.Enabled` and/or `EnableSeamLogging`
- For each chunk with a density blob and known `ChunkCoord`:
  1. Find east neighbor chunk (`ChunkCoord + (1,0)`) with density blob
  2. Compare densities along shared border:
     - A east border vs B west border for all `y,z` samples
  3. Find north neighbor (`ChunkCoord + (0,1)`) and compare:
     - A north border vs B south border for all `y,x` samples
- Compute:
  - `maxAbsDelta`
  - `countAboveEpsilon`
  - `argMax` location (indices) + optionally computed world position
- If `countAboveEpsilon > 0`:
  - Log a single compact line:
    - `SEAM_MISMATCH East A( cx,cz ) <-> B( cx+1,cz ) maxΔ=... at (y,z)=...`
  - Optionally add/overwrite a component on chunk:
    - `TerrainChunkSeamReport { float MaxDelta; int Count; int2 Neighbor; }`

**Acceptance Criteria**
- When a seam wall is visible, system either:
  - logs mismatches (→ bucket A), OR
  - logs no mismatches (→ buckets B/C/D)

---

## 7) Phase 4: PlayMode Invariant Tests

**Add folder:**  
`Assets/Tests/PlayMode/DOTS/Terrain/`

### 7.1 Test Harness
Create a common helper to:
- Create a test `World`
- Create/initialize system groups needed
- Ensure `TerrainDebugConfig` singleton is set (Enabled + fixed chunk set)
- Tick the world until:
  - density exists for all expected chunks
  - mesh exists for all expected chunks (for mesh tests)

**Acceptance Criteria**
- Tests can deterministically generate the same 2×2 or 3×3 chunk grid.

---

### 7.2 Border Density Continuity Tests
**File:** `TerrainBorderContinuityPlayModeTests.cs`

**Test cases:**
1. `BorderDensityMatches_EastWest_2x2()`
2. `BorderDensityMatches_NorthSouth_2x2()`
3. (Optional) same for 3×3

**Assertions:**
- For each neighbor pair in the test grid:
  - `maxAbsDelta < SeamEpsilon`
- On failure, print:
  - chunk coords
  - direction
  - max delta and index location

**Acceptance Criteria**
- If the seam bug is due to density mismatch, at least one test fails.

---

### 7.3 Mesh Validity Tests
**File:** `TerrainMeshValidityPlayModeTests.cs`

Run until mesh blobs exist (post Surface Nets build).

**Assertions:**
- Vertex positions finite (no NaN/Inf)
- Indices in range
- Triangle count reasonable ( > 0, < max expected )
- Degenerate triangles count below threshold (optional)

**Acceptance Criteria**
- Mesh build is structurally sound; if invalid, tests fail with actionable info.

---

### 7.4 Optional: Rendering/Winding sanity tests
These are tricky in pure data tests, but add at least:
- normals finite and non-zero if stored
- or ensure consistent triangle orientation heuristics if you have a convention

**Acceptance Criteria**
- If “wrong direction” is due to index winding producing invalid normals, you may catch it here.

---

## 8) “Stop & Report” Checklist (end of SPEC)
After Phase 4, STOP. Do not fix.

Report:
1. Do border density continuity tests pass?
2. If not, which borders fail and what are the max deltas?
3. Do mesh validity tests pass?
4. Do debug tints show rings aligned with lifecycle stage bands?
5. Does disabling backface culling change the “wrong direction” symptom?

**Decision matrix:**
- **Density mismatches found** → likely origin math / off-by-one / world→grid mapping.
- **No density mismatches; mesh invalid** → surface nets indexing/border handling.
- **No mismatches; mesh valid; rings align with stage** → streaming/pipeline state visibility (stale vs new).
- **No mismatches; mesh valid; culling toggle changes symptom** → winding order / normals.

---

## 9) Implementation Notes (guardrails)
- Keep diagnostics light; avoid per-frame heavy logs unless `EnableSeamLogging`.
- Prefer storing seam metrics on components and printing only summaries.
- Keep all debug systems inside a `DOTS.Terrain.Debug` namespace.
- Ensure debug-only systems don’t run in release builds if you have build defines (optional).

---

## 10) Suggested Phase Commit Plan
1. Phase 1: add config + lifecycle stage tracking
2. Phase 2: add tinting (coord + stage), optional bounds
3. Phase 3: add seam validator logs + optional report component
4. Phase 4: add PlayMode tests for continuity + mesh validity

Each phase should be reviewable and should not alter terrain generation output unless debug mode is enabled.

---
