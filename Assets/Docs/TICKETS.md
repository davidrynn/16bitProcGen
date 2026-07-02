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

> **Scope: "look at" moment (2026-06-29):** Decided the MVP vista is a **look-at** beat — crest the plain,
> see the hand across the haze, slingshot *toward* it. **Entering** the hand is explicitly out of MVP scope.
> Consequence: the critical path is **V1 → V2 → V3 → V4**; **V5 (Relic → WFC maze interior) is deferred**
> to a follow-up sprint, where it re-homes next to **W1** (both carry the same WFC bootstrap blocker,
> `STRUCTURE_PLACEMENT_SPEC.md` §12.5.1). Camera Feel C1–C3 stays secondary — slingshot-toward still applies.

### Vista Moment _(sprint lead — the MVP wow moment)_

> Target: player crests a plain, sees the giant stone hand across atmospheric haze, and slingshots toward
> it. (Entering the hand is out of MVP scope — see the "look at" scope note above.) Ordered by
> impact-per-hour per `AI/MVP_VISTA_MOMENT_SPEC.md` §4.

| ID  | Status | Subject |
|-----|--------|---------|
| V1  | [x] | Ground plane impostor — built & enabled; now receives fog so the plain hazes into the horizon (2026-07-01) |
| V2  | [x] | Atmospheric fog — enabled + impostor fog wired; "square from height" diagnosed as skybox horizon, not a fog plane (2026-07-01). Dynamic sky-tracking color → V6 |
| V3  | [ ] | Mountain skybox panel — painted silhouette framing the horizon _(color path folds into V9 P4)_ |
| V4  | [ ] | Hand mesh validation — confirm `testAlienHand.fbx` renders; tune scale/yOffset |
| V5  | [-] | _(Deferred — out of MVP "look at" scope)_ Relic → WFC maze interior — connect relic anchor to dungeon interior generation |
| V6  | [x] | Time-of-day + biome-dependent sky & tracking fog — Plains "Cloudbreak" preset; haze color follows the horizon (2026-07-01) |
| V7  | [ ] | BUG: player intermittently falls through the ground (collider-build timing / sky-drop landing) |
| V8  | [ ] | Distance-graded fog density — see the ground from height while still veiling the far horizon _(consumes V9 `_AtmoHorizon`)_ |
| V9  | [ ] | Atmosphere color authority — one palette source + global `_Atmo*` uniforms + shared aerial-perspective HLSL; unifies sky, disc, mountains, terrain, hero relic & fog color (supersedes disc `SyncTerrainColor`; folds V3 color path; hero relic gets reduced-strength fog exemption) |

#### V1 — Ground plane impostor
**Spec:** `AI/GROUND_PLANE_IMPOSTOR_SPEC.md`. Horizontal terrain-colored disc (~1500u radius) on the XZ
plane beyond the ~256u SDF chunk radius; world-space shaded with the terrain's noise octaves, radial alpha
fade hides the seam, fog dissolves the outer edge. Entity follows player XZ (one transform write). Eliminates
the void from altitude and enables the sky-drop intro. ½–1 day; no texture assets, no SDF pipeline changes.
- **Status (2026-07-01):** Built & enabled all along (the "unbuilt" assumption was wrong). Confirmed via MCP
  screenshots. The disc is a 3000u square grid (roundness comes from the radial shader fade), not a literal
  disc — acceptable; deferred as polish. Added `multi_compile_fog` + `MixFog` to `GroundPlaneImpostor.shader`
  so the plain now fades into the horizon haze instead of staying vivid to its edge. There was **no literal
  void/gap** — see `AI/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`.

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
- **Resolved (2026-07-01, via MCP screenshots — `AI/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`):** The "square
  from height" and warm tint were **not a fog plane** — fog was already disabled in config, and the warm
  band is the **skybox horizon** driven by the day/night cycle (`TimeOfDayController` → `SkyPreset`). Proven by
  setting the camera clear to solid blue (tan vanished). Applied: enabled gentle Exp² fog (`density 0.0015`,
  color `(0.62,0.74,0.85)`) in `ProjectFeatureConfig.asset` + fog support in the impostor shader → clean haze,
  no seam. **Remaining:** the fog color is static and will clash across the day/night cycle — making it track
  the sky horizon (and biome) moves to **V6**. De-oranging the sky palette also lives in V6.

#### V3 — Mountain skybox panel
Paint or source a mountain silhouette into the skybox (MVP Option A per `MVP_VISTA_MOMENT_SPEC.md` §2.4 —
2–4 hrs). Sells horizon depth. The seed-driven horizon ring (`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`) is the
Phase 2 system, deferred.

