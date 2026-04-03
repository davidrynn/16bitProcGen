# Biome Grass Streaming MVP Plan
_Status: DESIGN — MVP-ready implementation plan_  
_Last updated: 2026-03-01_

---

## 1. Purpose

Define a biome-aware, chunk-streamed grass pipeline that works with infinite terrain while preserving deterministic regeneration and future persistence integration.

This plan is MVP-first, but does not lock us into throwaway architecture.

---

## 2. Design Constraints

- DOTS-first runtime; systems own spawn/update/unload flow.
- Deterministic per-world generation: same `(worldSeed, chunkCoord)` must produce the same grass output.
- No full-instance persistence requirement in MVP. Future persistence stores only sparse deltas.
- Reuse existing grass rendering path (`GrassChunkGenerationSystem`, `GrassChunkRenderSystem`) where possible.
- Avoid coupling to legacy/placeholder terrain paths.

---

## 3. MVP Scope

### In Scope
- Per-chunk biome-aware grass density assignment.
- Deterministic grass regeneration per chunk.
- Streaming-safe load/unload behavior with no seams at chunk borders.
- Basic terrain-reactive filtering from available chunk mesh/surface data.
- Dirty/rebuild path on terrain edits.

### Out of Scope (Deferred)
- Species-level ecological simulation.
- Multi-layer flora stack (shrubs/flowers/trees) beyond grass.
- Full persistence serialization of grass instances.
- GPU-driven hierarchical culling overhaul.

---

## 4. Target Architecture

```
Terrain Chunk Streamed In
  -> Chunk has biome + surface metadata
  -> Grass Surface Tagging System assigns/updates TerrainChunkGrassSurface
  -> GrassChunkGenerationSystem deterministically scatters blades
  -> GrassChunkRenderSystem draws instanced blades

Terrain Edited
  -> Terrain systems mark chunk dirty
  -> Grass dirty flag set on affected chunk
  -> GrassChunkGenerationSystem rebuilds only dirty chunks

Chunk Streamed Out
  -> Grass buffers disposed with chunk entity teardown
```

---

## 5. Data Model (MVP + Future-Safe)

### 5.1 `TerrainChunkGrassSurface` (existing, retain)
- `Density` (0..1): final scalar used by blade count logic.
- `BiomeTypeId`: biome lookup key.
- `GrassType`: keep `0` default path; reserved values remain future extension points.

### 5.2 New: `GrassChunkDeterminism` (IComponentData)
- Purpose: explicit generation seed material without hardcoding assumptions in systems.
- Fields:
  - `uint WorldSeed`
  - `int2 ChunkCoordXZ`
  - `uint GenerationVersion` (defaults to `1`; bump when algorithm changes intentionally)

### 5.3 New: `BiomeGrassRuleSet` (ScriptableObject -> Blob at runtime)
- Per biome:
  - `DensityMultiplier`
  - `MinSlopeDeg`, `MaxSlopeDeg`
  - `MinHeight`, `MaxHeight`
  - `WindStrengthScalar`
  - `ColorVariationScalar`
- This reuses existing per-biome style fields and centralizes biome-to-grass behavior.

### 5.4 Future hook: `GrassDeltaMask` (not implemented in MVP)
- Sparse cell mask per chunk for player edits (cut/burn/planted) to be consumed post-generation.
- Declared now as a reserved integration point for persistence phase.

---

## 6. System Plan

### Phase A: Chunk Tagging and Rules Wiring
1. Add `BiomeGrassRuleSet` asset and runtime singleton loader.
2. Implement `GrassChunkSurfaceAssignmentSystem`:
   - Query streamed terrain chunks with biome data.
   - Set/update `TerrainChunkGrassSurface` (`Density`, `BiomeTypeId`, `GrassType=0`).
   - Attach `GrassChunkDeterminism` if missing.
   - Mark grass dirty only when values changed.

### Phase B: Deterministic Scatter Upgrade
1. Update generation seed derivation in `GrassChunkGenerationSystem`:
   - Seed from `WorldSeed + ChunkCoordXZ + GenerationVersion`.
2. Ensure border-stable world-space sampling rules (no chunk-local seam artifacts).
3. Apply biome rule filters (slope/height/density multiplier) before final blade output.

### Phase C: Streaming + Edit Reactivity
1. Confirm streamed-out chunk teardown disposes grass buffers safely.
2. Extend terrain edit dirty propagation:
   - When terrain chunk mesh/density is dirtied, also dirty grass surface.
3. Rebuild only dirty chunks; no global grass regeneration pass.

### Phase D: Validation and Hardening
1. Add deterministic regression tests.
2. Add streaming churn test (move camera/player, chunk enter/exit).
3. Add edit-reactivity test (carve terrain -> local grass rebuild).

---

## 7. Test Plan (MVP)

### EditMode
- `GrassDeterminism_SameSeedSameChunk_SameOutput`
- `GrassDeterminism_DifferentChunk_DifferentOutput`
- `BiomeRule_DensityMultiplier_Applied`
- `BiomeRule_SlopeFilter_RejectsOutOfRange`

### PlayMode
- `GrassStreaming_ChunkUnloadReload_RegeneratesIdentically`
- `GrassStreaming_NoChunkBorderSeamPops`
- `GrassEdit_DirtyChunkOnly_Rebuilds`

### Manual Validation
- Traverse across biome boundaries and verify visual blending by chunk, no missing strips.
- Profile 3x streaming radius movement and verify no sustained buffer leaks.

---

## 8. Implementation Steps (Concrete)

1. Create `BiomeGrassRuleSet` authoring asset and runtime access path.
2. Add `GrassChunkDeterminism` component.
3. Add `GrassChunkSurfaceAssignmentSystem` in terrain/grass simulation group.
4. Patch `GrassChunkGenerationSystem` to consume determinism + biome rules.
5. Patch terrain edit dirty pipeline to mark grass dirty.
6. Add/adjust tests (`Assets/Scripts/DOTS/Tests/Automated` and PlayMode suite).
7. Document tuning knobs and defaults in this file after first implementation pass.

---

## 9. Acceptance Criteria

- Grass appears/disappears purely by streamed chunk lifecycle; no manual runtime placement needed.
- Biome changes modify grass density/style via rules, not hardcoded switches.
- Re-entering previously visited chunks regenerates identical grass (before persistence deltas exist).
- Terrain edits trigger local chunk grass rebuild only.
- Existing grass rendering path remains functional and is not rewritten for MVP.

---

## 10. Risks and Mitigations

- Risk: biome metadata unavailable at chunk-tagging time.  
  Mitigation: defer tagging until biome component present; keep retry query lightweight.

- Risk: algorithm tweaks break deterministic continuity.  
  Mitigation: include `GenerationVersion` and bump intentionally.

- Risk: future persistence needs per-instance override.  
  Mitigation: keep `GrassDeltaMask` integration hook and avoid serializing generated blades.

---

## 11. Phase Placement

- Immediate track: Phase 2 world-streaming readiness work (while Phase 1 player-critical tasks continue).
- Persistence coupling point: Phase 4 `PERSISTENCE_SPEC` integration through sparse grass deltas only.

