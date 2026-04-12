# Terrain Tree Placement Spec
_Status: DESIGN - behavior spec for tree placement on the active terrain pipeline_
_Last updated: 2026-04-10_

---

## 1. Purpose

Specify how tree placement should behave on top of the active SDF + Surface Nets terrain pipeline.

This document defines tree placement behavior, constraints, and integration expectations. It is intended to follow the terrain height-map and biome-noise documents already in this folder.

This spec is deliberately terrain-MVP oriented:

- terrain shape first
- trees second
- grass third

Trees are treated as a higher-priority secondary placement system than grass because they validate biome readability, provide landmarks, and better expose problems in placement determinism and chunk streaming.

---

## 1.5 Related Docs

- [TERRAIN_SURFACE_SCATTER_PLAN.md](TERRAIN_SURFACE_SCATTER_PLAN.md) - rollout path for broadening tree-only placement into reusable surface-scatter families
- [TERRAIN_SURFACE_SCATTER_SPEC.md](TERRAIN_SURFACE_SCATTER_SPEC.md) - umbrella runtime contract for trees, rocks, bushes, and similar discrete props
- [TERRAIN_SURFACE_SCATTER_SCHEMA.md](TERRAIN_SURFACE_SCATTER_SCHEMA.md) - first ECS/data breakdown for tree-plus-rock surface scatter
- [../BIOME_GRASS_STREAMING_MVP_PLAN.md](../BIOME_GRASS_STREAMING_MVP_PLAN.md) - dense detail path that should remain separate from tree-style placement
- [../PERSISTENCE_SPEC.md](../PERSISTENCE_SPEC.md) - sparse divergence and promotion expectations for generated world content

---

## 2. Scope

This spec covers:

- deterministic tree placement on streamed terrain chunks
- biome-aware tree placement rules
- chunk-safe placement and regeneration
- spacing / exclusion logic
- near-player interactive tree promotion
- integration with future persistence

This spec does not cover:

- final harvesting or chopping gameplay implementation
- full tree growth simulation
- final far-distance rendering strategy for trees
- grass or fine ground-cover systems
- complex ecology simulation

---

## 3. Runtime Context

The authoritative terrain runtime path is:

- SDF density sampling
- Surface Nets chunk meshing
- ECS chunk rendering/upload
- streamed chunk lifecycle
- live terrain edits

Tree placement must extend this terrain path rather than depend on legacy full-world terrain generation or manually authored scene placement.

---

## 4. High-Level Goals

1. Trees must make biomes legible at gameplay distance.
2. Tree placement must regenerate deterministically from chunk and world inputs.
3. Tree placement must be stable under stream in / stream out.
4. Trees must avoid obviously invalid placement such as underwater, excessive slope, severe overlap, or floating placement.
5. The runtime must support a later split between render-only distant trees and interactive near-player trees.

---

## 5. Placement Model

### 5.1 Placement Is a Secondary Surface Process

Trees are not part of the base terrain field.

Trees are a secondary placement pass that consumes:

- terrain surface position
- terrain normal / slope
- biome information
- deterministic noise and seeded randomness
- exclusion and spacing constraints

### 5.2 Candidate-Based Placement

The system should evaluate candidate tree sites per chunk rather than randomly instantiating trees across the whole world.

At minimum, candidate evaluation should use:

- world-space sample position
- terrain height / hit point
- local slope check
- biome rule probability
- spacing / collision rule

### 5.3 Deterministic Spatial Distribution

Tree placement should read as naturally clustered or biome-appropriate rather than uniformly gridded.

Suitable approaches include:

- noise-modulated probability field
- jittered grid
- blue-noise-like or Poisson-style spacing approximation

The first implementation pass does not require a perfect Poisson disc implementation if a chunk-local deterministic approximation produces stable, believable spacing.

---

## 6. Biome-Aware Tree Behavior

### 6.1 Plains

Plains tree placement must read as:

- sparse
- intermittent clusters or lone trees
- broad open sightlines preserved

Plains should not become dense forest just because terrain is flat.

### 6.2 Forest

Forest tree placement must read as:

- the densest natural tree biome in the early set
- consistent tree presence with local clearings and gaps
- strong biome-defining coverage without becoming visually impenetrable everywhere

Forest is expected to be one of the strongest validation biomes for the placement system.

### 6.3 Mountains

Mountain tree placement must read as:

- sparse or absent on steep slopes and exposed ridges
- more likely in lower shelves, valleys, and gentler ledges
- clearly reduced with elevation and ruggedness

### 6.4 Desert

Desert tree placement must read as:

- extremely sparse or absent by default
- restricted to special cases such as oases, basin edges, or authored rule exceptions

Desert should not produce frequent generic tree scatter.

### 6.5 Snow / Alpine

Snow / alpine tree placement must read as:

- sparse to moderate depending on subregion
- increasingly limited with elevation, slope, and cold bias
- concentrated below harsh exposed ridges

### 6.6 Swamp / Lowland

Swamp placement should read as:

- clustered in wet lowlands or hummocks
- tolerant of flatter wet terrain
- shaped strongly by moisture and water-adjacent rules

### 6.7 Corrupted / Magical

Corrupted or magical biomes may intentionally break natural tree distribution expectations, but must still remain deterministic and chunk-safe.

---

## 7. Placement Constraints

### 7.1 Surface Validity

A tree candidate must be rejected when any of the following are true:

- terrain surface cannot be resolved
- position is underwater or below allowed wetness rules for that biome
- slope exceeds biome or species threshold
- position lies in explicitly excluded terrain band

### 7.2 Spacing

Trees must respect minimum spacing rules.

The system must avoid obvious overlap or unnatural pileups.

Spacing should support:

- minimum same-species spacing
- optional large-tree exclusion radius
- optional interaction with rocks, structures, or special gameplay landmarks later

### 7.3 Chunk Border Stability

Chunk borders must not create duplicate trees, missing trees, or visible seam lines.

Candidate generation and acceptance must therefore be based on world-space logic rather than chunk-local random state alone.

---

## 8. Determinism

Tree placement must be deterministic from stable inputs.

Required determinants:

- world seed
- generation version
- chunk coordinate
- world-space placement rules
- biome rule configuration

The same chunk regenerated later under the same inputs must produce the same tree placement result.

---

## 9. Streaming Behavior

### 9.1 Chunk Lifecycle

Tree placement must follow chunk lifecycle.

When a terrain chunk streams in:

- tree placement data for that chunk must be regenerated or restored deterministically

When a terrain chunk streams out:

- render-only tree state may be disposed
- persistent divergence data, if any, must remain recoverable through persistence systems

### 9.2 No Global Full-World Tree Population

The system must not depend on full-world up-front tree instantiation.

Tree generation should be chunk-local and streaming-compatible.

---

## 10. Interactive Promotion Model

The recommended long-term runtime model is:

- distant trees: lightweight render-only instances or chunk records
- near-player trees: promoted to interactive entities or prefabs when needed

This spec does not require the promotion system to exist immediately, but tree placement must not block it.

That means tree identity must be stable enough that a distant tree can later become an interactive tree without ambiguity.

---

## 11. Terrain Edit Interaction

### 11.1 Local Revalidation

Terrain edits must eventually support local tree revalidation.

At minimum, the system must support the idea that edited chunks can mark nearby tree placement dirty.

### 11.2 First Pass Simplification

The first implementation pass may choose one of these simplified behaviors:

- trees on edited chunks are fully regenerated from current terrain + deterministic rules
- trees on edited chunks are temporarily removed until a later regeneration pass

What is not acceptable:

- trees floating in empty space after terrain edits
- trees persisting on impossible slopes or inside carved voids

---

## 12. Persistence Expectations

Persistence should store divergence from generated defaults, not full generated tree state for every chunk.

Examples of divergence:

- chopped tree
- damaged tree
- stump state
- regrowth stage later

Untouched seeded trees should cost zero or near-zero persistence data.

---

## 13. Initial Implementation Recommendation

The first tree-placement implementation should target:

- Plains
- Forest
- Mountains
- Snow / Alpine

Desert should be supported as an explicit sparse-or-none case, but does not need full oasis special-case logic in the first pass.

Recommended first-pass behavior:

1. Generate deterministic tree candidates from chunk-local evaluation in world space.
2. Filter by terrain validity: slope, elevation band, biome allowance.
3. Apply deterministic spacing rejection.
4. Emit lightweight chunk-local tree placement records.
5. Render trees in a simple chunk-safe form.

This is enough to validate biome readability and placement logic before interactive tree systems are built.

---

## 14. Testing Requirements

### EditMode

- same seed + same chunk -> same tree placements
- different chunks -> different placements where expected
- forest biome yields denser valid placements than plains under comparable terrain
- mountain slope rules reject steep invalid placements
- desert rules produce sparse or zero placements by default

### PlayMode

- stream out / stream in regenerates identical tree placement
- no chunk-border duplicate trees
- no chunk-border missing strips where tree candidates should exist
- edited chunk revalidation removes invalid floating trees

### Manual Validation

- forests read visibly denser than plains
- mountains have reduced tree coverage on steep ridges
- snow / alpine treeline behavior feels plausible
- trees improve navigation and biome recognition at gameplay distance

---

## 15. Non-Goals

This spec does not require:

- final tree chopping gameplay
- full plant ecosystem simulation
- final HLOD or impostor solution
- perfect biologically realistic tree distribution
- grass integration

The target is deterministic, biome-legible, streamed tree placement that strengthens the terrain MVP.

---

## 16. Schema Follow-Up

The schema document that follows this spec should define:

- tree placement rule data per biome
- deterministic tree identity per chunk
- spacing and exclusion configuration
- render-only placement record format
- promotion hooks for interactive near-player trees
- persistence linkage for divergence from defaults

That schema should serve this behavior spec and the terrain biome docs in this folder.
