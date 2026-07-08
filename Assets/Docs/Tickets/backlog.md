# Backlog ticket detail

**Status:** ACTIVE
**Last Updated:** 2026-07-05

Board: [`TICKETS.md`](TICKETS.md)

---

### M4 — BUG: Ballistic-takeoff false-grounding past jump apex _(Codex review 2026-07-02)_
`PlayerGroundingSystem.ShouldSuppressGroundingDuringBallisticTakeoff` suppresses a ground-probe hit only while
the player is *rising* (`mode == Ballistic && verticalSpeed > 0.05`). With the default jump (`JumpImpulse = 5`
→ apex ≈ 1.27m) and `GroundProbeDistance = 1.3`, the downward ray still reaches the floor for the **entire**
hop. Past apex, `verticalSpeed` drops below the threshold, suppression releases, and the still-hitting ray
marks `IsGrounded = true` mid-air — firing landing logic before real touchdown and (after the
`ModeDemotionMinGroundedTime` hysteresis window) potentially demoting Mode while airborne.
- **Fix direction:** gate suppression on **actual contact/separation**, not velocity sign — e.g. only treat a
  hit as grounded when the hit fraction/feet-to-surface distance is within a small contact epsilon, so the
  probe reaching ground from mid-air (apex < probe length) doesn't register as grounded. Keep it robust to both
  small jumps and high slingshot arcs.
- **Notes:** pre-existing on `main` (not introduced by the vista PR). Related to the grounding/landing cluster
  (**V7** fall-through, `LandingDetectionSystem`, Mode hysteresis). Needs playtesting — behavior change, not a
  cosmetic fix. Enable `DebugSettings.EnableFallThroughDebug` to observe the ungrounded/grounded transitions.

### M5 — Harden sky-drop landing against high-speed tunneling _(open — spun off from V7, 2026-07-03)_
The sky-drop (V7) lands stably today only because the landing chunk carries a *thick* Surface Nets mesh
collider — Unity.Physics penetration recovery catches the ~−87 m/s body without CCD. A thin or absent collider
under the landing XZ could still tunnel. Direction: guarantee a built collider under the spawn XZ before the
readiness gate releases (and/or thicken the landing-zone collider), so the sky-drop does not depend on
penetration-recovery luck. Related to the grounding/landing cluster — **V7**, **V10**, **M4**.

### R1 — Low-poly tree/rock LODs + enable relic LOD
**Intent:** Reduce environment-object render cost via LOD. **Priority: trees and rocks first** — they cost more FPS than the giant relics. Relics second.

- **Trees / rocks (do first):** authored via `TreeChunkRenderSystem` / `RockChunkRenderSystem`. Add a far LOD so dense scatter holds frame budget. Keep within poly/draw budgets — reference `ArtAndDOTS_Pipeline.md` and `Terrain/DOTS_Terrain_LOD_SPEC.md`.
- **Spike (decide before building meshes):** for the low-poly far representation, do we author **new low-poly scattered meshes** (Blender) or generate them at runtime via **decimation/impostor** from the existing meshes? Timebox the spike, pick one, note the decision here.
- **Relics (second):** `RelicLodSelectionSystem` already implements distance-based full↔impostor swap (`RelicLodParams` + `RelicRenderConfig.LodSwapDistance`/`LodHysteresis`) but is `[DisableAutoCreation]`, so it only runs if explicitly created. "Large relics not using LOD" is most likely just that it is never enabled — confirm whether the bootstrap creates it; if not, enable and verify each realized relic has valid `RelicLodParams` and a 2-entry `RenderMeshArray`.

**Open questions (resolve in-ticket):**
1. New authored low-poly art vs. runtime decimation/impostor? → resolved by the spike above.
2. Target swap distances / poly budgets — do these exist, or does R1 establish them?
3. Should enabling/verifying `RelicLodSelectionSystem` be a separate quick ticket, or stay folded into R1?

**Acceptance:** Trees/rocks have a far LOD that holds frame budget at a populated viewpoint. Large relics visibly swap to impostor past `LodSwapDistance` (confirm via `DebugSettings.LogRendering` transition log). Add/extend an EditMode test alongside `StructureLodTests` for any new swap logic.

### R2 — Speed-biased scatter LOD (drop detail during fast airborne movement)
**Spec:** `Terrain/Scatter/SCATTER_LOD_SPEED_BIAS_SPEC.md` (extends `SURFACE_SCATTER_LOD_SPEC.md`).

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

### T3 — Triage six pre-existing PlayMode test failures _(opened 2026-07-08 — surfaced during V9 P3 verification)_

The PlayMode suite (96 tests) has 7 standing failures. All were **proven pre-existing** during the V9 P3
session via a clean-baseline A/B (stash all changes → identical failures, same messages → restore). One is
already tracked (`CODEBASE_SIMPLIFICATION_PLAN.md` **S14**: `TreePlacementEditModeTests.
GeneratePlacements_VariantAndYaw_AssignedWithinExpectedRange` — no placement ever selects a non-zero tree
variant). The remaining six were untracked until this ticket:

**Group 1 — player visual GameObject never created (5 tests, same root symptom):**
`BasicSceneSetupTests.PlayerVisual_GameObjectExists`, `PlayerEntityBootstrapTests.PlayerVisual_Created`,
`.PlayerVisualSync_HandlesDestroyedEntity`, `.PlayerVisualSync_SyncsPositionWithEntity`,
`.PlayerVisualSync_SyncsRotationWithEntity` — all `Expected: not null, But was: null` on the visual
GameObject. Historically these tests failed on *sync lag/rotation* (`Player/PLAYER_BOOTSTRAP_FIX_SPEC.md`
Phases 2–3); "not created at all" is a different, later breakage — suspects include the FPS-only reversal
(`PlayerFirstPersonVisibility` body-hide, 2026-06-20) and the BoxPlayer visual swap. Triage = find when
creation stopped in the test environment, then decide fix vs. retire-the-tests-against-current-design.

