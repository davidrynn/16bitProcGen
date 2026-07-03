# Terrain Binary Edit Layer Spec

**Date:** 2026-05-14
**Status:** PROPOSED
**Owner:** Terrain / DOTS
**Supersedes:** n/a (additive to TERRAIN_VOXEL_CHUNK_EDIT_SPEC.md)

---

## 1. Purpose

Add a low-cost, visually boxy terrain edit layer on top of the existing SDF + Surface Nets pipeline
**without modifying or replacing it**. Player edits in this mode write to a per-chunk binary voxel
mask. A dedicated face-extraction system generates edit geometry directly from the mask, producing
Minecraft-style hard-edged quads at voxel-cell boundaries.

This is a purely additive feature. The SDF path (`SDFEdit` buffer → density resample → Surface Nets)
remains untouched and fully accessible for complex sculpting or future procedural edits.

---

## 2. Goals

1. Player edits in `BinaryBox` mode produce axis-aligned, hard-edged quads — no surface interpolation rounding.
2. Edit operations do **not** trigger `TerrainChunkNeedsDensityRebuild`. The terrain mesh is never invalidated by player edits in this mode.
3. Edit mesh rebuild cost is O(edit_face_area), not O(chunk_volume).
4. Binary edit state is trivially fast to restore on load — no re-evaluation of SDF required.
5. Existing `FreeSphere` and `SnappedCube` (SDF) modes remain available and unchanged.
6. Feature can be removed or toggled off at `ProjectFeatureConfig` level with no side-effects on SDF path.

---

## 3. Non-Goals

1. No changes to `TerrainChunkDensitySamplingSystem`, `TerrainChunkMeshBuildSystem`, or Surface Nets.
2. No smooth blending between binary edit faces and SDF terrain mesh — hard seam is acceptable and visually intentional (retro aesthetic).
3. No undo/redo within this spec (operation log deferred to `PERSISTENCE_SPEC.md`).
4. No multi-chunk spanning single binary edit in this phase.
5. No greedy mesh optimisation in this phase (naive face-pair extraction is sufficient; greedy merging is a later pass).

---

## 4. Architecture Overview

```
┌──────────────────────────────────────────────────────┐
│ TerrainEditInputSystem                               │
│  PlacementMode.BinaryBox ─────► writes TerrainBinaryEditMask  (NEW)
│  PlacementMode.FreeSphere/SnappedCube ─► SDFEdit buffer (unchanged)
└──────────────────────────────────────────────────────┘

Per-chunk state (NEW):
  TerrainBinaryEditMask    — NativeArray<byte>  (1 byte/voxel: 0=empty 1=carved 2=added)
  TerrainChunkNeedsBinaryEditMeshBuild  — zero-size tag component

┌──────────────────────────────────────────────────────┐
│ TerrainChunkBinaryEditMeshSystem    (NEW)            │
│  Reads: TerrainBinaryEditMask + TerrainChunkDensity  │
│  Emits: edit mesh entity (child of chunk)            │
└──────────────────────────────────────────────────────┘

Render:
  Terrain mesh entity   ← existing pipeline, never rebuilt on edit
  Edit mesh entity      ← new, rebuilt only when mask changes
  Both rendered via Entities Graphics, same material or separate materials
```

---

## 5. Data Model

### 5.1 TerrainBinaryEditMask Component

```csharp
namespace DOTS.Terrain
{
    /// <summary>
    /// Per-chunk binary edit state. One byte per voxel in the density grid
    /// (resolution+1)³ matching TerrainChunkDensityGridInfo.Resolution.
    /// Values: 0 = unedited, 1 = carved (subtracted), 2 = added (placed).
    /// </summary>
    public struct TerrainBinaryEditMask : IComponentData, IDisposable
    {
        public NativeArray<byte> Values;  // Allocator.Persistent
        public int3 Resolution;

        public bool IsCreated => Values.IsCreated;

        public void Dispose()
        {
            if (Values.IsCreated) Values.Dispose();
        }
    }
}
```

**Why 1 byte per voxel instead of bit-packing:** Burst operates cleanly on byte arrays without
alignment complications. At 16³ resolution this is 4 096 bytes per edited chunk — acceptable.
Bit-packing can be introduced later if memory pressure materialises.

### 5.2 TerrainChunkNeedsBinaryEditMeshBuild Tag

```csharp
public struct TerrainChunkNeedsBinaryEditMeshBuild : IComponentData { }
```

Mirrors the existing `TerrainChunkNeedsDensityRebuild` / `TerrainChunkNeedsMeshBuild` pattern.
Added by `TerrainEditInputSystem` when a binary edit is applied; removed by
`TerrainChunkBinaryEditMeshSystem` after the mesh is built.

