# Stylized Procedural Terrain System Design  
**For 16-BitCraft (Stylized Low-Poly Destructible World)**  

---

## Overview
A deterministic, stylized world generator supporting **biome-based procedural generation**, **full destructibility**, and **constraint-driven world content** (flora, fauna, ruins, towns, caves, etc.).  
Built to integrate with **chunk-streamed ECS or OpenGL systems**, emphasizing low-poly performance and persistence.

---

## 0. Terrain Core Decision

| Option | Pros | Cons |
|:-------|:------|:------|
| **Heightmap + decals/holes** | Simple, fast, supports water/roads/flora | Cannot support caves or full destructibility |
| **Voxel / SDF terrain** | Fully destructible, supports caves and overhangs | Higher memory/CPU, requires chunk streaming and async meshing |

✅ **Chosen:** **SDF voxel terrain** with dual-contouring or marching-cubes meshing.  
Destructibility and persistence are achieved via *CSG (Constructive Solid Geometry)* operations on voxel data.

---

## 1. World Model & Coordinate System

- **Regions**: Large (e.g. 1024×1024m) world tiles — top-level planning unit.  
- **Chunks**: Small voxel volumes (e.g. 32³ or 48³) within regions — loaded/unloaded dynamically.  
- **Seeds**:  
  - `WorldSeed`  
  - `RegionSeed(regionId)`  
  - `ChunkSeed(chunkId)`  

### Base Environmental Fields
| Field | Description |
|:------|:-------------|
| Elevation (E) | Underlying terrain shape |
| Temperature (T) | Affects biome distribution |
| Moisture (M) | Determines water and vegetation density |

Biomes are derived from combinations of **T**, **M**, **Elevation**, and **Latitude**.

---

## 2. Generation Pipeline

### Phase A – Regional Planning (Coarse, async)
1. **Biome Field:**  
   Sample temperature/moisture/elevation to classify biome per cell.
2. **River Network:**  
   Generate flow field from elevation; accumulate runoff; trace channels.
3. **Lakes:**  
   Detect basins; assign lake level and area.
4. **Settlements:**  
   Place towns/ruins based on biome and distance from rivers/roads.
5. **Road Graph:**  
   Connect settlements with a minimum-cost path (avoiding steep slopes or water).
6. **POI Index:**  
   Store references to caves, dungeons, ruins, and towns with deterministic seeds.

### Phase B – Chunk Synthesis (Fine)
1. **SDF Terrain:**  
   Combine biome-specific noise functions for the base terrain.  
   Apply cuts for rivers/lakes and flattening for roads.
2. **Meshing:**  
   Dual contouring or marching cubes, producing **flat-shaded** low-poly meshes.
3. **Decoration Constraints:**  
   Constraint solver places flora, rocks, and props using blue-noise sampling.
4. **WFC Structures:**  
   Generate ruins/dungeons via **Wave Function Collapse** (tile-based, deterministic).
5. **Fauna Spawns:**  
   Spawn wildlife based on biome and proximity rules.

### Phase C – Runtime Systems
- **Water Simulation:** Planar lakes and spline rivers.  
- **Navigation:** Incremental navmesh or grid updated after terrain edits.  
- **LOD:** Use coarse voxel grids and impostors at distance.

---

## 3. Constraint System (Declarative)

Constraints are applied to any object placement (flora, fauna, props, etc.):

```yaml
PlacementRule:
  Biome: [Taiga, Plains]
  Slope: [0, 25]
  DistanceToWater: [1, 30]
  NotWithin:
    Tag: Road
    Radius: 4
  DensityMax: 0.15/m²
```

**Adjacency/Sockets (for WFC):**
- `Socket(North)=Door`
- `Socket(South)=Hall`
- Allowed rotations: `[0°, 90°, 180°, 270°]`

**Budgets:**
- Per chunk, target densities for flora/fauna/structures with variance.

---

## 4. Persistence & Destructibility

### Deterministic Base + Sparse Deltas
- **Do not store full chunks.**
- Save:
  1. **Edit Journal:** Append-only list of CSG ops (add/remove material).
  2. **Entity State:** Items picked, flora harvested, enemies defeated.
  3. **Player Builds:** Ownership and construction records.

