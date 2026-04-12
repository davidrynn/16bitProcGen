# Terrain Strategy Plan
_Status: DESIGN - proposed sequencing document_
_Last updated: 2026-04-09_

---

## 1. Purpose

Define the next terrain strategy for the active SDF + Surface Nets pipeline so the project can move from a simple prototype height signal to a biome-aware world-generation model without reintroducing legacy terrain dependencies.

This plan is intentionally architecture-first:

- Plan: sequencing, scope, and delivery strategy
- Spec: target runtime behavior
- Schema: concrete data model to implement the spec

That ordering is deliberate. The current terrain system already has working chunk sampling, meshing, upload, and editing. The main risk is adding the wrong data model too early and locking the active pipeline into a brittle authoring surface.

---

## 2. Current Situation

### Active Terrain Path

- SDF sampling drives terrain density.
- Surface Nets builds chunk meshes.
- Runtime terrain editing already exists.
- Chunk streaming and LOD are active work streams.

### Current Limitation

- Base terrain shape is still driven by a minimal ground function.
- The active field does not yet support true biome-aware layered noise.
- The exposed `noiseValue` behaves like a scalar offset, not a sampled noise field.

### Consequence

- Terrain can be tuned to be flatter or wavier.
- Terrain cannot yet reliably express plains, rolling hills, mountains, desert dunes, swamp basins, or similar biome identities through the active SDF runtime path.

---

## 3. Strategy Goals

1. Keep the SDF pipeline as the single authoritative runtime terrain path.
2. Replace the current prototype ground signal with deterministic layered noise suitable for biome variation.
3. Separate terrain-shape generation from biome classification so art/gameplay tuning remains flexible.
4. Preserve chunk seam continuity, edit correctness, and streaming determinism.
5. Avoid coupling new work to legacy heightmap or legacy biome systems.

---

## 4. Design Principles

- DOTS-first runtime behavior.
- Deterministic sampling from `(worldSeed, worldPos, biome inputs)`.
- World-space sampling only; no chunk-local formulas that create seams.
- Biomes should influence terrain through parameterized rules, not hardcoded one-off branches spread across systems.
- Terrain edits remain overlays on top of the base procedural field, not baked replacements for it.
- Small, testable milestones. No full terrain rewrite.

---

## 5. Recommended Work Order

### Phase 1 - Terrain Behavior Definition

Create a behavior spec for biome-aware procedural terrain in the active SDF pipeline.

Deliverables:

- Agreed terminology for elevation, moisture, ruggedness, and biome blending
- Agreed list of supported biome terrain archetypes
- Explicit non-goals for the first implementation pass

### Phase 2 - Schema Design

Create the ECS/config schema that supports the behavior spec.

Deliverables:

- Field settings component shape
- Authoring/config asset shape
- Per-biome rule data shape
- Seed and deterministic sampling inputs

### Phase 3 - Base Signal Upgrade

Replace the current sine-only terrain signal with deterministic layered noise in the active SDF field.

Deliverables:

- Layered elevation signal
- Optional ruggedness mask
- Backward-compatible bootstrap defaults where practical

### Phase 4 - Biome Classification and Blending

Introduce biome-space evaluation using multiple continuous fields.

Deliverables:

- Biome lookup from elevation/moisture/temperature or equivalent fields
- Smooth biome blending in world space
- Biome-to-terrain rule mapping

### Phase 5 - Streaming, LOD, and Edit Integration

Ensure the new terrain logic behaves correctly with chunk streaming, LOD changes, and live edits.

Deliverables:

- Seam-safe chunk boundaries
- Deterministic regeneration on stream in/out
- Correct dirty propagation after terrain edits

### Phase 6 - Tuning and Content Expansion

Add richer biome identities and refine parameter ranges.

Deliverables:

- Plains
- Hills / grassland
- Mountains
- Desert
- Forest
- Snow / alpine
- Swamp or corrupted biome as later extension

---

## 6. Scope Boundaries

### In Scope

- Base terrain signal strategy for the active SDF terrain path
- Biome-aware terrain generation rules
- Deterministic noise layering
- Seam-safe chunk sampling
- Integration expectations for edits, streaming, and LOD

### Out of Scope for the First Pass

- Full erosion simulation
- Rivers
- Cave networks beyond current SDF extensibility hooks
- Weather-driven terrain mutation
- Full persistence format for terrain generation inputs
- Reuse of legacy `BiomeComponent` as the primary runtime terrain source

---

## 7. Key Strategic Decisions

### 7.1 Use Continuous Control Fields

Biome identity should not come from one scalar alone.

At minimum, terrain strategy should assume:

- Elevation field
- Moisture field
- Ruggedness or ridge field

Temperature or latitude-equivalent shaping can remain a second-step extension.

### 7.2 Separate Biome Selection from Shape Generation

The system should not encode logic like "desert means this one hardcoded formula" directly in the sampler.

Instead:

- Sample continuous fields in world space
- Evaluate biome weights or dominant biome
- Apply biome-specific shaping rules to the terrain result

This keeps biome logic tunable and blendable.

### 7.3 Preserve Edit Authority

Procedural terrain provides the base field.
Edits remain the authoritative local override layer.

This preserves the current destructible-terrain architecture instead of forcing edits to rewrite generation state immediately.

---

## 8. Milestones

1. Approve plan and behavior spec.
2. Create schema document for ECS/config data model.
3. Implement layered elevation noise in `SDFMath` / `SDFTerrainField` path.
4. Add deterministic world seed plumbing.
5. Add biome rule evaluation.
6. Add tests for seam continuity, determinism, and edit correctness.
7. Tune default biome presets.

---

## 9. Acceptance Criteria

- The active SDF terrain path can express at least plains, hills, mountains, and desert through parameterized generation rules.
- Streaming a chunk out and back in regenerates identical terrain for the same world seed.
- Chunk borders remain seam-safe.
- Terrain edits remain local, deterministic, and compatible with procedural rebuilds.
- No new runtime dependency on the legacy heightmap terrain path is introduced.

---

## 10. Immediate Next Step

Create and review the behavior spec before designing the schema.

Reason:

- The plan answers "what order and why"
- The spec answers "what the terrain should do"
- The schema should only be created after those two are stable enough to avoid churn
