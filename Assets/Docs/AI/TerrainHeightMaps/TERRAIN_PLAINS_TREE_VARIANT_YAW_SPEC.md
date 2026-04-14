# Plains Tree Variant + Yaw Spec

**Status:** ACTIVE
**Last Updated:** 2026-04-13
**Owner:** DOTS Terrain / Surface Scatter

---

## 1. Purpose

Enable the Plains tree system to:

1. Choose one of three tree mesh variants per accepted placement.
2. Apply deterministic random Y-axis rotation per tree instance.

This extends the current MVP tree pass, which currently uses a single mesh and identity rotation.

## 2. Scope

Included:

1. Tree placement data contract update to include deterministic yaw.
2. Deterministic `TreeTypeId` assignment for three plains variants (`0..2`).
3. Tree render config/bootstrap update to accept mesh variants.
4. Tree render system update to bucket and draw per variant mesh with Y rotation.
5. Automated tests for deterministic variant/yaw behavior.

## 3. Non-Goals

1. Full biome-aware tree family switching beyond the current plains-oriented tree pipeline.
2. Per-variant material overrides (single shared material remains supported).
3. Entities Graphics migration (retain current `Graphics.RenderMeshInstanced` path).
4. Runtime authoring UI beyond existing inspector fields.

## 4. Technical Design

### 4.1 Placement Record Contract

`TreePlacementRecord` gains:

- `YawRadians` (`float`): deterministic yaw in `[0, 2pi)`.

`TreeTypeId` remains `byte`, now actively used as plains variant index.

### 4.2 Deterministic Variant + Yaw Assignment

In `TreePlacementAlgorithm.GeneratePlacements`:

1. Reuse deterministic candidate hash (`worldSeed`, `chunkCoord`, `cellX`, `cellZ`).
2. Compute `TreeTypeId = hash % 3`.
3. Compute `YawRadians` from hash bits mapped to `[0, 2pi)`.

Determinism must hold across stream out/in cycles and for repeated generation calls.

### 4.3 Rendering Contract

`TreeRenderConfig` supports either:

1. `MeshVariants` (preferred; up to 3 used for this spec), or
2. legacy single `Mesh` fallback.

`TreeChunkRenderSystem`:

1. Prepares variant mesh list per frame.
2. Buckets instance matrices by `TreeTypeId`.
3. Uses `Quaternion.Euler(0, degrees(YawRadians), 0)` for each instance.
4. Submits one or more instanced draw batches (up to 1023 per call) per variant.
5. Computes per-variant world bounds for SRP culling safety.

### 4.4 Bootstrap Authoring

`TreeVisualBootstrap` exposes:

1. `treeMeshVariants` (`Mesh[]`) for plains variant assignment.
2. Existing `treeMesh` as fallback for backward compatibility.

## 5. Acceptance Criteria

1. Plains tree render path can draw all three provided tree meshes in runtime.
2. Tree variant choice is deterministic for fixed seed/chunk/candidate.
3. Tree yaw is deterministic and non-constant across placements.
4. Yaw is applied on Y-axis only.
5. Existing single-mesh fallback remains functional.
6. Existing scatter render contract tests still pass.

## 6. Implementation Checklist

- [x] Add `YawRadians` to `TreePlacementRecord`.
- [x] Add deterministic `TreeTypeId` (`0..2`) assignment in `TreePlacementAlgorithm`.
- [x] Add deterministic `YawRadians` assignment in `TreePlacementAlgorithm`.
- [x] Extend `TreeRenderConfig` with mesh variant list + legacy fallback.
- [x] Extend `TreeVisualBootstrap` inspector fields to wire variants.
- [x] Update `TreeChunkRenderSystem` to bucket by variant and apply yaw rotation.
- [x] Preserve begin-camera-render submission behavior.
- [x] Preserve stale submission state clearing semantics.
- [x] Add/extend tests for deterministic variant and yaw range.
- [x] Validate with workspace error check.

## 7. Related Docs

1. `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md`
2. `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_TREE_PLACEMENT_SPEC.md`
3. `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md`