#### V4 — Hand mesh validation
Confirm `Assets/Models/testAlienHand.fbx` renders correctly at scene scale in Play Mode (already wired as
`Relic.asset` `DefaultTemplateId: relic_hand`). Tune `scale` / `yOffset` in `RelicVisualBootstrap` inspector
so the four-finger hand reads from ~200–400u. Per `MVP_VISTA_MOMENT_SPEC.md` §2.1.

#### V5 — Relic → WFC maze interior _(deferred — out of MVP "look at" scope, 2026-06-29)_
Connect the relic anchor to WFC dungeon interior generation so the hand is enterable. Bridges structure
placement to the existing WFC pipeline — reuses the dungeon realizer path. Depends on the WFC bootstrap +
deterministic-seed fixes noted in `AI/STRUCTURE PLACEMENT/STRUCTURE_PLACEMENT_SPEC.md` §12.5.1. See
`WFC/MAP_WFC.md`, `WFC_Dungeon_Test_Plan.md`.

#### V6 — Time-of-day + biome-dependent sky & tracking fog
**Spec/plan:** `AI/SKYBOXPLAN.md` (Phase 3), `AI/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`.
**Intent:** Kill the orange horizon and make the atmosphere read as one coherent haze at any time of day —
per the opening inspiration (`Docs/Temp_OpeningInspiration.png`): a cool, broken-overcast highland day with
distant mountains dissolved into pale blue-grey haze. Keep the **Highlands** fiction (no ocean).

The sky architecture already exists but is empty/unwired: `TimeOfDayController` runs a live cycle over a
`SkyPreset`; `BiomeSkyMapping` (`BiomeType → SkyPreset`) and `ApplyBiome()` are implemented but
`DefaultBiomeSkyMapping.asset` has a null fallback + no entries, and nothing calls `ApplyBiome()`. Scope this
to the single MVP biome and leave the biome-switch bridge as a marked seam.

- **Create the Plains "Cloudbreak" `SkyPreset`** — cool overcast steppe with broken sunlight. Read off the
  inspiration: zenith `~(0.45,0.52,0.60)`, horizon `~(0.68,0.72,0.74)`, high cloud coverage (bright white /
  grey undersides), soft reduced-intensity sun. Tune dawn/noon/dusk/night keyframes so **no** daytime horizon
  is orange (current preset horizons are all warm). Set it as the active + fallback preset.
- **Fog tracks the sky** — promote `SKYBOXPLAN.md`'s Phase 3+ "fog integration" extension: drive
  `RenderSettings.fogColor` (and optionally density) from the evaluated `SkySettings.horizonColor` each frame
  in `SkyController` so haze always matches the horizon at every time-of-day/biome (the impostor already reads
  RenderSettings fog per V1/V2). Replaces the static fog color set in `DotsSystemBootstrap.ApplyDistanceFog`.
- **Populate + assign `DefaultBiomeSkyMapping`** — Plains entry + fallback = Cloudbreak; assign it to the
  `TimeOfDayController` so the biome path is live and correct for one biome, ready to extend.
- **Decision (2026-07-01):** keep the live day/night cycle (fog-tracks-sky makes it look right, so no need to
  pin a fixed time). Colors are **biome-dependent** by design.
- **Deferred seam:** the DOTS→sky biome-change signal (a caller for `ApplyBiome`) — build when a 2nd biome
  exists to switch to. Verify then how "current biome" is signaled at runtime.
- **Acceptance:** across a full day/night cycle, horizon reads cool (never orange) and the ground plain +
  scatter + impostor haze into one matching horizon color; validate in Play Mode at several altitudes/times.
- **Done (2026-07-01):** ✅ Created `Assets/Resources/Sky/CloudbreakSkyPreset.asset` (cool overcast palette,
  no warm daytime horizon). ✅ `SkyController` now drives `RenderSettings.fogColor` from the evaluated
  horizon each update (`_driveFogColor`, default on). ✅ Populated + assigned `DefaultBiomeSkyMapping` (Plains
  → Cloudbreak + fallback); set the `TimeOfDayController` `activePreset` = Cloudbreak and default start time to
  midday. Scene saved. Validated via MCP screenshots (ground + altitude) — cool grey-blue sky, clouds, plain
  dissolves into a matching haze, zero orange.
- **Fog density (2026-07-01):** raised `0.0015 → 0.0022`. At altitude the 600u far clip cuts the ground before
  light fog saturates, leaving a sharp green silhouette against the mist; `0.0022` dissolves that far edge into
  the haze while keeping the plain readable (`0.003` removed the edge but went milky). This is a **far-clip
  trade-off** — the real long-term fix for "see far from altitude without a fog bubble" is a larger far clip +
  V3 mountain/horizon panel, not heavier fog. Near-ground vista already reads clean at any density.
