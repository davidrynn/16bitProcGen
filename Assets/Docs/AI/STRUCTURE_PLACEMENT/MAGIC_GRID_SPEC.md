# Magic Grid Spec
_Status: DESIGN - analytic world-space magic lattice: power sources, a building affordance, and a placement oracle_
_Last updated: 2026-06-29_
_Owner: World Generation / Structure Placement_

---

## 1. Purpose

Define the **magic power grid**: a regular, deterministic, world-space lattice whose intersections
("nodes") are **sources of magic power** in the game world. The grid serves three roles:

1. **Power source (lore).** Nodes are where world magic concentrates. This is the diegetic reason
   power-significant content clusters at intersections rather than scattering arbitrarily — it reads
   as authored, not gamey.
2. **Building affordance (gameplay).** This is a building-and-exploration game. The player builds
   **freeform, piece-by-piece** anywhere; but **on a node** they can build with **WFC assistance** —
   power-assisted construction of cities and fortresses whose central seat *is* the node.
3. **Placement oracle (generation).** Nodes are a deterministic candidate source for node-bound
   content, which collapses that content's placement from an area scan to an O(nodes) check.

The grid's defining property is that it is **analytic** — computed from world position, never stored
or sampled. The untouched world is therefore free to query; only nodes the player has *claimed or
built on* carry sparse state. This keeps the grid cheap while making it a real gameplay system.

---

## 2. Scope

This spec covers:

- a world-space 2D (XZ) lattice defined by a single spacing value, decoupled from chunk size
- pure analytic query functions: nearest node, is-on-node, is-on-line, per-node hash identity
- the **building affordance** rule: WFC-assisted building on nodes, freeform off nodes
- **sparse claimed-node state** (alignment / ownership / what is built) tied to `StableAnchorId`
- a per-template **`NodeAffinity`** model so node-binding is a property of the thing, not the family
- the **two-sources-one-pipeline** relationship between the grid and the free/chunk placement system
- a universal analytic **power-influence query** that affects content regardless of how it was placed
- a subtle terrain visual cue (additive brightness volume at nodes), with air-warp deferred

## 3. Non-Goals

This spec does **not** cover:

- the Y axis — the grid is a ground-plane (XZ) lattice only; vertical structure is out of scope
- coupling the grid to chunk boundaries or any terrain-pipeline artifact (explicitly forbidden — §5)
- **dynamic energy**: flow networks, propagation, depletion, or per-frame grid simulation
- **live NPC migration / attraction simulation** driven by node alignment (see §8.3 — evaluate lazily)
- air-warp / heat-haze refraction (deferred polish — see §10.3)
- reusing the grid as the engine's spatial index for streaming/culling (see §9.4 — explicitly cautioned)
- replacing the free/chunk placement system for node-independent content

> **In-scope change from prior revision:** player-activated nodes (claiming, alignment, building) were
> previously a Non-Goal. They are now in scope as **sparse state on touched nodes** (§8.3). The
> untouched *world* remains static and analytic; only claimed nodes carry records.

---

## 4. Related Docs

- [STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md) - shared anchor pipeline the grid feeds into; per-template support (§12.5.3) that `NodeAffinity` extends
- [STRUCTURE_PLACEMENT_PLAN.md](STRUCTURE_PLACEMENT_PLAN.md) - rollout sequencing for structure families (relic/dungeon)
- [../PERSISTENCE_SPEC.md](../PERSISTENCE_SPEC.md) - sparse-delta persistence model that claimed-node state reuses
- [../../WFC/MAP_WFC.md](../../WFC/MAP_WFC.md) - WFC realization for node-built cities/fortresses
- [../TERRAIN_ECS_NEXT_STEPS_SPEC.md](../TERRAIN_ECS_NEXT_STEPS_SPEC.md) - SDF terrain pipeline the visual cue shader rides on
- [../RENDER_PERF_PROFILE_REPORT.md](../RENDER_PERF_PROFILE_REPORT.md) - scene is vertex-bound; the analytic shader cue adds no geometry

---

## 5. Core Decisions

These are settled and constrain the design:

1. **2D, XZ only.** The grid is a ground-plane lattice. Y is ignored for queries and the visual cue.
   (Revisit only if flying/underground structures later need vertical nodes.)
2. **Decoupled from chunks.** `Spacing` is its own world-space value, never derived from chunk
   dimensions. Chunk size is a pipeline artifact; binding gameplay to it would silently relocate all
   grid content if chunk size changed, and would stack content onto the seam/LOD edges where
   complexity already lives. The grid *may* be chosen as a multiple of chunk size for incidental
   alignment, but nothing in code reads chunk size to derive the grid.
3. **Large spacing.** Intersections are rare power landmarks, not a fine mesh. Default target on the
   order of 128–256 world units (tunable). Large spacing keeps the visual cue and node markers sparse.
