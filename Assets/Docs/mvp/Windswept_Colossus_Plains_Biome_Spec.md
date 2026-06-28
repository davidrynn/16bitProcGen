# Windswept Colossus Plains Biome Spec

**Status:** ACTIVE
**Last Updated:** 2026-06-11
**Owner:** Biome / Terrain

---

## Purpose

A vast open biome characterized by rolling grasslands, sparse vegetation, exposed stone, and exceptional sight lines. The terrain should communicate age, scale, and isolation. Large landmarks (colossi, ruins, towers, craters) should be visible from great distances.

This is a **procedural biome definition**: parameters here are meant to be consumed by the project's terrain, grass, scatter, and structure-placement systems. Each generation concern is tagged with its implementation status (**EXISTS / PARTIAL / POST-MVP**) so the MVP scope is unambiguous.

## Non-Goals

- Shot-for-shot recreation of the reference image (see Visual Target below — we want the *feel*).
- Sky/vista *implementation* — owned by the vista stack (TICKETS V2 fog, V3 mountain skybox, `GROUND_PLANE_IMPOSTOR_SPEC.md`). This spec only states what the biome contributes.
- Relic/landmark *content* — owned by the structure placement specs. This spec defines the seating interface only.

---

## Visual Target & Constraints

**Reference:** `Assets/Docs/Temp_OpeningInspiration.png` (colossal buried hand on an open moorland plain).

- **Feel over fidelity.** The target is the reference's *emotional read* — scale, openness, wind, a far-off impossible thing pulling the player forward — rendered in the project's low-res, low-poly 16-bit style. Photoreal grass, volumetric clouds, and water simulation are explicitly not goals.
- **Palette decision (deliberate):** the reference image is greener/wetter than this spec's palette. We target the **drier end** of the same biome family (late-summer Highlands rather than spring). Revisit only as a color-tuning pass, not a structural change.
- **Performance is a first-class constraint.** The Basic Terrain Scene is **vertex-bound** with ~92% of frame vertices coming from scatter (`AI/RENDER_PERF_PROFILE_REPORT.md`). Every scatter family in this biome must respect the vert budgets in TICKETS B1–B7 and ship far-LOD meshes per `AI/TerrainHeightMaps/SURFACE_SCATTER_LOD_SPEC.md`. Density values below are starting points; the frame budget wins every conflict.

### Scatter Palette Atlas (convention)