**Group 2 — takeoff mode transition:**
`PlayerWallContactCommandPlayModeTests.GroundedJump_DoesNotImmediatelyRegroundWhileStillAscending` —
expected `Ballistic` on the takeoff frame, got `Grounded`. Likely the same grounding-suppression territory
as **M4** (ballistic-takeoff false-grounding past jump apex — suppress by contact/separation, not velocity
sign); triage together with M4 before fixing either independently.

**Evidence:** MCP test-run jobs 2026-07-08, with-changes vs clean-baseline failure lists identical.
**Not in scope:** the S14 tree-variant failure (already tracked); EditMode (223/223 green).
**Spec:** `Rendering/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` §19 (addendum).
Immense relics fictionally visible from kilometers pop out at the ~600u far clip today. Fix lives in the
**Phase 2 seed-driven horizon ring**, not the ground disc (horizontal plane; never grows vertical features):
the ring's generator consumes **authored anchors (V12)** and renders each hero relic as a silhouette card at
its true bearing, colored through the shared V9 atmosphere path (high aerial strength). The near↔far handoff
hides inside haze saturation at the far clip — the real mesh is a ghost when it clips out, the card fades in
behind the same haze. Scaled-proxy rendering documented as the alternative if a crisp distant silhouette is
ever demanded (not planned — contradicts the dissolve-into-haze vista language).
- **Depends on:** V12 (authored anchors = the data source) and the Phase 2 horizon ring itself (post-MVP);
  consumes V9's palette/HLSL. Long-term the skybox mountains migrate into the same ring and the skybox
  returns to atmosphere-only.
- **Narrowed by R6 (2026-07-06):** with the landmark draw distance in place, cards only need to cover
  **>2000u**. The §19 handoff assumption ("haze ~full at the far clip") predates the V9 round-5 thin haze
  and is superseded by R6's dithered edge fade at the landmark distance.

---

### R6 — Landmark draw distance — relics never cull _(pulled into the Vista Moment work-set 2026-07-07)_
Detail moved to [`vista-moment.md`](vista-moment.md) per the kanban convention (Build-order step 1 there).
Spec: `Rendering/LANDMARK_DRAW_DISTANCE_SPEC.md`.

---

### W1 — Magic power grid _(placeholder — design-stage, not yet broken into tickets)_
**Spec:** `Structures/MAGIC_GRID_SPEC.md` (DESIGN). Analytic world-space XZ lattice: power-source nodes, WFC-build-on-node affordance, sparse claimed-node alignment state, per-template `NodeAffinity`, universal influence query. Decisions captured in the spec; §13 lists the open questions to resolve before build.

**Not scheduled — sequences behind its foundation.** Don't break into tickets until Structure Placement is on the board:
- Depends on the **Structure Placement** anchor pipeline (`STRUCTURE_PLACEMENT_PLAN.md` §8 Steps 1–3) — the grid is a candidate-source variant reusing its `StructureAnchorRecord` / footprint / persistence machinery.
- That in turn depends on the known **WFC bootstrap gaps** (`HybridWFCSystem` not created by `DotsSystemBootstrap`; deterministic seed) — see `STRUCTURE_PLACEMENT_SPEC.md` §12.5.1.
- Orthogonal to **P2 Magic Hand System** (shares the "magic" fiction only; no dependency either way).

**Earliest natural entry:** after the free anchor planner exists, add the grid as a `NodeBound` candidate source + the on-node WFC build affordance.

---

### Biome Art — Windswept Colossus Plains scatter models (B1–B7)

**Source spec:** `Assets/Docs/Biomes/Windswept_Colossus_Plains_Biome_Spec.md`. These are **model authoring tickets** — runtime placement/render systems are separate work where noted.

**Shared conventions (apply to all B tickets):**
- Author in Blender (`BlenderSource/`), export FBX to `Assets/Models/<Family>/` following the `Assets/Models/Trees/` layout. Keep `.blend` sources in `BlenderSource/`.
- One material per family; bake color variation into mesh variants via vertex colors — scatter renders through `Graphics.RenderMeshInstanced` (URP), so per-instance material variation is unavailable.
- The scene is vertex-bound with ~92% of frame verts from scatter (`RENDER_PERF_PROFILE_REPORT.md`); vert budgets below are **hard caps**. Budgets are proposed here — reconcile with `ArtAndDOTS_Pipeline.md` and feed the answer back into R1 open question 2.
- Every near mesh ships with a far-LOD mesh per `SURFACE_SCATTER_LOD_SPEC.md` (system is inert until far meshes are assigned). Far-mesh bounds must approximately match the near mesh (grounding offset is computed from the mesh actually drawn — §4.5).
- Pivot at mesh base; design rock-family meshes to read correctly when partially buried (no visible flat underside at ~20–30% sink).
- **Dependency note (corrected 2026-06-11):** tree and rock scatter families exist (`TreeChunkRenderSystem` / `RockChunkRenderSystem`), and short grass already renders via the GPU-instanced blade system (`GrassChunkGenerationSystem`, `GrassType 0`) — baseline grass needs **no mesh authoring**. B1–B3 slot into the rock family. B5–B7 target the reserved sparse-clump variant (`GrassType 1` in `TerrainChunkGrassSurface`, not yet implemented). B4 (shrubs) needs a new family or a second tree-family config. Per-step status lives in `Biomes/Windswept_Colossus_Plains_Biome_Spec.md` § Procedural Generation Rules.

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
