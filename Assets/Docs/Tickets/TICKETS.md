# Tickets

**Status:** ACTIVE
**Last Updated:** 2026-07-21

Lightweight task tracker. Status: `[ ]` pending · `[x]` done · `[-]` blocked

**Ticket ID scheme** (canonical): prefix = track, number increments within the track and is never
reused. `V` Vista Moment · `C` Camera Feel · `A` Animation · `M` Movement · `P` Phase 1 core ·
`W` World Power/WFC · `R` Rendering · `T` Testing · `B` Biome Art · `H` World Structure (the `H`
macro-structure authority — `WORLD_STRUCTURE_SPEC.md`).

This tracker runs as **Kanban**: backlog → current focus → done. Work-sets are scoped by content,
not timeboxed — a work-set stays "current focus" until it's actually done, however long that takes.

---

## Completed work-set: Vista Moment _(closed 2026-07-21)_

**Owner call 2026-07-21: the vista is good enough — ship it and move on.** The MVP wow moment is
built and validated end-to-end: hero hand guaranteed at (0, 900) reading through haze from spawn
(V11/V12/V16), landmark draw distance so it never culls (R6), one atmosphere authority driving sky,
disc and terrain (V9), the mountain band (V15), mid-field disc variation (V17), and the meteor
arrival beat (V13/V14). Rather than hold the work-set open for a queue of owner eyeball passes on
polish that is already at ship quality, **V9 · V13 · V15 · V17 · R6 are closed as-is** and their
residual polish is bundled into backlog ticket **V20**.

Full detail for every ticket — build history, re-scope notes, and the Camera Feel / Animation
framing — lives in [`done/vista-moment.md`](done/vista-moment.md), moved there untouched per the
kanban convention.

| Closed as-is | Residual (→ V20 backlog) |
|---|---|
| V9 Atmosphere color authority | P5 saturation grade; full day/night sweep; drop-altitude P3 seam check |
| V13 Burning-descent VFX | owner eyeball of burn/sparks/fade band; **comet SFX blocked — no audio pipeline exists** |
| V15 Sky mountain band | drop-altitude skirt check |
| V17 Mid-field disc variation | owner eyeball of P4 in normal play; P3 vertex undulation → now a World-Structure Phase B item (V17 P3 executes against `H`) |
| R6 Landmark draw distance | eyeball the 0.5 s spawn dissolve; permanence at 1500u+ |

**Also carried out of the work-set, unfinished** (see Backlog): **V18** hero hand variants ·
**V19** hero rubble mound (the hand still reads as floating — highest-value Vista follow-up) ·
**C1–C3** Camera Feel (never started) · **A2/A3/A8/A9** Animation.

---

## Completed work-set: World Structure — Phase A (the `H` authority) _(closed 2026-07-19)_

Foundation phase of the post-vista world-depth track. Spine + reasoning:
[`Terrain/WORLD_STRUCTURE_SPEC.md`](../Terrain/WORLD_STRUCTURE_SPEC.md) (§11 sequencing, §4 the `H`
contract, **§5 interaction matrix — read before coding**). Phase A is **pure foundation**: it stands
up the seeded macro-heightfield function, its shader globals, its settings/save-hash surface, and the
vista-protecting corridor mask — **wiring zero consumers and producing zero visual change** (sky
band → B, disc undulation → B, SDF near-field → C). Track/ticketing owner-ratified 2026-07-19.
**Phase A COMPLETE 2026-07-19 (H1 + H2 + H3; full `H` EditMode suite 19/19 green) — B/C/D/E now unblocked.**

**Decision (resolves spec §12 open-Q4):** tunables live in a **new `WorldStructureSettings` asset**,
not an extension of `TerrainGenerationSettings` — the latter is in `DOTS.Terrain.Legacy` (quarantined,
CLAUDE.md forbids extending). New asset is the only quarantine-safe home for the §5.1 save-hash surface.

