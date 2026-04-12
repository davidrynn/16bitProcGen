# Terrain Surface Scatter Spec
_Status: DESIGN - runtime contract for reusable chunk-scattered terrain props_
_Last updated: 2026-04-11_
_Owner: Terrain / World Generation DOTS_

---

## 1. Purpose

Specify how reusable surface scatter should behave on top of the active SDF + Surface Nets terrain pipeline.

`Surface Scatter` is the runtime layer for sparse to moderately dense terrain-following props such as trees, bushes, rocks, and ore nodes. It is the implementation-side counterpart to the broader design idea of `Secondary Placement`.

---

## 2. Scope

This spec covers:

- chunked deterministic placement of discrete terrain props
- family-based rules for trees, bushes, rocks, ore nodes, and similar props
- render-only far state, optional near-player promotion, and sparse divergence
- invalidation and regeneration on terrain edits, streaming, and LOD changes
- the split between surface scatter and dense details

## 3. Non-Goals

This spec does not cover:

- grass/details implementation
- authored landmark or structure placement
- full ecology simulation
- a required object-oriented interface hierarchy for runtime polymorphism
- a one-size-fits-all state model shared by every prop family

---

## 4. Related Docs

- [TERRAIN_SURFACE_SCATTER_PLAN.md](TERRAIN_SURFACE_SCATTER_PLAN.md) - rollout and sequencing for this runtime
- [TERRAIN_SURFACE_SCATTER_SCHEMA.md](TERRAIN_SURFACE_SCATTER_SCHEMA.md) - concrete ECS/data breakdown for tree-plus-rock family rollout
- [TERRAIN_TREE_PLACEMENT_SPEC.md](TERRAIN_TREE_PLACEMENT_SPEC.md) - tree-specific behavior that should fit within surface scatter
- [../BIOME_GRASS_STREAMING_MVP_PLAN.md](../BIOME_GRASS_STREAMING_MVP_PLAN.md) - dense detail path that remains separate
- [../PERSISTENCE_SPEC.md](../PERSISTENCE_SPEC.md) - sparse persistence model for generated world state

---

## 5. Terminology

- `Secondary Placement`: design-level term for props placed after base terrain generation
- `Surface Scatter`: runtime layer for low- to medium-density discrete props placed on terrain surfaces
- `Scatter Family`: a prop family with its own rule set, render assets, and optional interaction model
- `Scatter Record`: one deterministic accepted placement for a family on a chunk
- `Details`: the separate dense-clutter path used for grass and similar high-count instances

`Surface Scatter` is the recommended repo term because it covers vegetation and non-vegetation props without collapsing into engine-specific editor terminology like `foliage` or `details`.

---

## 6. Runtime Context

The authoritative terrain runtime path is:

- SDF density sampling
- Surface Nets chunk meshing
- ECS chunk rendering and upload
- streamed chunk lifecycle
- live terrain edits
- active LOD policy

Surface Scatter must extend this terrain path.
It must not depend on:

- full-world up-front spawning
- editor-painted instances as the primary runtime source
- a separate non-DOTS terrain placement stack

---

## 7. Core Requirements

1. Surface Scatter placement must be deterministic from stable inputs such as world seed, chunk coordinate, generation version, and family rule data.
2. Surface Scatter must be chunk-safe under stream in and stream out.
3. Chunk borders must not produce duplicate or missing props.
4. Terrain edits and LOD changes must be able to invalidate render-only state and rebuild it from current terrain plus sparse divergence.
5. Families may share lifecycle behavior without being forced into identical placement rules or state models.

---

## 8. Family Classification

### 8.1 Families That Belong in Surface Scatter

The following families are good candidates for Surface Scatter:

- trees
- bushes and shrubs
- rocks and boulders
- ore or resource nodes
- sparse deadwood or similar readable set dressing

These families share the following properties:

- they are visually discrete
- they benefit from explicit spacing rules
- they are sparse enough that explicit per-placement records are affordable
- some of them may later need interaction, promotion, or sparse persistence

### 8.2 Families That Do Not Belong in Surface Scatter

The following are not good candidates for this runtime:

- grass
- flowers
- pebbles
- leaf litter
- very high-count clutter
- billboarded density fields

These belong in a `details` path, not in Surface Scatter.

### 8.3 Separate Placement Domains

The following should remain separate from Surface Scatter:

- landmarks
- ruins
- authored structures
- quest props
- encounter-specific objects

These are semantic placements, not generic surface scatter.

