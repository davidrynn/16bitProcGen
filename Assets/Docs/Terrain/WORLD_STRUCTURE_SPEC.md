# World Macro-Structure Spec — the `H` Authority

**Status:** DESIGN — owner-ratified decisions 2026-07-18; Phase A is the next world-track build
**Last Updated:** 2026-07-18
**Owner:** Terrain / World
**Phase:** Post-vista world depth (mountains · water · persistence · WFC resume)
**Keywords:** macro heightfield, H function, mountains, lakes, water, persistence, save, determinism, WFC, pocket interiors, horizon ring, disc undulation, world seed

---

## 1. Purpose

Four post-vista concerns — **large mountains**, **water bodies**, **persistence**, and the
**WFC/dungeon resume** — are not four systems. They converge on one architectural spine: a
single seeded **world macro-structure function** that every representation of the world samples,
plus a hard determinism invariant that makes persistence a delta problem instead of a
serialization problem.

This spec defines that spine (the `H` authority, §4), the cross-track constraints that make or
break it (§5 — read this before building anything), what is deliberately **not** being built and
why (§6), and per-track briefs sized for execution sprints (§7–§10). It is the reasoning
document; each executing sprint expands its brief into dials, slices, and acceptance the way
V9/R6/V14 build records did.

**The pattern is already proven twice in this codebase.** The V9 atmosphere authority (one
palette, every distance-facing surface consumes it) and `GroundNoise.hlsl` (one world-space
noise function; terrain and disc sample it so the color seam aligns *by construction*,
guarded by `TerrainChunkMaterialContractTests`). `H` is the same move one level up: from
"shared color of the ground" to "shared shape of the world."

## 2. Decision log (2026-07-18, owner-ratified)

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| D1 | Deliverable shape | One integrated spec (this doc), per-track briefs for lower-model sprints | The value is in the couplings and contracts, not per-track prose; separate shallow specs would miss exactly the interactions §5 captures |
| D2 | V5 dungeon interiors | **Pocket space** (portal to a separately generated region), not SDF-carved | Isolates WFC from streaming/colliders/editing/lighting entirely; reuses the existing dungeon spawn path; the fiction benefits (a dead god's interior being bigger than his hand is a feature, not a bug). Carving is future W2-adjacent work — see §6.2 |
| D3 | Mountains scope | **Consistent first, reachable later** — all far/mid/near representations sample `H` now; walkable terrain stays gentle highlands; vertical-chunked reachability is a later sprint | A fraction of the cost of reachability, makes the world read as one place immediately, and *zero rework later* because every consumer already samples the same function. Reachability lands on the existing `UNDERGROUND_VERTICAL_STREAMING_SPEC.md` design |
| D4 | Water scope | **Lakes/tarns only**; rivers explicitly deferred; no ocean (standing Highlands fiction) | Still water in basins is cheap and complete (plane + shore fade + swim volume). Rivers require carving channels into `H` plus flow direction — large terrain-gen cost for mostly-visual payoff. See §6.1 |

## 3. The invariant (read first)

> **Everything procedural is a pure function of `(worldSeed, authored data)`. No system may
> consume nondeterministic state — frame time, camera, player position, load order, physics
> results — at generation time. Persistence saves only deltas against that regenerable base.**

This is already true of the load-bearing systems: relic anchors hash-jitter from stable IDs
(`StableAnchorId` was *designed* seed- and position-independent for persistence refs,
`STRUCTURE_PLACEMENT_SPEC.md` §9.5), and WFC seeds deterministically. The invariant is what
makes §9's persistence design small. Treat any violation as a correctness bug, not a style
issue. Executing sprints should encode it as tests where cheap (same seed → identical anchor
buffer / identical `H` samples / identical WFC collapse).

## 4. The `H` authority — contract

### 4.1 Function

`H(x, z)` — the world macro heightfield. World-space XZ in, world-Y offset out (units = world
units). Deterministic from `worldSeed`. Reference shape (executor tunes constants, not
structure):

```
H(x,z) = A(r) · ridgedFBM( (x,z) · macroFreq + seedOffset ; 4 octaves ) · M(x,z)
```

- **`ridgedFBM`** — ridged fractal, ~4 octaves. The V15 sky band already uses ridged FBM for
  its silhouette; `H` inherits that visual language (and eventually replaces the band's
  private noise — §7).
- **`seedOffset`** — `hash(worldSeed)` mapped into a bounded range (≤ ~10⁴). Keep noise-space
  coordinates small; do not offset by raw large integers (float precision).
- **`A(r)` — the wilderness ramp.** Amplitude envelope over distance `r` from the playfield
  origin: `A = lerp(A_near, A_far, smoothstep(rampStart, rampEnd, r))`. Suggested starting
  points: `A_near` small enough that near-field terrain stays within the current single-layer
  chunk vertical range (**executor must verify the actual chunk Y extent before setting
  this — flagged, do not guess**), `A_far` ~150–250u, ramp ~600→2500u. This is the D3
  decision *encoded in the function*: the playfield is a gentle highland bowl; the rim of the
  world rises into true mountains that live beyond the 600u world edge where only impostor
  representations render. When reachability lands (Phase 2), the ramp relaxes progressively —
  no consumer changes.
- **`M(x,z)` — authored flatten masks.** Product of smooth radial falloffs (1 = full `H`,
  0 = flat), authored via a scene bootstrap in the `AuthoredAnchorBootstrap` pattern
  (serialized inspector entries; no ScriptableObject editing — standing owner preference).
  **MVP requirement: one mask covering the spawn→hero-hand vista corridor** so macro relief
  can never occlude the MVP sightline to (0, 900). Masks are authored data → inside the
  determinism invariant.

### 4.2 Implementation home

- **`WorldStructure.cs` + `WorldStructure.hlsl`** — sibling pair to the `GroundNoise.hlsl`
  precedent: identical math in C# (Burst-friendly static, for SDF gen / placement / lake
  authoring) and HLSL (for disc undulation and the sky band / ring). **Parity is guarded by
  an EditMode fixture in the `TerrainChunkMaterialContractTests` mold** — same constants both
  sides, sampled at fixed points, asserted equal. This test is not optional; it is the seam
  guarantee.