### On Load
1. Regenerate chunk deterministically from seed.  
2. Replay edit journal and apply entity deltas.  
3. Periodically compact journals to snapshots.

Example storage:
```
/World/Region_10_12/Chunk_05_06_03.bin
```

---

## 5. Roads, Paths, and Surface Integration

- Generated from settlement graph (Phase A).  
- For each chunk intersected by a road spline:
  - Flatten/raise SDF along the path.
  - Create exclusion bands for trees and large rocks.
  - Optionally spawn props (lanterns, fences) via constraint sampling.

---

## 6. Caves and Dungeons

### Caves (Natural)
- 3D Worley/Perlin ridged noise → thresholded pockets.  
- Connect with paths biased by drainage direction.  
- Carve using negative SDF blending.

### Dungeons (Designed)
- WFC on a 3D grid (e.g. 16×16×4).  
- Emit negative SDF for rooms/corridors.  
- Add positive geometry for walls/columns.  
- Entrances aligned with surface slopes < 35°.

---

## 7. ECS/DOTS Breakdown

### Archetypes
| Entity | Components |
|:-------|:------------|
| **RegionPlan** | BiomeMap, RiverGraph, RoadGraph, POIIndex |
| **ChunkTerrain** | SDFGrid, MeshHandle, Collider, LODLevel, DirtyFlags |
| **ChunkEdits** | EditJournal, LastCompactTick |
| **SpawnField** | Masks (slope/water/soil), RNG state |
| **StructureRequest** | POIRef, WFCSeed, Bounds |
| **FaunaSpawner** | Biome, Cap, Cooldown, ActiveCount |

### Systems
1. `RegionPlannerSystem` – Generate large-scale biome and river data  
2. `ChunkSDFBuildSystem` – Build voxel grids asynchronously  
3. `ChunkMeshBakeSystem` – Create mesh + collider  
4. `DecorationConstraintSystem` – Apply placement rules  
5. `WFCStructureSystem` – Generate ruins/dungeons  
6. `FaunaSpawnSystem` – Handle wildlife logic  
7. `PersistenceApplySystem` / `PersistenceRecordSystem` – Save/load deltas  
8. `NavmeshUpdateSystem` – Update pathfinding regions  
9. `StreamingPrioritySystem` – Manage load/unload priority by distance

---

## 8. Performance Guidelines

| Component | Recommendation |
|:-----------|:---------------|
| **Chunk Size** | 32³–48³ voxels near player; 16³ at distance |
| **Brush Resolution** | Coarse (spherical/box SDFs) for speed |
| **Shading** | Flat-shaded with grouped normals |
| **Threading** | Async meshing + Burst jobs; never block main thread |
| **Grass/Foliage** | GPU instanced, culled by cell visibility |

---

## 9. Development Milestones

| Step | Deliverable |
|:-----|:-------------|
| 1 | SDF voxel core (streaming parity with current terrain) |
| 2 | Biome field + biome-specific terrain shaping |
| 3 | Rivers and lakes |
| 4 | Flora and rock constraints system |
| 5 | Roads and paths |
| 6 | WFC ruins on surface |
| 7 | Edit journal persistence |
| 8 | Fauna spawners and AI |
| 9 | Cave networks and treasures |
| 10 | LODs, optimization, polish |

---

## 10. Risk Management

| Risk | Mitigation |
|:------|:------------|
| High memory from voxels | Use sparse grids, low-res LODs |
| WFC stalls | Limit grid size, use backtracking watchdog |
| Navmesh rebuild cost | Localized updates by dirty AABB |
| Non-determinism | Centralize RNG seeds |
| Save bloat | Periodic journal compaction, dedup by AABB |

---

## Summary

This system combines:
- **Biome-driven procedural terrain generation**  
- **Constraint-based flora/fauna placement**  
- **Wave Function Collapse for structures**  
- **Full voxel/SDF destructibility**  
- **Persistent edit journals for replayable state**  

All components fit cleanly into a **staged ECS pipeline**, enabling stylized, infinite, and reactive worlds with a cohesive low-poly aesthetic.

---

**Next Steps:**  
1. Implement `SDFGrid`, `EditJournal`, and `PlacementRule` data structures.  
2. Prototype a 12-tile WFC ruin set (16×16 footprint).  
3. Integrate with existing chunk streaming and noise factory.
