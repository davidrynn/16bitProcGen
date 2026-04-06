# Terrain Voxel + Chunk Coordinate Edit Spec

**Date:** 2026-04-05
**Status:** PROPOSED
**Owner:** Terrain / DOTS

---

## 1. Purpose

Define a Minecraft-style terrain modification mode for the SDF terrain pipeline where edits are cube-based, snapped to a voxel/chunk-aligned grid, and deterministic across chunk boundaries.

This spec extends the current SDF edit workflow (raycast input -> SDFEdit buffer -> density resample -> Surface Nets rebuild) without reintroducing legacy heightmap systems.

---

## 2. Goals

1. Add an edit mode that applies axis-aligned cubes instead of spheres.
2. Snap cube placement to a discrete lattice anchored in either global space or chunk-local space.
3. Support configurable cube size in the range `[0.25, 1.0] * chunkStride`.
4. Keep edits seam-safe across neighboring chunks.
5. Preserve DOTS-first architecture and Burst-safe data flow.

---

## 3. Non-Goals

1. No marching-cubes replacement (keep Surface Nets).
2. No save/load persistence changes in this phase.
3. No material painting or biome metadata editing.
4. No conversion to strict block mesh rendering (still SDF + Surface Nets).

---

## 4. Terminology and Coordinate Model

### 4.1 Existing Definitions

- `chunkResolution`: samples per axis used by `TerrainChunkGridInfo.Resolution`.
- `voxelSize`: world-space spacing between adjacent samples (`TerrainChunkGridInfo.VoxelSize`).
- `chunkStride`: world span of a chunk in X/Z.

For this project, chunk stride is:

`chunkStride = (chunkResolution - 1) * voxelSize`

This matches chunk placement in `TerrainBootstrapAuthoring`.

### 4.2 Edit Lattice Cell Size

`cellSize = chunkStride * editCellFraction`

Where:
- `editCellFraction` is clamped to `[0.25, 1.0]`.
- For stable density sampling, `cellSize` should be quantized to integer multiples of `voxelSize`.

Recommended quantization:

`cellSize = max(voxelSize, round(cellSize / voxelSize) * voxelSize)`

### 4.3 Snap Spaces

Two snap spaces are supported:

1. **GlobalSnap** (default)
- Anchor lattice to a world anchor (typically `(0,0,0)`).
- Produces globally stable coordinates independent of chunk entity selection.

2. **ChunkLocalSnap**
- Anchor lattice to the owning chunk `TerrainChunkBounds.WorldOrigin`.
- Placement is local to chunk coordinates and useful for per-chunk tooling.

---

## 5. Data Model Changes

## 5.1 Extend Edit Shape Model

Update `SDFEdit` to support shape-aware edits.

Current:
- `Center`
- `Radius`
- `Operation`

Proposed additions:
- `Shape` (`Sphere`, `Box`)
- `HalfExtents` (`float3`) used when `Shape == Box`

Keep `Radius` for backward compatibility and sphere mode.

### 5.2 New Enum Types

- `SDFEditShape`
  - `Sphere = 0`
  - `Box = 1`

- `TerrainEditPlacementMode`
  - `FreeSphere = 0` (current behavior)
  - `SnappedCube = 1`

- `TerrainEditSnapSpace`
  - `Global = 0`
  - `ChunkLocal = 1`

### 5.3 Config Surface

Expose edit settings through a singleton config component (or mirror from `ProjectFeatureConfig`):

- `PlacementMode`
- `SnapSpace`
- `EditCellFraction` (`0.25..1.0`)
- `GlobalSnapAnchor` (`float3`, default zero)
- `CubeDepthCells` (optional, default `1`)

`CubeDepthCells` enables placing 1x1xN cells along camera forward (optional extension).

---

## 6. Placement and Snapping Rules

## 6.1 Input Hit Point

Use current center-ray DOTS physics hit path from `TerrainEditInputSystem`.

Let:
- `pHit`: world hit point (or fallback point)
- `s`: computed `cellSize`

### 6.2 Global Snap Formula

`pSnapped = anchor + round((pHit - anchor) / s) * s`

Where `anchor = GlobalSnapAnchor`.

### 6.3 Chunk-Local Snap Formula

1. Find owning chunk by point-in-AABB against `TerrainChunkBounds` and grid extent from `TerrainChunkGridInfo`.
2. Convert to local:
   - `pLocal = pHit - chunkOrigin`
3. Snap to cell-center in local lattice:
   - `cell = floor(pLocal / s)`
   - `pSnapped = chunkOrigin + (cell + 0.5) * s`

If no owning chunk is found, fallback to GlobalSnap.

### 6.4 Cube Dimensions

For a cubic edit:

- `halfExtents = 0.5 * s` on all axes for single-cell cube.
- If `CubeDepthCells > 1`, extend one axis by `CubeDepthCells * s` (optional later phase).

### 6.5 Add/Subtract Semantics

Keep existing CSG semantics:
- Add -> `OpUnion(base, cube)`
- Subtract -> `OpSubtraction(base, cube)`

Where `cube = SdBox(worldPos - pSnapped, halfExtents)`.

---

## 7. System Changes

## 7.1 TerrainEditInputSystem

Add branch by `PlacementMode`:

- `FreeSphere`: unchanged behavior.
- `SnappedCube`:
  1. Compute `cellSize` from chunk metrics.
  2. Snap hit point based on selected snap space.
  3. Push `SDFEdit` with `Shape=Box`, `Center=pSnapped`, `HalfExtents`.

