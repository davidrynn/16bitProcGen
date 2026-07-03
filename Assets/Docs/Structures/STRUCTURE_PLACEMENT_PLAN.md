# Structure Placement Plan
_Status: DESIGN (MVP SCOPED) - sequencing plan for semantic structure and relic placement runtime_
_Last updated: 2026-04-16_
_Owner: Terrain / World Generation / WFC_

---

## 1. Purpose

Define the rollout path for a reusable `Structure Placement` runtime on top of the active SDF + Surface Nets terrain pipeline.

This runtime targets low-count, high-importance world objects such as dungeons, villages, giant relics, ruins, and other landmarks that players can discover and interact with.

The goal is to keep this domain separate from `Surface Scatter` so authored-meaningful structures do not inherit small-prop assumptions.

---

## 2. Naming Decision

This repo should use:

- `Secondary Placement` for the design umbrella of content placed after base terrain generation.
- `Surface Scatter` for sparse to moderate discrete terrain props (trees, rocks, bushes, ore nodes).
- `Structure Placement` for semantic, larger-footprint world structures (dungeons, villages, relics, ruins).
- `Details` for dense clutter fields (grass, flowers, pebbles).

`Structure Placement` is the recommended runtime term because it covers both procedural compounds (villages, dungeons) and singular landmarks (ancient relics).

---

## 3. Scope

This plan covers:

- deterministic world-space anchor placement for major structures
- hard spacing and exclusion constraints so structures are far apart
- family-driven realization paths (prefab, WFC, hybrid)
- streaming-safe lifecycle and near-player activation rules
- persistence-ready identity and save/apply hooks in foundation phases
- explicit boundary with `Surface Scatter` and dense `Details`

## 4. Non-Goals

This plan does not cover:

- replacing or merging the current `Surface Scatter` runtime
- replacing WFC internals with a new solver
- authored quest scripting, narrative content, or encounter logic
- dense decorative clutter generation
- final balancing of loot/economy tied to structure content

---

## 5. Related Docs

- [../TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_PLAN.md](../Terrain/Scatter/TERRAIN_SURFACE_SCATTER_PLAN.md) - boundary between scatter props and structures
- [../TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md](../Terrain/Scatter/TERRAIN_SURFACE_SCATTER_SPEC.md) - scatter runtime contract
- [../PERSISTENCE_SPEC.md](../Persistence/PERSISTENCE_SPEC.md) - structure placement persistence layer and sparse deltas
- [../TERRAIN_ECS_NEXT_STEPS_SPEC.md](../Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md) - primary SDF terrain pipeline context
- [../../WFC/MAP_WFC.md](../WFC/MAP_WFC.md) - current dungeon/WFC system map
- [MAGIC_GRID_SPEC.md](MAGIC_GRID_SPEC.md) - analytic magic lattice supplying grid-bound anchor candidates (relics/cities/fortresses)

---

## 6. Current State

### 6.1 What Already Exists

- `Surface Scatter` has a deterministic per-chunk lifecycle for trees and rocks.
- WFC dungeon generation exists as a dedicated runtime path.
- persistence design already includes a structure placement layer.

### 6.2 Missing Piece

There is no single runtime contract for semantic structures that need:

- region-scale anchor planning
- hard spacing over long world distances
- family-specific realization (dungeon layout vs village layout vs single relic)
- structure-specific persistence and interaction boundaries

---

## 7. Strategic Decisions

### 7.1 Keep Placement Domains Separate

`Surface Scatter` and `Structure Placement` should remain separate runtimes with explicit handoff points.

### 7.2 Region-Scale Anchor Planning First

Structure anchors should be generated in world-space at a coarser planning scale than chunk-local scatter, then realized as chunks stream in.

### 7.3 Hard Spacing Is Mandatory

Structure families must support a hard minimum spacing radius, with optional cross-family exclusion rules.

### 7.4 Family-Driven Realization

Different families can share anchor planning while using different realization paths:

- dungeons -> WFC or tile-socket realization
- villages -> layout graph and compound prefab realization
- giant relics -> singular landmark prefab or procedural assembly

### 7.5 Deterministic Identity + Sparse Persistence

