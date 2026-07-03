# Structure Placement Spec
_Status: DESIGN (MVP SCOPED) - runtime contract for deterministic semantic structure placement_
_Last updated: 2026-04-16_
_Owner: Terrain / World Generation / WFC_

---

## 1. Purpose

Specify how `Structure Placement` should behave on top of the active SDF + Surface Nets terrain pipeline.

`Structure Placement` is the runtime layer for low-count, high-significance world content such as dungeons, villages, giant relics, and ruins.

---

## 2. Scope

This spec covers:

- deterministic region-scale anchor planning for structures
- hard spacing and exclusion constraints for far-apart placement
- family-driven realization paths (WFC, prefab, hybrid)
- streaming, activation, and regeneration behavior
- interaction and persistence expectations
- integration boundaries with `Surface Scatter` and `Details`

## 3. Non-Goals

This spec does not cover:

- dense clutter generation (`Details`)
- tree/rock/bush placement (`Surface Scatter`)
- replacing WFC internals or tile authoring format
- quest narrative scripting and encounter authoring logic
- final balance of rewards/economy in structures

---

## 4. Related Docs

- [STRUCTURE_PLACEMENT_PLAN.md](STRUCTURE_PLACEMENT_PLAN.md) - rollout sequencing and priorities
- [MAGIC_GRID_SPEC.md](MAGIC_GRID_SPEC.md) - analytic XZ lattice supplying grid-bound anchor candidates (relic/city/fortress) as a deterministic variant of §9.2
- [../TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md](../Terrain/Scatter/TERRAIN_SURFACE_SCATTER_SPEC.md) - adjacent runtime for scatter props
- [../TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_PLAN.md](../Terrain/Scatter/TERRAIN_SURFACE_SCATTER_PLAN.md) - placement domain boundary
- [../PERSISTENCE_SPEC.md](../Persistence/PERSISTENCE_SPEC.md) - structure placement persistence layer
- [../TERRAIN_ECS_NEXT_STEPS_SPEC.md](../Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md) - SDF terrain pipeline context
- [../../WFC/MAP_WFC.md](../WFC/MAP_WFC.md) - WFC systems and dungeon references

---

## 5. Terminology

- `Structure Placement`: runtime domain for semantic, low-count, high-importance world structures
- `Structure Family`: a category with its own rules and realization path (dungeon, village, relic)
- `Structure Anchor`: deterministic accepted world-space placement candidate for one structure instance
- `Footprint Reservation`: world-space area reserved by an accepted anchor to block overlapping structures and scatter
- `Realization`: converting an accepted anchor into concrete world output (prefabs, WFC layout, or hybrid)
- `Structure Record`: persisted or runtime record for deterministic structure identity and placement metadata
- `Structure Delta`: sparse persistence record for modifications from seeded defaults

---

## 6. Runtime Context

The authoritative world runtime path remains:

- SDF density sampling
- Surface Nets meshing
- ECS chunk rendering/streaming
- terrain edit invalidation and rebuild

`Structure Placement` extends this path. It must not depend on full-world up-front authored scene content.

`Structure Placement` should coexist with:

- `Surface Scatter` for medium/small discrete props
- `Details` for dense clutter
- WFC runtime for dungeon-family realization

---

## 7. Core Requirements

1. Structure anchors must be deterministic from stable inputs (world seed, planning cell coordinate, generation version, family rules).
2. Structures must support hard minimum spacing so anchors are far apart by design.
3. Placement must enforce hard constraints (terrain fit, biome, exclusion bands, water/road rules) before acceptance.
4. Candidate acceptance must avoid overlaps with existing structure footprints and reserved exclusion volumes.
5. Every accepted anchor must have stable identity that survives chunk streaming and deterministic regeneration.
6. Untouched generated structures must regenerate from seed; only modifications should persist.
7. Structure realization must be streaming-safe, budgeted, and deterministic.
8. Families may share anchor planning while retaining family-specific realization and interaction models.
9. Scatter/detail systems must receive structure reservations so small props do not spawn through major structures.
10. Persistence identity fields and lock semantics must exist before family-specific realization systems ship.
11. Apply and record hooks must be present in baseline runtime behavior even when disk serialization is deferred.