Also keep current cooldown and debug logs, with extra log fields:
- mode
- snapSpace
- cellSize
- snapped center
- owning chunk coord (if local)

## 7.2 SDFTerrainField

Update edit application loop:

1. Switch on `edit.Shape`.
2. Sphere path uses existing `SdSphere`.
3. Box path uses `SdBox`.
4. Apply add/subtract operations unchanged.

## 7.3 TerrainChunkEditUtility

Current dirty-marking uses sphere-vs-AABB intersection.

Proposed:
- Add box-vs-AABB intersection for box edits.
- Keep conservative fallback (mark all chunks) only for invalid edit payloads.

The dirty region must include neighboring chunks so seam overlap samples rebuild consistently.

---

## 8. Chunk Coordinate Integration

## 8.1 Deriving Chunk Coordinate from World Position

For globally anchored chunk grid in X/Z:

`chunkCoord.x = floor((pWorld.x - chunkGridAnchor.x) / chunkStride)`

`chunkCoord.z = floor((pWorld.z - chunkGridAnchor.z) / chunkStride)`

Use existing `TerrainChunk.ChunkCoord` when mapping to concrete entities.

## 8.2 Preferred Runtime Lookup

Avoid assuming perfect regular grid at runtime. Use entity chunk bounds first:

1. Query chunks with `TerrainChunk`, `TerrainChunkBounds`, `TerrainChunkGridInfo`.
2. Choose chunk containing `pHit` (AABB test).
3. Use its `WorldOrigin` and `ChunkCoord` for local snapping metadata.

This keeps behavior correct if streaming or offsets change.

---

## 9. UX / Controls

1. Keep current add/subtract bindings (RMB add, LMB subtract, E/Q keys).
2. Add toggle key for placement mode (example: `T`).
3. Add toggle key for snap space (example: `G`).
4. Optional: cycle `editCellFraction` presets `{0.25, 0.5, 0.75, 1.0}` via mouse wheel or bracket keys.

Defaults:
- Placement mode: `SnappedCube`
- Snap space: `Global`
- Fraction: `0.25`

---

## 10. Debug and Diagnostics

Extend terrain edit debug output with structured data:

- `op` (`Add`/`Subtract`)
- `shape`
- `snapSpace`
- `cellSize`
- `hitPos`
- `snappedPos`
- `chunkCoord`

Optional debug gizmos:
- Draw wire cube for last snapped edit cell.
- Draw affected chunk AABBs in a distinct color.

Use existing `DebugSettings.LogTerrainEdit` path (no direct `Debug.Log` in systems).

---

## 11. Test Plan (SPEC -> TEST -> CODE)

## 11.1 Unit Tests

1. Snap math
- Global snap produces expected lattice points for positive and negative coordinates.
- Chunk-local snap is stable at boundaries.

2. Shape sampling
- `SdBox` edit subtracts interior points and preserves exterior behavior per op rules.

3. Dirty marking
- Box touching chunk edge marks both touched chunk and neighbor.

## 11.2 Integration Tests

1. Place subtract cube near chunk seam -> both chunks rebuild -> no seam hole.
2. Place add cube across seam -> manifold surface remains continuous.
3. Switch between `Global` and `ChunkLocal` snap and verify deterministic snapped centers.

## 11.3 Runtime Validation

In PlayMode with edit debug enabled:

1. Click repeatedly while moving camera.
2. Verify snapped centers stay on lattice.
3. Verify cube size preset changes are reflected in world.

---

## 12. Rollout Plan

## Phase A: Data + Sampling

1. Extend `SDFEdit` for shape support.
2. Update `SDFTerrainField` shape switch.
3. Keep input system outputting spheres (no behavior change yet).

## Phase B: Snapped Cube Input

1. Add placement mode config.
2. Implement global snap cube generation.
3. Add debug output for snapped data.

## Phase C: Chunk-Local and Dirty Optimization

1. Add chunk lookup + local snap mode.
2. Add box-vs-AABB dirty marking.
3. Add seam-focused automated tests.

## Phase D: UX polish

1. Keybind toggles and preset cycling.
2. Optional visual cube preview.

---

## 13. Acceptance Criteria

1. User can enable snapped cube mode and perform add/subtract edits.
2. Cube centers always align to configured lattice.
3. Cube size is configurable from `0.25` to `1.0` of chunk stride.
4. Edits at chunk boundaries rebuild all affected chunks and do not introduce seam cracks.
5. Existing sphere mode remains available for debugging/legacy parity.

---

## 14. Risks and Mitigations

1. **Risk:** Non-quantized cell size causes fuzzy or unstable edit boundaries.
- **Mitigation:** Quantize to `voxelSize` multiples.

2. **Risk:** Chunk-local mode jumps at chunk boundaries.
- **Mitigation:** Explicitly document mode semantics; default to global snap.

3. **Risk:** Rebuild cost spikes when marking too many chunks.
- **Mitigation:** Add precise box/AABB dirty intersection.

4. **Risk:** Input complexity/confusion.
- **Mitigation:** Keep sensible defaults and add debug logs + optional HUD labels.

---

## 15. File Targets (Planned)

Primary implementation targets:

- `Assets/Scripts/DOTS/Terrain/SDF/SDFEdit.cs`
- `Assets/Scripts/DOTS/Terrain/SDF/SDFTerrainField.cs`
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs`
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkEditUtility.cs`

Possible config targets:

- `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs`
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`

Tests:

- `Assets/Scripts/DOTS/Tests/Automated/TerrainChunkEditUtilityTests.cs`
- New tests for snap math and box edit behavior in DOTS terrain test assemblies.
