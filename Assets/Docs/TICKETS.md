# Tickets

Lightweight task tracker. Status: `[ ]` pending · `[x]` done · `[-]` blocked

---

## Sprint: MVP Vista Moment

> **FPS-only reversal + re-sequence (2026-06-20):** MVP ships **first-person only**. `PlayerCameraSettings.IsThirdPerson`
> now defaults to `false` and the third-person body is hidden in first-person (`PlayerFirstPersonVisibility`).
> Third-person stays as a **dev/debug toggle (V key)** for inspecting these clips. Consequence: the full-body
> clips in **A1–A8 are not visible in normal play** — they only show in the dev toggle. This made **Camera
> Feel (C1–C3)** the primary in-game feel feedback in first-person, and **A9 (first-person arms viewmodel)**
> the real animation payoff; A1–A8 became mostly dev-toggle / A9-prep work. _(Superseded as sprint lead by
> the 2026-06-29 Vista re-anchor below — Camera Feel is now secondary, A9 deferred.)_

> **Re-anchor to Vista (2026-06-29):** Re-focused on the project's designated MVP wow moment — the vista
> discovery of the giant four-fingered stone hand across a hazy plain (`AI/MVP_VISTA_MOMENT_SPEC.md`,
> `MASTER_PLAN.md` §1/§5). Blockers V1–V5 were sitting in the backlog while movement-feel polish led;
> they're cheap (~½–1 day each) and mostly unbuilt, so they now **lead the sprint**. **Camera Feel C1–C3**
> stays in-sprint as **secondary** — it makes slingshotting toward the relic feel good. **Animation A9**
> (arms viewmodel) is **deferred** — biggest cost, off the wow-moment critical path.

### Vista Moment _(sprint lead — the MVP wow moment)_

> Target: player crests a plain, sees the giant stone hand across atmospheric haze, slingshots toward it,
> and enters the WFC maze interior. Ordered by impact-per-hour per `AI/MVP_VISTA_MOMENT_SPEC.md` §4.

| ID  | Status | Subject |
|-----|--------|---------|
| V1  | [ ] | Ground plane impostor — terrain-colored disc beyond chunk radius (sky-drop world extent) |
| V2  | [ ] | Atmospheric fog tuning — blue-grey haze, foreground sharp / horizon veiled |
| V3  | [ ] | Mountain skybox panel — painted silhouette framing the horizon |
| V4  | [ ] | Hand mesh validation — confirm `testAlienHand.fbx` renders; tune scale/yOffset |
| V5  | [ ] | Relic → WFC maze interior — connect relic anchor to dungeon interior generation |

#### V1 — Ground plane impostor
**Spec:** `AI/GROUND_PLANE_IMPOSTOR_SPEC.md`. Horizontal terrain-colored disc (~1500u radius) on the XZ
plane beyond the ~256u SDF chunk radius; world-space shaded with the terrain's noise octaves, radial alpha
fade hides the seam, fog dissolves the outer edge. Entity follows player XZ (one transform write). Eliminates
the void from altitude and enables the sky-drop intro. ½–1 day; no texture assets, no SDF pipeline changes.

#### V2 — Atmospheric fog tuning _(canonical fog ticket — folds in former A6)_
**Intent:** Fog should read as thin mist suspended in air, not a tint on objects. From altitude it currently
renders as a visible square, which breaks the illusion.
- Bias toward distance/altitude; reduce density so near geometry is largely unaffected. Shift color blue-grey
  (`#8FA8C0`), tune start/end so foreground is sharp and horizon veiled.
- Eliminate the "square from height" artifact — if a finite plane/quad drives the fog, replace with a
  camera-relative/global effect or skirt it so no edge shows at fly heights.
- Reconcile with the vista stack — check interaction with `AI/GROUND_PLANE_IMPOSTOR_SPEC.md` and
  `AI/MVP_VISTA_MOMENT_SPEC.md` so fog and horizon read as one atmosphere.
- **Open questions (resolve first):** (1) what renders the fog today — URP Volume/global fog, a custom
  shader/material, a skybox blend, or a scene quad? (`WeatherSystem.WeatherType.Fog` sets weather state only,
  no visuals — source is elsewhere.) (2) Is the "square from height" a dedicated fog plane or the ground-plane
  impostor edge?