4. **Static world, sparse claimed state.** Untouched nodes have no stored state — they are pure
   analytic positions. Only nodes the player claims/builds on carry a record (§8.3). There is no
   per-frame grid simulation.
5. **Analytic, not stored.** No lattice data is baked or sampled. Every positional query is a pure
   function of world position, Burst-safe, zero allocation.

---

## 6. Data Contract

A single singleton component defines the lattice geometry. There is no per-node storage for untouched
nodes; claimed nodes are stored sparsely (§8.3).

```csharp
using Unity.Entities;

namespace DOTS.Generation
{
    /// <summary>
    /// World-space magic lattice geometry. Deliberately decoupled from chunk size — this is a
    /// gameplay structure, not a pipeline artifact. Stored once as a singleton; positional queries
    /// are pure functions of world position, so nothing about untouched nodes is sampled or held
    /// per-frame. XZ ground-plane only; Y is ignored.
    /// </summary>
    public struct MagicGrid : IComponentData
    {
        public float Spacing;        // world units between lines; large (128–256), chunk-independent
        public float NodeRadius;     // how close to an intersection counts as "on" the node (logic)
        public float LineRadius;     // how close to a line counts as "on" it (visual cue / weaker effects)
        public float InfluenceRadius;// power-influence reach of a node for nearby content (§9.3)
        public uint  Seed;           // deterministic per-node identity; mirrors the fixed-seed WFC philosophy
    }
}
```

---

## 7. Query Oracle

All positional queries are static, stateless, and analytic. This is what makes the grid cheaper than
the area-based placement it can replace — candidate locations are *computed*, not searched.

```csharp
using Unity.Burst;
using Unity.Mathematics;

namespace DOTS.Generation
{
    /// <summary>
    /// Stateless queries against a <see cref="MagicGrid"/>. Pure functions of XZ world position —
    /// no storage, no scan. The visual cue (§10) uses the same math, guaranteeing the glowing
    /// lattice sits exactly where these predicates return true.
    /// </summary>
    [BurstCompile]
    public static class MagicGridMath
    {
        // Nearest intersection (the "power node") to any XZ world position.
        public static float2 NearestNode(in MagicGrid g, float2 worldXZ)
            => math.round(worldXZ / g.Spacing) * g.Spacing;

        // Is this position sitting on an intersection? (node-bound content + the player-on-node
        // building-affordance check)
        public static bool IsNode(in MagicGrid g, float2 worldXZ)
            => math.distance(worldXZ, NearestNode(g, worldXZ)) <= g.NodeRadius;

        // Is this position on a line but not necessarily a node? (weaker effects / conduits / cue)
        public static bool IsOnLine(in MagicGrid g, float2 worldXZ)
        {
            float2 d = math.abs(worldXZ - math.round(worldXZ / g.Spacing) * g.Spacing);
            return math.min(d.x, d.y) <= g.LineRadius;
        }

        // Is this position within the power influence of its nearest node? (universal effect query,
        // independent of how the object was placed — see §9.3)
        public static bool IsUnderInfluence(in MagicGrid g, float2 worldXZ)
            => math.distance(worldXZ, NearestNode(g, worldXZ)) <= g.InfluenceRadius;

        // Deterministic per-node hash — decide *what* a node is, and seed WFC, without storing
        // anything. Same node coordinate always yields the same value, mirroring UseFixedWFCSeed.
        public static uint NodeHash(in MagicGrid g, float2 nodeXZ)
        {
            int2 cell = (int2)math.round(nodeXZ / g.Spacing);
            return math.hash(new int3(cell, (int)g.Seed));
        }

        // Stable identity for a node, used as StableAnchorId for claimed/built nodes (§8.3).
        public static int2 NodeCell(in MagicGrid g, float2 worldXZ)
            => (int2)math.round(worldXZ / g.Spacing);
    }
}
```

---

## 8. Runtime Roles & Cost

### 8.1 Building affordance (primary gameplay role)

The grid's headline purpose is a build-mode rule, decided by one query when the player initiates a build:

```csharp
bool wfcAssisted = MagicGridMath.IsNode(grid, playerXZ);
// on a node  → WFC-assisted construction (cities/fortresses; the node becomes the seat of power)
// off a node → freeform, piece-by-piece building
```

This is a handful of Burst-compiled ops — effectively free. WFC realization is therefore triggered by
**player action on a node**, seeded by node identity + the player's alignment choice (§8.3), not only
at world-generation time.

### 8.2 Why this is cheap, and reduces rather than adds compute

Positional queries (`IsNode`, `NearestNode`, `IsUnderInfluence`) are pure functions — no memory, no
scan. For node-bound placement, candidate generation drops from O(area) to O(nodes-in-region). The
only added cost is the visual cue (§10), which is GPU-side and rides on terrain shading already
happening. The untouched world stays free; cost scales only with the sparse set of claimed nodes.

