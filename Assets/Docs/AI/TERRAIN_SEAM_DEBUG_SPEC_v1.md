# TERRAIN_SEAM_DEBUG_SPEC_v1.md
**Project:** Unity 6 DOTS – SDF + Surface Nets Terrain  
**Goal:** Determine whether visible terrain seams (vertical 90° walls / ring patterns) are caused by
density sampling mismatches between chunks or by later meshing/rendering stages.

**Non-goal:** Fix the terrain. No algorithm changes in this spec.

---

## 1. Core Question
Do adjacent chunks produce identical density values at shared world-space border positions?

If **no** → bug is in sampling / chunk origin / off-by-one logic.  
If **yes** → bug is in surface nets meshing, normals, winding, or upload.

---

## 2. Minimal Determinism (Required)

### 2.1 TerrainDebugConfig singleton
Add a debug-only singleton component:

**Fields**
- `bool Enabled`
- `bool FreezeStreaming`
- `int2 FixedCenterChunk`
- `int StreamingRadiusInChunks`
- `float SeamEpsilon`
- `bool EnableSeamLogging`

**Rules**
- Debug behavior only applies when `Enabled == true`.
- Default runtime behavior unchanged when disabled.

---

### 2.2 Streaming system changes
**File**
Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs

**Behavior when debug enabled**
- If `FreezeStreaming == true`, do not spawn/despawn chunks due to player movement.
- Use `FixedCenterChunk` and `StreamingRadiusInChunks` instead of player position.
- Resulting chunk set must be deterministic across runs.

---

## 3. Minimal Lifecycle Tracking (Required)

### 3.1 TerrainChunkDebugState
Add a lightweight component:

struct TerrainChunkDebugState : IComponentData
{
int2 ChunkCoord;
byte Stage; // 0=Spawned, 1=NeedsDensity, 2=DensityReady, 3=NeedsMesh, 4=MeshReady, 5=Uploaded
}


### 3.2 Update stage in pipeline
Update stage in:
- `TerrainChunkStreamingSystem`
- `TerrainChunkDensitySamplingSystem`
- `TerrainChunkMeshBuildSystem`
- `TerrainChunkMeshUploadSystem`

No logic changes—stage is for diagnostics only.

---

## 4. Seam Validator (Required)

### 4.1 Density metadata
Ensure `TerrainChunkDensityBlob` exposes enough info to:
- Map density grid indices → world position
- Know grid resolution and chunk origin

(Add metadata only if missing.)

---

### 4.2 TerrainSeamValidatorSystem
Add a system that runs **after density sampling**.

**Behavior**
- For each chunk with a density blob:
  - Find east `(cx+1, cz)` and north `(cx, cz+1)` neighbors.
  - Compare shared border density values.
- Compute:
  - `maxAbsDelta`
  - `countAboveEpsilon`
- If `countAboveEpsilon > 0` and `EnableSeamLogging`:
  - Log one line:
    ```
    SEAM_MISMATCH A(cx,cz) ↔ B(cx+1,cz) maxΔ=... samples=...
    ```

**Important**
- Compare densities at the same world positions.
- Do not modify densities or meshes.

---

## 5. Single PlayMode Test (Required)

### 5.1 Border continuity test
Add one PlayMode test:

**Test**
- Create a test World
- Enable `TerrainDebugConfig`
- Spawn a deterministic 2×2 chunk grid
- Run systems until density blobs exist
- Assert:
  - No seam mismatches above `SeamEpsilon`

**Failure output must include**
- Chunk coordinates
- Border direction
- Max delta

---

## 6. Stop Point (Mandatory)
After implementing:
1. Run scene with debug enabled
2. Run PlayMode test
3. Report:
   - Do seam mismatches occur?
   - Do mismatches align with visible seams?

**Do not attempt fixes until this report exists.**

---

## 7. Interpretation Guide
- **Mismatches detected** → sampling / origin math / off-by-one bug
- **No mismatches** → investigate surface nets, normals, winding, upload
- **Ring patterns align with chunk Stage differences** → streaming/pipeline visibility issue

---

## 8. Out of Scope (Future)
- Mesh validity tests
- Backface culling toggles
- Visual debug overlays
- Screenshot/golden tests
- LOD (not present)

These are only added if needed after v1 results.