Every accepted anchor must have a deterministic ID so untouched structures regenerate from seed while modifications persist as sparse deltas.

### 7.6 Reserve Footprints Across Systems

Accepted structure anchors should publish footprint reservations so scatter/detail systems can avoid overlaps.

### 7.7 DOTS-First Runtime

Planning and lifecycle state should be ECS-driven. MonoBehaviours remain authoring/bootstrap only.

### 7.8 Persistence Baseline Before Realizers

Persistence readiness is a gate, not a late phase.

Before family realizers ship, the runtime must already define and enforce:

- deterministic stable anchor identity
- source and generation-version tracking
- lock semantics for modified/discovered/player-built structures
- apply and record hooks (even if disk IO is still a stub)

---

## 8. MVP Work Order

The full 6-phase plan (§8.1) remains the long-term target. This MVP cut gets structures visible in-world with the minimum infrastructure that doesn't create migration debt.

### MVP Scope Decisions

- **Ship Relic + Dungeon families first.** Village compound realizer is deferred — it's the most complex realization path and not required for MVP.
- **De-gate realizers from full persistence.** Realizers do NOT require disk I/O or full apply/record systems. They DO require `StableAnchorId` + `GenerationVersion` + `PersistenceFlags` on the anchor record and an in-memory apply pass so identity survives streaming.
- **Same-family spacing only.** Cross-family exclusion spacing is deferred.
- **AABB footprints only.** Sufficient for grid-aligned dungeons and roughly symmetric relics. Irregular/rotated compound footprints are a later upgrade.

### MVP Prerequisites (must resolve before coding)

1. **WFC bootstrap gap**: `HybridWFCSystem` has `[DisableAutoCreation]` but is NOT created by `DotsSystemBootstrap` (line 428 creates `DungeonRenderingSystem` and `DungeonVisualizationSystem` only). The dungeon realizer bridge must either bootstrap `HybridWFCSystem` itself or `DotsSystemBootstrap` must be updated to create it when `EnableDungeonSystem` is true.
2. **WFC deterministic seed**: `HybridWFCSystem` defaults to `DateTime.Now.Ticks` as RNG seed (line 72). For structure placement determinism, WFC instances spawned by the dungeon realizer must receive a seed derived from the anchor's `StableAnchorId`, not wall-clock time.
3. **Contract alignment**: Enum and record field names must match between this spec and `PERSISTENCE_SPEC.md` (now unified — see §11 data contract).

### MVP Test Asset

`Assets/Models/Odd_Head_Relic_v1.fbx` — scaled large (10x–20x) as the placeholder relic for visual verification across all phases.

### MVP Step 1 — Components + Family Rules

- `StructureAnchorRecord` (IBufferElementData) with `StableAnchorId`, `GenerationVersion`, `PersistenceFlags`
- `StructureFamilyId` enum (Dungeon, Relic — Village deferred)
- `StructureFamilyRuleset` ScriptableObject for authoring spacing, biome, slope, realization mode
- `StructurePlacementSource` enum (shared with persistence)

**Test gate**: EditMode unit tests confirming components compile, buffer can be created and populated on an entity, and `StructureFamilyRuleset` SO can be instantiated with expected defaults. Check Unity console for zero compilation errors.

### MVP Step 2 — Anchor Planning System

- `StructureAnchorPlanningSystem : ISystem` — deterministic region-scale candidates
- Hash-based candidates from world seed + planning cell coords
- Same-family hard spacing with deterministic tie-break (row-major evaluation order; earlier-accepted anchors win)
- Basic terrain-fit checks (slope/elevation)
- Writes accepted anchors to singleton buffer entity
- In-memory apply pass: on planning, check existing anchor buffer and preserve any anchor with `Locked` or `Modified` flags

**Test gate**: EditMode tests for determinism (same seed → same anchor set), spacing invariants (no two accepted anchors closer than min radius), and tie-break stability. Debug gizmo overlay draws accepted anchor positions as spheres in Scene View — take Scene View screenshot via MCP for visual confirmation that anchors are spread across terrain.

### MVP Step 3 — Relic Realizer (simplest family)