- **Acceptance:** from ground and max fly height, no plane edge; near objects (~1 chunk) negligible tint; far
  horizon softened. Validate in Play Mode at several altitudes.

#### V3 — Mountain skybox panel
Paint or source a mountain silhouette into the skybox (MVP Option A per `MVP_VISTA_MOMENT_SPEC.md` §2.4 —
2–4 hrs). Sells horizon depth. The seed-driven horizon ring (`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`) is the
Phase 2 system, deferred.

#### V4 — Hand mesh validation
Confirm `Assets/Models/testAlienHand.fbx` renders correctly at scene scale in Play Mode (already wired as
`Relic.asset` `DefaultTemplateId: relic_hand`). Tune `scale` / `yOffset` in `RelicVisualBootstrap` inspector
so the four-finger hand reads from ~200–400u. Per `MVP_VISTA_MOMENT_SPEC.md` §2.1.

#### V5 — Relic → WFC maze interior
Connect the relic anchor to WFC dungeon interior generation so the hand is enterable. Bridges structure
placement to the existing WFC pipeline — reuses the dungeon realizer path. Depends on the WFC bootstrap +
deterministic-seed fixes noted in `AI/STRUCTURE PLACEMENT/STRUCTURE_PLACEMENT_SPEC.md` §12.5.1. See
`WFC/MAP_WFC.md`, `WFC_Dungeon_Test_Plan.md`.

---

### Camera Feel _(secondary — slingshot feel toward the relic; was sprint lead pre-2026-06-29)_

> **FPS adaptation:** these tickets were specced against the third-person orbit camera, so the **distance
> dolly/pullback** terms (`TargetDistance`, `BallisticDistanceAdd`) are third-person concepts. In first-person
> the camera is head-locked, so reinterpret those as no-ops; the **FOV punch/narrow, shake, dip, camera-local
> drop, speed lines, and dust burst** all carry over to FPS as-is and are where the feel actually comes from.

| ID  | Status | Subject |
|-----|--------|---------|
| C1  | [ ] | Camera charge pullback and FOV narrow during slingshot charge |
| C2  | [ ] | Camera FOV punch and speed lines on launch |
| C3  | [ ] | Landing camera dip and dust burst |

#### C1 — Camera charge pullback and FOV narrow
Per `MOVEMENT_PLANNING.md` Step 3:
- Dolly back: `TargetDistance = BaseDistance + 2.5 * ChargeNormalized` _(third-person only — no-op in FPS)_
- FOV narrow: `TargetFOV = BaseFOV - 5° * ChargeNormalized`
- Camera shake ramps with charge: amplitude 0.01 → 0.06
- Orbit locks to charge direction during SlingshotCharging (no free-look) _(FPS: look = aim, no orbit to lock)_
- Exponential smoothing (damping = 8). On cancel: reverse over ~150ms

**Test (EditMode):** TargetDistance == BaseDistance + ChargeDistanceAdd * ChargeNormalized

#### C2 — Camera FOV punch and speed lines on launch
Per `MOVEMENT_PLANNING.md` Step 5:
- FOV punch on launch: +8–15°, fast attack ~80ms, decay over 300–500ms
- Speed FOV: +0.15°/m/s above 15 m/s threshold, capped at +12°
- Ballistic camera pulls back (BallisticDistanceAdd = 1.5m), damping loosens (= 6) _(pullback third-person only — no-op in FPS)_
- Speed lines: camera-parented particles or screen-space shader, 0%→100% opacity over 15→40 m/s, fade 300ms on drop

**Test (EditMode):** TargetFOV includes launch punch on first ballistic frame, decays over subsequent frames

#### C3 — Landing camera dip and dust burst
Per `MOVEMENT_PLANNING.md` Step 7:
- Shake amplitude 0.05–0.20 proportional to vertical impact speed, decay 150–300ms
- FOV dip: 2–4°, 200ms recovery
- Camera drops 0.3–0.8m, recovers over 200ms _(camera-local dip — applies in FPS)_
- Dust burst at feet: size/density scale with impact speed (min speed = 5 m/s, max radius = 3m)
- Hard landing (vertical > 12 m/s): full shake + dip. Slide landing (horizontal > 8 m/s): smooth transition, no dip

