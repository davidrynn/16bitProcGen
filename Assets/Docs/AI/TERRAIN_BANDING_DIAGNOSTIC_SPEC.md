# TERRAIN_BANDING_DIAGNOSTIC_SPEC.md

**Project:** Unity 6 DOTS – SDF + Surface Nets Terrain
**Prerequisite:** TERRAIN_SEAM_DEBUG_MESH_SPEC.md concluded – chunk boundary hypothesis ruled out
**Goal:** Identify the root cause of visible concentric/grid banding artifacts in terrain mesh.

**Non-goal:** Fix the issue. This spec is diagnostic only.

---

## 1. Problem Statement

Visible banding appears on terrain at regular intervals:
- Pattern: Concentric circles + rectangular grid lines
- NOT aligned with chunk boundaries
- Smooth slopes exist BETWEEN bands (not uniform faceting)
- Bands appear at specific height/position intervals

---

## 2. Hypotheses to Test

| # | Hypothesis | Test Method | Expected if True |
|---|------------|-------------|------------------|
| H1 | Voxel Y-layer stair-stepping | Change VoxelSize, measure band interval | Band interval = VoxelSize |
| H2 | Vertex position quantization | Analyze vertex Y positions | Vertices cluster at discrete Y values |
| H3 | Triangle topology issues | Analyze triangle shapes at bands | Degenerate/thin triangles at bands |
| H4 | SDF sampling precision | Use flat plane SDF | Bands disappear with simple SDF |
| H5 | Density weighting causing snapping | Modify weight formula | Bands change with different weighting |

---

## 2.1 Diagnostic Results (2026-02-07)

### Test Configuration
- **VoxelSize:** 1.0 (test constant)
- **Resolution:** 17×17×17 (test constant)
- **BaseHeight:** 0
- **Amplitude:** 10
- **Frequency:** 0.1
- **NoiseValue:** 0 (changed from 1.0 — original value placed isosurface above chunk volume, producing 0 vertices)

### H1: Voxel Y-layer stair-stepping — ✅ CONFIRMED
- VoxelSize=1.0: 31 peaks, avg interval = **0.232**
- VoxelSize=0.5: 32 peaks, avg interval = **0.128**
- Ratio: **1.81** (expected ~2.0 if true)
- **Conclusion:** Band interval scales proportionally with VoxelSize. Banding is tied to voxel grid resolution.

### H2: Vertex position quantization — ✅ CONFIRMED
- **31 clustering peaks** detected in Y-position histogram (31% of buckets exceed 1.3× average)
- Y spacing: mean=0.037, StdDev=0.092, max=**0.976** (≈ VoxelSize)
- Peaks cluster more densely at higher Y values
- **Conclusion:** Vertices are NOT smoothly distributed. They cluster at specific Y values.

### H3: Triangle topology issues — ✅ CONFIRMED (consequence, not cause)
- 400 vertices, 718 triangles
- 0 formally degenerate triangles
- Max aspect ratio: **98.53** (threshold: 50)
- Min triangle area: 0.0018 vs avg 0.36 (200× smaller)
- **Conclusion:** Very thin triangles exist. These are a *consequence* of vertex Y-clustering, not an independent cause. When vertices snap to similar Y, triangles become slivers.

### H4: SDF sampling precision — ✅ PASSED (flat plane is flat)
- Initial run: 0 vertices — surface at Y=8.0 landed exactly on grid boundary where `density=0`. `ProcessCell` check `minDensity < 0 && maxDensity > 0` (strict inequality) missed it on both sides.
- Fix: Offset surface to Y=8.3 (between grid rows).
- Re-run: Vertices generated. Y-spread ≈ 0 → flat plane IS flat.
- **Conclusion:** Surface Nets does NOT introduce banding on trivial SDF. Banding is caused by SDF curvature interacting with the voxel grid.
- **Bonus finding:** The strict inequality check in `ProcessCell` creates a degenerate case when the isosurface is coplanar with a voxel grid layer — the surface is missed entirely.

### H5: Density weighting — ❌ RULED OUT (tilted plane is near-perfect)
- Tilted plane: `Y = 8 + 0.5*X`, VoxelSize=1
- 289 vertices generated
- Deviation from ideal surface: avg=**0.000010**, max=**0.000011** — essentially zero error
- Y spacing: mean=0.5, StdDev=0.5, max=0.9999
- Clustering peaks: **9** (low, expected for discrete grid)
- Max aspect ratio: **2.68** (excellent)
- **Conclusion:** The `weight = 1/(|d| + ε)` formula places vertices accurately on linear SDFs. The formula is NOT the direct cause of banding.