> **Rendering refactor (2026-04-16):** The initial MVP implementation used `Graphics.RenderMeshInstanced` for batch rendering. This caused a frustum culling artifact ("globe eating") on the 15x-scaled relic mesh due to a single `worldBounds` AABB for the entire batch. See [RELIC_RENDER_REFACTOR_SPEC.md](../Archives/StructurePlacement_2026/RELIC_RENDER_REFACTOR_SPEC.md) (archived) for full diagnosis and the replacement design.

- `RelicRealizationSystem` — spawns one ECS entity per accepted relic anchor with Entities Graphics render components (`RenderMeshArray`, `MaterialMeshInfo`, `RenderBounds`, `LocalToWorld`)
- Each entity has per-entity `RenderBounds` for correct individual frustum culling
- Tracks realized entities via `StructureRealizedTag.StableAnchorId` for lifecycle cleanup
- Uses `Odd_Head_Relic_v1.fbx` at anchor positions, scaled ~15x
- Cleans up entities when anchors are removed or re-planned

**Test gate**: Enter Play Mode → relic meshes appear at anchor positions on terrain. Orbit camera at distance → no partial disappearance or spherical clipping. Multiple relics individually culled when off-screen. **Stop here for user sign-off before proceeding.**

### MVP Step 4 — Dungeon Realizer Bridge

- `DungeonStructureRealizationSystem` — creates WFC entities at anchor positions
- Bootstraps `HybridWFCSystem` if not already active
- Passes seed derived from `StableAnchorId` to WFC component (not `DateTime.Now.Ticks`)
- Existing WFC collapse + rendering systems handle the rest

**Test gate**: Enter Play Mode → dungeon WFC structures generate at dungeon-family anchor positions. Take MCP screenshot for user visual confirmation. Verify: dungeons appear at different locations than relics, WFC collapse completes without timeout, deterministic seed produces same layout on replay. **Stop here for user sign-off.**

### MVP Step 5 — Footprint Reservations

- `StructureFootprintReservationSystem` — publishes AABB exclusion zones from accepted anchors
- Scatter systems check reservations before spawning (no-spawn zones)

**Test gate**: Enter Play Mode → trees/rocks do NOT spawn inside structure footprints. Take MCP screenshot comparing a structure area vs. nearby open terrain. EditMode test confirming scatter generation skips reserved zones. **Stop here for user sign-off.**

---

## 8.1 Full Work Order (Post-MVP)

The phases below extend the MVP into the complete system. They are not gated for MVP but remain the long-term plan.

### Phase A - Village Compound Realizer

- village layout graph generator
- compound prefab realization
- village-specific footprint shapes (upgrade from AABB if needed)

### Phase B - Cross-Family Exclusion + Advanced Spacing

- cross-family exclusion spacing rules
- exclusion from authored protected zones and gameplay corridors

### Phase C - Full Persistence Apply/Record

- `StructurePersistenceApplySystem` — loads sparse deltas from disk by `StableAnchorId`
- `StructurePersistenceRecordSystem` — detects divergence and writes sparse deltas
- no-reroll lock semantics validated in tests
- disk serialization backing store

### Phase D - Streaming and Activation Policies

- near-player activation/promotion policy
- activation behavior that respects persistence lock states
- deterministic re-entry for untouched structures

### Phase E - Debug and Test Coverage

- deterministic placement tests (seed and spacing invariants)
- overlap/exclusion tests against scatter data
- debug overlays for anchors, footprints, and rejection reasons

---

## 9. Family Fit Rules

### Good `Structure Placement` Candidates

- dungeons
- villages
- giant relics
- major ruins and landmarks
- large encounter compounds with persistent identity

### Not Good `Structure Placement` Candidates

- trees and rocks used as ambient set dressing
- bushes, ore nodes, and similar medium-count props
- dense grass/flower clutter

These belong to `Surface Scatter` or `Details`.

---

## 10. Acceptance Criteria

- the repo has a canonical `Structure Placement` plan and spec
- boundary between structures and scatter/details is explicit and testable
- structure anchors are defined as deterministic, constrained, and spacing-aware
- at least one realization path each is defined for relic, dungeon, and village families
- persistence and streaming expectations are explicit for untouched vs modified structures
- persistence prerequisites are defined and required before family realizers
- documentation index points to the canonical docs for this runtime