**Test (EditMode):** ShakeOffset proportional to VerticalSpeed, clamped to max. LandingImpactEvent fires exactly one frame then disables.

---

### Animation _(deferred from this sprint by the 2026-06-29 Vista re-anchor — A9 follows the vista; A1–A8 are third-person body)_

> **Re-scope (2026-06-29):** Under FPS-only, the third-person body is hidden in play, so the full-body
> clips (A2–A8) are invisible except via the dev V-key toggle — they do **not** gate the MVP. **A9
> (first-person arms viewmodel)** is the real FPS animation work but is **deferred behind the Vista Moment**
> this sprint (biggest cost, off the wow-moment path); it follows once the vista lands. A1–A8 stay parked
> under **Dev-toggle / deferred** below.

#### Deferred — follows the vista _(was live; deferred by Vista re-anchor 2026-06-29)_

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

#### A9 — First-person arms viewmodel (the real fix for FPS-only MVP) _(deferred — follows the vista)_
With MVP reversed to first-person only (2026-06-20), the full third-person body is hidden in play
(`PlayerFirstPersonVisibility`) and the A1–A8 clips are invisible except via the dev V-key toggle. The
proper FPS feedback for charge/launch/glide is a dedicated **arms viewmodel**: a first-person arms rig with
FPS-authored clips, shown only in first-person.

The groundwork is already done and forward-compatible — `PlayerFirstPersonVisibility` hides the body in
first-person, so this ticket only adds the arms rig and shows it in the same place (no rework of the body-hide
or camera-mode plumbing).

- Author/acquire a first-person arms rig + clips: slingshot charge pull, launch/release, glide arms-spread, idle/move bob.
- Show the arms rig only when `IsThirdPerson == false`; hide it (and show the full body) in the third-person dev toggle. Extend `PlayerFirstPersonVisibility` — it already owns the first/third-person visibility swap.
- Drive arms clips from the same `PlayerAnimatorBridge` parameters where they map; add FPS-specific params only where the body params don't translate.
- Scope check before building: decide whether arms are a separate `Animator` (own controller) or share the existing controller. Capture the decision here.
- **Validate:** in first-person, charge pull / launch / glide read clearly on the arms with no body clipping; V-key toggle still shows the full body + existing third-person clips for debugging.

**Dev-toggle / deferred ticket detail (third-person body, A1–A8):**

#### A1 — Wire slingshot clips into animator controller
Wire the 3 exported FBX clips into `PlayerAnimatorController` per `SLINGSHOT_ANIMATION_CONTROLLER_SPEC.md`.
- `Player_Slingshot_Charge_Start` — trigger on slingshot input down, no loop, transitions into Hold
- `Player_Slingshot_Charge_Hold` — loops while input held, exits on release or cancel
- `Player_Slingshot_Release` — trigger on launch (transition to Ballistic), no loop

Animator parameters must match what `PlayerAnimatorBridge` already dispatches. No physics/movement changes.

#### A2 — Fix animator controller transition blend times _(blocked by A1)_
All state transitions currently snap at 0s. Fix in Unity Editor — no code changes.
- Grounded → SlingshotCharging: ~0.1s ease-out
- SlingshotCharging → Ballistic: ~0.05s (fast pop)
- Ballistic → Gliding: ~0.2s ease-out
- Any Airborne → Grounded: ~0.1s sharp-in

Set `Has Exit Time = false` and non-zero `Transition Duration` on each. Validate in Play Mode.

#### A3 — Stabilize landing animations _(blocked by A1)_
Per `PLAYER_LANDING_ANIMATION_SPEC.md`:
- **Phase 1:** Add fallback flag in `LandingConfig`. When enabled, bridge fires only original `LandingTrigger` for all landings — restores known-good behaviour. Tiered triggers (Standard/Hard/Slide) currently fire into dead states.
- **Phase 3:** Once controller states exist, flip flag to enable tiered dispatch. Code is already written; controller states + clips are the only missing pieces.

