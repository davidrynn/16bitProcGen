# Terrain Surface Scatter Plan
_Status: DESIGN - sequencing plan for reusable terrain surface scatter_
_Last updated: 2026-04-11_
_Owner: Terrain / World Generation DOTS_

---

## 1. Purpose

Define the rollout path for a reusable surface-scatter runtime on top of the active SDF + Surface Nets terrain pipeline.

This plan exists because the current tree runtime already proves a useful chunk lifecycle:

- deterministic per-chunk generation
- render-only far state
- terrain and LOD invalidation with rebuild
- sparse divergence support

The next step is deciding how that lifecycle should expand to rocks, bushes, and similar props without collapsing grass/details and authored landmarks into the same system.

---

## 2. Naming Decision

Industry terms vary:

- Unreal commonly uses `foliage` for instanced outdoor props.
- Unity splits `trees` from `details`.
- Procedural toolchains often use `scatter` or `scattering`.

This repo should use:

- `Secondary Placement` for the design-level concept of props placed after terrain generation.
- `Surface Scatter` for the runtime layer that owns low- to medium-density chunked prop placement.
- `Details` for the separate dense-clutter path used by grass, flowers, pebbles, and similar high-count instances.

This naming is broader than `foliage` and clearer than `details` for non-vegetation props like rocks or ore nodes.

---

## 3. Scope

This plan covers:

- evolving the current tree lifecycle into a reusable surface-scatter pattern
- family-based support for trees, bushes, rocks, ore nodes, and similar props
- deterministic generation, invalidation, LOD strip/rebuild, and optional sparse deltas
- the runtime boundary between surface scatter, dense details, and authored placement

## 4. Non-Goals

This plan does not cover:

- replacing the grass/details path with the tree runtime
- full ecology simulation or biome content tuning
- authored landmarks, structures, ruins, or quest props
- a mandatory C# interface hierarchy for runtime polymorphism
- a full schema for every future prop family in the first pass

---

## 5. Related Docs

- [TERRAIN_SURFACE_SCATTER_SPEC.md](TERRAIN_SURFACE_SCATTER_SPEC.md) - runtime contract for the shared surface-scatter layer
- [TERRAIN_SURFACE_SCATTER_SCHEMA.md](TERRAIN_SURFACE_SCATTER_SCHEMA.md) - first ECS/data breakdown for tree-plus-rock family rollout
- [TERRAIN_TREE_PLACEMENT_SPEC.md](TERRAIN_TREE_PLACEMENT_SPEC.md) - tree-specific placement behavior on the active terrain path
- [../BIOME_GRASS_STREAMING_MVP_PLAN.md](../BIOME_GRASS_STREAMING_MVP_PLAN.md) - dense detail path that should remain separate from surface scatter
- [../PERSISTENCE_SPEC.md](../PERSISTENCE_SPEC.md) - sparse divergence and world-state persistence expectations
- [TERRAIN_MVP_PRIORITY_NOTE.md](TERRAIN_MVP_PRIORITY_NOTE.md) - terrain first, trees and major markers second, grass third

---

## 6. Current State

### 6.1 What Already Exists

The current tree runtime already demonstrates the core lifecycle needed for reusable surface scatter:

- deterministic world-space candidate generation
- stable local IDs
- chunk-local placement records
- terrain dirty invalidation
- LOD-driven render-state teardown
- render-only far instances with future promotion hooks
- sparse deltas for changed trees

The current grass runtime already covers the opposite end of the spectrum:

- dense instance counts
- cheaper per-instance representation
- separate generation and render tradeoffs
- `details` style behavior rather than discrete prop state

### 6.2 Missing Piece

The repo does not yet have an umbrella contract for props that are:

- discrete enough to matter individually
- sparse enough that explicit records are affordable
- still numerous enough that authored entities everywhere would be wasteful

That gap is the target of `Surface Scatter`.

---

## 7. Strategic Decisions

### 7.1 Reuse the Lifecycle, Not the Tree Rules

The tree system should act as the reference implementation for lifecycle behavior, not as a literal shared code path for every prop family.

### 7.2 Prefer Data-Driven Families Over OO Interfaces

In the DOTS hot path, reuse should come from family data and shared lifecycle systems, not from interface-based runtime polymorphism.

### 7.3 Split By Density and Interaction

Surface Scatter should own low- to medium-density discrete props.
Dense clutter should stay in a separate details path.

### 7.4 Keep Deltas Family-Specific

Not every family needs tree-style lifecycle stages.

Examples:

- trees may need stump and regrowth states
- rocks may need intact and depleted states
- bushes may need hidden and harvested states
- decorative rocks may need no persistent delta at all

### 7.5 Do Not Abstract Too Early

Generalization should happen only after at least one second family, preferably rocks, proves which tree concepts are truly shared.

---

## 8. Recommended Work Order

### Phase 1 - Establish the Umbrella Contract

Deliverables:

- agree on the `Surface Scatter` term
- document which prop categories belong in surface scatter versus details
- keep trees as the reference family

### Phase 2 - Extract Reusable Core from the Tree Lifecycle

Deliverables:

- identify shared generation-tag and invalidation behavior
- identify shared stable-ID expectations
- keep family-specific placement rules and state models separate

### Phase 3 - Add a Second Family Through the Same Lifecycle

Recommended family: rocks

Deliverables:

- deterministic rock placement records
- terrain and LOD invalidation parity with trees
- render-only far rocks with optional simple state such as depleted or non-depleted

### Phase 4 - Add a Third Family with Simpler Variation

Recommended family: bushes or shrubs

Deliverables:

- family-specific spacing and clustering rules
- validation that not all families need the same delta model
- confirmation that the shared layer remains lean

### Phase 5 - Promotion and Persistence by Family

Deliverables:

- near-player promotion only for interactive families
- sparse divergence only where gameplay requires it
- no persistence cost for untouched generated props

### Phase 6 - Hold the Boundary With Dense Details

Deliverables:

- explicit documentation that grass, flowers, and similar clutter stay on the details path
- no accidental monolithic `all terrain props` system

---

## 9. Family Fit Rules

### Good Surface Scatter Candidates

- trees
- bushes and shrubs
- rocks and boulders
- ore/resource nodes
- large deadwood or similar sparse set dressing

### Bad Surface Scatter Candidates

- grass
- flowers
- pebbles
- leaf litter
- tiny debris fields
- billboard clutter intended to read as density, not as individual props

### Separate Authored or Semantic Placement

- ruins
- structures
- landmarks
- quest props
- bespoke encounter objects

---

## 10. Acceptance Criteria

- the repo has a stable documented term for the umbrella runtime: `Surface Scatter`
- trees remain the reference implementation, but surface scatter is documented as broader than trees
- at least one non-tree family can follow the same lifecycle without inheriting tree-specific rules
- dense details remain explicitly out of scope for surface scatter
- the docs make clear that DOTS reuse should be data-driven rather than interface-driven in the hot path