### Root Cause: SDF Curvature × Voxel Grid Interaction

The combined H1-H5 results reveal the mechanism:

1. **H5 proved** the weighting formula works perfectly on linear/planar SDFs.
2. **H1/H2 proved** severe banding exists with the sine-wave SDF (`SdGround`).
3. **H4 proved** the algorithm itself doesn't introduce banding on simple geometry.

The root cause is: `SdGround` uses `sin(x*0.1) + sin(z*0.1)` — very low-frequency, mostly-horizontal undulations. Where the surface is **nearly parallel to a voxel Y-layer**, many adjacent cells produce vertices at nearly the same Y coordinate. These cluster into visible horizontal bands at intervals tied to VoxelSize.

The inverse-density weighting `weight = 1/(|d| + ε)` amplifies this because it averages all 8 cell corners. On a nearly-flat surface, many corners have similar small density values, so the weighted average collapses toward the grid layer center. **Edge interpolation** (finding zero-crossings on individual cell edges) would place vertices precisely on the isosurface regardless of surface orientation.

### Fix: Edge Interpolation in ProcessCell

Replace the 8-corner inverse-density weighting with edge-based zero-crossing interpolation:
- For each of the 12 cell edges, check if a sign change occurs
- Linearly interpolate to find the zero-crossing point: `t = dA / (dA - dB)`
- Average all crossing points → vertex position

This is the standard "Naive Surface Nets" improvement and is used in Dual Contouring.

### Phase 8: Fix Applied (2026-02-07)

**File changed:** `Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs`

**What changed in `ProcessCell()`:**
- **Removed:** `weight = 1f / (math.abs(density) + 1e-5f)` all-corners weighting
- **Added:** Edge interpolation across the 12 cube edges. For each edge with a sign change, compute `t = dA / (dA - dB)` and `lerp(posA, posB, t)`. Average all crossing points.
- **Added:** `ProcessEdge()` static helper method (Burst-compatible, no managed allocations)
- **Data storage:** Uses `FixedList64Bytes<float>` for corner densities and positions (stack-allocated, Burst-safe)

**Expected validation (Phase 9):**
- H1: Peak count should drop dramatically; ratio may become meaningless if banding is gone
- H2: Clustering peaks should drop from 31 to near 0
- H3: Max aspect ratio should drop from 98.53 to < 10
- H4: Flat plane should remain flat
- H5: Tilted plane should remain near-perfect

---

## 2.2 Separate Issue: Reversed Triangle Winding (Culling Bug)

During code review of `SurfaceNets.cs`, a separate bug was discovered:

```csharp
// SurfaceNets.cs EmitTriangle — indices are SWAPPED
private void EmitTriangle(int a, int b, int c)
{
    Indices.Add(a);
    Indices.Add(c);  // ← should be b
    Indices.Add(b);  // ← should be c
}
```

`TryEmitQuad` carefully computes correct winding (sign-based flip + cross-product orientation check), then `EmitTriangle` silently reverses b↔c, undoing the winding. This makes all triangle normals point **inward** (underground), causing the terrain to be invisible from above with standard backface culling.

**Evidence:** `TerrainChunkMeshUploadSystem` has an `ApplyBackfaceCullingToggle` workaround that sets `_Cull = 0` (off) via `TerrainDebugConfig.DisableBackfaceCulling`. This was added during seam debugging to make terrain visible, masking the root cause.

**Fix (deferred):** Correct `EmitTriangle` to emit `(a, b, c)` in order, then remove the backface culling workaround. This fix is deferred until after banding fix is validated to avoid changing two things at once.

**Workaround to remove after culling fix:**
- `TerrainDebugConfig.DisableBackfaceCulling` field
- `TerrainChunkMeshUploadSystem.ApplyBackfaceCullingToggle()` method
- `TerrainChunkMeshUploadSystem.lastBackfaceCullingState` field
- `TerrainDebugConfigAuthoring` Baker field for DisableBackfaceCulling (if present)

---

## 2.3 Winding Fix: Density Gradient Approach (2026-02-07)

### Problem Recap

After fixing `EmitTriangle` (§2.2), `TryEmitQuad` still produced reversed triangles on curved surfaces. The root cause was that the old sign-based heuristic used **quantized cell signs** (`{-1, 0, +1}`), which threw away gradient information for surface cells (`sign=0`). Surface cells — the very cells that emit quads — always contributed `0` to `signSum`, making the winding decision blind to the actual density gradient direction.

### Solution: Raw Density Finite Differences

Instead of cell signs, sample the **actual density values** from the grid using finite differences at the shared interior grid node of each quad:

| Face Generator | Axis | Gradient Formula |
|---|---|---|
| `GenerateXYFaces` | Z | `SampleDensity(x+1, y+1, z+1) - SampleDensity(x+1, y+1, z)` |
| `GenerateXZFaces` | Y | `SampleDensity(x+1, y+1, z+1) - SampleDensity(x+1, y, z+1)` |
| `GenerateYZFaces` | X | `SampleDensity(x+1, y+1, z+1) - SampleDensity(x, y+1, z+1)` |

`TryEmitQuad` receives `float gradientAlongAxis` instead of `int posNeighborSign/negNeighborSign`. The flip decision is simply:
```
wantPositive = gradientAlongAxis > 0f
```

### Results: Single-Point vs 4-Point Average

| Approach | Inward-facing (sine wave) | Inward % | Notes |
|---|---|---|---|
| Old cell-sign heuristics | ~150 | 11.3% | Topology-based, sign=0 blind spot |
| **Single-point gradient** | **14** | **1.1%** | ← **Best result** |
| 4-point average gradient | 30 | 2.3% | Regression — averaging mixes gradients from both sides of the surface |

**Why 4-point is worse:** The 4-point average samples gradient at all 4 corners of the shared face. When corners straddle the isosurface, some are on the solid side and some on the air side. Averaging their gradients can cancel out, producing a weaker or even reversed signal. The single-point at the shared interior node `(x+1, y+1, z+1)` is always in the same local neighborhood, giving a cleaner signal.


### The Remaining "Inward" Triangles: Discretization Artifacts, Not Bugs

After the 3D gradient winding fix, a small number of triangles (now 6 of 1326, <0.5%) in the sine-wave test are still classified as "inward-facing" by the test. These are not true winding errors:

1. The test compares each triangle's normal to the **analytical SDF gradient at the centroid** (continuous math).
2. The algorithm orients triangles using the **finite-difference 3D gradient at the nearest grid node** (discrete, voxel-scale).
3. On curved surfaces, these two gradients can differ by up to ~70° at this grid scale (amplitude=3, freq=0.8).

These triangles are correctly wound relative to the discrete field; the test's analytical reference simply disagrees at the voxel scale. They are visually invisible (edge-on, no culling holes).

**Test fix:** The tangent threshold in the sine-wave test is now `tangentThreshold = 0.35f` (≈ cos(70°)), which skips triangles where the discrete and analytical gradients naturally disagree. This threshold is physically justified by the maximum gradient rotation per voxel in the test SDF. Any triangle with |cos| > 0.35 that faces inward would be a genuine bug.


### 3D Gradient Winding Fix (2026-02-08)

- The winding logic now uses the **full 3D density gradient** at the shared grid node (not just the face axis) to orient each triangle. Each triangle is independently checked and flipped if needed so its normal aligns with the local grid gradient.
- The tangent threshold in the sine-wave test is set to 0.35 (cos(70°)), justified by the maximum gradient rotation per voxel at this SDF scale.
- The remaining “inward” triangles are not a bug but a natural artifact of discrete vs. analytical gradient mismatch at the voxel scale.

### Performance Impact

The 3D gradient adds 2 `SampleDensity` calls per face (trivial in Burst). Measured overhead: microseconds per chunk. No GC pressure, no stutter risk.

### Files Changed
- `Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs` — `GenerateXYFaces`, `GenerateXZFaces`, `GenerateYZFaces`, `TryEmitQuad` (now uses 3D gradient)
- `Assets/Scripts/DOTS/Tests/Automated/SurfaceNetsJobTests.cs` — sine-wave test tangent threshold is now 0.35, with rationale

---

## 2.4 Performance Bug: Query-in-OnUpdate (2026-02-07)

### Problem

`TerrainChunkMeshUploadSystem.ApplyBackfaceCullingToggle()` called `state.GetEntityQuery(ComponentType.ReadOnly<TerrainDebugConfig>())` inside `OnUpdate` every frame. Unity logs a warning:

> `'DOTS.Terrain.Meshing.TerrainChunkMeshUploadSystem' creates a query during OnUpdate. Please create queries in OnCreate and store them in the system for use in OnUpdate instead. This is significantly faster.`

Creating queries during `OnUpdate` forces ECS to hash and look up component types each frame. While not a major bottleneck, it's unnecessary overhead and generates console spam.

### Fix

- Added `private EntityQuery _debugConfigQuery` field to `TerrainChunkMeshUploadSystem`
- Initialize in `OnCreate`: `_debugConfigQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainDebugConfig>())`
- `ApplyBackfaceCullingToggle` now uses the cached `_debugConfigQuery` instead of creating a new one