| ID  | Status | Subject |
|-----|--------|---------|
| H1  | [x] | `WorldStructure.cs`/`.hlsl` sibling pair + parity test — `H(x,z)` = ridgedFBM(~4 oct) × `A(r)` wilderness ramp × `M(x,z)` flatten mask, identical math in Burst C# and HLSL (the `GroundNoise.hlsl` precedent). New `WorldStructureSettings` asset holds **all** tunables (single save-hash input surface, §4.2/§5.1). EditMode parity fixture in the `TerrainChunkMaterialContractTests` mold (same constants both sides → equal samples at fixed points) + determinism assertion (same seed → identical field). The hard prerequisite for B/C/D/E. **Built 2026-07-19** — `Assets/Scripts/DOTS/Terrain/WorldStructure.cs` (Burst-static `H`; ridged transform `(1−\|2n−1\|)²` inherited from V15 §5.5; noise a private copy of `GroundNoiseCore` so the §5.1 hash surface stays decoupled) + `Assets/Shaders/WorldStructure.hlsl` (line-for-line mirror, no per-frame globals yet) + `WorldStructureSettings.cs` (all dials + FNV-1a `ComputeConfigHash`; `M`=1, mask is H3). `WorldStructureParityTests` **8/8 green** (determinism, [0,1] ridged, ramp endpoints/monotonic, envelope bound, §5.3 near-field slab guard preview, seed bounding, hash field-sensitivity). NUnit can't run HLSL → parity = structural mirror + C# pins, same as the `GroundNoise` contract tests. Concrete `.asset` instance + Resources-load path deferred to H2 (its consumer) |
| H2  | [x] | `_WorldMacro*` global seeding — one-shot bootstrap broadcast of `H`'s constants → shader globals (the `AtmosphereBroadcast` editor+player init pattern) so Phase-B HLSL consumers never read zeroed globals. **No per-frame broadcast** — `H` is static per world (§4.2, §6.6). **Built 2026-07-19** — `WorldStructureBroadcast` (static, `RuntimeInitializeOnLoad` + editor `InitializeOnLoad`, `Push`/`PushFromSettings`); `_WorldMacro*` globals + `SampleWorldMacroHeightGlobal(worldXZ)` wrapper added to `WorldStructure.hlsl` (Phase-B's consumer surface); concrete `Assets/Resources/WorldStructureSettings.asset` created + `DefaultConstants` fallback (never-zeroed guarantee). `WorldStructureBroadcastTests` 3/3 (global-name wiring via `GetGlobal*`, default/field lockstep, non-degenerate seed) — full H suite 11/11 |
| H3  | [x] | Corridor mask + guard tests — author the spawn→hero-hand (0, 900) flatten mask via the `AuthoredAnchorBootstrap` serialized-entry pattern (no ScriptableObject editing). EditMode guards: `\|H\| ≤ ~few u` along the corridor sightline (§5.4 vista protection) and `\|H\| + noiseAmplitude` fits the ~16u chunk slab inside r<300u (§5.3 guard; `A_near ≤ ~4u` per resolved spec Q1). **Built 2026-07-19** — `WorldStructureMask` (capsule-segment falloff regions, `M = ∏ smoothstep`; `SampleWithMask` = the full `H = A·ridgedFBM·M`) mirrored in `WorldStructure.hlsl` (`WorldMacroMask` folded into `SampleWorldMacroHeightGlobal` so consumers can't forget it). Broadcast seeds `WorldStructureMask.DefaultVistaCorridor` by default → **sightline protected even without a scene component**; `WorldStructureMaskBootstrap` (optional, AuthoredAnchorBootstrap pattern) is the in-scene authoring surface. Guards `WorldStructureMaskTests` 7/7 (sightline flat §5.4, slab budget §5.3, [0,1], far=full-relief, determinism) + broadcast mask-wiring test — **full H suite 19/19**. Scene wiring deferred (broadcast default already guarantees protection; scene has a pending owner toggle) |

Downstream (not this work-set): Phase B (sky band → `H`, disc undulation = V17 P3 on `H`), Phase C
(near-field `H` in SDF base), Phase D (lakes), Phase E (persistence — parallel after A), Phase F (WFC
resume + V5 pocket interiors). Sequencing: `WORLD_STRUCTURE_SPEC.md` §11.

---

## Backlog

_Tickets not yet pulled into a work-set._

| ID  | Subject | Group |  
|-----|---------|-------|
| [V18](backlog.md#v18--hero-hand-weathered--ruined-variants) | Hero hand weathered/ruined variants — Blender fracture cuts on the V11 cage before the subsurf bake | Vista follow-up |
| [V19](backlog.md#v19--hero-hand-rubble-mound-base) | **Hero hand rubble mound base** — the hand still reads as *floating* from spawn; Blender-authored mound joined into the hero mesh. Highest-value Vista follow-up | Vista follow-up |
| [V20](backlog.md#v20--vista-residual-polish-bundle) | Vista residual polish bundle — V9 P5 saturation + day/night sweep, V13/V15/V17/R6 owner eyeballs. Deferred 2026-07-21 as good-enough | Vista follow-up |
| [C1–C3](backlog.md#c1c3--camera-feel-slingshot) | Camera Feel — charge pullback + FOV narrow (C1), launch FOV punch + speed lines (C2), landing dip + dust burst (C3). Never started | Camera Feel |
| [A2/A3/A8/A9](backlog.md#a2a3a8a9--animation-carried-out-of-the-vista-work-set) | Animation — A9 first-person arms viewmodel (the real FPS-only payoff, rigging started 2026-07-12); A2/A3/A8 third-person body, dev-toggle only | Animation |
| M1  | ~~Glide mechanic (Space hold → GlideCharging → Gliding)~~ **APPEARS BUILT — verify & close (2026-07-21).** Code check during the MASTER_PLAN reconciliation found `Assets/Scripts/Player/Systems/GlideSystem.cs` implementing both mode transitions, created by `DotsSystemBootstrap` under `EnableGlideSystem` (code default `true`, `ProjectFeatureConfig.asset` = 1), plus `CameraGlideFeedbackSystem` and animator states. Ticket has been sitting in the backlog as unbuilt. Needs one in-play confirmation (Space-hold → glide → landing feels right), then close — or re-scope to whatever is actually missing | Movement |
| M2  | Chain slingshot (chain window + additive velocity) | Movement |
| M3  | Thermal columns (vertical lift volumes) | Movement |
| [M4](backlog.md#m4--bug-ballistic-takeoff-false-grounding-past-jump-apex-codex-review-2026-07-02) | BUG: Ballistic-takeoff false-grounding past jump apex — suppress by contact/separation, not velocity sign | Movement |
| [M5](backlog.md#m5--harden-sky-drop-landing-against-high-speed-tunneling-open--spun-off-from-v7-2026-07-03) | Harden sky-drop landing against high-speed tunneling (thin/absent collider under landing XZ; no CCD) | Movement |
| M6  | ~~Terrain editing no longer works. Shifting to edit-mode then attempting to edit does nothing~~ **FIXED 2026-07-19** — root cause was config, not code: `ProjectFeatureConfig.asset` had `EnableTerrainEditInputSystem: 0` (code default is `true`), so `TerrainEditInputSystem` was never created at bootstrap — Tab still toggled edit mode but Q/E/click had no handler. Flipped the asset flag to 1 (via MCP); no errors on play-start. Actual Q/E carve/fill still owner-verify in play. NB pre-existing edit issues if exercised: BUG-004 (BlobAssetReference on edit raycast), BUG-008 (edit-buffer growth) | Terrain |
| P1  | Basic HUD (charge indicator + chain window indicator) | Phase 1 |
| P2  | Magic Hand System (raycast, charge, binary terrain edit) | Phase 1 |
| E1  | Blocked-edit visual feedback — red-X reticle pulse (+ optional tooltip) when a terrain edit is rejected by the player-safety volume. Post-MVP: terrain editing itself needs substantial work first (owner 2026-07-03; salvaged from archived Cursor plan) | Editing UX |
| [W1](backlog.md#w1--magic-power-grid-placeholder--design-stage-not-yet-broken-into-tickets) | Magic power grid (placeholder — see `Structures/MAGIC_GRID_SPEC.md`) | Phase 2 / World Power |
| [W2](backlog.md#w2--destructible-hero-relics-mesh-at-distance-sdf-stamp-up-close-idea--not-fleshed-out-owner-2026-07-09-from-the-v11-blender-session) | Destructible hero relics — mesh at distance, SDF stamp up close (idea, not fleshed out; V11 master rig doubles as the SDF description) | Terrain |
| [R1](backlog.md#r1--low-poly-treerock-lods--enable-relic-lod) | Low-poly tree/rock LODs + enable relic LOD | Rendering |
| [R2](backlog.md#r2--speed-biased-scatter-lod-drop-detail-during-fast-airborne-movement) | Speed-biased scatter LOD (drop detail during fast airborne movement) | Rendering |
| [R3](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Camera-specific scatter LOD bucketing (multi-camera correctness) | Rendering |
| [R4](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Pebble chunk-cull cleanup parity (`TerrainChunkLodApplySystem`) | Rendering |
| [R5](backlog.md#r5--hero-relics-in-the-far-field-impostor-stack-opened-2026-07-05--from-the-far-field-discussion) | Hero relics in the far-field impostor stack — silhouette cards for **>2000u**. V12 done (data source = anchor buffer `Source == Authored`); still blocked by the Phase-2 horizon ring (R6 itself closed 2026-07-21) | Rendering |
| [T1](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Scatter LOD test coverage (Pebble render contract, GeneratePlacements, OnUpdate routing) | Testing |
| [T3](backlog.md#t3--triage-six-pre-existing-playmode-test-failures-opened-2026-07-08--surfaced-during-v9-p3-verification) | Triage 6 pre-existing PlayMode failures — player visual never created (×5, suspects: FPS-only reversal / BoxPlayer swap), grounded-jump takeoff mode (triage with M4); proven pre-existing via clean-baseline A/B 2026-07-08 | Testing |
| [B1](backlog.md#b1--boulder-group-models) | Boulder group models (1–6m, weathered, partially buried) | Biome Art |
| [B2](backlog.md#b2--pebble-cluster-models) | Pebble cluster models (10–50cm fields) | Biome Art |
| [B3](backlog.md#b3--stone-outcrop-models) | Stone outcrop models (5–30m navigation markers) | Biome Art |
| [B4](backlog.md#b4--steppe-shrub-models) | Steppe shrub models (0.5–1m heath bushes) | Biome Art |
| [B5](backlog.md#b5--prairie-grass-tuft-models) | Prairie grass tuft models (20–80cm, wind-ready) | Biome Art |
| [B6](backlog.md#b6--tall-grass-patch-models) | Tall grass patch models (1–1.5m) | Biome Art |
| [B7](backlog.md#b7--wildflower-cluster-models) | Wildflower cluster models (10–40cm, 3 colorways) | Biome Art |

---

## How this works

- Current-focus detail lives in the work-set doc. **No work-set is currently open** — both Vista
  Moment and World Structure Phase A closed; the next one is under discussion (2026-07-21).
- When a work-set completes, its doc moves to `done/` **untouched**
  (`done/vista-moment.md` is the first).
- Backlog detail lives in `backlog.md` until a ticket is pulled into a current work-set.