#### A4 — Import Kevin Iglesias pack and wire basic movement animations _(blocked by A2, A3)_
Pack is ready for import into `Assets/Kevin Iglesias/` (currently empty).
1. Import pack
2. Map to `PlayerMovementMode` states per `PLAYER_CHARACTER_VISUAL_SWAP_SPEC.md`:
   - Grounded idle → Idle clip
   - Grounded moving → Walk/Run (speed float parameter)
   - Ballistic falling → Airborne_Fall or equivalent
   - Landing → Landing_Hard / Landing_Soft (feeds A3 Phase 3)
3. Wire into `PlayerAnimatorController`
4. Validate all basic states in Play Mode. Do not replace slingshot states from A1.

#### A5 — Wire glide animation state _(blocked by A4)_
Add Gliding animator state driven by `PlayerMovementMode.Gliding`.
- Clip: Kevin Iglesias glider/arms-spread (from A4 import)
- Transition in from Ballistic on Gliding mode (~0.2s blend), loop, transition out on exit
- Confirm `PlayerAnimatorBridge` dispatches Gliding mode; add parameter if missing
- Validate: hold Space mid-flight → arms-spread pose blends smoothly from tuck

#### A8 — Simplify airborne animation: single fall clip while in air (MVP)
The animator graph has grown complex. MVP/POC decision (2026-06-10): every in-air state plays the existing fall clip (`HumanM@Fall01`).
- Assign `HumanM@Fall01` to `BallisticRise` — it currently has **no motion**, so rising shows a T-pose. `Falling`, `GlideCharging`, and `ThermalBoost` already use it.
- **Keep distinct state labels** (`BallisticRise` vs `Falling`): post-MVP we may put a dedicated ballistic/tuck anim on the upward arc and blend to free-fall on the downward arc.
- Optional cleanup: with both states playing the same clip, the paired `MovementMode == 2 && BallisticRising` true/false transitions can collapse into single `MovementMode == 2` transitions where that reduces graph noise. Do **not** remove the `BallisticRising` parameter — `PlayerAnimatorBridge` still dispatches it and the future rise anim needs it.
- Update the `BallisticRisingHash` comment in `PlayerAnimatorBridge.cs` ("drives T-pose vs Falling split") to match.
- Spec: `PLAYER_CHARACTER_VISUAL_SWAP_SPEC.md` airborne mapping table + `SLINGSHOT_ANIMATION_CONTROLLER_SPEC.md` MVP note (both updated 2026-06-10).

_(Former A6 → folded into backlog **V2**. Former A7 → backlog **R1**. Both were rendering/environment work, not animation. A9 added 2026-06-20 for the FPS-only reversal.)_

---

## Backlog

_Tickets for later sprints — not yet scheduled._

| ID  | Subject | Group |  
|-----|---------|-------|
| M1  | Glide mechanic (Space hold → GlideCharging → Gliding) | Movement |
| M2  | Chain slingshot (chain window + additive velocity) | Movement |
| M3  | Thermal columns (vertical lift volumes) | Movement |
| P1  | Basic HUD (charge indicator + chain window indicator) | Phase 1 |
| P2  | Magic Hand System (raycast, charge, binary terrain edit) | Phase 1 |
| W1  | Magic power grid (placeholder — see `AI/STRUCTURE PLACEMENT/MAGIC_GRID_SPEC.md`) | Phase 2 / World Power |
| R1  | Low-poly tree/rock LODs + enable relic LOD | Rendering |
| R2  | Speed-biased scatter LOD (drop detail during fast airborne movement) | Rendering |
| R3  | Camera-specific scatter LOD bucketing (multi-camera correctness) | Rendering |
| R4  | Pebble chunk-cull cleanup parity (`TerrainChunkLodApplySystem`) | Rendering |
| T1  | Scatter LOD test coverage (Pebble render contract, GeneratePlacements, OnUpdate routing) | Testing |
| B1  | Boulder group models (1–6m, weathered, partially buried) | Biome Art |
| B2  | Pebble cluster models (10–50cm fields) | Biome Art |
| B3  | Stone outcrop models (5–30m navigation markers) | Biome Art |
| B4  | Steppe shrub models (0.5–1m heath bushes) | Biome Art |
| B5  | Prairie grass tuft models (20–80cm, wind-ready) | Biome Art |
| B6  | Tall grass patch models (1–1.5m) | Biome Art |
| B7  | Wildflower cluster models (10–40cm, 3 colorways) | Biome Art |