### 8.3 Claimed-node state (sparse, persistence-aligned)

A node gains state only when the player claims/builds on it — alignment ("good"/"bad" as the player
chooses), ownership, and what was built. This slots directly into the existing Structure Placement
persistence model:

- **Untouched node** → analytic, regenerates from seed, zero stored state.
- **Claimed/built node** → a sparse record keyed by `StableAnchorId`, where the id derives from
  `NodeCell` (§7) so identity is stable across replays. Persisted as a sparse delta, exactly like any
  modified structure (see [PERSISTENCE_SPEC.md](../PERSISTENCE_SPEC.md)).

```csharp
/// Sparse state for a node the player has claimed or built on. Untouched nodes have no record.
public struct ClaimedNode : IBufferElementData
{
    public int2  Cell;        // NodeCell identity; also the StableAnchorId source
    public sbyte Alignment;   // player-chosen good(+)/bad(-) lean; 0 = neutral
    public uint  OwnerId;     // player/faction claim
    // realized structure(s) reference this via StableAnchorId, reusing StructureAnchorRecord
}
```

**Compute note:** alignment influences gameplay (e.g. which content a node attracts or empowers), but
this must be evaluated **lazily / event-driven** (on visit, on load, as a weight), *not* as a live
per-frame migration simulation. Live attraction dynamics are an explicit Non-Goal (§3) — the place
where compute would grow if added, analogous to dynamic energy flow.

---

## 9. Relationship to the Free / Chunk Placement System

The grid and the existing free placement system (hashed planning cells, per
[STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md) §9.2) are **not** competing pipelines. They
relate at two well-defined layers, and node-binding is decided per **template**, not per family.

### 9.1 Per-template `NodeAffinity` (decide at the thing, not the family)

"Relic" is not the right unit for the node decision — an ancient shrine belongs on a node, a fallen
god's hand does not, yet both are relics. Node-binding is therefore a property of the **template**,
extending the existing per-template support (`TemplateId`, `AvailableTemplateIds`,
[STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md) §12.5.3):

```csharp
public enum NodeAffinity : byte
{
    NodeIndependent = 0,  // placed by the free/chunk system (e.g. hand of a fallen god); default
    NodeBound       = 1,  // only ever placed on a node (e.g. ancient shrine, temple, seat of power)
}
```

Kept **binary** for determinism — "some relics suit nodes" is handled by tagging *those specific
templates* `NodeBound`, not by a probabilistic maybe. This also makes the family taxonomy less
load-bearing: content need not be perfectly sorted into families when each template declares its own
node relationship.

### 9.2 Upstream — two candidate sources, one acceptance pipeline

- **Grid source** produces node candidates for `NodeBound` templates.
- **Free/chunk source** produces scattered candidates for `NodeIndependent` templates.
- Both feed the **same** `StructureAnchorRecord` buffer and the **same** downstream machinery:
  footprint reservation, spacing, deterministic tie-break ([STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md)
  §9.2.1), and persistence by `StableAnchorId`.

Once accepted, a candidate is just an anchor — the source that generated it no longer matters.
Conflict (a free relic wanting a spot a node-city claimed) resolves via the **existing
footprint-reservation + tie-break logic**, with node content able to reserve first / take priority.

For `NodeBound` candidate generation, iterate the intersections in a region and ask a deterministic
per-node question — O(nodes), not O(area):

```csharp
// Candidate sites = grid intersections in the region. Only a handful fall inside a region.
for (/* each intersection nodeXZ within region bounds */)
{
    uint h = MagicGridMath.NodeHash(grid, nodeXZ);
    if (NodeHostsContent(h))                       // deterministic, seed-stable
        AcceptNodeAnchor(nodeXZ, templateForNode(h));
}
```

### 9.3 Downstream — one universal influence query (the runtime bridge)

Regardless of *how* an object was placed, any object can ask whether it falls under a node's power:

```csharp
bool affected = MagicGridMath.IsUnderInfluence(grid, objectXZ);
// + nearest node's alignment from ClaimedNode, if any
```

This is the relationship that ties the two systems together at gameplay time. A fallen god's hand
placed **independently** by the free system can still sit inside a node's influence radius and be
affected by it — glow benevolently near a good-aligned node, twist near a corrupted one — **without
the node having placed it.** Influence is analytic (`NearestNode` + distance) and universal.

### 9.4 Keep concerns separate (and don't overload the grid as a spatial index)

| Concern | Scope | When | Decides |
|---|---|---|---|
| **Placement affinity** | per-template (`NodeBound` / `NodeIndependent`) | generation-time | *which source places it* |
| **Power influence** | universal — any object, any source | runtime, analytic | *which node affects it* |

Placement affinity is upstream and discrete; influence is downstream, universal, and continuous.