- **Deferred seam (unchanged):** no DOTS→sky biome-change signal yet (`ApplyBiome` still uncalled) — build
  with the 2nd biome. Clouds use default coverage; bump on `TimeOfDayController.defaultCloudSettings` for a
  more overcast sky.

#### V7 — BUG: player falls through the ground _(flagged 2026-07-01)_
Intermittent — the player sometimes drops through terrain. Suspect collider-build timing vs. player arrival,
especially on sky-drop landing. Investigate: `PlayerTerrainSafetySystem`, `TerrainChunkColliderBuildSystem`,
the sky-drop readiness gate (`SkyDropGravityHoldSeconds` in `ProjectFeatureConfig`). Enable
`EnablePlayerFallThroughDiagnosticSystem` to capture repro data. _(Not yet investigated.)_

#### V8 — Distance-graded fog density _(flagged 2026-07-01; approach decided 2026-07-02)_
The uniform-density fog (V6 landed at `0.0022`, Exp²) is too thick to see the ground from altitude — raising
density to hide the far-clip edge also greys out the near/below ground. Goal: near/downward views stay clear
while the far horizon still veils.

**Root cause.** Unity's built-in fog (Linear, Exp, Exp²) is **purely distance-based and altitude-blind** — it
fogs a fragment by its Euclidean distance from the camera. At the ~400u sky-drop height *everything* is far, so
the ground directly below veils just as hard as the horizon. This is a structural limit of built-in fog, not a
tuning miss. The thing we actually want (clear looking down, veiled at the horizon) is a different axis —
**height-based / aerial-perspective fog** — than pure distance.

**Two routes considered:**

- **Route A — Linear retune (cheap, config-only, partial). ← CHOSEN for now.** Infrastructure already exists:
  `ProjectFeatureConfig` has `FogStartRatio` (0.14) / `FogEndRatio` (0.308), and
  `DotsSystemBootstrap.ApplyDistanceFog()` already wires them for Linear mode — currently dormant because
  `FogMode` is `3` (Exp²). Flip `FogMode → 1` (Linear) to get an explicit **zero-fog near zone** (nothing
  before `start`) that Exp² can't produce; from altitude the nearer ground below fogs less than the horizon at
  far clip. Still distance-based, so any single start/end is a compromise between low- and high-altitude
  framing — accepted as good-enough for the MVP vista. Near-zero risk, ~config-only, fully reversible.

- **Route B — Height-based fog (proper, the JC3 aerial-perspective look).** Fog density as a function of
  world-Y so a downward ray through thin high air stays clear while a long horizontal ray through dense low air
  veils — altitude-independent, reads right at any height. URP 17.2 has **no** built-in height fog (that's
  HDRP), so this requires custom shader work: extend the shared fog term in the terrain / scatter / impostor
  shaders, or a depth-based URP renderer feature that reconstructs world position. Larger surface area, more
  testing. This is where **V3** (mountain horizon panel) naturally pairs in — painted silhouette + height fog
  is the real "see terrain to the horizon" combo.

**Decision (2026-07-02):** Ship **Route A** first (nearly free, may be sufficient), judge it at altitude via
screenshots, and **escalate to Route B only if the distance compromise still reads wrong.** Route B remains the
documented fallback.

**Reversibility:** Route A changes only serialized fields in `ProjectFeatureConfig.asset` — `FogMode` back to
`3` and, if start/end are retuned, the prior `FogStartRatio`/`FogEndRatio` values. Record the pre-change values
below before editing so the exact Exp²/`0.0022` baseline can be restored with a single asset revert; make no
code/shader changes under Route A.

_Baseline before Route A (restore point):_ `FogMode: 3` (Exp²), `FogDensity: 0.0022`,
`FogColor: {0.62, 0.74, 0.85}`, `FogStartRatio: 0.14`, `FogEndRatio: 0.308`, `VistaCameraFarClip: 600`.

**Route A applied (2026-07-02, first pass — config-only, `ProjectFeatureConfig.asset`):** `FogMode: 1`
(Linear), `FogStartRatio: 0.55` (start = 330u), `FogEndRatio: 1.0` (end = 600u = far clip). Rationale: the
clear near-zone reaches past the 400u sky-drop so the ground below reads ~74% visible at altitude, while the
ramp finishes at the far clip to veil the horizon and hide the hard clip edge (V2). At ground-level play all
real terrain (≤180u streaming radius) is inside the clear zone; only the impostor disc (330–600u) hazes.
`FogDensity: 0.0022` left in the asset unused (Linear ignores it) as the Exp² restore value. `FogColor` is
still driven live from the horizon by `SkyController._driveFogColor` (V6) — unchanged. **Pending judgement in
Unity at altitude;** retune the two ratios if the near-zone reads too crisp or the horizon too thin.

