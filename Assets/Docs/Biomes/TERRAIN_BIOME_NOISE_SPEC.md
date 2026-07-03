# Terrain Biome Noise Spec
_Status: DESIGN - behavior spec for the active SDF pipeline_
_Last updated: 2026-04-10_

---

## 1. Purpose

Specify how biome-aware terrain generation should behave in the active SDF + Surface Nets terrain pipeline.

This document defines behavior, not the final data model. It is intended to precede the schema and implementation work.

---

## 2. Scope

This spec covers:

- Base procedural terrain shape for the active SDF runtime path
- Biome-aware terrain variation
- Continuous field evaluation and biome blending
- Deterministic world-space sampling behavior
- Integration expectations for chunk seams, edits, streaming, and LOD

This spec does not cover:

- Full cave generation
- River simulation
- Erosion simulation
- Full persistence serialization format
- Vegetation and prop placement rules beyond terrain-shape implications

---

## 3. Runtime Context

The authoritative terrain runtime path is:

- SDF density sampling
- Surface Nets meshing
- ECS chunk rendering/upload
- Live add/subtract terrain edits

The terrain behavior specified here must extend that path rather than create a parallel legacy generation path.

---

## 4. Terrain Generation Model

### 4.1 Continuous Control Fields

The terrain system must support multiple continuous world-space fields.

Minimum required fields:

- `Elevation`: broad vertical terrain structure
- `Moisture`: biome selection input, independent from elevation
- `Ruggedness`: controls whether terrain feels smooth, hilly, ridged, or harsh

Optional later fields:

- `Temperature`
- `BiomeMask` or regional selector field
- `ErosionMask`

### 4.2 Layered Elevation Noise

Elevation must be generated from layered deterministic noise, not a single sine wave and not a single scalar offset.

The first implementation pass should support three conceptual layers:

- Low-frequency landform layer
- Mid-frequency hills / undulation layer
- High-frequency detail layer

The combined elevation result must be normalized or remapped to a stable tuning range.

### 4.3 Terrain Redistribution

The system must support post-noise remapping of elevation to shape the world distribution.

Examples:

- Higher exponent to flatten plains and widen valleys
- Lower exponent or mountain boost to create stronger peaks
- Terracing as an optional later extension

This behavior should be parameterized and deterministic.

### 4.4 Ruggedness and Ridge Behavior

The system must allow biomes to differ not only by height, but also by terrain character.

Examples:

- Plains: low ruggedness, wide smooth transitions
- Hills: moderate ruggedness, mid-frequency emphasis
- Mountains: high ruggedness, ridge-enhanced signals
- Desert: low-to-moderate elevation variance but dune-like directional structure

The first implementation pass may use a single ruggedness field or ridge transform rather than a full library of special terrain operators.

---

## 5. Biome Model

### 5.1 Biomes Are Not Direct Noise Functions

Biomes must be determined from combinations of continuous fields rather than one noise formula per biome.

Biome selection should be derived from field relationships such as:

- low elevation + low moisture -> dry flats / desert edge
- mid elevation + medium moisture -> plains / grassland
- mid elevation + high moisture -> forest
- high elevation + high ruggedness -> mountains

### 5.2 Biome Blending

Biome transitions must support blending in world space.

Hard chunk boundaries between biomes are not acceptable.

At minimum, the system must support one of the following:

- weighted biome blending
- smooth threshold blending
- dominant biome with blend zone falloff

The exact blending math is a schema/implementation detail, but visible terrain discontinuities between neighboring biome regions are not acceptable.

### 5.3 Initial Supported Terrain Archetypes

The first biome-aware terrain pass must support these archetypes:

- Plains
- Hills / grassland
- Forest
- Mountains
- Desert
- Snow / alpine

The second pass should support:

- Swamp or corrupted biome

These archetypes are intended to produce distinct terrain structure, not only different material assignments or prop sets.

---

## 6. Biome Terrain Behavior

### 6.1 Plains

Plains must read as:

- mostly flat or gently rolling
- low height variance
- wide traversable surfaces
- low ruggedness

Plains should emphasize low-frequency terrain and suppress high-frequency detail.

### 6.2 Hills / Grassland

Hills must read as:

- rolling terrain
- moderate slope variance
- stronger mid-frequency forms than plains
- still readable and traversable for player movement

### 6.3 Mountains

Mountains must read as:

- greater vertical relief
- sharper slope transitions
- stronger ridge or ruggedness contribution
- visually distinct from simple scaled-up hills

### 6.4 Desert

Desert must read as:

- dry and sparse
- smoother large forms than mountains
- terrain variation driven by broad waves, dunes, basins, or sparse rocky shelves
- low moisture as a biome-selection requirement

Desert should not simply be plains recolored as sand.

### 6.5 Forest

Forest must read as:

- structurally closer to plains or low hills than to mountains
- moderately moist and less dry than plains
- varied enough to avoid flat monotony, but calm enough to support dense tree placement later
- broad surfaces interrupted by shallow rises, low ridges, and occasional pockets

Forest terrain should emphasize:

- slightly stronger mid-frequency variation than plains
- higher moisture bias than plains
- low-to-moderate ruggedness
- stable ground suitable for secondary placement systems

Forest should not rely on trees alone to communicate the biome.

### 6.6 Snow / Alpine

Snow / alpine terrain must read as:

- high elevation or cold-region terrain
- steeper and harsher than hills
- composed of ridges, shelves, wind-smoothed slopes, and cold plateaus
- less chaotic than corrupted terrain

Snow / alpine terrain should emphasize:

- elevated terrain bias
- medium-to-high ruggedness
- ridge contribution stronger than hills
- optional temperature or snowline bias in later passes

Snow / alpine should not simply be mountains recolored white.

### 6.7 Swamp / Lowland

Swamp or wet lowland terrain must read as:

- low relief
- high moisture
- shallow basins and poorly drained flats
- interrupted traversable ridges and hummocks instead of broad clean plains

Swamp terrain should emphasize:

- low-to-moderate elevation variance
- high moisture bias
- elevation compression / flattening
- local pockets or basin shaping to avoid featureless flatness

### 6.8 Corrupted / Magical

Corrupted or magical terrain must read as:

- intentionally unnatural
- structurally unstable, strange, or sharply stylized compared to natural biomes
- visibly distinct even before props, vegetation, or material changes

Corrupted terrain may use:

- stronger high-frequency contribution than natural biomes
- unusual ridge or terrace shaping
- cellular or warped detail layers
- more aggressive redistribution than natural terrain

This biome may bend realism more than the others, but must still preserve seam safety and determinism.

### 6.9 Recommended Initial Recipe Families

The following are recommended starting recipe families for tuning. These are relative patterns, not final numeric constants.

#### Plains

- low-frequency weight: high
- mid-frequency weight: low
- high-frequency weight: very low
- moisture: low-to-medium or medium
- ruggedness: low
- redistribution: stronger flattening exponent
- special modifiers: none required

Target feel:

- wide traversable surfaces
- gentle rolls
- clear player movement space

#### Hills / Grassland

- low-frequency weight: high
- mid-frequency weight: medium
- high-frequency weight: low
- moisture: medium
- ruggedness: low-to-medium
- redistribution: moderate flattening
- special modifiers: none required

Target feel:

- rolling travel lines
- visible slope variation
- still traversal-friendly

#### Forest

- low-frequency weight: medium-to-high
- mid-frequency weight: medium
- high-frequency weight: low
- moisture: medium-to-high
- ruggedness: low-to-medium
- redistribution: moderate
- special modifiers: none required in first pass

Target feel:

- more varied than plains but calmer than mountains
- terrain suitable for later tree placement

#### Mountains

- low-frequency weight: medium
- mid-frequency weight: medium
- high-frequency weight: low-to-medium
- moisture: variable
- ruggedness: high
- redistribution: weaker flattening, stronger peak bias
- special modifiers: ridge enhancement recommended

Target feel:

- major vertical relief
- prominent ridge lines
- clear distinction from hills

#### Desert

- low-frequency weight: high
- mid-frequency weight: low
- high-frequency weight: very low for base terrain
- moisture: low
- ruggedness: low-to-medium
- redistribution: moderate flattening
- special modifiers: directional warp, dune waves, or sparse basin/shelf shaping recommended

