# DOTS Terrain LOD Plan

Date: 2026-04-06
Scope: SDF + Surface Nets terrain pipeline in DOTS/ECS

## 1. Goals

- Add chunk-based LOD that scales terrain rendering cost with distance.
- Preserve destructible terrain behavior and edit correctness.
- Keep compatibility with current staged pipeline:
  - Density sampling
  - Mesh build
  - Render prep/upload
  - Collider build
- Minimize disruptive refactors by extending existing systems/components.

## 2. Design Principles

- DOTS-first runtime logic in systems and components.
- Single authoritative chunk entity per chunk coordinate.
- LOD selection based on chunk-space distance rings.
- Hysteresis and per-frame rebuild budgets to avoid thrashing.
- Phase implementation so we can ship value early.

## 3. LOD Data Model

### 3.1 Singleton settings

Add `TerrainLodSettings : IComponentData`:

- Ring distances in chunk units.
- Per-LOD sample settings:
  - Effective voxel scale
  - Effective sampling resolution
- Hysteresis value (in chunks).
- Max LOD for collider generation.
- Max LOD for high-cost visual systems (e.g. grass).
- Per-frame rebuild caps:
  - Max density rebuilds
  - Max mesh rebuilds
  - Max collider rebuilds

### 3.2 Per-chunk LOD state

Add `TerrainChunkLodState : IComponentData`:

- `CurrentLod`
- `TargetLod`
- `LastSwitchFrame`

### 3.3 Tag for changed LOD

Add `TerrainChunkLodDirty : IComponentData` to mark chunks whose LOD changed and need rebuild.

## 4. System Architecture and Order

### 4.1 LOD selection

Add `TerrainChunkLodSelectionSystem`:

- Runs after chunk streaming center coordinate is known.
- Computes ring distance in chunk space (Chebyshev distance).
- Applies hysteresis around ring boundaries.
- Writes `TargetLod` only when actual threshold crossing happens.

### 4.2 LOD apply

Add `TerrainChunkLodApplySystem`:

- For chunks where `TargetLod != CurrentLod`:
  - Update `TerrainChunkGridInfo` (resolution/voxel size for selected LOD).
  - Reconcile bounds origin/extent if needed.
  - Add `TerrainChunkNeedsDensityRebuild`.
  - Add `TerrainChunkLodDirty`.
  - Set `CurrentLod = TargetLod`.

### 4.3 Existing pipeline remains staged

Existing systems continue to process rebuild tags:

- `TerrainChunkDensitySamplingSystem`
- `TerrainChunkMeshBuildSystem`
- `TerrainChunkRenderPrepSystem`
- `TerrainChunkMeshUploadSystem`
- `TerrainChunkColliderBuildSystem`

Collider build gated by LOD policy (`CurrentLod <= ColliderMaxLod`).
Grass rebuild gated by LOD policy (`CurrentLod <= GrassMaxLod`).

## 5. Seam Strategy for Mixed LOD

- Enforce neighbor LOD delta <= 1.
- Add temporary seam skirts on coarse chunk borders adjacent to finer chunks.
- Keep skirts render-only.

Transition-cell meshing (Transvoxel-style) is out of scope for this project.

## 6. Streaming and Gameplay Policy

- Keep loaded chunk radius and split quality policy into rings:
  - Loaded radius
  - Full-detail radius (LOD0)
  - Collider radius
- Keep edit-authoritative area near player at LOD0.
- For far edited chunks: temporarily promote to LOD0 while edit is active.

## 7. Performance Controls

- Cap rebuild work per frame.
- Prioritize by distance from player.
- Add LOD switch cooldown to reduce oscillation.
- Keep blob and mesh disposal discipline when replacing data.

## 8. Implementation Milestones

1. Milestone 1 — LOD state and selection
   - Add LOD settings singleton and per-chunk state.
   - Add LOD selection and apply systems.
   - Streaming system attaches LOD state on chunk spawn.
   - Trigger density rebuilds on LOD change.

2. Milestone 2 — Seams and policy
   - Add neighbor delta clamp.
   - Add seam skirts on coarse/fine borders.
   - Gate collider and grass generation by LOD.

3. Milestone 3 — Diagnostics and validation
   - Add diagnostics, automated tests, and profiling pass.

## 9. Test Plan

- LOD ring selection test with hysteresis.
- Boundary thrash resistance test.
- Neighbor continuity test for mixed LOD (delta <= 1).
- Collider policy test by LOD level.
- Edit correctness test across promotion/demotion.
- Streaming + LOD integration test during movement sweeps.

## 10. Integration Notes

- Extend existing systems/components rather than introducing parallel terrain pipelines.
- Keep SDF and Surface Nets data flow intact.
- Keep debug logging routed through `DebugSettings` toggles.
- Keep all structural changes deferred via ECB in systems.