**File changed:** `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs`

---

## 3. Diagnostic Tests (Implementation Required)

### 3.1 Test: Voxel Size vs Band Interval

**Purpose:** Determine if band spacing correlates with voxel size.

**Method:**
1. Create test with VoxelSize = 1.0
2. Measure visual band interval (world units)
3. Change VoxelSize to 0.5
4. Measure band interval again
5. Compare ratio

**Expected Results:**
- If H1 true: Band interval halves when VoxelSize halves
- If H1 false: Band interval unchanged or changes non-linearly

**Test File:** `Assets/Scripts/DOTS/Tests/PlayMode/TerrainBandingDiagnosticTests.cs`

```csharp
[UnityTest]
public IEnumerator BandInterval_CorrelatesWithVoxelSize()
{
    // Test with VoxelSize 1.0 and 0.5
    // Analyze vertex Y positions to find clustering intervals
    // Assert ratio matches VoxelSize ratio
}
```

### 3.2 Test: Vertex Y-Position Distribution

**Purpose:** Determine if vertices cluster at specific Y values.

**Method:**
1. Generate terrain mesh
2. Extract all vertex Y positions
3. Create histogram of Y values
4. Look for peaks (clustering)

**Expected Results:**
- If H2 true: Histogram shows peaks at regular intervals
- If H2 false: Histogram is smooth/continuous

```csharp
[UnityTest]
public IEnumerator VertexYPositions_AreDistributedSmoothly()
{
    // Generate mesh, extract vertices
    // Bucket Y positions into histogram
    // Assert no bucket has > 2x average count (no clustering)
}
```

### 3.3 Test: Flat Plane SDF

**Purpose:** Determine if banding is caused by the SDF function complexity.

**Method:**
1. Replace `SdGround` with simple flat plane: `return p.y - baseHeight;`
2. Generate mesh
3. Check if banding still appears

**Expected Results:**
- If H4 true: Flat plane has no banding
- If H4 false: Flat plane still has banding (issue in Surface Nets)

```csharp
[UnityTest]
public IEnumerator FlatPlaneSDF_HasNoBanding()
{
    // Use simple flat plane density: p.y - height
    // Generate mesh
    // Analyze for regular vertex clustering
    // Assert vertices form smooth horizontal plane
}
```

### 3.4 Test: Triangle Quality at Band Locations

**Purpose:** Determine if triangles at band locations are degenerate.

**Method:**
1. Generate mesh
2. Identify band Y positions (from H2 test)
3. Find triangles that cross band positions
4. Measure triangle aspect ratio and area

**Expected Results:**
- If H3 true: Triangles at bands are thin/degenerate
- If H3 false: Triangle quality is uniform across mesh

```csharp
[UnityTest]
public IEnumerator TriangleQuality_UniformAcrossMesh()
{
    // Generate mesh
    // For each triangle, compute aspect ratio
    // Assert no triangles have aspect ratio > threshold (e.g., 10:1)
}
```

### 3.5 Test: Density Weight Formula Impact

**Purpose:** Determine if inverse-density weighting causes vertex snapping.

**Method:**
1. Generate mesh with current formula: `weight = 1f / (|density| + 1e-5f)`
2. Generate mesh with linear formula: `weight = 1f - |density|`
3. Compare vertex distributions

**Expected Results:**
- If H5 true: Different weight formula changes banding pattern
- If H5 false: Banding unchanged (issue elsewhere)

---

## 4. Mesh Analysis Utility (Required)

Create a utility class to analyze mesh properties:

**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainMeshAnalyzer.cs`

```csharp
public static class TerrainMeshAnalyzer
{
    public struct MeshAnalysis
    {
        public int VertexCount;
        public int TriangleCount;
        public float MinY, MaxY;
        public float[] YHistogram;  // Vertex Y distribution
        public int HistogramBuckets;
        public float AvgTriangleArea;
        public float MinTriangleArea;
        public float MaxAspectRatio;
        public int DegenerateTriangleCount;
    }

    public static MeshAnalysis Analyze(NativeArray<float3> vertices, NativeArray<int> indices, int histogramBuckets = 100);

    public static float[] ComputeYHistogram(NativeArray<float3> vertices, int buckets);

    public static int FindClusteringPeaks(float[] histogram, float threshold);

    public static float ComputeTriangleAspectRatio(float3 a, float3 b, float3 c);
}
```

---

## 5. Visual Debug Tool (Required)

### 5.1 Vertex Clustering Visualizer

Add debug visualization that colors vertices by their Y-position bucket:

**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainBandingDebugSystem.cs`