### 5.3 TerrainChunkBinaryEditMeshRef Component

```csharp
/// <summary>Entity that holds the edit mesh RenderMeshArray for this chunk.</summary>
public struct TerrainChunkBinaryEditMeshRef : IComponentData
{
    public Entity MeshEntity;
}
```

Each chunk that has binary edits owns one child entity carrying the edit `Mesh`. This keeps
rendering responsibilities separate from terrain mesh entities and avoids submesh bookkeeping.

### 5.4 PlacementMode Extension

Add to existing `TerrainEditPlacementMode` enum:

```csharp
public enum TerrainEditPlacementMode : byte
{
    FreeSphere  = 0,
    SnappedCube = 1,
    BinaryBox   = 2   // NEW — routes to binary mask, not SDFEdit buffer
}
```

---

## 6. Edit Input Flow (BinaryBox Mode)

In `TerrainEditInputSystem.TryBuildEdit` (or a sibling helper), when
`settings.PlacementMode == TerrainEditPlacementMode.BinaryBox`:

1. Use existing snapping path (chunk-local or global) to produce `snappedCenter` and `halfExtents`.
2. Compute the integer voxel range covered by the box:
   ```
   voxelMin = floor((snappedCenter - halfExtents - chunkOrigin) / voxelSize)
   voxelMax = ceil ((snappedCenter + halfExtents - chunkOrigin) / voxelSize)
   Clamp to [0, resolution)
   ```
3. For each voxel (x, y, z) in the range, set `mask[idx] = 1` (carved) or `2` (added).
4. Add `TerrainChunkNeedsBinaryEditMeshBuild` to the affected chunk entity.
5. Do **not** push an `SDFEdit`. Do **not** add `TerrainChunkNeedsDensityRebuild`.

**Affected chunks:** only the chunk(s) whose voxel ranges overlap the edit AABB. Use existing
`TerrainChunkEditUtility` AABB intersection to find them.

---

## 7. Face Extraction System

### 7.1 TerrainChunkBinaryEditMeshSystem

```
[UpdateInGroup(SimulationSystemGroup)]
[UpdateAfter(TerrainChunkMeshUploadSystem)]
[DisableAutoCreation]
```

**Why after upload system:** terrain mesh for the chunk is already final before edit mesh is built,
avoiding a one-frame flicker where terrain mesh is absent.

**Per-frame work:**
1. Query chunks with `TerrainChunkNeedsBinaryEditMeshBuild`.
2. For each such chunk, call `BuildEditMesh(chunk, mask, densityBlob)`.
3. Upload result to the chunk's edit mesh child entity.
4. Remove `TerrainChunkNeedsBinaryEditMeshBuild`.

### 7.2 Face Extraction Rules

For each voxel at flat index `i` with 3D coord `(x, y, z)`:

**Carved voxel (`mask[i] == 1`):**
- For each of the 6 face directions `(±X, ±Y, ±Z)`:
  - Let `n` = neighbor voxel index in that direction.
  - If neighbor is in bounds AND `mask[n] != 1` (not also carved):
    - Check terrain density: `density[n] < 0` → terrain at neighbor is solid.
    - If solid: emit a quad on the face of voxel `i` facing toward `n`.
    - This exposes the wall between carved air and unmodified solid terrain.

**Added voxel (`mask[i] == 2`):**
- For each of the 6 face directions:
  - Let `n` = neighbor.
  - If neighbor is out-of-bounds, or `mask[n] != 2`:
    - Check terrain density: `density[n] >= 0` → neighbor is air (or terrain is empty there).
    - If air: emit a quad on the face of voxel `i` facing toward `n`.
    - This closes the external face of the placed block.

**Quad geometry:** each quad is two triangles. Vertex positions are at the four corners of the
voxel face plane, exactly on the cell boundary — no sub-voxel interpolation.

### 7.3 Burst Job

```csharp
[BurstCompile]
public struct BinaryEditFaceExtractionJob : IJob
{
    [ReadOnly] public NativeArray<byte>  Mask;
    [ReadOnly] public NativeArray<float> Densities;
    public int3   Resolution;
    public float  VoxelSize;
    public float3 ChunkOrigin;
    public NativeList<float3> Vertices;   // output
    public NativeList<int>    Triangles;  // output
}
```

Burst-safe, no managed allocations. `NativeList` is pre-allocated with an estimated upper bound
(`6 * editVoxelCount * 4` vertices) and trimmed after.

---

## 8. Rendering

### 8.1 Edit Mesh Entity Setup

On first binary edit to a chunk, `TerrainChunkBinaryEditMeshSystem` creates a child entity:

```csharp
var meshEntity = entityManager.CreateEntity();
entityManager.AddComponentData(meshEntity, new LocalTransform { /* zero, world space */ });
entityManager.AddComponentData(meshEntity, new RenderBounds { ... });
entityManager.AddSharedComponent(meshEntity, new RenderMeshArray(...));
// Store reference on chunk entity
entityManager.AddComponentData(chunkEntity, new TerrainChunkBinaryEditMeshRef { MeshEntity = meshEntity });
```

The edit mesh uses the same terrain material as the Surface Nets mesh. A separate material is
deferred — intentional hard seam is acceptable for retro style; material matching is a later pass.

### 8.2 Mesh Upload

Use `Mesh.MeshDataArray` to avoid GC, matching the existing `TerrainChunkMeshUploadSystem` pattern.
Upload happens on the main thread after the Burst job completes (same frame, not deferred).

### 8.3 Draw Call Cost

One additional draw call per chunk that has binary edits. Unedited chunks have zero overhead.
At typical play distances (8–16 loaded chunks), this is 0–16 additional draw calls — negligible.

---

## 9. Persistence

Binary state is stored separately from the SDF operation log.

**Save:** serialize `TerrainBinaryEditMask.Values` as a flat `byte[]` per chunk. Chunk is identified
by its `TerrainChunk.ChunkCoord`. No re-evaluation needed on load.

**Load:** deserialize `byte[]` → `TerrainBinaryEditMask`, add
`TerrainChunkNeedsBinaryEditMeshBuild` tag → system rebuilds edit mesh immediately.
No density resampling, no Surface Nets pass.

**Relationship to PERSISTENCE_SPEC.md:** this spec defines the binary state format; the edit journal
(for undo/redo) and world save file layout remain under `PERSISTENCE_SPEC.md`. The binary mask IS
the save state for binary edits — no journal replay required on load.

**SDF edits (existing path):** persist the `SDFEdit` list as before; require density + meshing
replay on load. The two paths are independent.

---

## 10. Performance Summary

| Operation | SDF path (existing) | Binary path (this spec) |
|-----------|--------------------|-----------------------|
| Edit trigger cost | O(chunk_volume) density + Surface Nets per affected chunk | O(edit_face_area) face extraction only |
| Terrain mesh invalidated by edit? | Yes | No |
| Load/restore cost | Re-run all SDF edits + density + Surface Nets | Upload mask bytes + O(face_area) extraction |
| Memory per edited chunk | SDFEdit list (compact) | ~4 KB mask (16³ chunk) |
| Draw calls added | 0 (merged into terrain mesh) | 1 per edited chunk |

For a 16³ chunk with a 3×3 edit, face extraction processes at most ~54 voxel faces vs. 4 096 voxels
for the SDF density resample. Terrain mesh is never touched after initial load.

---

## 11. Test Plan

### 11.1 Unit Tests

| Test | Expectation |
|------|-------------|
| Carve voxel (1,1,1), neighbor (2,1,1) is solid terrain | Quad emitted on +X face of (1,1,1) |
| Carve voxel (1,1,1), neighbor (2,1,1) is also carved | No quad emitted |
| Add voxel (1,1,1), neighbor (0,1,1) is air | Quad emitted on -X face |
| Carve at chunk boundary voxel | Only in-bounds faces processed; no out-of-bounds access |
| BinaryBox edit write: 3×3 box at chunk center | Correct range of mask bytes set to 1 |
| Load/save round-trip | mask bytes → serialize → deserialize → identical mask |

### 11.2 Integration Tests

| Test | Expectation |
|------|-------------|
| BinaryBox subtract near chunk seam | Terrain mesh not rebuilt; edit mesh appears on correct chunk(s) |
| SDF edit and BinaryBox edit in same session | Both meshes visible; terrain mesh rebuilt only for SDF edit |
| Switch PlacementMode FreeSphere → BinaryBox → FreeSphere | No mask corruption; SDF path unaffected |

### 11.3 Play Mode Validation

1. Enter edit mode (Tab), select `BinaryBox` (new key, e.g., `B`).
2. Left-click to carve — verify hard-edged square hole, no rounded corners.
3. Right-click to place — verify hard-edged block appears.
4. Verify carving does not trigger chunk density rebuild (check log: no
   `TerrainChunkNeedsDensityRebuild` message after binary edit).
5. Reload scene — verify edits persist and no SDF replay occurs.

---

## 12. Rollout Plan

### Phase A — Data and Routing (no visible change to SDF path)

1. Add `TerrainBinaryEditMask`, `TerrainChunkNeedsBinaryEditMeshBuild`,
   `TerrainChunkBinaryEditMeshRef` components.
   - Mask allocated at `densityGridResolution` (`grid.Resolution + int3(1)`), not `grid.Resolution`.
