# WFC-First Terrain Region Approach

## Goal
Drive large-scale terrain layout via Wave Function Collapse (WFC) tiles constrained by biome data, then specialize each tile with deterministic noise, constraints, and post-processing.

## High-Level Flow
1. **Biome Grid Authoring**
   - Sample world-scale climate maps (temperature, moisture, elevation bands).
   - Populate a coarse grid of biome slots (e.g., 128 m chunks).

2. **Tile Catalog & Constraints**
   - Define `TerrainTileArchetype` blob assets for each biome, including:
     - Allowed neighbors (N/E/S/W/vertical).
     - Entry/exit markers for roads, rivers, cave mouths.
     - Noise recipe IDs (heightfield kernels, erosion masks, splat profiles).
     - Post-processors (flora sets, fauna spawn tables, structure packages).

3. **WFC Collapse Pass**
   - For each biome grid, run constraint propagation (reuse `HybridWFCSystem` patterns).
   - Seed collapse per region using deterministic hashes of world coordinates + biome data.

4. **Chunk Realization**
   - Convert collapsed tile descriptors into `TerrainChunkGenerationRequest` components.
   - Extend `HybridTerrainGenerationSystem` to read tile descriptors and apply:
     - Tile-specific noise seeds and scaling.
     - Lake/river splines from tile metadata.
     - Layered constraint solvers for flora, fauna, resources.
     - Structure placement hooks (ruins, towns, dungeons).

5. **Persistence & Streaming**
   - Store collapsed tile states and chunk modifications in blob streams keyed by region.
   - On load, replay tile descriptors before terrain mesh generation.
   - Destruction events feed back into modification blobs per chunk.

6. **Post-Generation Systems**
   - Road/path stitching: connect compatible edge markers with spline builders.
   - Cave & dungeon injection: trigger WFC-in-WFC for subterranean layers.
   - Debug toggles: add `DebugSettings.EnableWFCTerrainDebug` for diagnostics.

## Required Code Touchpoints
- `HybridWFCSystem`: add biome-aware tile catalogs and chunk-level constraint results.
- `HybridTerrainGenerationSystem`: accept tile descriptors, extend noise job parameters.
- `TerrainEntityManager`: request tile collapse prior to chunk spawn.
- Persistence layer: extend `TerrainModificationSystem` save/load with tile state.
- Authoring tooling: ScriptableObject editors for `TerrainTileArchetype` catalogs.

## Data Structures
- `TerrainTileArchetype`: blob storing neighbor masks, noise recipe IDs, spawn sets.
- `BiomeTileGrid`: chunked blob asset mapping world coordinates to biome slots.
- `ChunkTileDescriptor`: component bridging collapsed tile to generation jobs.
- `StructureSpawnSet`: references to prefab registries for ruins/towns/dungeons.

## Pros & Cons vs Noise-First + Local WFC Approach

| Aspect | WFC-First Terrain Tiles | Noise-First with Local WFC Decorations |
| --- | --- | --- |
| **Macro Coherence** | Strong biome-level theming and guaranteed adjacency constraints. | Emergent but less deterministic; harder to enforce road/river continuity. |
| **Authoring Complexity** | Requires richer tile catalogs and constraint authoring (higher upfront cost). | Lower initial setup; noise settings per biome are simpler. |
| **Runtime Cost** | Additional WFC collapse per region; manageable with coarse grids and DOTS jobs. | Cheaper at macro level; WFC only for small structures. |
| **Variation** | Depends on tile library breadth; risk of repetition without large pattern sets. | High micro-variation from noise; macro sameness unless noise is stratified. |
| **Destructibility & Persistence** | Tile descriptors integrate cleanly with existing chunk persistence. | Similar effort; modifications stay local to noise-generated meshes. |
| **Extensibility** | Easier to add systemic features (roads, rivers) via constraint markers. | Requires bespoke solvers to align features across chunks. |
| **Tooling Requirements** | Needs editor tooling for tile constraints and visualization. | Mostly reuses existing noise authoring tools. |
| **Risk** | Constraint deadlocks if catalogs are sparse; must handle retries/fallbacks. | Noise artifacts may clash at chunk borders but rarely hard-fail. |

## Next Actions
1. Prototype `TerrainTileArchetype` ScriptableObject and blob baking pipeline.
2. Extend `HybridWFCSystem` with biome tile catalogs and chunk descriptor output.
3. Add tile descriptor ingestion to `HybridTerrainGenerationSystem`.
4. Build debug view to display collapsed tile IDs in the scene.  