# Tickets

**Status:** ACTIVE
**Last Updated:** 2026-07-05

Lightweight task tracker. Status: `[ ]` pending · `[x]` done · `[-]` blocked

**Ticket ID scheme** (canonical): prefix = track, number increments within the track and is never
reused. `V` Vista Moment · `C` Camera Feel · `A` Animation · `M` Movement · `P` Phase 1 core ·
`W` World Power/WFC · `R` Rendering · `T` Testing · `B` Biome Art.

This tracker runs as **Kanban**: backlog → current focus → done. Work-sets are scoped by content,
not timeboxed — a work-set stays "current focus" until it's actually done, however long that takes.

---

## Current Focus: Vista Moment

Full detail for every ticket in this section — history, re-scope notes, and the Camera Feel /
Animation framing — lives in [`vista-moment.md`](vista-moment.md).

### Vista Moment _(the MVP wow moment)_

| ID  | Status | Subject |
|-----|--------|---------|
| V1  | [x] | Ground plane impostor — built & enabled; now receives fog so the plain hazes into the horizon (2026-07-01) |
| V2  | [x] | Atmospheric fog — enabled + impostor fog wired; "square from height" diagnosed as skybox horizon, not a fog plane (2026-07-01). Dynamic sky-tracking color → V6 |
| V3  | [ ] | Mountain skybox panel — painted silhouette framing the horizon _(color path folds into V9 P4)_ |
| V4  | [x] | Hand mesh validation — renders end-to-end, scale 500 ≈ 40–50u tall; reads as a boulder, not a hand → follow-ups V11/V12 (2026-07-05) |
| V5  | [-] | _(Deferred — out of MVP "look at" scope)_ Relic → WFC maze interior — connect relic anchor to dungeon interior generation |
| V6  | [x] | Time-of-day + biome-dependent sky & tracking fog — Plains "Cloudbreak" preset; haze color follows the horizon (2026-07-01) |
| V7  | [x] | BUG: player falls through ground on sky-drop landing — readiness-gate probe now reaches terrain (65adabb, 2026-07-03) |
| V8  | [x] | Distance-graded fog density — Route A judged & rejected, reverted to Exp² baseline; Route B (height fog) folded into V9's height-aware `ApplyAerialPerspective` (merged 2026-07-05) |
| V9  | [ ] | **← NEXT BUILD.** Atmosphere color authority — one palette source + global `_Atmo*` uniforms + shared **height-aware** aerial-perspective HLSL (folds V8 Route B, 2026-07-05); unifies sky, disc, mountains, terrain, hero relic & fog; adds skybox haze band + hero exemption. MVP slice = P1+P4+P4b |
| V10 | [x] | BUG: player falls through terrain during traversal — colliders built player-nearest-first, 3×3 ring budget-exempt (1883659, 2026-07-03) |
| V11 | [ ] | Hero hand mesh authoring — silhouette-first re-pose/new mesh so four fingers read at 200–400u (spun off V4, 2026-07-05) |
| V12 | [ ] | Authored anchor candidate source — guaranteed hero hand in view of spawn; quests + debug layouts reuse it (spun off V4, 2026-07-05) |

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
| A9  | [ ] | First-person arms viewmodel (the real fix for FPS-only MVP) | — | — |

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
| [R1](backlog.md#r1--low-poly-treerock-lods--enable-relic-lod) | Low-poly tree/rock LODs + enable relic LOD | Rendering |
| [R2](backlog.md#r2--speed-biased-scatter-lod-drop-detail-during-fast-airborne-movement) | Speed-biased scatter LOD (drop detail during fast airborne movement) | Rendering |
| [R3](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Camera-specific scatter LOD bucketing (multi-camera correctness) | Rendering |
| [R4](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Pebble chunk-cull cleanup parity (`TerrainChunkLodApplySystem`) | Rendering |
| [R5](backlog.md#r5--hero-relics-in-the-far-field-impostor-stack-opened-2026-07-05--from-the-far-field-discussion) | Hero relics in the far-field impostor stack — silhouette cards in the Phase 2 horizon ring from V12 authored anchors | Rendering |
| [T1](backlog.md#r3--r4--t1--surface-scatter-lod-follow-ups-deferred-from-codex-review-2026-06-27) | Scatter LOD test coverage (Pebble render contract, GeneratePlacements, OnUpdate routing) | Testing |
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