2. Add `PlacementMode.BinaryBox` to enum.
3. Add routing branch in `TerrainEditInputSystem` — writes mask, does not push SDFEdit.
   - Clamp edit range to each chunk's own voxel grid bounds.
4. Add `TerrainChunkBinaryEditMask` cleanup to chunk unload path — dispose `NativeArray` and
   destroy child mesh entity before chunk entity is removed.
5. Unit-test mask write logic and cleanup path.

### Phase B — Face Extraction + Mesh Upload

1. Implement `BinaryEditFaceExtractionJob`.
2. Implement `TerrainChunkBinaryEditMeshSystem` (create child entity, upload mesh).
3. Integration test: carve in BinaryBox mode, verify hard-edged quad appears.

### Phase C — Polish and Persistence

1. Persist/restore binary mask (serialize chunk mask on save, restore on load).
2. Add keybind toggle for BinaryBox mode.
3. Match edit mesh material to terrain mesh material.
4. Play Mode validation checklist.

---

## 13. Acceptance Criteria

1. Carving in `BinaryBox` mode produces a square hole with no rounded corners at any voxel size.
2. No density resampling or Surface Nets pass is triggered by a binary edit.
3. Switching between `BinaryBox` and `SnappedCube` modes works at runtime without artefacts.
4. Binary edit state survives a scene reload without re-running any SDF computation.
5. Unedited chunks have zero performance or memory overhead from this feature.
6. All existing SDF edit automated tests pass unchanged.

---

## 14. Known Constraints and Mitigations

| Risk / Constraint | Policy |
|-------------------|--------|
| **Interior terrain mesh bleeds through carved regions** — the SDF density is unchanged, so Surface Nets geometry still exists inside carved voxels. Visible when looking into a carved area at an angle or from inside a tunnel. | **Phase 1 scope: surface-only edits.** Binary edits that go deeper than ~1–2 voxels below the visible surface should fall back to the SDF path (which rebuilds the terrain mesh). A `MaxBinaryEditDepthCells` setting controls this. Deep edits (tunnels, caves) are deferred to a follow-on spec that addresses full mesh invalidation. |
| **`TerrainBinaryEditMask` NativeArray leaks on chunk unload** | `TerrainChunkUnloadSystem` (or equivalent streaming teardown) must query all entities with `TerrainBinaryEditMask` being destroyed and call `.Dispose()`. Similarly, `TerrainChunkBinaryEditMeshRef.MeshEntity` must be destroyed via ECB before the parent chunk entity is removed. Add explicit cleanup step to Phase A. |
| **Mask resolution must match density blob resolution** — the density blob uses `resolution + 1` per axis for seam stitching. The mask must use the same dimensions. | Mask is allocated at `densityGridResolution = grid.Resolution + int3(1)`, not `grid.Resolution`. `TerrainChunkDensityGridInfo.Resolution` is the density grid resolution (already `+1`); use that directly. Assert at edit time: `mask.Resolution.Equals(densityGrid.Resolution)`. |
| **Cross-chunk edits write incorrect voxel ranges** — an edit straddling two chunks must write only the voxels within each chunk's own grid to each chunk's mask. | During mask write, clamp the global voxel range to `[chunkOrigin, chunkOrigin + densityResolution)` for each affected chunk independently. |
| **Face extraction when `TerrainChunkDensity` is absent** | If the chunk has no density blob yet, skip face extraction and re-add `TerrainChunkNeedsBinaryEditMeshBuild` — it will retry next frame after density is ready. |
| Seam at binary/SDF mesh boundary is visually jarring | Acceptable in retro context; material matching deferred to Phase C |
| Out-of-bounds voxel access during face extraction | Clamp all neighbor lookups; unit tests cover boundary voxels |
| Two draw calls per edited chunk hurts batching | Profile before optimising; merge submesh or GPU instancing can reduce later |
| BinaryBox edit accidentally triggers SDF dirty path | Unit test: assert no `TerrainChunkNeedsDensityRebuild` added after BinaryBox edit |

---

## 15. File Targets

**New files:**
- `Assets/Scripts/DOTS/Terrain/SDF/TerrainBinaryEditMask.cs`
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkBinaryEditMeshSystem.cs`
- `Assets/Scripts/DOTS/Tests/EditMode/TerrainBinaryEditMaskTests.cs`

**Modified files:**
- `Assets/Scripts/DOTS/Terrain/SDF/SDFEdit.cs` — add `BinaryBox` to `TerrainEditPlacementMode`
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs` — add routing branch
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` — register new system
- `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` — add feature flag

**Unchanged (explicitly):**
- `TerrainChunkDensitySamplingSystem.cs`
- `TerrainChunkMeshBuildSystem.cs`
- `SurfaceNets.cs`
- `SDFTerrainField.cs`
- `SDFMath.cs`