---

## 8. Placement Domain Boundaries

### 8.1 Belongs to `Structure Placement`

- dungeons
- villages
- giant relics
- major ruins and landmark compounds
- bespoke POIs with meaningful gameplay interaction

### 8.2 Does Not Belong to `Structure Placement`

- trees, bushes, rocks, ore nodes
- grass, flowers, pebbles, litter fields
- tiny ambient clutter meant to read as density

### 8.3 Integration Contract with Surface Scatter

Accepted structure anchors must publish footprint/exclusion data consumed by scatter systems. Scatter families should treat these areas as no-spawn zones except where explicitly allowed by family rules.

> **Implementation note**: The existing `SurfaceScatterLifecycleUtility` provides tag/buffer bookkeeping only (set-or-add placement buffers, generation tags). It does not contain reservation or exclusion logic. Footprint exclusion checking must be built as new logic in the scatter generation systems (e.g. `TreePlacementGenerationSystem`) that queries `StructureFootprintReservation` buffers.

---

## 9. Placement Model

### 9.1 Two-Stage Runtime

Stage A: `Anchor Planning`

- evaluate deterministic candidates at region-scale planning cells
- apply hard constraints
- enforce hard spacing and exclusion
- accept/reject and write anchor records

Stage B: `Anchor Realization`

- when streaming/load policy allows, realize accepted anchors into world content
- route by family to realization pipeline (WFC dungeon, village layout generator, singular relic prefab assembly)

### 9.2 Deterministic Candidate Generation

Candidates should be generated from world-space hashed cell identities, not local chunk restart loops, to prevent seam and streaming divergence.

### 9.2.1 Deterministic Tie-Break Rule

When neighboring planning cells both generate candidates within each other's spacing radius, a deterministic tie-break must resolve which candidate survives:

1. Planning cells are evaluated in a fixed deterministic order (row-major or Morton/Z-curve over cell coordinates).
2. Earlier-evaluated accepted anchors take priority — a later candidate that violates the spacing of any already-accepted anchor is rejected.
3. Tie-break order must depend only on cell coordinates and world seed, never on streaming order, frame timing, or evaluation parallelism.

This guarantees that `StableAnchorId` sets are identical across seed replays regardless of which chunks the player visits first.

### 9.3 Constraints and Scoring

At minimum, each family should support:

- required terrain-fit constraints (slope, elevation, clearance)
- required world-context constraints (biome tags, distance to roads/water/spawn)
- hard spacing radius
- optional weighted preferences used only after hard constraints pass

### 9.4 Spacing and Exclusion

Spacing should support:

- same-family minimum spacing
- optional cross-family exclusion spacing
- exclusion from authored protected zones and critical gameplay corridors

---

## 10. Family Rule Model

`Structure Placement` should be family-driven, not hardcoded per family branch.

Each family should provide rule data such as:

- min and max anchor spacing
- anchor attempt density per planning cell
- required biome set or biome weights
- slope/elevation/clearance limits
- exclusion radii by tag/category
- realization mode (`RelicPrefab`, `DungeonWFC`, `VillageGraph`, etc.)
- streaming realization budget class
- interaction profile and persistence profile

Rule sets should be blob-backed or mirrored into unmanaged runtime data for deterministic Burst-safe access.

---

## 11. Runtime Data Contract (Initial)

This is an initial contract target, not a frozen schema.

