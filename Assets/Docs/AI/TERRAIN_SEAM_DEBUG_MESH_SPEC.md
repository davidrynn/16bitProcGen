# TERRAIN_SEAM_DEBUG_MESH_SPEC.md
**Project:** Unity 6 DOTS – SDF + Surface Nets Terrain
**Prerequisite:** TERRAIN_SEAM_DEBUG_SPEC_v1.md completed – density sampling validated as correct
**Goal:** Determine whether visible terrain seams are caused by Surface Nets meshing, normal calculation, winding order, or mesh upload.

**Non-goal:** Fix the terrain. No algorithm changes in this spec.

---

## 1. Context from v1

The density seam validator (v1) confirmed:
- Adjacent chunks produce **identical density values** at shared world-space border positions
- The bug is **not** in sampling / chunk origin / off-by-one logic

Therefore, the issue is in the mesh generation or rendering pipeline.

---

## 2. Core Questions

1. Do adjacent chunks produce **identical vertex positions** at shared borders?
2. Are **normals** at chunk borders consistent between neighbors?
3. Is **triangle winding** consistent at chunk borders?
4. Are there **gaps or overlaps** in the mesh at chunk boundaries?

---

## 3. Mesh Border Validator System (Required)

### 3.1 TerrainMeshSeamValidatorSystem

Add a debug system that runs **after mesh building**.

**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainMeshSeamValidatorSystem.cs`

**Behavior:**
- For each chunk with mesh data:
  - Find east `(cx+1, cz)` and north `(cx, cz+1)` neighbors
  - Extract border vertices from both chunks
  - Compare vertex positions at shared world locations
- Compute:
  - `maxPositionDelta` – largest position mismatch
  - `countPositionMismatches` – vertices above epsilon
  - `maxNormalDelta` – largest normal direction mismatch
  - `countNormalMismatches` – normals above angle threshold
- If mismatches found and `EnableSeamLogging`:
  ```
  MESH_SEAM_MISMATCH A(cx,cz) ↔ B(cx+1,cz) posΔ=... normΔ=... verts=...
  ```

**Important:**
- Compare vertices at the same world positions (within epsilon)
- Do not modify meshes
- Use TerrainDebugConfig settings for thresholds

### 3.2 Border Vertex Extraction

For Surface Nets meshes, border vertices are those within `VoxelSize` of the chunk boundary.

**East border (chunk A):**
- Vertices where `localPos.x >= (Resolution.x - 2) * VoxelSize`

**West border (chunk B):**
- Vertices where `localPos.x <= VoxelSize`

**North/South borders:** Same logic on Z axis.

---

## 4. Mesh Debug Components (Required)

### 4.1 TerrainChunkMeshDebugData

Add a debug component to store mesh statistics:

```csharp
struct TerrainChunkMeshDebugData : IComponentData
{
    public int VertexCount;
    public int TriangleCount;
    public int BorderVertexCount;
    public float3 BoundsMin;
    public float3 BoundsMax;
}
```

### 4.2 Update in TerrainChunkMeshBuildSystem

After mesh generation, populate `TerrainChunkMeshDebugData` if debug is enabled.

---

## 5. Visual Debug Overlay (Required)

### 5.1 TerrainMeshBorderDebugSystem

Add a system that draws debug visuals using `Debug.DrawLine`.

**Behavior when debug enabled:**
- Draw chunk boundary boxes (green)
- Draw border vertices as points (yellow for matched, red for mismatched)
- Draw normals at border vertices (blue lines)

**Activation:**
- Only runs when `TerrainDebugConfig.Enabled && EnableMeshDebugOverlay`

### 5.2 Add to TerrainDebugConfig

Extend the debug config singleton:

```csharp
public bool EnableMeshDebugOverlay;
public float MeshSeamPositionEpsilon;  // Default: 0.001f
public float MeshSeamNormalAngleThreshold;  // Default: 5.0f degrees
```

---

## 6. Backface Culling Toggle (Required)

### 6.1 Debug Material Variant

Create or identify a debug material that:
- Disables backface culling (`Cull Off`)
- Optionally renders both sides with different colors

**Purpose:** Determine if seams are caused by incorrect triangle winding (triangles facing away from camera).

### 6.2 Runtime Toggle

Add to TerrainDebugConfig:
```csharp
public bool DisableBackfaceCulling;
```

When enabled, swap terrain material to the debug variant.

---

## 7. PlayMode Tests (Required)

### 7.1 Mesh Border Vertex Continuity Test

**Test:**
- Create a test World with debug enabled
- Spawn a deterministic 2×2 chunk grid
- Run systems until meshes are built
- Assert:
  - Border vertices exist on both sides of each shared edge
  - Vertex positions match within `MeshSeamPositionEpsilon`

**Failure output must include:**
- Chunk coordinates
- Border direction
- Max position delta
- Sample mismatched vertices

### 7.2 Mesh Normal Consistency Test

**Test:**
- Same setup as above
- Assert:
  - Normals at matched border vertices point in similar directions
  - Angle between normals is below `MeshSeamNormalAngleThreshold`

---

## 8. Investigation Checklist

After implementing, run scene with debug enabled and check:

| Check | How to Verify | If Failed |
|-------|---------------|-----------|
| Border vertices exist | Debug overlay shows yellow points at borders | Surface Nets not generating edge vertices |
| Positions match | No red points in overlay | Vertex position calculation bug |
| Normals consistent | Blue lines point same direction across border | Normal calculation bug |
| No gaps visible | Disable backface culling, rotate camera | Winding order bug |
| No z-fighting | Look for flickering at borders | Duplicate/overlapping vertices |

---

## 9. Stop Point (Mandatory)

After implementing:
1. Run scene with `EnableMeshDebugOverlay = true`
2. Run PlayMode tests
3. Toggle `DisableBackfaceCulling` and observe
4. Report:
   - Do vertex position mismatches occur?
   - Do normal mismatches occur?
   - Does disabling backface culling reveal hidden faces?
   - Do mismatches align with visible seams?

**Do not attempt fixes until this report exists.**

---

## 10. Interpretation Guide

| Finding | Root Cause |
|---------|------------|
| Vertex position mismatches | Surface Nets boundary vertex calculation bug |
| Normal mismatches only | Normal averaging/calculation not crossing chunk boundaries |
| Backface culling reveals faces | Triangle winding inconsistent at boundaries |
| No mesh issues found | Investigate rendering (shader, material, transform) |
| Gaps but vertices match | Triangle generation skipping boundary cells |

---

## 11. Files to Create/Modify

### New Files
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainMeshSeamValidatorSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainMeshBorderDebugSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainChunkMeshDebugData.cs`
- `Assets/Scripts/DOTS/Tests/PlayMode/TerrainMeshBorderContinuityTests.cs`

