# Tickets

**Status:** ACTIVE
**Last Updated:** 2026-07-19

Lightweight task tracker. Status: `[ ]` pending · `[x]` done · `[-]` blocked

**Ticket ID scheme** (canonical): prefix = track, number increments within the track and is never
reused. `V` Vista Moment · `C` Camera Feel · `A` Animation · `M` Movement · `P` Phase 1 core ·
`W` World Power/WFC · `R` Rendering · `T` Testing · `B` Biome Art · `H` World Structure (the `H`
macro-structure authority — `WORLD_STRUCTURE_SPEC.md`).

This tracker runs as **Kanban**: backlog → current focus → done. Work-sets are scoped by content,
not timeboxed — a work-set stays "current focus" until it's actually done, however long that takes.

---

## Current Focus: Vista Moment

Full detail for every ticket in this section — history, re-scope notes, and the Camera Feel /
Animation framing — lives in [`vista-moment.md`](vista-moment.md).

### Build order _(decided 2026-07-07 — ticket IDs are track labels, not sequence; this is the sequence)_

1. **R6** (P2 → P1 → P3; P4 separable) — landmark draw distance. This is what makes the vista's
   "huge hand, far away, always visible" possible at all: today anything past the 600u far clip
   simply doesn't render. R6 P3 also supersedes the 2026-07-07 hero-concealer patch in
   `Atmosphere.hlsl`.
2. **V11** (hero hand mesh — ✅ done 2026-07-15) + **V12** (authored anchors — ✅ done 2026-07-08,
   eyeball 2026-07-15) — the authored giant hand itself. Guaranteed hand at (0, 900), procedural
   relics rare, silhouette reads from spawn through haze. **This step is complete.**
3. **V9 P3 eyeball → V17 P1+P2 → V9 P5** _(V17 slotted 2026-07-09)_ — validate the P3 terrain↔disc
   seam baseline first (V17 modifies both sides of it, so it would confound that check), then land
   V17's mid-field variation, then P5 saturation stays last — it's a one-shot global grade and V17 P1
   changes the luminance distribution it grades. Polish; doesn't block the vista trick. V17 P3
   (vertex undulation) is judged separately after V15's drop-altitude skirt check — both shape the
   same disc→sky-band handoff.
4. **R5** (backlog) — silhouette cards for >2000u. Blocked by R6 (narrows its contract), V12 (its
   data source), and the Phase-2 horizon ring. Post-MVP.

### Vista Moment _(the MVP wow moment)_

