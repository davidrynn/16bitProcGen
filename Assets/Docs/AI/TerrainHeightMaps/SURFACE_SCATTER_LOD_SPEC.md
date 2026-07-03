# Surface Scatter Distance LOD Spec (Trees & Rocks)

**Status:** ACTIVE
**Last Updated:** 2026-06-11
**Owner:** Terrain / Rendering

---

## 1. Purpose

Cut scatter vertex load by swapping tree/rock instances to low-poly meshes beyond a configurable camera distance. Profiling ([../RENDER_PERF_PROFILE_REPORT.md](../RENDER_PERF_PROFILE_REPORT.md)) showed the Basic Terrain Scene is vertex-bound with **92% of frame vertices coming from scatter trees/rocks** (674 instances × ~1,420 verts). Unity `LODGroup` cannot be used — scatter renders via `Graphics.RenderMeshInstanced` with no GameObjects — so LOD selection happens inside the existing render-system instance loop ("Option A").

## 2. Scope

- `TreeChunkRenderSystem` / `RockChunkRenderSystem`: route each instance to a near or far mesh bucket by camera distance before instanced submission.
- `TreeRenderConfig` / `RockRenderConfig`: optional far-LOD mesh list + swap distance.
- `TreeVisualBootstrap` / `RockVisualBootstrap`: inspector fields for the above.
- Shared pure selection logic in `DOTS.Terrain.SurfaceScatter` (testable without a World).

## 3. Non-Goals

- Billboard/impostor rendering (see relic billboard spec for that pattern).
- Migration to Entities.Graphics (post-MVP path already noted in `TreeChunkRenderSystem` header).
- Grass or other scatter families; chunk-level LOD3 culling changes (`CulledScatterLod` stays as-is).
- Authoring the low-poly meshes themselves (asset task; system is inert until meshes are assigned).

## 4. Design

### 4.1 LOD model

Two stateless LOD levels per mesh variant, selected per instance per frame:

- **Near (LOD 0):** existing `MeshVariants[i]` — unchanged behavior.
- **Far (LOD 1):** `LodMeshVariants[i]`, drawn when `distanceSq(camera, instance) > LodSwapDistance²`.

Selection is a pure function of continuous camera distance, recomputed every frame. **No hysteresis**: unlike `RelicLodSelectionSystem` (which mutates per-entity `MaterialMeshInfo` and needs hysteresis to avoid churn), this path rebuilds instance buckets every frame anyway, so boundary re-evaluation is free and cannot oscillate.

### 4.2 Config contract

| Field | Type | Semantics |
|---|---|---|
| `LodMeshVariants` | `Mesh[]` | Parallel **by source index** to `MeshVariants`. Entry may be null → that variant never swaps (always near). |
| `LodSwapDistance` | `float` | World-space distance to camera. `<= 0` disables LOD entirely. |

Alignment rule: `CollectVariantMeshes` compacts null `MeshVariants` entries; the far mesh follows its source entry through compaction (if `MeshVariants[3]` lands in variant slot 1, `LodMeshVariants[3]` is the far mesh for slot 1). Legacy single-mesh fallback (`config.Mesh`) takes `LodMeshVariants[0]` as its far mesh when present.

**Authoring auto-pair (editor-only, added 2026-06-11):** index alignment no longer has to be maintained by hand. `TreeVisualBootstrap` / `RockVisualBootstrap` auto-fill empty `LodMeshVariants` slots with the sub-asset named `<NearMeshName>_Far` from the same model file (e.g. `Boulder_01` → `Boulder_01_Far` inside `Boulders.fbx`), via `OnValidate` and a "Auto-Pair Far LOD Meshes" context-menu command. Rules: manual assignments always win; a near variant with no `_Far` sibling stays null (always near); pairing is editor-only (`SurfaceScatterLodAuthoringUtility` needs AssetDatabase — unreferenced sub-assets aren't addressable at runtime), so the runtime config contract above is unchanged. Pure pairing logic: `SurfaceScatterLodUtility.AutoPairFarMeshes`, covered by `SurfaceScatterLodUtilityTests`.

### 4.3 Bucket layout

Pending-matrix buckets double from `MaxVariants` (8) to `MaxVariants × 2` (16): near block `[0..7]`, far block `[8..15]` (far bucket = `variantIndex + MaxVariants`). `SubmitToCamera` and world-bounds building iterate all buckets, skipping empty ones — instancing efficiency per drawn mesh is unchanged.

### 4.4 Fallback rules (zero-regression requirement)

An unconfigured project behaves byte-for-byte like today:

1. `LodSwapDistance <= 0` → all instances near.
2. Far mesh null for a variant → that variant's instances always near.
3. No camera available (`Camera.main` null, e.g. headless tests) → all instances near.

### 4.5 Tree grounding offset

Trees ground instances by mesh-bottom (`-mesh.bounds.min.y`). The offset is computed from the **mesh actually drawn** (near or far). Authoring guidance: keep far-mesh bounds approximately matching the near mesh to avoid a vertical pop at the swap distance.

## 5. Related Docs

- [../RENDER_PERF_PROFILE_REPORT.md](../RENDER_PERF_PROFILE_REPORT.md) — profiling evidence motivating this spec
- [TERRAIN_SURFACE_SCATTER_SPEC.md](TERRAIN_SURFACE_SCATTER_SPEC.md) — scatter runtime contract
- [TERRAIN_PLAINS_TREE_VARIANT_YAW_SPEC.md](TERRAIN_PLAINS_TREE_VARIANT_YAW_SPEC.md) — variant selection this spec extends
- [../STRUCTURE_PLACEMENT/RELIC_LOD_IMPOSTOR_SPEC.md](../STRUCTURE_PLACEMENT/RELIC_LOD_IMPOSTOR_SPEC.md) — Entities.Graphics LOD precedent (different render path, same goal)

## 6. Acceptance Criteria

1. EditMode tests pass: pure LOD selection (disabled / below / above threshold), bucket-index math, far-mesh registration incl. compaction alignment, and all pre-existing scatter contract tests unchanged.
2. With no LOD config assigned, rendering output and test results are identical to before the change.
3. With low-poly far meshes assigned and a swap distance tuned in Play mode, a profiler re-capture shows scatter vertex count drops materially (target: >40% reduction in scatter vertices at typical camera positions).
4. No visual artifacts at the swap boundary other than the expected mesh change (no floating/buried trees).