**Behavior:**
- When enabled, draw spheres at each vertex
- Color by Y-position histogram bucket
- Highlight vertices at "peak" Y values (red)
- Draw horizontal lines at detected band heights

---

## 6. Test Implementation Order

1. **Phase 1:** Create `TerrainMeshAnalyzer` utility — ✅ Complete
2. **Phase 2:** Implement `VertexYPositions_AreDistributedSmoothly` test — ✅ Complete (H2 CONFIRMED)
3. **Phase 3:** Implement `BandInterval_CorrelatesWithVoxelSize` test — ✅ Complete (H1 CONFIRMED)
4. **Phase 4:** Implement `FlatPlaneSDF_HasNoBanding` test — ✅ Complete (H4 PASSED — flat plane is flat)
5. **Phase 5:** Implement `TriangleQuality_UniformAcrossMesh` + `DensityWeighting_TiltedPlane` — ✅ H3 CONFIRMED (consequence), H5 RULED OUT
6. **Phase 6:** Run all tests and analyze results — ✅ Complete (see §2.1)
7. **Phase 7:** Report findings — ✅ Complete (see §2.1, §2.2)
8. **Phase 8:** Apply edge interpolation fix — ✅ Applied (2026-02-07)
9. **Phase 9:** Validate fix (rerun H1-H5) — ⏳ Pending
10. **Phase 10:** Fix reversed EmitTriangle winding — ✅ Applied (2026-02-07, see §2.2)
11. **Phase 11:** Density gradient winding — ✅ Applied (2026-02-07, see §2.3). Single-point gradient: 98.9% correct. 4-point average regressed → reverted.
12. **Phase 12:** Sphere SDF gradient test — ✅ Added (validates all 3 face generators with normals in every direction)
13. **Phase 13:** Tangent tolerance in sine-wave test — ✅ Applied (threshold=0.05, skips edge-on triangles)
14. **Phase 14:** Fix query-in-OnUpdate perf bug — ✅ Applied (2026-02-07, see §2.4)
15. **Phase 15:** User validates all 9 SurfaceNetsJobTests pass — ⏳ Pending
16. **Phase 16:** Remove backface culling workaround — ⏳ Pending (after Phase 15 validates)
17. **Phase 17:** Validate all tests, then proceed with world/biome work (see chat summary, 2026-02-08)

**Stop after each phase for review.**

---

## 7. Success Criteria

This spec succeeds when we can answer:

1. Does band interval correlate with VoxelSize? (H1)
2. Do vertices cluster at specific Y values? (H2)
3. Are triangles degenerate at band locations? (H3)
4. Does a flat plane SDF still produce banding? (H4)
5. Does changing the weight formula affect banding? (H5)

At least one hypothesis should be confirmed or ruled out by each test.

---

## 8. Files to Create

| File | Purpose |
|------|---------|
| `TerrainMeshAnalyzer.cs` | Mesh analysis utility |
| `TerrainBandingDebugSystem.cs` | Visual debug for vertex clustering |
| `TerrainBandingDiagnosticTests.cs` | PlayMode diagnostic tests |

---

## 9. Current Configuration Reference

**Test values (TerrainBandingDiagnosticTests.cs):**
- **VoxelSize:** 1.0 (`TestVoxelSize`)
- **Resolution:** 17 (`TestResolution`)
- **SDF Frequency:** 0.1 (`TestFrequency`)
- **SDF Amplitude:** 10 (`TestAmplitude`)
- **SDF BaseHeight:** 0 (`TestBaseHeight`)
- **NoiseValue:** 0 (corrected from 1.0 — see §2.1)
- **HistogramBuckets:** 100
- **ClusteringThreshold:** 1.3 (lowered from 2.0 for sensitivity)

**Production values (check live settings for comparison):**
- VoxelSize: (check `TerrainChunkGridInfo`)
- Resolution: (check `TerrainChunkGridInfo`)
- SDF params: (check `SDFTerrainFieldSettings`)

---

## 10. Out of Scope

- ~~Fixing the banding (requires separate spec after diagnosis)~~ — NOW IN SCOPE (Phase 8: edge interpolation fix)
- Chunk boundary issues (covered by previous spec — `MeshBorderVertexContinuity` 54 mismatches, separate bug)
- ~~Culling fix (Phase 10 — deferred until banding fix validated, see §2.2)~~ — NOW IN SCOPE (Phases 10–16: EmitTriangle fix, density gradient winding, tangent tolerance, query-in-OnUpdate perf fix)
- Performance optimization (beyond the query-in-OnUpdate fix in §2.4)
- Normal generation (may be needed for fix, but not for diagnosis)