```csharp
public enum StructureFamilyId : byte
{
    Dungeon = 0,
    Relic = 1,
    // Village and Ruin deferred to post-MVP
}

public enum StructurePlacementSource : byte
{
    SeededAnchor = 0,
    WFC = 1,
    PlayerBuilt = 2,
}

[System.Flags]
public enum StructurePersistenceFlags : byte
{
    None = 0,
    Locked = 1 << 0,
    Modified = 1 << 1,
    Destroyed = 1 << 2,
    Discovered = 1 << 3,
}

/// Canonical runtime anchor record. StructurePlacementRecord in
/// PERSISTENCE_SPEC is the serialized subset of these fields.
public struct StructureAnchorRecord : IBufferElementData
{
    public StructureFamilyId Family;
    public int2 PlanningCell;
    public float3 WorldPosition;
    public quaternion Rotation;
    public float Radius;
    public uint StableAnchorId;
    public uint GenerationVersion;
    public FixedString64Bytes TemplateId;          // prefab key or generator template key
    public StructurePlacementSource Source;
    public StructurePersistenceFlags PersistenceFlags;
}

public struct StructureFootprintReservation : IBufferElementData
{
    public uint StableAnchorId;
    public float3 Center;
    public float3 Extents;
}

public struct StructurePersistenceRecord : IBufferElementData
{
    public uint StableAnchorId;
    public uint GenerationVersion;
    public StructurePlacementSource Source;
    public StructurePersistenceFlags Flags;
    public uint ModifiedAtTick;
}
```

Identity requirement: `StableAnchorId` must derive from deterministic candidate identity, not acceptance-order index, and must be the key used by persistence apply/record flows.

---

## 12. System Breakdown (Initial)

### 12.1 Anchor Planning

`StructureAnchorPlanningSystem : ISystem`

- deterministic region-scale candidate evaluation
- hard spacing and constraints
- writes accepted anchor records

### 12.2 Reservation Publication

`StructureFootprintReservationSystem : ISystem`

- computes reservation volumes from accepted anchors
- publishes exclusion data for scatter/detail runtimes

### 12.3 Persistence Seam (MVP: In-Memory; Full: Disk-Backed)

**MVP baseline**: Anchor records carry `StableAnchorId`, `GenerationVersion`, and `PersistenceFlags` in the ECS buffer. The anchor planning system performs an in-memory apply pass — when re-planning, it preserves any anchor marked `Locked` or `Modified` rather than regenerating it. No disk I/O is required for MVP.

**Full system** (post-MVP Phase C): `StructurePersistenceApplySystem` and `StructurePersistenceRecordSystem` add disk-backed sparse delta storage:

- apply existing structure deltas/flags by `StableAnchorId` from disk
- record structure divergence as sparse deltas only
- enforce lock semantics that prevent silent re-roll after divergence

### 12.4 Realization Request

`StructureRealizationRequestSystem : ISystem`

- turns nearby/required anchors into realization requests based on streaming and budget

### 12.5 Family Realization

**MVP families**: `DungeonStructureRealizationSystem`, `RelicStructureRealizationSystem`
**Post-MVP**: `VillageStructureRealizationSystem`

- perform family-specific realization deterministically
- use `EntityCommandBuffer` for structural changes

#### 12.5.1 Dungeon Realizer — WFC Integration Requirements

The dungeon realizer bridges to the existing `HybridWFCSystem`. Two known issues must be resolved:

1. **Bootstrap gap**: `HybridWFCSystem` has `[DisableAutoCreation]` but `DotsSystemBootstrap` does not create it (only `DungeonRenderingSystem` and `DungeonVisualizationSystem` are bootstrapped). The dungeon realizer must either create `HybridWFCSystem` on demand or `DotsSystemBootstrap` must be updated to include it when `EnableDungeonSystem` is true.

2. **Deterministic seed**: `HybridWFCSystem.OnCreate()` defaults to `DateTime.Now.Ticks` as the RNG seed unless `DebugSettings.UseFixedWFCSeed` is toggled. For structure placement determinism, WFC instances spawned by the dungeon realizer must receive a seed derived from the anchor's `StableAnchorId` (e.g. `hash(worldSeed, StableAnchorId)`), not wall-clock time. This may require a per-instance seed field on `WFCComponent` or `WFCGenerationSettings`.

#### 12.5.2 Relic Realizer

