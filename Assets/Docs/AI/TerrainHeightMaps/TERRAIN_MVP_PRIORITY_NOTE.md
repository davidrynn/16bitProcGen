# Terrain MVP Priority Note
_Status: DESIGN - priority guidance_
_Last updated: 2026-04-10_

---

## 1. Purpose

Record the intended order of importance for the terrain-system MVP so feature-specific docs do not accidentally imply the wrong implementation priority.

---

## 2. Core Priority Order

### Priority 1 - Core Terrain Generation

- Biome-aware terrain shape in the active SDF pipeline
- Deterministic chunk sampling
- Seam-safe chunk borders
- Streaming and LOD compatibility
- Edit correctness on top of the procedural field

This is the foundation. Without this, later placement systems are tuning noise on unstable terrain.

### Priority 2 - Trees and Major Biome Markers

- Tree placement or equivalent large biome markers
- Deterministic secondary placement on terrain surface
- Biome readability at gameplay distance
- Landmarking and navigation support

Trees matter earlier than grass because they validate biome identity, support navigation, and provide stronger feedback that biome rules are working.

### Priority 3 - Grass and Fine Ground Cover

- Grass density and style variation
- Fine biome polish
- Ground-cover richness near the player

Grass is valuable, but it is secondary polish compared to terrain shape and major placement features.

---

## 3. Immediate Interpretation for Current Docs

- The terrain height-map strategy/spec/schema docs under `Assets/Docs/AI/TerrainHeightMaps/` describe current active terrain-shape planning.
- Grass docs remain useful, but should be treated as deferred design.
- New tree-placement docs should be considered higher priority than grass docs for terrain MVP execution.

---

## 4. Practical Rule

If a terrain feature answers "what biome am I in?" or "where am I going?" it is likely pre-grass priority.

That usually means:

- terrain form first
- trees and major props second
- grass third