---

## 9. Placement Model

### 9.1 Candidate-Based Per-Chunk Placement

Surface Scatter should evaluate candidate placement sites per chunk rather than spawn props globally.

At minimum, candidate evaluation should support:

- world-space sample position
- resolved terrain surface position
- surface normal or slope
- family probability or density rule
- family spacing or exclusion rules
- optional biome, elevation, moisture, or wetness gating

### 9.2 World-Space Determinism

Candidate generation and acceptance must be based on world-space logic.
Chunk-local restarts that break seams are not acceptable.

### 9.3 Stable Local Identity

Accepted placements must have stable local identity derived from deterministic candidate slots or an equivalent deterministic rule.
Identity must remain stable across regeneration when neighboring candidates are rejected or accepted differently after terrain changes.

---

## 10. Family Rule Model

Surface Scatter should be family-driven, not hardcoded per runtime branch.

Each family should be able to define rules such as:

- density or spawn probability
- minimum spacing
- large-object exclusion radius
- slope limits
- elevation or biome bands
- normal-alignment policy
- scale and rotation variation
- render asset set
- whether the family can be promoted to interactive entities
- whether the family stores sparse deltas

This is a behavioral contract, not a fixed schema.
The exact ECS data model may evolve separately.

---

## 11. State and Persistence Model

### 11.1 Render-Only Default State

By default, Surface Scatter families should exist as render-only chunk records rather than persistent entity instances everywhere.

### 11.2 Sparse Divergence Only

Untouched generated props should not require per-instance persistence.
Only divergence from generated defaults should be stored.

### 11.3 Family-Specific Deltas

Delta models are family-specific.

Examples:

- trees: full, damaged, stump, and regrowth-related states
- rocks: intact, cracked, depleted
- bushes: visible, harvested, regrowing
- decorative rocks: no delta at all

A shared sparse-delta concept is acceptable.
A forced shared lifecycle enum for all families is not required.

---

## 12. Invalidation and Regeneration

### 12.1 Terrain Edit Invalidation

When terrain density or mesh changes invalidate a chunk, Surface Scatter render-only state for affected families must be removable and rebuildable.

### 12.2 LOD and Streaming Invalidation

When a chunk is culled or streamed out, render-only scatter state may be disposed.
When the chunk becomes active again, it must regenerate from:

- world seed
- chunk coordinate
- current terrain surface
- generation version
- any stored sparse divergence for the family

### 12.3 Deterministic Re-entry

Re-entering a previously visited chunk must reproduce identical untouched placements.

---

## 13. Promotion Model

Surface Scatter families may opt into near-player promotion when gameplay needs true entities.

Promotion is appropriate when a family needs:

- collision with gameplay semantics
- health or damage state
- harvesting or destruction
- inventory or resource yield
- animation or bespoke interaction

Families that do not need this should remain render-only at all distances where practical.

Promotion policy is family-specific.
A family without interaction should not inherit promotion complexity just because trees use it.

---

## 14. Rendering Model

Surface Scatter should favor family-aware batching and instancing.

Requirements:

- render-only far state should be cheap to rebuild
- families with compatible assets may share a renderer path
- families with incompatible materials, animation, or batching needs may use separate renderers
- high-density details remain on a different render path

A single monolithic renderer for every terrain prop is not required.

---

## 15. Implementation Constraints

1. DOTS-first runtime behavior is required.
2. Reuse in the hot path should be data-driven, not interface-driven.
3. Shared code should stay lean and cover only the truly common lifecycle.
4. Tree-specific logic should not be promoted into the shared layer unless a second family proves it is genuinely shared.
5. Dense details must remain a separate runtime category.

---

## 16. Test Requirements

### EditMode

- same seed and chunk produce identical records for a family
- neighboring chunks do not produce border duplicates
- family spacing and exclusion rules are enforced
- invalidation plus regeneration preserves stable identity for untouched placements

### PlayMode

- streaming out and back in regenerates identical untouched placements
- terrain edits rebuild only affected chunk families
- LOD cull and restore do not leave stale render-only state behind
- promotion and demotion, where supported, preserve family identity

---

## 17. Acceptance Criteria

- Surface Scatter is documented as the umbrella runtime for discrete terrain-following prop families
- trees are compatible with the runtime but do not define it entirely
- at least one non-tree family can fit the contract without inheriting tree-only state
- grass/details remain explicitly outside the runtime boundary
- the runtime contract is compatible with sparse persistence and optional near-player promotion