---

### R1 — Low-poly tree/rock LODs + enable relic LOD
**Intent:** Reduce environment-object render cost via LOD. **Priority: trees and rocks first** — they cost more FPS than the giant relics. Relics second.

- **Trees / rocks (do first):** authored via `TreeChunkRenderSystem` / `RockChunkRenderSystem`. Add a far LOD so dense scatter holds frame budget. Keep within poly/draw budgets — reference `ArtAndDOTS_Pipeline.md` and `DOTS_Terrain_LOD_SPEC.md`.
- **Spike (decide before building meshes):** for the low-poly far representation, do we author **new low-poly scattered meshes** (Blender) or generate them at runtime via **decimation/impostor** from the existing meshes? Timebox the spike, pick one, note the decision here.
- **Relics (second):** `RelicLodSelectionSystem` already implements distance-based full↔impostor swap (`RelicLodParams` + `RelicRenderConfig.LodSwapDistance`/`LodHysteresis`) but is `[DisableAutoCreation]`, so it only runs if explicitly created. "Large relics not using LOD" is most likely just that it is never enabled — confirm whether the bootstrap creates it; if not, enable and verify each realized relic has valid `RelicLodParams` and a 2-entry `RenderMeshArray`.

**Open questions (resolve in-ticket):**
1. New authored low-poly art vs. runtime decimation/impostor? → resolved by the spike above.
2. Target swap distances / poly budgets — do these exist, or does R1 establish them?
3. Should enabling/verifying `RelicLodSelectionSystem` be a separate quick ticket, or stay folded into R1?

**Acceptance:** Trees/rocks have a far LOD that holds frame budget at a populated viewpoint. Large relics visibly swap to impostor past `LodSwapDistance` (confirm via `DebugSettings.LogRendering` transition log). Add/extend an EditMode test alongside `StructureLodTests` for any new swap logic.

### R2 — Speed-biased scatter LOD (drop detail during fast airborne movement)
**Spec:** `AI/TerrainHeightMaps/SCATTER_LOD_SPEED_BIAS_SPEC.md` (extends `SURFACE_SCATTER_LOD_SPEC.md`).

**Intent:** Shrink the tree/rock LOD swap distance as player speed rises, pushing more scatter to the far (low-poly) mesh during fast flight. The scene is vertex-bound (~92% verts from scatter), so a smaller near band directly cuts the bottleneck — and at high airborne speed the player can't resolve near detail anyway, so it's perceptually cheap.

**Why it's near-free (the original question — "would changing LOD cost more than it's worth?"):** No. The scatter render path already rebuilds instance buckets every frame and selects near/far per instance with a stateless distance compare. Biasing the swap distance by speed is one velocity read + arithmetic per frame on a code path that already runs — no re-uploads, no thrash. Gain is capped by the near↔far vert gap, so it's only worth wiring once R1's far meshes are real.

- **Depends on R1** — inert until far LOD meshes exist for trees/rocks.
- Read `PlayerMovementState.Velocity` (horizontal `xz` speed) in each render system's `OnUpdate`; feed a speed-scaled swap distance into the existing `SelectLodLevel` calls.
- Use a smooth `smoothstep`/lerp ramp over a speed window (defaults 15→40 m/s, scale 1.0→0.4), **not** a hard threshold — a binary snap pops the whole scene and oscillates near the threshold.
- Add `EnableSpeedLodBias` + window/scale fields to `TreeRenderConfig`/`RockRenderConfig` + bootstraps; off by default = zero regression.
- Pure bias logic in `SurfaceScatterLodUtility`, EditMode-tested.

**Open questions (resolve in-ticket):** horizontal vs. full velocity magnitude on ballistic arcs; shared vs. per-config (tree/rock) tuning. See spec §7.