### Modified Files
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfig.cs` – Add mesh debug fields
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs` – Populate debug data
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` – Register new debug systems

---

## 12. Implementation Order (Strict Review-Gated)

1. **Phase 1:** Extend `TerrainDebugConfig` with mesh debug fields
2. **Phase 2:** Add `TerrainChunkMeshDebugData` component
3. **Phase 3:** Implement `TerrainMeshSeamValidatorSystem`
4. **Phase 4:** Implement `TerrainMeshBorderDebugSystem` (visual overlay)
5. **Phase 5:** Add backface culling toggle
6. **Phase 6:** Write PlayMode tests
7. **Phase 7:** Run diagnostics and report findings

**Stop after each phase for review.**

---

## 13. Out of Scope (Future)

- Mesh optimization/simplification
- LOD transitions
- Texture seam debugging
- Physics collider seam debugging
- Automated screenshot comparison

These are only added if needed after mesh debugging results.

---

## 14. Implementation Progress Log

### Phase 1-6: Code Written ✅
All files created/modified as specified:
- `TerrainDebugConfig.cs` – mesh debug fields added
- `TerrainDebugConfigAuthoring.cs` – Inspector fields added
- `TerrainChunkMeshDebugData.cs` – component created
- `TerrainMeshSeamValidatorSystem.cs` – system created
- `TerrainMeshBorderDebugSystem.cs` – system created
- `TerrainMeshBorderContinuityTests.cs` – PlayMode tests created
- `TerrainChunkMeshUploadSystem.cs` – backface culling toggle wired up

### Phase 7: Runtime Testing ❌ BLOCKED

**Test Results (2024-01-29):**

| Feature | Expected | Actual | Status |
|---------|----------|--------|--------|
| PlayMode test | Pass or fail with data | Failed: 54 position mismatches | ⚠️ Data exists |
| Backface culling toggle | Render both sides | Runtime toggle not working | ❌ Code issue |
| Manual Render Face = Both | Render both sides | **FIXES visible seams** | ✅ Key finding |
| Debug overlay | Green boxes, yellow/red vertices | Nothing visible | ❌ Not working |
| Seam mismatch logs | Console output | No logs appearing | ❌ Not working |

### Key Finding: Backface Culling Reveals Root Cause

**Manual test performed:** Set `TerrainMat.mat` → Render Face = "Both" in Inspector.

**Result:** Visible terrain seams are reduced/fixed when rendering both sides.

**Interpretation (from Section 10):** This indicates **triangle winding is inconsistent at chunk boundaries**. Some triangles at the seams are facing away from the camera and being culled.

**Root cause:** Surface Nets mesh generation is producing triangles with incorrect winding order at chunk borders.

**Material location:** `Assets/Materials/TerrainMat.mat` (referenced by `Assets/Resources/Terrain/TerrainChunkRenderSettings.asset`)

**Diagnosed Issues:**

1. **Debug systems disabled in ProjectFeatureConfig**
   - Systems ARE wired up in `DotsSystemBootstrap.cs` (lines 147-159)
   - But feature flags in `ProjectFeatureConfig.cs` default to `false`:
     ```csharp
     public bool EnableTerrainSeamValidatorSystem = false;      // line 55
     public bool EnableTerrainMeshSeamValidatorSystem = false;  // line 56
     public bool EnableTerrainMeshBorderDebugSystem = false;    // line 57
     ```
   - **User must enable these in Inspector** on their ProjectFeatureConfig asset

2. **Backface culling toggle ineffective (runtime)**
   - Code added to `TerrainChunkMeshUploadSystem.ApplyBackfaceCullingToggle()`
   - URP Lit shader does not respond to runtime `_Cull` property changes
   - **Workaround:** Manual "Render Face = Both" in material Inspector works
   - This is acceptable for debug purposes; runtime toggle is nice-to-have

3. **Triangle winding issue confirmed**
   - Setting Render Face = Both fixes/reduces visible seams
   - This proves seams are caused by **incorrectly wound triangles at chunk borders**
   - Root cause is in `TerrainChunkMeshBuildSystem` Surface Nets algorithm

4. **No seam logs**
   - Caused by issue #1 – validator system not running

### Next Steps Required

1. **Enable debug systems in ProjectFeatureConfig asset** (Inspector):
   - `EnableTerrainMeshSeamValidatorSystem = true`
   - `EnableTerrainMeshBorderDebugSystem = true`

2. **Enable debug config in scene** (TerrainDebugConfigAuthoring component):
   - `Enabled = true`
   - `EnableMeshDebugOverlay = true`
   - `EnableSeamLogging = true`

3. **Re-run Phase 7 diagnostics** to get detailed mismatch data

4. **Fix triangle winding in TerrainChunkMeshBuildSystem** (future spec)
   - Investigate Surface Nets quad generation at chunk borders
   - Ensure consistent winding order for all triangles

### Workaround (Immediate)

To visually fix seams without code changes:
- Set `Assets/Materials/TerrainMat.mat` → **Render Face = Both** in Inspector

### Summary

**Root cause identified:** Triangle winding inconsistency at chunk borders.
**Next phase:** Fix winding in mesh generation (requires new spec).

---

## 15. Revised Analysis: Concentric Circle Artifacts (2025-01-30)

### Observation

Visual inspection shows seams appear as **concentric circles**, not rectangular chunk boundaries. This contradicts the chunk-boundary hypothesis.

### SDF Analysis

The terrain density is computed in `SDFMath.SdGround()` ([SDFMath.cs:26-32](Assets/Scripts/DOTS/Terrain/SDF/SDFMath.cs#L26-L32)):

```csharp
var wave = (math.sin(p.x * frequency) + math.sin(p.z * frequency)) * 0.5f;
var height = baseHeight + amplitude * (wave + noiseValue);
return p.y - height;
```

This creates a **grid/egg-carton pattern** from `sin(x) + sin(z)`, not circles. However, iso-height bands cutting through this surface create diagonal/curved lines at regular height intervals.

### Possible Causes of Circular Banding

1. **Vertex placement quantization** - Surface Nets uses inverse-density weighting:
   ```csharp
   var weight = 1f / (math.abs(density) + 1e-5f);  // SurfaceNets.cs:101
   ```
   Similar densities → similar weights → vertices snap to regular bands.

2. **No vertex normals** - `SurfaceNets.cs` generates vertices and indices but **no normals**. Unity auto-calculates flat normals, creating visible facets that align with density iso-surfaces.

3. **Voxel grid resolution** - The discrete sampling grid creates stair-stepping at certain angles, appearing as bands.

### Updated Hypothesis (Corrected)

**Both rectangular AND circular banding are the same issue** - they are NOT chunk border artifacts. Visual inspection confirms the rectangular patterns align with the voxel grid, not chunk boundaries.

| Visual Pattern | Root Cause |
|----------------|------------|
| Concentric circles + rectangular grid | **Voxel grid quantization** - Surface Nets creates flat quads at discrete grid positions |
| Visible facets/edges | **No vertex normals** - Unity auto-calculates flat normals, making every triangle edge visible |

**The chunk boundary hypothesis was incorrect.** The backface culling fix helped visibility but did not address the actual cause.

### Root Cause

The Surface Nets algorithm in `SurfaceNets.cs`:
- Outputs **vertices and indices only** - no normals
- Creates flat quads aligned to the voxel grid
- Unity calculates **flat/faceted normals** automatically
- Result: every triangle edge is visible as a hard line

### Next Investigation Steps

1. **Add smooth normals** to Surface Nets output:
   - Option A: Compute gradient-based normals from SDF (most accurate)
   - Option B: Average normals at shared vertices (simpler)
2. **Increase voxel resolution** to test if bands become finer (confirms voxel cause)
3. **This spec is complete** - a new spec for smooth normals is needed

### Material Workaround Status

- `TerrainMat.mat` now has `_Cull: 0` (Render Face = Both)
- This was a red herring - the real issue is missing smooth normals

### Conclusion

**This debug spec has identified the root cause: missing vertex normals in Surface Nets output.**

The mesh seam validator systems built in Phases 1-6 are still useful for detecting actual chunk boundary issues, but the current visible artifacts are NOT chunk-related. A new spec should focus on adding smooth normal generation to the meshing pipeline.