Target feel:

- broad dry landforms
- dunes, shelves, or basins
- not just recolored plains

#### Snow / Alpine

- low-frequency weight: medium
- mid-frequency weight: medium
- high-frequency weight: low-to-medium
- moisture: medium or region-dependent
- ruggedness: medium-to-high
- redistribution: moderate peak emphasis
- special modifiers: snowline/temperature bias later; ridge support recommended

Target feel:

- cold plateaus, shelves, and exposed ridges
- strong relief, but less chaotic than corrupted terrain

#### Swamp / Lowland

- low-frequency weight: medium
- mid-frequency weight: very low
- high-frequency weight: low
- moisture: high
- ruggedness: low
- redistribution: strong compression / flattening
- special modifiers: basin bias recommended

Target feel:

- wet flats with shallow depressions and islands of traversable ground

#### Corrupted / Magical

- low-frequency weight: variable
- mid-frequency weight: medium
- high-frequency weight: medium-to-high
- moisture: author-driven
- ruggedness: medium-to-high
- redistribution: stylized and intentionally exaggerated
- special modifiers: warp, terraces, cellular detail, or unnatural ridge patterns allowed

Target feel:

- clearly unnatural terrain silhouette
- recognizable as a special biome before shader or prop treatment

---

## 7. Determinism

The terrain result for a given sample position must be deterministic from stable inputs.

Required determinants:

- world seed
- world-space position
- biome rule configuration
- explicit generation version if algorithm changes need controlled migration

The same chunk regenerated later under the same inputs must produce the same terrain.

---

## 8. Chunk and Seam Behavior

### 8.1 World-Space Sampling

All base fields must be sampled in world space.

Chunk-local formulas that restart at chunk origins are not acceptable because they introduce visible seams.

### 8.2 Seam Safety

Neighboring chunks must agree on border samples for identical world-space positions.

The biome-aware terrain model must preserve the seam guarantees already required by the Surface Nets pipeline.

### 8.3 Streaming Stability

Streaming chunks in and out must not change terrain shape for the same location and seed.

---

## 9. Edit Behavior

### 9.1 Base Field + Edit Overlay

Procedural generation defines the base terrain field.
Runtime edits remain an overlay or modifier layer applied on top of the base field.

### 9.2 Edit Correctness

Changing the base procedural field must not break:

- additive edits
- subtractive edits
- local terrain rebuild behavior
- downstream mesh rebuild expectations

### 9.3 Biome Independence of Existing Edits

Biome-aware generation may change the base terrain shape, but edits must remain biome-agnostic at the operation level unless a future spec explicitly adds biome-specific edit rules.

---

## 10. LOD and Performance Expectations

The biome-aware field must remain compatible with current and planned LOD behavior.

Requirements:

- Lower-detail chunks may sample a coarser version of the same world-space field.
- LOD changes must not alter the underlying biome identity of a region.
- Field evaluation must be suitable for Burst-safe sampling jobs.

The first pass does not need GPU-based procedural field evaluation. CPU/Burst evaluation is acceptable if deterministic and performant enough for current chunk budgets.

---

## 11. Testing Requirements

Minimum validation requirements:

### EditMode

- Same seed + same position -> same field values
- Different biome rule presets produce distinguishable terrain responses
- Plains are statistically flatter than hills
- Mountains are statistically more rugged than hills

### PlayMode

- Chunk borders remain continuous across biome boundaries
- Stream out / stream in regenerates identical terrain
- Live edits still rebuild correctly on biome-aware terrain

### Manual Validation

- Plains read visually different from hills
- Mountains read visually different from hills, not just taller
- Desert reads structurally different from plains, not only by material/color

---

## 12. Non-Goals

This spec does not require:

- perfectly realistic geology
- erosion simulation in the first pass
- biome-specific vegetation systems
- climate simulation beyond what is needed for biome selection
- full authored world regions

The target is readable, stylized, deterministic biome terrain that fits the current game architecture.

---

## 13. Schema Follow-Up

The schema document that follows this spec should define:

- field settings component(s)
- biome rule data structures
- authoring/config asset structures
- seed and version inputs
- optional blending and override structures

That schema should serve this behavior spec, not replace it.