**Acceptance:** Per spec §8 — bias-off output identical to current; with bias on + far meshes, profiler shows a further scatter vert drop during sustained high-speed flight with no measurable bias cost; no whole-scene pop/oscillation near the min-speed threshold.

---

### R3 / R4 / T1 — Surface scatter LOD follow-ups _(deferred from Codex review 2026-06-27)_

Non-blocking gaps surfaced reviewing the surface-scatter-LOD commit. None cause a crash; all degrade safely (draw-near / draw-nothing). Deferred by decision — captured here so they aren't lost.

**R3 — Camera-specific LOD bucketing.** `TreeChunkRenderSystem` / `RockChunkRenderSystem` / `PebbleChunkRenderSystem` pick near/far buckets once per frame from `Camera.main` in `OnUpdate`, but submission is per-camera via `beginCameraRendering`. Secondary cameras (scene view, split-screen) therefore get LOD chosen for the main camera's viewpoint. Correct and cheaper for the single-camera MVP (see the explanatory comment at each `Camera.main` read). Only schedule if multi-camera ships; fix = bucket per submitted camera, or filter submission to the intended camera.

**R4 — Pebble chunk-cull cleanup parity.** `TerrainChunkLodApplySystem` strips `TreePlacementRecord`/`RockPlacementRecord` buffers + tags when a chunk culls to LOD3, but has no `PebblePlacementRecord`/`ChunkPebblePlacementTag` equivalent. No visual bug — the render system already skips culled chunks — but pebble buffers accumulate on culled chunks and they stay in the pebble render query just to be skipped. Fix = add the matching pebble removal block alongside the tree/rock one.

**T1 — Scatter LOD test coverage.** Current tests cover pure LOD selection (`SurfaceScatterLodUtilityTests`) and mesh registration (`SurfaceScatterRenderSystemContractTestsBase`) but not the runtime paths most likely to break: no `PebbleChunkRenderSystem` contract test, `PebblePlacementAlgorithmTests` never calls `GeneratePlacements`, and no `OnUpdate` near/far bucket-routing test. Fill the highest-risk gaps first (Pebble contract + `GeneratePlacements`).

---

### W1 — Magic power grid _(placeholder — design-stage, not yet broken into tickets)_
**Spec:** `AI/STRUCTURE PLACEMENT/MAGIC_GRID_SPEC.md` (DESIGN). Analytic world-space XZ lattice: power-source nodes, WFC-build-on-node affordance, sparse claimed-node alignment state, per-template `NodeAffinity`, universal influence query. Decisions captured in the spec; §13 lists the open questions to resolve before build.

**Not scheduled — sequences behind its foundation.** Don't break into tickets until Structure Placement is on the board:
- Depends on the **Structure Placement** anchor pipeline (`STRUCTURE_PLACEMENT_PLAN.md` §8 Steps 1–3) — the grid is a candidate-source variant reusing its `StructureAnchorRecord` / footprint / persistence machinery.
- That in turn depends on the known **WFC bootstrap gaps** (`HybridWFCSystem` not created by `DotsSystemBootstrap`; deterministic seed) — see `STRUCTURE_PLACEMENT_SPEC.md` §12.5.1.
- Orthogonal to **P2 Magic Hand System** (shares the "magic" fiction only; no dependency either way).

**Earliest natural entry:** after the free anchor planner exists, add the grid as a `NodeBound` candidate source + the on-node WFC build affordance.

---

### Biome Art — Windswept Colossus Plains scatter models (B1–B7)

**Source spec:** `Assets/Docs/mvp/Windswept_Colossus_Plains_Biome_Spec.md`. These are **model authoring tickets** — runtime placement/render systems are separate work where noted.