All biome scatter shares one project-owned palette atlas — same technique as the trees, but **without the vendor dependency** (trees currently sample Synty's `Generic_01_A.png`; new assets must not).

- **Texture:** `Assets/Models/Scatter/T_ScatterPalette.png` — 64×64, 4×4 grid of 16 flat color cells: the 12 spec palette colors plus 4 blend shades (rock mid-tone, soiled rock base, dark root green, dark lichen). Byte-exact to the RGB values in this spec.
- **UV rule:** every face maps all its loops to a single cell **center**. Zero UV derivatives per face → top mip sampled, no cell bleed regardless of filtering. Import with Point filtering (on-style for 16-bit) — but the convention survives bilinear too.
- **Materials:** two, shared across all scatter families: an opaque URP Lit + atlas for solids (rocks, shrubs), and a two-sided variant for card geometry (grass, flowers). No custom albedo shader needed.
- **Wind channel:** vertex color **alpha** (0 root → 1 tip) is reserved for the future wind shader; atlas owns albedo, vertex RGB is a fallback tint only.
- **Adding colors:** new biomes extend the atlas grid (4×4 → 8×8 holds 64 cells) rather than adding textures — one atlas, one material set, maximum instancing/batching.
- Tree migration onto this atlas is optional post-MVP cleanup (removes the Synty texture dependency from scatter entirely).

---

## Biome Identity

### Inspiration

- Scottish Highlands
- Icelandic plains
- Mongolian steppe
- New Zealand high country
- Shadow of the Colossus
- Elden Ring open regions

### Player Emotion

The player should feel:

- Small
- Curious
- Exposed
- Free to move
- Drawn toward distant landmarks

---

## Terrain Specification — EXISTS (MVP)

Maps to noise parameters in the existing heightmap/SDF generation path (`TerrainGenerationSettings`).

### Base Terrain

- Broad rolling hills
- Long uninterrupted sight lines
- Minimal sharp cliffs

```yaml
Base Height Variation:
  Range: 10m - 60m

Noise Scale:
  500m - 1500m

Slope:
  Most terrain under 15°
```

### Secondary Terrain

Occasional:

- shallow valleys
- dried creek beds (see Water Features — these are the MVP "water")
- low ridges
- depressions

```yaml
Valley Depth:
  5m - 25m

Ridge Height:
  10m - 40m
```

### Erosion — EXISTS

An erosion pass exists (`TerrainErosion.compute`, `ApplyErosion` kernel via `ComputeShaderManager`). Use it to soften crests and carve the shallow channels above; no new erosion work is required for MVP.

---

## Ground Materials — POST-MVP (MVP approximation defined)

There is **no terrain material painting / splat system**. True per-surface material blending is post-MVP system work.

**MVP approximation:** a single terrain material plus grass-blade color and density variation. "Exposed soil" reads as patches where grass `Density → 0` over a brown-tinted terrain base; "stone" reads via boulder/outcrop scatter rather than painted ground.

Target distribution (drives the post-MVP painter, and the MVP grass-density masks):

| Surface | Coverage | Colors | Locations |
|---|---|---|---|
| Windswept Grass | 70–85% | Dry Grass RGB(166,153,102), Muted Olive RGB(114,125,76), Pale Green RGB(140,155,110) | default |
| Exposed Soil | 10–20% | Dust Brown RGB(125,102,76), Dark Earth RGB(92,74,54) | hill crests, trails, erosion channels, around rocks, relic seating rings |
| Stone | 5–10% | Weathered Granite RGB(110,110,110), Ancient Basalt RGB(70,70,75) | outcrop/boulder zones |

---

## Vegetation Specification

Densities are given in **system-consumable units** (blades/m², instances per hectare). All values are proposed starting points — tune in Play Mode against the frame budget.

### Short Prairie Grass — EXISTS (MVP)

Covered by the GPU-instanced blade system (`GrassChunkGenerationSystem`, `GrassType 0`). No mesh authoring needed. This biome's `GrassBiomeParams` entry:

| Field | Value |
|---|---|
| `BaseColor` | Muted Olive RGB(114,125,76); noise toward Dry Grass / Pale Green |
| `MinBladeHeight` | 0.2 m |
| `MaxBladeHeight` | 0.8 m |
| `DensityMultiplier` | 1.0 (this biome is the baseline) |
| `ColorNoiseScale` | large patches (tens of meters) — uneven, wind-mottled read |

- Target density: **30–60 blades/m²** at `Density = 1`, capped by `MaxBladesPerChunk`.
- Per-chunk `Density` varies 0.3–1.0 via low-frequency noise; drops to 0 in soil patches and relic seating rings.
- Wind animation: per-biome wind strength via the existing grass settings — this is the biome's primary motion cue.
- **MVP dependency:** production chunk tagging. `TerrainChunkGrassSurface` is currently applied via the POC menu item; topmost-surface tagging during generation is required for the biome to self-assemble.

### Tall Grass Patches — PARTIAL (post-MVP)

Maps to the **reserved sparse-clump variant** (`GrassType 1` in `TerrainChunkGrassSurface` — not yet implemented). Models: TICKETS **B6**.

- Height 1–1.5 m, authored clump meshes.
- **10–20 clumps/ha** inside eligible masks only (creek beds, valley bottoms, sheltered slopes); 0 elsewhere. Coverage <5% of biome area.

### Wildflowers — POST-MVP

Rides the `GrassType 1` clump path or a future flower family. Models: TICKETS **B7**.

- Clusters 10–40 cm; white, pale purple, yellow.
- **2–6 clusters/ha**, small clusters, never fields.

### Shrubs — POST-MVP

No shrub family exists. Smallest-change option: a second tree-family config (`TreeVisualBootstrap` pattern — shrubs behave like mini-trees: bounds-grounded, yaw-varied). Models: TICKETS **B4**.

- Hardy steppe bushes / low heath, 0.5–1 m.
- **5–15 instances/ha**. Coverage <2%.

### Trees — EXISTS (MVP)

Existing tree family (`TreeChunkRenderSystem`) with `Tree_Oak_LowPoly_01`. Generally absent in this biome:

- **≤1 instance/ha**, and only inside water-adjacency or landmark masks; 0 in open plain.

---

## Rock Specification — EXISTS (rock family); models pending

The rock scatter family (`RockChunkRenderSystem` / `RockRenderConfig`) renders all three tiers; tiers differ by mesh set and placement rule. "Clustered, not uniform" is achieved with a low-frequency **rocky-zone mask covering 10–20% of area** — densities below apply inside the mask, zero outside.

### Pebble Clusters — models: TICKETS B2

- Pre-clustered patch meshes (5–12 pebbles, elements 10–50 cm).
- **10–30 clusters/ha** inside rocky zones.
- **Runtime family implemented (2026-06-11):** dedicated pebble scatter family (`PebbleVisualBootstrap`, `PebblePlacementParams`, `PebblePlacementAlgorithm`) with rocky-zone-masked clustered placement, tunable from the inspector. Do **not** wire pebble meshes into `RockRenderConfig` — the rock family's uniform distribution can't express clustering. Pre-clustered patches keep this within `TERRAIN_SURFACE_SCATTER_PLAN.md` §9 family-fit rules (individual pebbles would belong to the details path).

### Boulder Groups — models: TICKETS B1

- 1–6 m, rounded, weathered, partially buried (sink 20–30%).
- Colors: Granite Gray, Dark Basalt, Lichen Green (vertex color).
- **1–3 groups/ha** inside rocky zones, 3–8 boulders per group.

### Stone Outcrops — models: TICKETS B3

- 5–30 m, horizon-breakers and navigation markers; silhouette readability from 500 m matters more than close detail.
- **1–4 per km²**.
- **Open question (B3):** scatter family vs. structure/relic placement path — at 30 m these behave like small landmarks.

---

## Water Features

There is **no water rendering or drainage system**. Re-scoped:

### Dried Creek Beds — MVP (geometry only)

The MVP "water". Pure terrain carving — winding shallow channels (2–10 m wide, 0.5–1.5 m deep) with exposed-soil read (grass density 0) along the bed. Delivers the reference image's winding-channel composition with zero water rendering. Tall grass masks key off these channels.

### Seasonal Streams — POST-MVP

Reflective shallow water in the carved channels. Requires a water material/rendering decision first.

### Marsh Patches — POST-MVP

Rare, 10–100 m, for visual variety and future wildlife spawning. Requires water rendering plus a moisture signal.

---

## Atmosphere & Sky

### Fog — owned by TICKETS V2 (canonical fog ticket)

- Distance haze only; Blue Gray RGB(170,180,190); near geometry unaffected.
- View distance target: 2–10 km (with horizon treatment below).

### Sky — STUB (fill `BiomeSkyMapping` entry)

The project supports per-biome sky via `BiomeSkyMapping` / `BiomeSkyEntry`. This biome's entry, when authored:

- Dramatic broken cumulus, large patches of blue — sky is ~60% of a typical frame and carries the "exposed" feeling.
- Haze color matches the fog blue-gray.
- **Cloud shadows on terrain — POST-MVP.** The dappled light patching is the single biggest "windswept" cue in the reference; cheapest approximation is a scrolling light cookie. Stub until the vista stack lands.

### Horizon — owned by V3 / `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`

A low distant ridge should enclose the plain so the 2–10 km view ends at a silhouette, not empty sky. This spec only requires that the biome *assume* a horizon ring exists; it does not implement one.

---

## Landmark Integration

Landmark content (what relics exist) is owned by the structure placement specs (`AI/STRUCTURE PLACEMENT/`). This biome hosts:

- Giant buried hands, colossal skulls, broken towers, ancient machines, massive craters, buried arches.
- **Scale:** major landmarks 50–250 m — large enough to dwarf the 10–60 m terrain relief.
- **Spacing:** one major landmark every 1–3 km. **Visible from:** 500 m–5 km.

### Relic Seating Hooks — interface to structure placement (MVP for any placed relic)

Rules the biome supplies so a procedurally placed relic looks *seated* rather than dropped:

1. **Seating basin:** flatten/blend terrain to ≤5° slope within 1.5× the relic footprint radius, falling off to ambient terrain over a further 0.5× footprint.
2. **Scatter clearance:** hard exclusion of trees/boulders/outcrops within the footprint; grass `Density` fades to 0 approaching the base.
3. **Soil ring:** exposed-soil read in a ring around the base (MVP: grass density 0; post-MVP: painted soil material).
4. **Sight-line corridors — POST-MVP stub:** bias terrain low along approach lines so the landmark stays visible while traveling toward it.

---

## Biome Selection & Boundaries — STUB

- **MVP:** this is the sole/default biome (`BiomeTypeId 0`); no selection logic needed.
- **POST-MVP:** selection signals and edge blending per `AI/BIOME_TERRAIN_FIELD_SPEC.md` (world-field driven region classification). When neighbors exist, blend scatter densities and grass color over the boundary falloff; hard-switch sky/fog per dominant biome.

---

## Procedural Generation Rules

Ordered pipeline with implementation status:

| # | Step | Status | Notes |
|---|---|---|---|
| 1 | Generate elevation | **EXISTS** | heightmap/SDF noise params above |
| 2 | Apply erosion | **EXISTS** | `TerrainErosion.compute` pass |
| 3 | Generate drainage | POST-MVP | MVP fallback: noise-carved dried creek beds (step 4a) |
| 4a | Carve dried creek beds | **MVP** | geometry only, no water |
| 4b | Place streams/marshes | POST-MVP | needs water rendering |
| 5 | Paint terrain materials | POST-MVP | MVP approximation: grass density/color masks |
| 6 | Place rock formations | **EXISTS** (system) | rock family; meshes pending B1–B3 |
| 7 | Place grass | **PARTIAL** | blade system exists; needs production chunk tagging |
| 8 | Place shrubs | POST-MVP | needs family (B4) |
| 9 | Place flowers | POST-MVP | `GrassType 1` path (B7) |
| 10 | Place landmarks | **EXISTS** (system) | structure placement + seating hooks above |

**MVP scope = steps 1, 2, 4a, 6, 7, 10** plus fog (V2). That subset already delivers the biome's core read: rolling open terrain, mottled wind-blown grass, rock clusters, dried channels, and a distant relic seated correctly.

---

## Success Criteria

- Player can see distant landmarks from almost anywhere.
- Most screenshots contain 70%+ open sky and terrain.
- Terrain feels natural without becoming noisy.
- Players are encouraged to travel toward distant objects.
- Environment communicates ancient history before explicit storytelling.
- **Performance:** a populated viewpoint holds frame budget; scatter respects B1–B7 vert caps and far-LOD swap shows a material scatter-vertex reduction (target per `SURFACE_SCATTER_LOD_SPEC.md`: >40%).

### Design Theme

The biome should feel like the preferred resting place of impossible things:

- giant hands
- ribs
- masks
- machinery
- broken statues
- forgotten relics

Every discovery should be visible from a great distance and naturally pull the player across the landscape.

---

## Related Docs

- `Assets/Docs/TICKETS.md` — B1–B7 (scatter models), V2 (fog), V3 (mountain skybox), R1 (LODs)
- `AI/RENDER_PERF_PROFILE_REPORT.md` — vertex-bound finding driving all density/budget caps
- `AI/TerrainHeightMaps/SURFACE_SCATTER_LOD_SPEC.md` — far-LOD contract every scatter family must satisfy
- `AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md` — scatter runtime contract
- `AI/BIOME_GRASS_STREAMING_MVP_PLAN.md` / `GrassBiomeSettings` — grass system this spec parameterizes
- `AI/BIOME_TERRAIN_FIELD_SPEC.md` — post-MVP biome selection fields
- `AI/STRUCTURE PLACEMENT/STRUCTURE_PLACEMENT_SPEC.md` — landmark placement consuming the seating hooks
- `AI/GROUND_PLANE_IMPOSTOR_SPEC.md`, `AI/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`, `AI/MVP_VISTA_MOMENT_SPEC.md` — vista stack this biome plugs into