**Caution:** do **not** reuse the magic grid as the engine's spatial index for streaming/culling/broad-
phase. Its spacing is chosen for gameplay feel (large, lore-flavored), not query granularity, and
chunks already provide spatial partitioning. The valuable form of "nodes tracking large objects" is
the **gameplay influence query** above (content/semantics), not a performance bucket. Overloading it
would couple a perf structure to a gameplay-tuning knob.

---

## 10. Visual Cue

### 10.1 Intent

A subtle **area** effect (brightness now; air-warp later) that reads as "this zone is charged,"
centered on intersections — not flat lines painted on the ground. The player should sense the grid
ambiently rather than see a literal wireframe.

### 10.2 Brightness (committed)

An additive glow volume at each near-camera node: a translucent dome/sphere mesh with an emissive
shader that fades from node center outward, plus bloom in post. Properties:

- **Sparse by construction.** Large spacing means only one or two nodes are ever on screen, so the
  transparent draws are negligible. Spawn markers only for nodes near the camera — derive the
  candidate set from `NearestNode(playerXZ)` plus its immediate neighbors.
- **Instanced.** Use `Graphics.RenderMeshInstanced` for the node markers. The legacy
  `Graphics.DrawMeshInstanced` is invisible under URP and must not be used.
- **Consistent with logic.** The fade uses world-distance-from-node, so the visible glow is exactly
  the intersection zone `IsNode` cares about — no drift between what the player sees and what the
  game checks.
- **Alignment-tinted (optional).** A claimed node's brightness color can lean to its alignment
  (good/bad), reading the sparse `ClaimedNode` state; untouched nodes use a neutral hue.
- **No project-wide render change.** Additive brightness needs no special URP setting, so this layer
  is not blocked on render-pipeline configuration.

### 10.3 Air-warp / heat-haze (deferred polish)

A refraction shimmer over the zone is desirable but explicitly deferred. It requires the warp shader
to sample **Scene Color**, which under URP needs **Opaque Texture enabled** in the URP asset — a
per-frame buffer copy with a small always-on cost plus transparent overdraw. Adding it later is a
localized change: enable Opaque Texture and swap the brightness volume's shader for a distortion
variant. Not gated for the first pass.

---

## 11. Content Relationship Summary

| Content | Relationship to node |
|---|---|
| **Cities** | Node-**centered** — node is the seat/temple; city sprawls terrain-adaptively around it. Built via WFC on the node. |
| **Fortresses / player power-builds** | Node-**enabled** — WFC-assisted building is *unlocked* on a node; freeform piece-by-piece off-node. |
| **`NodeBound` relics** (shrines, altars) | Node-**placed** — only spawn on nodes; small central footprint, same forgiving case as a seat. |
| **`NodeIndependent` relics** (fallen god's hand) | Node-**independent** placement; still node-**influenced** if within a power radius. |

Terrain-fit note for node-centered content: only the **central seat** must sit on the node (small
footprint); the city/fortress sprawls outward and adapts to terrain. The optional soft-bind (slide the
seat to the best terrain within a small radius of the node, or yield nothing if none fits) keeps the
legible "settlements cluster on the grid" read without requiring a large flat pad.

---

## 12. Acceptance Criteria

- a canonical `Magic Grid` spec exists and is discoverable from the index and the
  `Structure Placement` related-docs links
- lattice geometry is a single analytic singleton; untouched nodes have no per-node storage
- positional queries are pure, Burst-safe, XZ-only, and chunk-independent
- the **building affordance** (WFC on node, freeform off node) is specified as a single near-free query
- **claimed-node state** is sparse, keyed by node-cell `StableAnchorId`, and reuses the sparse-delta
  persistence model; alignment effects are specified as lazy, not a live simulation
- node-binding is a **per-template `NodeAffinity`**, not a per-family rule
- the grid and free/chunk systems are specified as **two candidate sources into one pipeline**, bridged
  at runtime by a **universal analytic influence query**
- the "don't reuse the grid as a spatial index" caution is explicit
- the visual cue is additive brightness now, with air-warp explicitly deferred and its URP Opaque
  Texture dependency noted

---

## 13. Open Questions (for when this moves from design to build)

- **Spacing value:** exact default within the 128–256 band, and whether to pick a chunk-size multiple
  for incidental alignment (without code reading chunk size).
- **`NodeHostsContent` rule:** how densely nodes host major content — are *all* nodes buildable seats,
  or do only some hash-qualify for pre-generated cities while all remain player-buildable?
- **Alignment model:** the range/semantics of good↔bad, how the player sets it, and which content/effects
  read it.
- **Soft-bind radius:** the search radius for sliding a node-centered seat to viable terrain, and the
  "yield nothing if none fits" threshold.
- **Marker authoring:** the dome/volume mesh + emissive material asset for the brightness cue.