| ID  | Status | Subject |
|-----|--------|---------|
| V1  | [x] | Ground plane impostor — built & enabled; now receives fog so the plain hazes into the horizon (2026-07-01) |
| V2  | [x] | Atmospheric fog — enabled + impostor fog wired; "square from height" diagnosed as skybox horizon, not a fog plane (2026-07-01). Dynamic sky-tracking color → V6 |
| V3  | [-] | _(Superseded 2026-07-09)_ Mountain skybox panel — the procedural sky band fills this role (V9 P4 colors + V15 silhouette/composition); reopen only if a painted panel is ever preferred |
| V4  | [x] | Hand mesh validation — renders end-to-end, scale 500 ≈ 40–50u tall; reads as a boulder, not a hand → follow-ups V11/V12 (2026-07-05) |
| V5  | [-] | _(Deferred — out of MVP "look at" scope)_ Relic → WFC maze interior — connect relic anchor to dungeon interior generation |
| V6  | [x] | Time-of-day + biome-dependent sky & tracking fog — Plains "Cloudbreak" preset; haze color follows the horizon (2026-07-01) |
| V7  | [x] | BUG: player falls through ground on sky-drop landing — readiness-gate probe now reaches terrain (65adabb, 2026-07-03) |
| V8  | [x] | Distance-graded fog density — Route A judged & rejected, reverted to Exp² baseline; Route B (height fog) folded into V9's height-aware `ApplyAerialPerspective` (merged 2026-07-05) |
| V9  | [ ] | Atmosphere color authority — one palette source + global `_Atmo*` uniforms + shared **height-aware** aerial-perspective HLSL (folds V8 Route B); **MVP slice P1+P4+P4b built & validated 2026-07-05**; **P2 disc palette + `SyncTerrainColor` deletion built 2026-07-07**; **P3 terrain palette consumption built 2026-07-08** (569a — terrain is a direct palette consumer). P2 noon vista + P3 ground-level seam eyeballed OK 2026-07-15 (in-game vista screenshots — no visible terrain↔disc seam). Remaining: P3 drop-altitude check, P5 saturation (last), full day/night sweep |
| V10 | [x] | BUG: player falls through terrain during traversal — colliders built player-nearest-first, 3×3 ring budget-exempt (1883659, 2026-07-03) |
| V11 | [x] | Hero hand mesh authoring — silhouette-first re-pose/new mesh so four fingers read at 200–400u (spun off V4, 2026-07-05). **Built + swapped in 2026-07-11** (subsurf bake 2.7k tris, −20° buried-body pitch, palm faces spawn); **owner eyeball passed 2026-07-15** (in-game screenshots: four fingers + thumb read from spawn through haze; dark-stone value contrast up close). Drop-view hero hang tracked under R6 P4. Variants → V18 |
| V12 | [x] | Authored anchor candidate source — built & Play-Mode-validated 2026-07-08 (spec §9.5): authored pre-pass overrides the planner via existing tie-break; scene-bootstrap authoring; hero hand guaranteed at (0, 900) as `relic_hand_hero`. Includes relic-rarity retune (96 → 6, spec §9.6). From-spawn eyeball passed 2026-07-15 (lone-colossus composition reads; distance/yaw kept as-is); silhouette was V11 (done) |
| V13 | [ ] | Burning-descent VFX (meteor entry) — FP screen-edge flames/embers, ignites on V14 break-open, burns off before C3 dust handoff; arrival-sequence trigger, never altitude/speed. **Built 2026-07-18** (863bc0f) — procedural screen-space layer (V14 architecture), altitude-band burn-off 230→120u play-verified (ignite t=12.01 / extinguish t=19.55 on a 12s test hold, ~1.5s before landing), one-shot by construction, `EnableMeteorDescentVfx` config flag, EditMode envelope tests green. **Iterated 2026-07-19 (owner):** embers now stream radially OUT from screen center (aspect-corrected rays, log-radius warp acceleration) as small round screen-space dots instead of rising up the frame; ember visuals moved to a **tunable material asset** (`Resources/Materials/MeteorDescentFlames.mat` — `_EmberDensity/_EmberSize/_EmberSpeed/_EmberJitter` + flame colors, inspector-editable; controller instantiates a copy and drives only `_Intensity`); burn-off band now **config-driven** (`ProjectFeatureConfig.MeteorDescentFadeStartY/EndY`, default **340→240** = extinguishes sooner/higher, was 230→120). Envelope tests 5/5 green. Remaining: owner eyeball of the burn + sparks + earlier fade in play; **comet-drop SFX (blocked on audio pipeline — none exists, see Audio track)** (opened 2026-07-08; `Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md`) |
| V14 | [x] | Meteor-interior loading shell — diegetic initial load: full-screen meteor interior over the V7 readiness gate, breaks open on real gate release (binary + min-hold, no fake progress). Built 2026-07-18 — min-hold in the gate itself (gravity + break-open one beat by construction), overlay polls gate-component removal (no new bridge), fully procedural visuals. Round 2 same day (owner feedback): lit-rock interior + crack-light bleed, plate-by-plate center-out dissolve (1.75 s, burning front), break-open on the release beat + face-down spawn. **Owner eyeball PASSED 2026-07-18 — ticket closed.** V13 rides the same release signal (`Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md` §12) |
| V15 | [ ] | Sky mountain band — ridged FBM silhouette (was 3 sine harmonics), second back ridge (finer, round-2 retune), horizon demarcation line (darker `_AtmoHorizon`-shifted range above, ground skirt untouched below); snow caps built, off-by-default toggle (round 3). Ground look owner-approved 2026-07-09; remaining: drop-altitude skirt check (`Rendering/SKY_MOUNTAIN_BAND_SPEC.md`) |
| V16 | [x] | Relic size pop-in fix — LOD made dormant-by-design: no authored impostor art means the swap rendered the same mesh at a smaller fixed size (zero verts saved, visible pop). Relics without an authored `ImpostorMesh` now skip the LOD components entirely; machinery kept for real billboard art (built 2026-07-09; dormancy note in `Structures/RELIC_LOD_IMPOSTOR_SPEC.md`). **Walk-toward check passed 2026-07-15** — no size change on approach to the hero at 900u |
| V17 | [ ] | Mid-field disc variation — **P1 macro luminance + P2 relief shading built 2026-07-16; P4 patchy haze built + capture-validated same day** (eye-level finding: haze crushes surface-side variation, so P4 modulates the haze amount itself — owner green-lit; `ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` §5.3b; live-tunable on the Cloudbreak preset, tuned 0.004/0.5). EditMode green (atmosphere + parity fixtures). Remaining: owner eyeball of the P4 look in normal play, P3 optional vertex undulation (judged after V15's skirt check). Speckling/cloud-shadows out of scope → R1/R5 + weather track (opened 2026-07-09; `Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md` §12) |
| V18 | [ ] | Hero hand weathered/ruined variants — Blender fracture cuts on the V11 cage before the subsurf bake (uneven mid-segment breaks + rubble per the ossified-god fiction), separate FBX per variant, template entries so procedural hands rotate them via the existing anchor hash. Runtime damage is W2, not this (opened 2026-07-11) |
| V19 | [ ] | Hero hand rubble mound base — the hand still reads as *floating* at the ground/from-spawn vista; per `Docs/Temp_OpeningInspiration.png` it must rise out of a broad rocky rubble mound tapering into the plain. **Blender, not procedural** (owner-decided 2026-07-19): at the 900u vista distance there is NO streamed terrain under the hero (it renders as a landmark mesh past the ~180u streaming radius), and near-field `H` is capped at ~4u by the single-layer slab — so a procedural terrain raise can't make the from-spawn base. Author a low-poly rubble mound in the hero master (`ArtSource/ColossalHand.blend`), **joined into the hero Mesh on export** (single-Mesh relic render path) so it inherits R6 never-cull + `RelicHero` haze automatically. Retune `yOffset` so the wrist emerges from the mound crown. Notes: up-close the broad base will lightly clip the ~flat streamed terrain (buried base, acceptable); no collider at MVP (look-at scope); largely supersedes the R6 P4 hero-hang fix for steady-state. Opened 2026-07-19 |

### Rendering — vista support _(R6 pulled from backlog 2026-07-07; step 1 of the Build order)_

| ID  | Status | Subject |
|-----|--------|---------|
| R6  | [ ] | Landmark draw distance — hero relics never cull: **P2+P1+P3 built 2026-07-07; P4 spawn fade built 2026-07-16** (`RelicSpawnFade` BRG property + fade system, ~0.5s dither-in on realization; fade math EditMode-tested, plumbing play-verified). Partial validation 2026-07-15: hero at 900u renders + persists, no cull/pop on walk-toward; ground vista unchanged. Remaining: eyeball the 0.5s dissolve on a relic-streaming session, ~~drop-altitude hero hang~~ (**retired 2026-07-19** — owner decision: the hand is not shown during the scripted drop, so P4 reduces to the spawn dither + not framing the hero mid-descent), permanence at 1500u+. Spec: `Rendering/LANDMARK_DRAW_DISTANCE_SPEC.md` (ACTIVE) |

Details: [vista-moment.md](vista-moment.md)

### Dev Tooling _(pulled in 2026-07-05)_

| ID  | Status | Subject |
|-----|--------|---------|
| T2  | [x] | Dev determinism pins — case-by-case pin convention (CLAUDE.md) + time-of-day pin on `TimeOfDayController`, enabled in scene & verified (2026-07-05) |

Details: [vista-moment.md](vista-moment.md)

### Camera Feel _(secondary — slingshot feel toward the relic; was sprint lead pre-2026-06-29)_

| ID  | Status | Subject |
|-----|--------|---------|
| C1  | [ ] | Camera charge pullback and FOV narrow during slingshot charge |
| C2  | [ ] | Camera FOV punch and speed lines on launch |
| C3  | [ ] | Landing camera dip and dust burst |

Details: [vista-moment.md](vista-moment.md)

### Animation _(deferred from this sprint by the 2026-06-29 Vista re-anchor — A9 follows the vista; A1–A8 are third-person body)_

#### Deferred — follows the vista

| ID  | Status | Subject | Blocks | Blocked By |
|-----|--------|---------|--------|------------|
| A9  | [ ] | First-person arms viewmodel (the real fix for FPS-only MVP) — arms source = rig the baked V11 hand mesh directly (armature → relaxed re-pose → owner tweaks; rigging phase started 2026-07-12; detail in vista-moment.md) | — | — |

#### Dev-toggle / deferred (third-person body) _(not MVP-gating — body hidden in first-person play)_

| ID  | Status | Subject | Blocks | Blocked By |
|-----|--------|---------|--------|------------|
| A1  | [x] | Wire slingshot clips into animator controller (done) | A2, A3 | — |
| A2  | [ ] | Fix animator controller transition blend times | A4 | A1 |
| A3  | [ ] | Stabilize landing animations _(verify hidden-animator state first — may still fire into dead states)_ | A4 | A1 |
| A4  | [-] | Import Kevin Iglesias pack and wire basic movement animations | A5 | A2, A3 |
| A5  | [-] | Wire glide animation state | — | A4 |
| A8  | [ ] | Simplify airborne animation: single fall clip while in air | — | — |

Details: [vista-moment.md](vista-moment.md)

---

## Current Focus: World Structure — Phase A (the `H` authority)

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
| M1  | Glide mechanic (Space hold → GlideCharging → Gliding) | Movement |
| M2  | Chain slingshot (chain window + additive velocity) | Movement |
| M3  | Thermal columns (vertical lift volumes) | Movement |
| [M4](backlog.md#m4--bug-ballistic-takeoff-false-grounding-past-jump-apex-codex-review-2026-07-02) | BUG: Ballistic-takeoff false-grounding past jump apex — suppress by contact/separation, not velocity sign | Movement |
| [M5](backlog.md#m5--harden-sky-drop-landing-against-high-speed-tunneling-open--spun-off-from-v7-2026-07-03) | Harden sky-drop landing against high-speed tunneling (thin/absent collider under landing XZ; no CCD) | Movement |
| M6  | Terrain editing no longer works. Shifting to edit-mode then attempting to edit does nothing | Terrain |
| P1  | Basic HUD (charge indicator + chain window indicator) | Phase 1 |
| P2  | Magic Hand System (raycast, charge, binary terrain edit) | Phase 1 |
| E1  | Blocked-edit visual feedback — red-X reticle pulse (+ optional tooltip) when a terrain edit is rejected by the player-safety volume. Post-MVP: terrain editing itself needs substantial work first (owner 2026-07-03; salvaged from archived Cursor plan) | Editing UX |
| [W1](backlog.md#w1--magic-power-grid-placeholder--design-stage-not-yet-broken-into-tickets) | Magic power grid (placeholder — see `Structures/MAGIC_GRID_SPEC.md`) | Phase 2 / World Power |
| [W2](backlog.md#w2--destructible-hero-relics-mesh-at-distance-sdf-stamp-up-close-idea--not-fleshed-out-owner-2026-07-09-from-the-v11-blender-session) | Destructible hero relics — mesh at distance, SDF stamp up close (idea, not fleshed out; V11 master rig doubles as the SDF description) | Terrain |
| [R1](backlog.md#r1--low-poly-treerock-lods--enable-relic-lod) | Low-poly tree/rock LODs + enable relic LOD | Rendering |
| [R2](backlog.md#r2--speed-biased-scatter-lod-drop-detail-during-fast-airborne-movement) | Speed-biased scatter LOD (drop detail during fast airborne movement) | Rendering |
| [R3](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Camera-specific scatter LOD bucketing (multi-camera correctness) | Rendering |
| [R4](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Pebble chunk-cull cleanup parity (`TerrainChunkLodApplySystem`) | Rendering |
| [R5](backlog.md#r5--hero-relics-in-the-far-field-impostor-stack-opened-2026-07-05--from-the-far-field-discussion) | Hero relics in the far-field impostor stack — silhouette cards for **>2000u**. V12 done (data source = anchor buffer `Source == Authored`); still blocked by R6 P4/validation + Phase-2 horizon ring (see Build order) | Rendering |
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

- Current-focus detail lives in the work-set doc (`vista-moment.md` today).
- When a work-set completes, its doc moves to `done/` **untouched**.
- Backlog detail lives in `backlog.md` until a ticket is pulled into a current work-set.