**Shared conventions (apply to all B tickets):**
- Author in Blender (`BlenderSource/`), export FBX to `Assets/Models/<Family>/` following the `Assets/Models/Trees/` layout. Keep `.blend` sources in `BlenderSource/`.
- One material per family; bake color variation into mesh variants via vertex colors — scatter renders through `Graphics.RenderMeshInstanced` (URP), so per-instance material variation is unavailable.
- The scene is vertex-bound with ~92% of frame verts from scatter (`RENDER_PERF_PROFILE_REPORT.md`); vert budgets below are **hard caps**. Budgets are proposed here — reconcile with `ArtAndDOTS_Pipeline.md` and feed the answer back into R1 open question 2.
- Every near mesh ships with a far-LOD mesh per `SURFACE_SCATTER_LOD_SPEC.md` (system is inert until far meshes are assigned). Far-mesh bounds must approximately match the near mesh (grounding offset is computed from the mesh actually drawn — §4.5).
- Pivot at mesh base; design rock-family meshes to read correctly when partially buried (no visible flat underside at ~20–30% sink).
- **Dependency note (corrected 2026-06-11):** tree and rock scatter families exist (`TreeChunkRenderSystem` / `RockChunkRenderSystem`), and short grass already renders via the GPU-instanced blade system (`GrassChunkGenerationSystem`, `GrassType 0`) — baseline grass needs **no mesh authoring**. B1–B3 slot into the rock family. B5–B7 target the reserved sparse-clump variant (`GrassType 1` in `TerrainChunkGrassSurface`, not yet implemented). B4 (shrubs) needs a new family or a second tree-family config. Per-step status lives in `mvp/Windswept_Colossus_Plains_Biome_Spec.md` § Procedural Generation Rules.

#### B1 — Boulder group models
- 3–4 variants, 1–6 m. Rounded, weathered, glacial-erratic silhouettes per spec.
- Colors: Granite Gray RGB(110,110,110), Dark Basalt RGB(70,70,75), Lichen Green accents — vertex color.
- Budget: ≤500 verts near, ≤80 verts far LOD.
- Wire into `RockRenderConfig.MeshVariants` + `LodMeshVariants` via `RockVisualBootstrap`.

#### B2 — Pebble cluster models
- Author as **pre-clustered patches** (≈5–12 pebbles per mesh, elements 10–50 cm) — spec wants "clustered, not uniform", and per-pebble instances would explode instance counts.
- 2–3 cluster variants, rock-family palette. Budget: ≤150 verts near.
- Far LOD likely unnecessary at this size — decide whether to cull-at-distance instead of swapping; note the decision here.

#### B3 — Stone outcrop models
- 2–3 variants, 5–30 m, rare. Purpose is horizon-breaking and navigation — **silhouette readability from 500 m matters more than close-up detail**.
- Budget: ≤1,500 verts near, ≤200 far.
- **Open question (resolve before wiring):** render via the rock scatter family or via the structure/relic placement path? At 30 m these behave like small landmarks — `RelicLodSelectionSystem` + structure placement may fit better than per-frame scatter instancing.

#### B4 — Steppe shrub models
- 2–3 variants, 0.5–1 m: hardy steppe bushes / low heath per spec. Coverage <2%, so instance counts stay low.
- Budget: ≤300 verts near, ≤60 far.
- No shrub render family exists. Smallest-change option: a second tree-family config (shrubs behave like mini-trees — bounds-grounded, yaw-varied). Decide in the system ticket.

#### B5 — Prairie grass tuft models
- Crossed-card tufts, 20–80 cm; 3 variants across the spec palette (Dry Grass RGB(166,153,102), Muted Olive RGB(114,125,76), Pale Green RGB(140,155,110)).
- Author for vertex-shader wind: encode bend weight (e.g. vertex color alpha, 0 at root → 1 at tip) — **record the chosen convention here**, the wind shader ticket consumes it.
- Budget: ≤30 verts per tuft. This family will dominate instance counts ("Density: High") — cheapness is the entire game.
- Runtime is the biggest open dependency in the group: high-density grass may need its own batched path rather than the existing scatter loop. Models are forward-compatible either way.

#### B6 — Tall grass patch models
- 1–1.5 m, authored as patch clumps (not single blades). Coverage <5%, spawns near streams / valley bottoms / sheltered slopes.
- Same wind-encoding convention as B5. Budget: ≤60 verts per patch.

#### B7 — Wildflower cluster models
- Clusters 10–40 cm; three colorways: white, pale purple, yellow. Spec: "small clusters, never fields" — author as cluster meshes for sparse placement.
- Budget: ≤60 verts per cluster. Shares the grass-family render path and wind convention from B5.