- **Constants reach shaders as `_WorldMacro*` globals** seeded once at bootstrap (the
  `AtmosphereBroadcast` seeding pattern — editor/player init defaults so shaders never read
  zeroed globals). `H` needs **no per-frame broadcast**: it is static per world. Do not add
  one.
- **All tunables live in one serialized asset** (extend `TerrainGenerationSettings` or a new
  `WorldStructureSettings` — executor's call), because §9's save-compatibility hash covers
  exactly that asset. Scattered literals break save versioning; this is a hard rule.

### 4.3 Consumers (who may sample `H`)

| Consumer | Rep | Phase | Notes |
|----------|-----|-------|-------|
| SDF base density | near (≤180u) | C | Adds `H` to the base terrain Y term; amplitude already clamped by `A(r)` near the playfield |
| Ground-plane disc vertices | mid (180–600u) | B | The deferred **V17 P3 undulation, executed against `H`** — must damp to zero inside ~250u of the player (V17's documented streaming-replacement liability) |
| Sky mountain band silhouette | far (>600u) | B | Replaces the band's private ridged FBM with `H` sampled along azimuth at fictive distance — the fiction becomes literal (see §7 on preserving the owner-approved look) |
| Phase-2 horizon ring / R5 cards | far | later | `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` slots in as another consumer — same function, so band→ring migration cannot introduce silhouette mismatches |
| Lake placement | — | D | Basin detection + authored lake entries validate against `H` (§8) |
| Scatter/biome density (optional) | near | later | Altitude-aware scatter (sparser high) — cheap once `H` exists; ties into `TERRAIN_BIOME_NOISE_SPEC.md` |
| WFC site suitability (optional) | — | later | Anchors already terrain-snap Y via the SDF, which will include `H` — no new coupling needed |

Anything not on this table that wants `H` should be added *to this table* first — the
consumer list is the blast radius of every future `H` tweak.

## 5. Interaction matrix — the constraints that bite

This section exists because this repo's failure history is *locally plausible reasoning with
wrong global interactions* (Route A fog, `SyncTerrainColor`, two slant-concealer failures,
the zero-savings LOD swap). Each cell is a rule an executing sprint must satisfy.

**5.1 `H` × persistence.** SDF edit deltas replay against the *generated base field*. Any
change to `H` constants (or `TerrainGenerationSettings` noise dials) silently relocates the
base under saved edits. Rule: saves are stamped with a **terrain-config hash** =
`hash(worldSeed, WorldStructureSettings, TerrainGenerationSettings)`; mismatch → the save's
edits are invalid (MVP policy: offer regenerate-without-edits; migration is out of scope).
Consequence of the one-asset rule in §4.2: the hash has exactly one input surface.

**5.2 `H` × disc → skirt handoff.** The disc alpha-fades into the sky-band ground skirt at
the 600u world edge (`AtmoWorldEdgeHaze`, *horizontal* distance — the V9 P3 round-2 lesson:
never a camera-centric shell). Undulated disc vertices change the silhouette at that handoff.
Rules: (a) undulation amplitude must taper toward the disc's outer fade so the handoff edge
stays where the skirt expects it; (b) the skirt's below-line region keeps the **ground
palette** (load-bearing per V15 — recolor it and the far-clip edge returns); (c) validate at
the three altitudes that have historically broken (ground, ~40u, 400u drop).

**5.3 `H` × streaming/colliders (Phase C).** Near-field `H` must keep every streamed column
inside the current chunk vertical range (that's what `A_near` is for). The executor **must
measure** the real chunk Y budget and CI-guard it (EditMode test: max |H| inside r<300u <
budget). Collider cost: unchanged while amplitude is gentle; the nearest-first build order
(V10) needs no changes in Phase C. Reachable mountains (Phase 2, post-D3) inherit
`UNDERGROUND_VERTICAL_STREAMING_SPEC.md` — do not invent a second vertical-streaming design.

**5.4 `H` × the vista.** The MVP moment is a sightline: spawn → hero hand at (0,900) through
haze. The `M` corridor mask (§4.1) protects it. Rule: any change to masks or `A(r)` re-runs
the vista eyeball checklist (spawn view + drop view). Cheap insurance: an EditMode assertion
that `H` ≤ a few units along the sampled corridor line.

**5.5 `H` × sky band continuity.** The band's current look is owner-approved (V15 rounds
2–3). Swapping its silhouette source to `H` must start from constants tuned to *match the
current character* (frequency/amplitude at the band's fictive distance), then diverge only
deliberately. Keep the old noise path behind a shader toggle for A/B during the swap
(keep-functionality-as-toggles convention), delete after the owner approves.

**5.6 Water × atmosphere.** Water color/reflectivity must consume `_Atmo*` (horizon/ground
palette + haze) — every pre-V9 surface that held a private color clashed across the day
cycle; water will too. No private water blue. Shore fade uses depth (soft intersection), not
geometry tricks.

**5.7 Water × terrain edits.** Water is **not** in the SDF (§6.3). Edits below a lake surface
just work (the SDF changes; the plane doesn't). Draining a lake by digging its rim is
explicitly *not modeled* — accepted absurdity at MVP scale, documented so nobody "fixes" it
ad hoc; a future fluid pass owns it.

**5.8 Pocket interiors × the world.** The pocket region must be placed where it can never
collide with world systems: outside the streaming envelope, outside `H`'s domain of
consumers, outside the far plane's view of the playfield (and vice versa). Candidate: a
reserved XZ cell far outside the ±1024u structure region (executor verifies against streaming
window math, impostor coverage, and physics broadphase comfort). Portal = teleport + state
swap, not additive scene load, unless the executing sprint finds a hard blocker — single-World
keeps every existing system untouched.

**5.9 Precision.** At ≤2500u world coordinates and bounded noise-space offsets (§4.1), float
precision is a non-issue. The rule that keeps it one: noise inputs stay `position * freq +
smallOffset`; never accumulate large uncompensated offsets.

## 6. Non-goals — with teeth

Deferrals are recorded with rationale + revisit trigger so they stay decided (the
`Atmosphere.hlsl` do-not-reintroduce comment proved this works). Do not relitigate these in
build sessions; escalate to the owner with new evidence instead.

- **6.1 No rivers.** Carving credible channels requires `H`-aware erosion/flow (a full
  terrain-gen feature) and rendering flow. Payoff at MVP scale is a visual garnish lakes
  already provide. *Revisit when:* a mechanic needs flowing water, or the reachable-mountains
  sprint makes valley routing matter.
- **6.2 No SDF-carved interiors (v1).** Carving couples WFC to streaming, collider rebuilds,
  edit interaction, and interior lighting — four systems for zero MVP gain over a portal.
  *Revisit when:* W2 (destructible relics / SDF stamps) lands its mesh↔SDF bridge; carving
  then reuses that machinery instead of inventing its own.
- **6.3 Water is not in the SDF.** Water is a rendered surface + a swim volume, not solid
  field data. Putting it in the field buys nothing (it isn't walkable, isn't editable) and
  costs density-channel complexity everywhere.
- **6.4 No compute-shader WFC propagation.** The stub is unimplemented; dungeon grids are
  small; CPU propagation with Burst is the right tool. Park (delete the stub body, keep the
  seam comment). *Revisit when:* measured CPU propagation time actually hurts frame pacing on
  target hardware — a profiler number, not a vibe.
- **6.5 No ocean.** Standing owner decision (Highlands fiction). Horizon continuity is the
  atmosphere stack's job, not sea level's.
- **6.6 No per-frame `H` broadcast, no `H` texture bake (yet).** It's cheap ALU; bake only if
  a profiler says otherwise (then it's a targeted optimization with the same contract).
- **6.7 No reachable mountains in Phase C.** That's D3. The wilderness ramp is the design's
  way of making this a constants change later, not a rework.

## 7. Track brief — mountains (Phases B + C)

**Goal:** the world reads as one continuous highland bowl rising to a mountain rim; every
representation agrees because all sample `H`.

- **Phase B (visual consistency, no terrain change):** (1) `WorldStructure.cs/.hlsl` +
  parity test + `_WorldMacro*` seeding + settings asset + corridor mask; (2) sky band
  silhouette swaps to `H`-along-azimuth behind an A/B toggle (§5.5); (3) disc vertex
  undulation = V17 P3 executed against `H` (damping + handoff rules §5.2, judged after the
  V15 drop-altitude skirt check per the existing sequencing).
- **Phase C (gentle near-field):** `H` term into the SDF base density; chunk-Y-budget guard
  test (§5.3); scatter unaffected initially.
- **Phase 2 (reachable, separate sprint):** relax `A(r)` + vertical streaming per
  `UNDERGROUND_VERTICAL_STREAMING_SPEC.md`; LOD/collider budget work; explicitly out of this
  spec's scope.
- **Acceptance sketch:** band↔disc↔terrain silhouettes agree at the world edge from ground
  and 400u; vista corridor unchanged (5.4); parity test green; owner eyeball of the new rim.

## 8. Track brief — lakes (Phase D)

**Goal:** still water in `H` basins: seeded + authored lake entries (center, extent,
surfaceY), each validated `surfaceY < rim`. Authoring via bootstrap entries
(`AuthoredAnchorBootstrap` pattern). Render: flat transparent patch, depth-fade shores,
`_Atmo*`-driven color (§5.6), simple scrolling normal shimmer — no reflections/refraction at
MVP. Swim: volume trigger → the already-plumbed `SwimSpeed`/swim mode path (verify the
movement mode actually functions — it predates current movement work). Scatter suppression
under water surfaces. **Acceptance sketch:** a tarn near (but not in) the vista corridor;
readable shoreline at all times of day; swim in/out works; no scatter poking through;
determinism test (same seed → same lakes).

## 9. Track brief — persistence (Phase E, parallelizable)

**Goal:** save = `(header, deltas)`; world regenerates from seed, deltas replay.

- **Header:** save version, `worldSeed`, terrain-config hash (§5.1), timestamp.
- **Deltas v1:** per-chunk sparse SDF edit records keyed by chunk coord (the edit pipeline's
  existing buffer shapes are the natural wire format — executor confirms against
  `SDFEditTests` fixtures), replayed in application order on chunk load; player transform +
  movement mode. **Not saved:** anything regenerable (anchors, WFC results, scatter, lakes) —
  saving them would *mask* determinism bugs; their absence is the test.
- **Policy v1:** single slot; save on quit + interval; hash mismatch → load world, drop
  edits, tell the player (migration out of scope).
- **Future rows (designed-for, not built):** WFC interior state (chests/doors), W2 relic
  damage — both keyed by `StableAnchorId`, which is why it exists.
- **Acceptance sketch:** edit terrain → quit → relaunch → edits present at correct world
  positions; changing a noise dial invalidates cleanly; round-trip determinism test in
  PlayMode.

## 10. Track brief — WFC resume + V5 pocket interiors (Phase F)

**Goal:** un-pause the dungeon track on rails.

1. **Bootstrap gap** fix per `STRUCTURE_PLACEMENT_SPEC.md` §12.5.1 (the known blocker).
2. **Seeding:** per-anchor `hash(worldSeed, StableAnchorId)` replaces the single fixed debug
   seed (which stays as the dev pin).
3. **Park the compute stub** (§6.4).
4. **V5 pocket bridge (D2):** door/portal trigger on the hero relic → generate interior at
   the reserved pocket cell (§5.8) → teleport player + swap sky/atmosphere state → WFC
   collapse + existing `DungeonPrefabRegistry`/`DungeonEntitySpawningSystem` path → return
   portal. Interior atmosphere: interiors are enclosed — the atmosphere authority needs an
   "interior preset" (dark palette, zero haze) rather than new machinery.
5. **Acceptance sketch:** enter the hand → coherent generated interior → exit back to the
   exact world state; same anchor → same interior every run; zero impact on any exterior
   system while inside.

### 10.1 Code triage — `WFCCollapseSystem` (Fable review, 2026-07-18)

**Verdict: rewrite the collapse core; keep the data layer and spawn path.** The existing
`WFCCollapseSystem` (649 lines) is a prototype that *approximates* WFC, not a paused
implementation of it. Plan Phase F as "rewrite one well-understood algorithm against existing
data shapes," not "fix bugs in a working system." Findings, most severe first:

1. **The algorithm is not WFC.** There is no minimum-entropy cell selection and no
   constraint-propagation wave. Instead: a per-frame sweep over *all* cells, each pruning
   only against already-collapsed neighbors, with stochastic collapse hacks
   (`possibleCount <= 3 && rand < 0.5`, a 10% per-frame "force collapse", and a meaningless
   scalar `entropy -= 0.5` per frame). Comments admit it ("for testing: more aggressive
   collapse logic"). Contradictions (`possibleCount == 0`) are absorbed silently as
   `selectedPattern = -1` with no restart/backtrack — failed cells just become holes.
2. **Determinism is frame-coupled.** Collapse depends on per-frame RNG draws over unordered
   query iteration — results vary with frame pacing and chunk iteration order even under the
   fixed seed. This violates §3's invariant and breaks D2's "same anchor → same interior."
3. **O(n²)-per-frame with allocations.** `PruneWithCollapsedNeighbors`/`IsWallAt` call
   `ToComponentDataArray` per cell per frame; `PropagateConstraintsToNeighbors` calls
   `ToEntityArray` *inside* its loop. Fine for a 10×10 test grid; not a foundation.
4. **Dead/vestigial code:** the `ProcessWFCCellsJob` at file bottom (never scheduled;
   collapses by decaying float entropy), `HasAdjacentWall` (uncalled),
   `PropagateConstraintsToNeighbors` (entropy-nudging, not constraints), and the compute
   stub (§6.4 — park stands).
5. **Legacy coupling:** `[UpdateAfter(LegacyHeightmapTerrainGenerationSystem)]` +
   `using DOTS.Terrain.Legacy` tie it to the quarantined pipeline; the rewrite must not
   reference `DOTS.Terrain.Legacy` (CLAUDE.md quarantine rule). Also `SystemBase` where the
   project convention is `ISystem`.

**Salvageable as-is (the good news):** the data layer — `WFCCell`'s 32-bit possibility mask +
`WFCCellHelpers`, the `WFCPatternData`/`WFCConstraintData` blob shapes, `WFCBuilder`'s
macro-tile pattern definitions and `PatternsAreCompatible` edge rules — and the downstream
spawn path (`DungeonPrefabRegistry` → `DungeonEntitySpawningSystem`), which the board already
records as functional. The rewrite slots between them.

**Rewrite shape (small, well-bounded):** one Burst-compiled routine, classic WFC:
initialize domain masks → loop { pick min-entropy uncollapsed cell (deterministic tie-break:
hash(cell index, seed)) → collapse via `Random(hash(worldSeed, StableAnchorId))` → propagate
constraints outward via a BFS queue until fixpoint } → on contradiction, restart with
`seed+attempt` (bounded attempts, then flag failure loudly). Grids are small: run to
completion in one frame or time-slice by iteration count — either way the result is a pure
function of the seed, satisfying §3 by construction. EditMode tests: same seed → identical
grid; all edges compatible post-run; contradiction restart works; no `Legacy` references
(assembly-level assertion if cheap).

## 11. Sequencing & handoffs

```
Phase A  (foundation)   WorldStructure.cs/.hlsl + parity test + settings asset + masks
Phase B  (consistency)  sky band → H (A/B toggle) · disc undulation (V17 P3 on H)
Phase C  (near-field)   H term in SDF base + chunk-Y guard
Phase D  (lakes)        placement + render + swim
Phase E  (persistence)  deltas + hash — parallel to B–D after A
Phase F  (WFC + V5)     bootstrap fix · per-anchor seeds · pocket interiors
```

A is the only hard prerequisite; B/C/D/E can interleave with remaining vista polish. Each
phase is sized for an Opus-class sprint that expands its brief into slices/dials/acceptance
(the V9/R6/V14 build-record pattern) and **must read §5 before coding**. Ticketing: propose a
new track letter for world-structure work and one for persistence when pulling Phase A into
the board (ticket IDs are owner-discussed per the workflow convention — not assigned here).

## 12. Open questions (deliberately few)

1. ~~Chunk vertical budget~~ **Answered (code-verified 2026-07-18):** the chunk grid is
   **single-layer** (`TerrainBootstrapAuthoring` spawns `ChunkCoord = (x, 0, z)` only) with
   16³ voxels at `VoxelSize` 1 (LOD0; LOD1/2 are coarser samplings of the same ~16u slab).
   Total vertical window ≈ **16u**, already shared with the existing ±4u surface noise —
   so **`A_near` ≈ ≤4u**, materially tighter than the §4.1 guess. Consequences: (a) the
   near-field `H` term in Phase C is a *subtle* rolling-relief pass, which is fine — D3 puts
   real relief beyond the world edge; (b) reachable mountains (Phase 2) strictly require the
   vertical-chunking work (`UNDERGROUND_VERTICAL_STREAMING_SPEC.md`) — confirmed assumption,
   now with numbers; (c) the §5.3 guard test asserts `|H| + noiseAmplitude` fits the 16u slab
   inside the streamed radius.
2. ~~Swim mode status~~ **Answered (code-verified 2026-07-18):** `SwimSpeed` exists in
   `PlayerMovementConfig` (set 6f at bootstrap) but **no `Swimming` movement mode or branch
   exists in `PlayerMovementSystem`** — it is config-only, never implemented. Phase D scopes
   *building* the swim mode (mode enum entry, buoyancy/drag branch, water-volume detection),
   not just a trigger volume. Budget Phase D accordingly.
3. ~~Pocket cell coordinates~~ **Answered (code-verified 2026-07-18): the pocket goes *below*
   the slab, not far away in XZ.** `TerrainChunkStreamingSystem` maps player XZ → chunk coords
   with a hardcoded Y=0 layer (`ChunkCoord = (x, 0, z)`), so terrain exists *only* in the ~16u
   slab regardless of player depth — any Y well below it (suggest ≈ −300) is guaranteed void:
   no SDF ground, no colliders, no scatter, ever. A far-XZ pocket would be *wrong*: streaming
   follows player XZ and would happily generate terrain around any XZ you teleport to. At
   Y≈−300 the streamed slab and the player-following disc sit ~300u overhead — irrelevant
   inside an enclosed interior (and the interior atmosphere preset from §10.4 suppresses the
   sky). Physics broadphase is comfortable at these coordinates. Fiction bonus: entering the
   hand *descends into the god's remains* — the portal goes down, which is better than a
   teleport to nowhere.

   **Safety-system check (code-verified 2026-07-18): living below the slab is safe; the
   teleport itself is the trap.** `PlayerTerrainSafetySystem` has no "must have ground below"
   or absolute-Y assumption — it only snap-backs when the prev→current ray hits a collider
   (tunneling), works on any collidable geometry (its own doc comment already claims dungeon
   support), and is inert while grounded on interior prefab floors. **But a portal teleport is
   a huge single-frame displacement whose prev→current ray crosses the terrain slab — the
   safety net would "rescue" the player straight back out of the dungeon** (once per 0.5s
   cooldown). Hard rule for the Phase F portal: on teleport, write
   `PlayerMovementState.PreviousPosition = destination`, zero `PhysicsVelocity`, and reset
   `FallTime` in the same frame — the precedent is exactly how the readiness gate seeds
   `PreviousPosition` on release (`PlayerStartupReadinessSystem`). Any future fast-travel
   inherits this rule.
4. Whether `WorldStructureSettings` is a new asset or a `TerrainGenerationSettings` section —
   executor decides; the hash rule (§4.2) holds either way.

## Related Docs

- `Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` — the authority pattern this generalizes
- `Rendering/SKY_MOUNTAIN_BAND_SPEC.md` — current band; Phase B swaps its silhouette source
- `Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md` §12 — V17 P3 undulation (Phase B executes it)
- `Rendering/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` — Phase-2 ring; future `H` consumer
- `Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md` — reachable-mountains landing pad
- `Terrain/TERRAIN_BIOME_NOISE_SPEC.md` — biome noise; `H` is the macro layer above it
- `Structures/STRUCTURE_PLACEMENT_SPEC.md` §9.5/§12.5.1 — `StableAnchorId`, WFC bootstrap gap
- `../Tickets/TICKETS.md` — board; Phase A ticketing pending owner discussion