- spawns one ECS entity per accepted relic anchor with Entities Graphics render components (`RenderMeshArray`, `MaterialMeshInfo`, `RenderBounds`, `LocalToWorld`)
- per-entity `RenderBounds` for correct individual frustum culling (required for large-scale meshes; batch `RenderMeshInstanced` is unsuitable — see [RELIC_RENDER_REFACTOR_SPEC.md](../Archives/StructurePlacement_2026/RELIC_RENDER_REFACTOR_SPEC.md) (archived))
- tracks realized entities via `StructureRealizedTag.StableAnchorId` for lifecycle cleanup
- entity structure provides the natural hook for future LOD/impostor distance switching

#### 12.5.3 Multi-Template Relic Support

Different relic types (giant stone head, ruined tower, bone pile, stone circles) share the same placement family, spacing rules, and terrain-fit constraints. Visual variety is achieved through **templates** — each anchor gets a `TemplateId` that maps to a distinct mesh/material/scale configuration.

**Data model:**

- `StructureFamilyRuleset` carries `AvailableTemplateIds` — the list of template IDs valid for this family. `DefaultTemplateId` is used when the list is empty (backwards compatibility).
- `StructureAnchorPlanningSystem` assigns a `TemplateId` deterministically per anchor after the planning algorithm runs, using `StableAnchorId % templateCount` for uniform distribution.
- `RelicRenderConfig` holds a `List<RelicTemplateEntry>` instead of a single mesh/material. Each entry is keyed by `TemplateId` and carries mesh, material, scale, Y-offset, and impostor data.
- `RelicLodParams` (`IComponentData`) stores per-entity computed LOD parameters (full scale, impostor scale, mesh bounds for both LODs) set once at spawn time. This decouples `RelicLodSelectionSystem` from knowing which template an entity uses.

**Template selection is deterministic:** the same world seed always produces the same relic type at the same location. Adding new templates to the available list changes assignments for existing anchors (acceptable for pre-release; post-release would need a migration strategy).

**Invariant:** all templates within a family share `LodSwapDistance` and `LodHysteresis` (camera/scene-level settings). Per-template swap distances are a future enhancement if needed for very differently-sized templates.

### 12.6 Activation and Lifetime

`StructureActivationSystem : ISystem`

- handles near-player activation/promotions for interactive semantics
- manages teardown/demotion policy for out-of-range runtime state where allowed

### 12.7 Persistence Backing Store (Later Phase)

Storage and serialization backing may be introduced after baseline apply/record behavior, but must preserve:

- `StableAnchorId` continuity
- lock semantics
- sparse-delta persistence model

---

## 13. Persistence Model

`Structure Placement` aligns with the structure layer in [PERSISTENCE_SPEC.md](../Persistence/PERSISTENCE_SPEC.md):

- deterministic base placements regenerate from seed
- only destroyed/modified/generated-beyond-default structure state persists
- player-built structures persist as direct placement records
- persistence apply/record baseline behavior ships before family realizers

Any anchor that has diverged from seeded defaults should be marked as locked against re-roll during regeneration.

---

## 14. Invalidation and Regeneration Rules

1. Before player interaction, anchors may be recomputed when world generation version changes.
2. After a structure becomes persistent (modified, discovered, or player-built), its anchor identity and placement lock unless an explicit migration step is run.
3. Terrain edits near a persistent structure must not silently relocate the structure.
4. Streaming out should remove only runtime realization state that is safe to rebuild from anchor + persistence data.

---

## 15. Debug, Telemetry, and Testing

Required observability and validation:

- deterministic seed replay tests for anchor identity and location
- spacing invariants (no accepted anchor pairs violating minimum distance)
- overlap checks against structure reservations and scatter outputs
- realization determinism for each family
- debug overlays for accepted/rejected candidates and rejection reason categories

Logging in systems should use centralized debug settings infrastructure rather than direct `Debug.Log` calls.

---

## 16. Acceptance Criteria

- canonical plan/spec docs exist for `Structure Placement`
- runtime boundary with `Surface Scatter` and `Details` is explicit
- hard spacing and constraints are mandatory in the contract
- deterministic anchor identity and sparse persistence model are defined
- family-driven realization paths are defined for dungeons, villages, and relics
- index and related-doc links allow direct canonical discovery