#### V9 — Atmosphere color authority _(specced 2026-07-02)_
**Spec:** `AI/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md`.
**Intent:** Make one authority own the scene palette and have every distance-facing surface *consume* it, so
sky, ground disc, mountain impostor, terrain, and fog stay hue-unified and all shift together with the
day/night cycle and biome. Root problem: the four surfaces source color from four different mechanisms and two
are frozen — the sky + fog track time-of-day while the disc (frozen shader defaults), mountains (flat
`_MountainColor`), and terrain (baked Synty albedo × white `_BaseColor`) do not. The disabled
`SyncTerrainColor` was architecturally doomed because it reads the terrain tint from `_BaseColor`, which is
white (the color lives in the texture). This generalizes the shipped `_driveFogColor` coupling from one output
to all surfaces.
- **Architecture:** extend `TimeOfDayController` into the authority (managed/rendering, not ECS); broadcast
  global `_Atmo*` uniforms (`_AtmoHorizon/_AtmoZenith/_AtmoGround/_AtmoRock/_AtmoSun/_AtmoSaturation/_AtmoFarFade`)
  once per frame; add a shared `Atmosphere.hlsl` with `ApplyAerialPerspective(color, viewDist, strength)`. Disc
  and mountains share that function — disc low `strength`, mountains high `strength` (→ correctly desaturated,
  horizon-tinted). Ground-color decision = **Option B**: palette *tints* the Synty terrain (cheap `_BaseColor`
  write first, shadergraph albedo multiply for the full version).
- **Rollout:** P1 authority+contract → P2 disc consumes → P3 terrain tint → **P4 mountains (folds V3 color
  path)** → P5 saturation/overcast pass. Compute-light (a few ALU ops/fragment, no new passes/textures).
- **Deliberately deferred:** unifying the disc + mountain C#/DOTS bootstraps into one system — wait for a third
  impostor type. Share the interface (authority + HLSL), not the implementation.
- **Confirmed symptom — hero relic "white-out" at distance (2026-07-02).** The giant hand relic washes pale
  toward the horizon fog color at distance, dissolving into the cloud band behind it (the one object the vista
  exists to make you *look at*). **Root cause:** the relic is a **fifth distance-facing surface** not in the
  enumeration above — it renders full-terrain-grade fog with no hero treatment, so V6's `_driveFogColor`
  (fog = bright ~`0.7` grey-blue horizon) veils the tallest/most-distant geometry hardest. Confirmed by toggling
  `EnableDistanceFog: 0` → hands drop to flat warm-gray (fog is the whitening agent, not lighting/albedo).
  **Diagnostic facts:** not an impostor/LOD artifact — every relic template has null impostor mesh/material and
  `lodSwapDistance: 1000` > the 600u far clip, so relics never swap and are always the full LOD-0 mesh; the
  shared material `Assets/Materials/Unlit gray.mat` is misnamed — it is **URP/Lit** with warm-gray base
  `(0.55, 0.52, 0.48)`, so it does receive fog. **Requirement for V9:** the atmosphere authority must let the
  hero relic take a **reduced aerial-perspective strength** (hero exemption) so it stays a legible silhouette
  against the pale plain rather than saturating to fog like background terrain — the opposite end of the
  strength scale from mountains (high strength). Cheap-tuning overlap noted in **V8/V3** (hero sits in the light
  near-fog band; horizon panel carries the far wash). Cosmetic follow-up: rename `Unlit gray.mat` to stop
  implying it is unlit.
- **Relationships:** supersedes disc `SyncTerrainColor` (`GROUND_PLANE_IMPOSTOR_SPEC.md` §11); mountain color
  path per `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` §18; **V3** consumes P4; **V8** consumes `_AtmoHorizon`;
  hero-relic exemption relates to **V4** (hand mesh validation / placement) and the V8/V3 far-clip trade-off.
- **Status:** specced, not started. Acceptance = §11 of the spec (no surface holds a private base-color literal;
  cycle shifts all surfaces together; disc-edge/mountain-base/horizon read as one hue band) **+ the hero relic
  reads as a legible silhouette at vista distance, not a fog-saturated wash**.

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

_(Former A6 → folded into the vista fog ticket **V2**. Former A7 → backlog **R1**. Both were rendering/environment work, not animation. A9 added 2026-06-20 for the FPS-only reversal.)_

---

## Backlog

_Tickets for later sprints — not yet scheduled._

| ID  | Subject | Group |  
|-----|---------|-------|
| M1  | Glide mechanic (Space hold → GlideCharging → Gliding) | Movement |
| M2  | Chain slingshot (chain window + additive velocity) | Movement |
| M3  | Thermal columns (vertical lift volumes) | Movement |
| M4  | BUG: Ballistic-takeoff false-grounding past jump apex — suppress by contact/separation, not velocity sign | Movement |
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
