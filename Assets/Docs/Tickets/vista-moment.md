# Work-set: MVP Vista Moment

**Status:** ACTIVE
**Last Updated:** 2026-07-15

Board: [`TICKETS.md`](TICKETS.md)

---

> **FPS-only reversal + re-sequence (2026-06-20):** MVP ships **first-person only**. `PlayerCameraSettings.IsThirdPerson`
> now defaults to `false` and the third-person body is hidden in first-person (`PlayerFirstPersonVisibility`).
> Third-person stays as a **dev/debug toggle (V key)** for inspecting these clips. Consequence: the full-body
> clips in **A1–A8 are not visible in normal play** — they only show in the dev toggle. This made **Camera
> Feel (C1–C3)** the primary in-game feel feedback in first-person, and **A9 (first-person arms viewmodel)**
> the real animation payoff; A1–A8 became mostly dev-toggle / A9-prep work. _(Superseded as sprint lead by
> the 2026-06-29 Vista re-anchor below — Camera Feel is now secondary, A9 deferred.)_

> **Re-anchor to Vista (2026-06-29):** Re-focused on the project's designated MVP wow moment — the vista
> discovery of the giant four-fingered stone hand across a hazy plain (`Rendering/MVP_VISTA_MOMENT_SPEC.md`,
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
> impact-per-hour per `Rendering/MVP_VISTA_MOMENT_SPEC.md` §4.

#### V1 — Ground plane impostor
**Spec:** `Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md`. Horizontal terrain-colored disc (~1500u radius) on the XZ
plane beyond the ~256u SDF chunk radius; world-space shaded with the terrain's noise octaves, radial alpha
fade hides the seam, fog dissolves the outer edge. Entity follows player XZ (one transform write). Eliminates
the void from altitude and enables the sky-drop intro. ½–1 day; no texture assets, no SDF pipeline changes.
- **Status (2026-07-01):** Built & enabled all along (the "unbuilt" assumption was wrong). Confirmed via MCP
  screenshots. The disc is a 3000u square grid (roundness comes from the radial shader fade), not a literal
  disc — acceptable; deferred as polish. Added `multi_compile_fog` + `MixFog` to `GroundPlaneImpostor.shader`
  so the plain now fades into the horizon haze instead of staying vivid to its edge. There was **no literal
  void/gap** — see `Rendering/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`.

#### V2 — Atmospheric fog tuning _(canonical fog ticket — folds in former A6)_
**Intent:** Fog should read as thin mist suspended in air, not a tint on objects. From altitude it currently
renders as a visible square, which breaks the illusion.
- Bias toward distance/altitude; reduce density so near geometry is largely unaffected. Shift color blue-grey
  (`#8FA8C0`), tune start/end so foreground is sharp and horizon veiled.
- Eliminate the "square from height" artifact — if a finite plane/quad drives the fog, replace with a
  camera-relative/global effect or skirt it so no edge shows at fly heights.
- Reconcile with the vista stack — check interaction with `Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md` and
  `Rendering/MVP_VISTA_MOMENT_SPEC.md` so fog and horizon read as one atmosphere.
- **Open questions (resolve first):** (1) what renders the fog today — URP Volume/global fog, a custom
  shader/material, a skybox blend, or a scene quad? (`WeatherSystem.WeatherType.Fog` sets weather state only,
  no visuals — source is elsewhere.) (2) Is the "square from height" a dedicated fog plane or the ground-plane
  impostor edge?
- **Acceptance:** from ground and max fly height, no plane edge; near objects (~1 chunk) negligible tint; far
  horizon softened. Validate in Play Mode at several altitudes.
- **Resolved (2026-07-01, via MCP screenshots — `Rendering/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`):** The "square
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

#### V4 — Hand mesh validation _(validated 2026-07-05 — renders correctly; readability spun off to V11/V12)_
Confirm `Assets/Models/testAlienHand.fbx` renders correctly at scene scale in Play Mode (already wired as
`Relic.asset` `DefaultTemplateId: relic_hand`). Tune `scale` / `yOffset` in `RelicVisualBootstrap` inspector
so the four-finger hand reads from ~200–400u. Per `MVP_VISTA_MOMENT_SPEC.md` §2.1.
- **Validated (2026-07-05, Play Mode + MCP screenshots):** the pipeline works end to end. This seed places two
  relics ~250–300u from spawn toward +Z; the right one is confirmed `relic_hand` (silhouette matches the FBX
  asset preview). `scale: 500` / `yOffset: -5` yields a ~40–50u-tall object; renders stable with no LOD
  interference (`lodSwapDistance: 1000` > 600u far clip, null impostor → always the full mesh).
- **Finding — it does not read as a *hand*:** `testAlienHand.fbx` has short, fused finger-lobes, so at vista
  distance it reads as a weathered boulder. No `scale`/`yOffset` tuning fixes silhouette readability — that
  needs a better mesh → **V11**. Placement is also seed-luck across ±1024u (this seed was lucky); a
  *guaranteed* hand in view of spawn → **V12**. Closed as validated; the wow-moment readability lives in the
  follow-ups.

#### V5 — Relic → WFC maze interior _(deferred — out of MVP "look at" scope, 2026-06-29)_
Connect the relic anchor to WFC dungeon interior generation so the hand is enterable. Bridges structure
placement to the existing WFC pipeline — reuses the dungeon realizer path. Depends on the WFC bootstrap +
deterministic-seed fixes noted in `Structures/STRUCTURE_PLACEMENT_SPEC.md` §12.5.1. See
`WFC/MAP_WFC.md`, `WFC/WFC_Dungeon_Test_Plan.md`.

#### V6 — Time-of-day + biome-dependent sky & tracking fog
**Spec/plan:** `Rendering/SKYBOXPLAN.md` (Phase 3), `Rendering/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`.
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

#### V7 — BUG: player falls through ground on sky-drop landing _(fixed 2026-07-03)_
The sky-drop spawn (Y≈400) could drop the player through terrain on landing. Root cause: the readiness-gate
ground probe didn't reach the ground from spawn height, so the gate released on the 8 s timeout rather than on
true collider readiness. Fix: `ProbeDistance = math.max(96f, spawnY + 64f)` in `PlayerEntityBootstrap` so the
probe reaches terrain and the gate releases on readiness (`65adabb`). Validated in-editor 2026-07-03 — lands
stably at Y≈5.23, no fall-through. General-traversal fall-through split out to **V10**; residual high-speed
tunneling fragility tracked as **M5**.

#### V10 — BUG: player falls through terrain during traversal _(fixed 2026-07-03)_
Split from V7 after the reframe that fall-through "might happen any time going over the terrain," not only on
sky-drop. Root cause: `TerrainChunkColliderBuildSystem` built colliders 4/frame in arbitrary archetype order
with no player awareness, so freshly streamed or LOD-promoted chunks near the player could sit collider-less
for several frames. With no CCD (Unity DOTS Physics is discrete-collision) and a safety net that cannot
ray-hit a collider that does not yet exist, the player drops straight through the gap. Fix: each frame compute
the player's chunk, sort buildable chunks by Chebyshev distance to it, and build nearest-first; the 3×3
near-player ring (`NearPlayerColliderRadius = 1`) is built unconditionally (budget-exempt) while far chunks
respect the 4/frame throttle. Falls back to the original arbitrary-order loop when there is no player / LOD
policy (tests). Commit `1883659`, validated 2026-07-03.

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

**Route A judged (2026-07-05, Play Mode + MCP screenshots): REJECTED — revert to the Exp² baseline.** Failed
on both ends:
1. **Altitude:** from the 400u sky-drop the ground below is a featureless fog-grey void, not the predicted
   ~74%-visible — every surface is ≥400u away (deep inside the 330–600 ramp) and the below-horizon view is
   mostly the impostor at 400–600u slant distances, i.e. fully fogged. The "single start/end compromise"
   failure mode, confirmed empirically.
2. **No ground-level benefit either** — the Linear near-zone reads essentially the same as the Exp² baseline
   at ground level, so Route A carried risk without gain on the axis it was meant to fix.

**Correction during verification:** the *dark green band* along the horizon (a dark wall behind the pale
fogged plain) was initially read as a Route A regression, but it appears **identically under the restored
Exp² baseline** — it is fog-mode-independent, most likely the skybox ground hemisphere showing in the gap
between the 600u far clip and the geometric horizon. That is **V3 horizon-panel / V9 hue-unification**
territory, not a fog-mode bug.

Also confirmed V9's hero-relic concern from altitude: the relics vanish entirely into the fog wash.
**Outcome (2026-07-05):** baseline restored per the restore point above (`FogMode: 3`, `FogDensity: 0.0022`,
ratios `0.14`/`0.308`) and verified in Play Mode at midday (pinned per T2) — ground vista reads as before.

**Folded into V9 (owner decision 2026-07-05):** Route B ships as the height-aware term inside V9's shared
`ApplyAerialPerspective` (`ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` §5.3a) — both efforts touch the same shader
set, so each consumer's fog call is visited once, not twice. Density is a zero-sum knob under any
distance-only model (owner screenshot 2026-07-05: the sky-drop reads as falling into featureless haze at
`0.0022` Exp²), so no further tuning; the model change is the fix. V8's acceptance moved to that spec
(§11.8). **This ticket closes as merged — remaining work tracks under V9.**

#### V9 — Atmosphere color authority _(specced 2026-07-02)_
**Spec:** `Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md`.
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
- **Scope amendment (2026-07-05) — height-aware from day one; V8 folds in; next build.** After Route A's
  rejection (V8) and the owner's sky-drop screenshot (dropping into featureless haze), `ApplyAerialPerspective`
  ships **height-aware from its first version** (spec §5.3a: haze density decays exponentially with altitude,
  analytic closed form — the owner's "exponentially less in the real world" observation, and the JC3 look).
  Two consumers added: the **hero relic** (reduced-strength exemption, now spec §5.4 + P4b) and the **skybox
  haze band** (mountain silhouette in `ProceduralGradientSky.shader` pulls toward the per-frame horizon color —
  works with the live day/night cycle by construction, so the cycle stays in MVP). **MVP slice = P1 + P4 +
  P4b** — fixes the white-relic-vs-dark-mountains clash *and* the obscured sky-drop in one pass. This is the
  designated next build.
- **MVP slice built (2026-07-05) — P1 + P4 + P4b landed, validated in Play Mode via MCP screenshots.**
  - **P1:** `Assets/Shaders/Atmosphere.hlsl` (global `_Atmo*` contract + height-aware
    `ApplyAerialPerspective` per §5.3/§5.3a, plus `AtmoFarClipHaze` — a distance-only concealer that veils
    the last quarter before the far clip at any altitude, so converted surfaces never show the 600u cut).
    New `AtmosphereSettings` per-preset block on `SkyPreset` (ground/rock hues, saturation, haze
    density/falloff); `TimeOfDayController` broadcasts via new static `AtmosphereBroadcast` each frame
    (transition-blended, so biome preset changes never snap the palette). Defaults seeded at editor/player
    init so consumer shaders never sample zeroed globals in scenes without the controller. EditMode
    contract tests in `AtmosphereAuthorityTests` (full suite 215/215 green).
  - **P4:** `ProceduralGradientSky.shader` mountain band — `_MountainColor` literal removed as a source;
    base hue from `_AtmoRock→_AtmoGround` across the ridge **× `_MountainShade` (0.55)** — the palette hues
    are lit-surface colors, so unshaded they whited the ridge out (owner screenshot, round 2) — hazed by
    the height term at a fictive 900u distance with per-material dials (`_MountainHazeStrength 0.7`,
    `_MountainHazeFloor 0.28`; 0.85/0.45 dissolved the band entirely at ground level, 0.4 floor still
    washed it from altitude). Kills the dark unfogged wall.
  - **P4b:** new `Relic/RelicLit` shader (DOTS-instancing/Entities Graphics-compatible, Lambert+SH+shadows,
    **no pipeline fog**) + `Assets/Materials/RelicHero.mat` (`_AerialStrength 0.3`) assigned to the
    `relic_hand` template in `Basic Terrain Scene`; `relic_head`/`stone_outcrop` keep `Unlit gray.mat`
    (normal fog) per the "background relics haze normally" acceptance.
  - **Disc fog call converted** (small slice extension, spec §5.3a retirement model): `GroundPlaneImpostor`
    dropped altitude-blind `MixFog` for `ApplyAerialPerspective` — required for the un-obscured sky-drop
    since the below-view at 400u is mostly the disc (V8 finding). Disc palette literals untouched (that's
    P2). Haze density default `0.0012` anchors the converted disc to the still-Exp²-fogged terrain at the
    ~180u seam (both ≈15–20% veiled there).
  - **Validated:** mid-drop the ground below reads clearly and hazes continuously toward the horizon ring
    (was a featureless fog void); at ground both relics read as legible silhouettes at ~250–300u against a
    pale unified horizon; no dark green band; zero console errors. Screenshots:
    `Assets/Screenshots/v9_skydrop_altitude_v2.png`, `v9_ground_vista_v2.png`.
  - **Round-2 tuning (2026-07-05, owner screenshots from the drop):** three artifacts diagnosed and
    addressed. (1) Mountain band whited out from altitude → `_MountainShade 0.55` + floor `0.28` (above).
    (2) "Clear ring around the player looks strange" from altitude → this is three fog regimes meeting in
    concentric circles: unconverted Exp²-fogged terrain (pale center), the converted height-aware disc
    (clear dark ring), and the far-clip concealer (white wall at 450–600u slant). Added a small
    altitude-independent **distance-haze floor** to the shared term (`_AtmoDistanceHaze`, per-preset
    `distanceHaze 0.15`) so the clear zone grades into the concealer, and lowered the terrain's Exp²
    `FogDensity 0.0022 → 0.0015` (`ProjectFeatureConfig.asset`) so unconverted terrain stops washing pale
    from altitude — terrain only renders ≤180u from the player, where Exp² barely registers at ground
    level (~7% at the seam vs the disc's ~18%; judged acceptable until P3 converts terrain properly).
    Structural note: from 400u with a 600u far clip the visible world is inherently a ~450u circle — the
    full fix for the ring remains a larger far clip + the Phase-2 horizon ring, not fog tuning.
  - **Round-3 — far-field ground skirt (2026-07-05, owner idea: "mountains extend lower so the disc
    doesn't have to look so thick").** The skybox mountain band no longer cuts off below the horizon — it
    continues to the bottom of the sky as a **ground skirt** (distant land beyond the far clip), hazed
    along a ray clamped at the y=0 plane so it reads as land from altitude and as heavily-hazed horizon
    from the ground. The **disc now hides the far clip by alpha-fading out** (new
    `AtmoAerialHazeAmount` = shared term minus concealer) revealing the skirt behind it, instead of
    color-fogging to a white wall — the handoff matches what the sky draws at any camera altitude.
    Opaque consumers (hero relic) keep the concealer inside `ApplyAerialPerspective`. Validated via
    positioned altitude captures: the blank grey below-horizon expanse is gone, replaced by rolling land
    the ridge crests over; ground vista unchanged-or-better. Follow-up noted: **background relics**
    (Unlit gray + Exp² fog) now read as white blobs against the land-toned backdrop — consider a
    higher-strength `RelicLit` variant for them when P2/P3 visit the remaining consumers.
  - **Round-4 — zero-haze pin gates the skirt floor (2026-07-06, owner report: "toggles do nothing, still
    uniform haze").** Diagnosed via positioned altitude captures with the pin on and fog off: the full-screen
    pale wash from altitude was **the skirt's material-side `_MountainHazeFloor` (0.28)** lerping toward the
    near-white noon horizon color — from altitude the skirt fills the entire below-horizon view, so the
    "small" floor reads as uniform fog that no toggle governed. Fix: the sky shader gates the floor on the
    broadcast density (`saturate(_AtmoHazeDensity * 1e6)`), so `disableAtmosphereHaze` now zeroes the skirt
    floor too; normal look (pin off) is mathematically unchanged. A/B validated from 300u: pin on = clean
    olive land, pin off = hazed skirt. Two traps documented while diagnosing: `SkyController.EnsureMaterial`
    instantiates a **runtime copy** of the sky material at play start, so material-asset edits during play
    render nowhere; and `EnableDistanceFog` is applied once at bootstrap, so toggling it mid-play does
    nothing until play restarts. Also observed with haze fully off: `RelicHero`-material hands render
    near-white from altitude (overbright, not fog) — fold into the background-relic follow-up above.
  - **Round-5 — haze tuned down substantially (2026-07-06, owner call after seeing the pin-clean world;
    the skirt retired the fog's far-clip-concealer job, so the haze is now purely depth-cue/blend).**
    Final values after a second owner pass ("vertically thinner, gradient too gradual"): the layer went
    **thin-but-dense** — `hazeFalloff 1/60 → 0.1` (scale height 60u → **10u**: fog is a ground blanket,
    ~gone by 25u up), `hazeDensity` kept at `0.0012` (thin layers need density to stay visible at
    grazing angles), `distanceHaze 0.15 → 0.08`; sky material `_MountainHazeFloor 0.28 → 0.08`.
    Validated at ground (green holds deep, veil only at the horizon line, crisp relics) and from 40u
    (ground below fully clear, mist reads as a distinct thin band hugging the distant ground). Note the
    old 0.0012↔Exp²-seam anchoring is moot while the owner keeps `EnableDistanceFog: 0` (unconverted
    terrain ≤180u renders unfogged; no visible seam at these light densities).
  - **P2 built (2026-07-07, pending owner validation).** Disc `_GrassColor`/`_RockColor` material
    properties removed; the fragment samples the global `_AtmoGround`/`_AtmoRock` palette (no shade
    factor needed, unlike the sky band's `_MountainShade` — the disc's own in-frag lighting multiply
    already provides the shaded look). Noon output is identical by construction:
    `AtmosphereSettings.Default.groundColor/rockColor` were seeded equal to the old literals in P1.
    `SyncTerrainColor` + `_terrainColorSource` field deleted from `GroundPlaneImpostorBootstrap`
    (the scene's serialized reference becomes a benign orphan, dropped on next scene save); the dead
    `_HazeColor` property (unused since the MixFog→aerial conversion) removed too. Disc now follows
    biome-preset palette blends via the broadcast lerp. Spec §11.1/§11.5 disc acceptance met.
  - **Relic white-out root-caused (2026-07-07, two passes — the round-4 "hands near-white from
    altitude with haze off" observation).** First pass misdiagnosed it as unclamped additive lighting
    in `RelicLit.shader` (`ambient + full sun` > 1 on upward faces); a clamp + `_SunAttenuation` dial
    (default 0.5, disc convention) was applied — kept, it's correct — but owner screenshots showed
    hands still white. **Actual root cause: the far-clip concealer.** `ApplyAerialPerspective` added
    `AtmoFarClipHaze` at full strength, ignoring both the hero's `_AerialStrength 0.3` dial and the
    zero-haze pin (the pin zeroes `hazeDensity`/`distanceHaze`; the concealer is pure distance vs
    `_AtmoFarFade 600`). From altitude the whole visible world sits at 450–600u slant distance —
    inside the concealer band — so hero hands saturated to horizon-white regardless of any dial.
    (Scene-view corroboration: hands beyond 600u render at t=1 solid horizon color since the scene
    camera's far clip exceeds `_AtmoFarFade`; nearby `Unlit gray` relics stayed dark because built-in
    fog is off.) Fix: the concealer is now scaled by the surface's `strength` — hero caps at ~30%
    veil at the far plane. Trade-off accepted: reduced-strength surfaces pop in less veiled at the
    far clip; the real fix remains a larger far clip + Phase-2 horizon ring. `RelicLit` is the only
    `ApplyAerialPerspective` consumer (disc uses the alpha-fade path), so no other surface changes.
  - **Relic fix validated mechanically via MCP A/B (2026-07-07):** `_AerialStrength 0` → hands render
    proper warm-gray (shader live, lighting clamp works); `0.3` → near-white *in the scene view only*,
    because the scene camera renders hands **beyond the 600u game far clip** where the haze terms
    saturate — those hands never render in game. Game-view judgment at the design distance (250–400u)
    still an owner-eyeball item; if too white there, drop `_AerialStrength` (no code change).
  - **P3 built (2026-07-08, pending EditMode run + owner validation) — re-scoped from "terrain tint
    (Option B)" on a build-time finding (spec §6a).** The terrain↔disc seam had become the most
    visible artifact (terrain: Synty albedo, no haze; disc: `_AtmoGround` palette + aerial haze).
    Traced before coding: Surface Nets meshes upload **position-only** vertex buffers — no UVs, no
    tangents — so the Synty `Generic_Basic` graph sampled its albedo at one texel and the terrain
    has rendered as a single flat color all along (normal map equally inert). There was no texture
    to tint; Option B degenerated to direct palette consumption. Built: new `Terrain/TerrainLit`
    shader (RelicLit pattern — DOTS-instanced for Entities Graphics, ForwardLit + ShadowCaster +
    DepthOnly, Lambert+SH+shadows with the clamped-sun convention) coloring by the **same world-XZ
    FBM grass/rock mix as the disc**, factored into shared `GroundNoise.hlsl` so seam patches align
    by construction (world-space noise is continuous across the seam — patches flow from terrain
    onto the disc); haze via `ApplyAerialPerspective` (its first consumer, full opaque-world
    contract). `TerrainLit.mat` swapped into `TerrainChunkRenderSettings.asset`; `Generic_Grass.mat`
    orphaned. Net compute *drop* (zero texture fetches, opaque, no alpha-test/A2M) and closer to the
    documented art direction (flat palette cells per the biome spec's ground-materials MVP
    approximation — its RGB table is the palette tuning target). New
    `TerrainChunkMaterialContractTests` guard the material wiring + noise-dial parity. Owner note:
    grass-blade color iteration deferred (owner, 2026-07-08); optional 16-bit color quantization
    noted in spec §12 as an aesthetic dial.
  - **P3 round-2 — "square hole in fog" from the drop fixed (2026-07-08, owner screenshot; spec
    §6b).** Looking down from the drop, the streamed terrain window read as a crisp square inside a
    pale wash with a white rim. Two stacked causes, both slant-distance vs `_AtmoFarFade`: the disc's
    skirt handoff began at ~206u horizontal from 400u altitude, and the window's corners (~474u
    slant) whitened inside the old 450–600u `AtmoFarClipHaze` band — a concealer that, post-R6
    (far plane 2000), concealed no actual clip. Fix: `AtmoFarClipHaze` + `ApplyAerialPerspective`
    deleted; new `AtmoWorldEdgeHaze` measures the disc→skirt handoff **horizontally** (world edge =
    600u circle around the player, not a shell around the camera; ground level unchanged by
    construction since horizontal ≈ slant at eye height); disc's real clip edge covered by
    `AtmoLandmarkEdgeFade` (dormant behind the 900–1400u outer fade); terrain uses the plain shared
    haze with no edge term. Zero cost delta. Validated same session: positioned top-down capture at
    400u shows no square / no white band / soft circular world-edge fade
    (`Assets/Screenshots/v9p3_fix_topdown_400u.png`), ground vista matches the pre-fix capture
    (`v9p3_fix_ground_vista.png`); note positioned MCP captures DO render BRG geometry on Unity
    6000.4 (old constraint memory outdated). Residual, accepted: scatter/pebble detail cutoff at the
    window edge against same-hue disc.
  - **P2 + P3 ground-level halves eyeballed OK (2026-07-15, owner in-game vista screenshots):**
    noon vista reads unified after the disc palette swap (plain, haze, horizon one hue band) and no
    terrain↔disc seam is visible at ground level. Same session verified the **unpinned day/night
    cycle live** (raw time advancing + sun/sky tracking it — see the T2 note below).
  - **Remaining (ticket stays open):** P3 drop-altitude seam check (scatter/grass tufts are the only
    unconverted surfaces — watch for them popping against the palette-hazed terrain), P5
    saturation/overcast pass; full day/night sweep still an owner-eyeball item.

#### V11 — Hero hand mesh authoring _(opened 2026-07-05 — spun off V4)_
`testAlienHand.fbx` renders fine but reads as a boulder: short, fused finger-lobes with no silhouette
separation. Author (or re-pose in Blender) a hero hand whose four fingers read at 200–400u *through haze* —
silhouette-first: long separated fingers, a readable gesture (reference `Docs/Temp_OpeningInspiration.png`,
`MVP_VISTA_MOMENT_SPEC.md` §2.1). Keep the current asset as placeholder until swap. Source in
`BlenderSource/`, export to `Assets/Models/` per the B-ticket conventions. Retune `scale`/`yOffset` on the
new mesh (current 500/−5 ≈ 40–50u tall; colossus presence likely wants 80–150u) — judge via screenshots.
- **Constraint from W2 (backlog, 2026-07-09):** the Blender master rig (segmented boxes + transforms) is
  the source of truth for a possible future SDF-destructible version — keep the rig's segment transforms
  exportable; bakes (smooth/variant meshes) are derived artifacts, never the master.
  _Amended 2026-07-12: the proportion pass edited the bake cage directly — the **mesh is now canon**,
  the rig approximate (see the W2 rig-desync caveat and the A9 re-fit note)._
- **Built + swapped in (2026-07-11):** hero mesh authored (hex low-poly cage → Subdivision Surface,
  sculpted joints/creases, wrist stub — see `ArtSource/ColossalHand.blend`), baked at subdiv level 1
  (1,376 verts / 2,748 tris) and exported to `Assets/Models/ColossalHand/ColossalHand_Pristine.fbx`.
  `relic_hand_hero` template now uses it: scale 10, yOffset 2; authored anchor yaw 90. Pose is baked at
  export time (−20° pitch, wrist descending underground, palm tilted toward spawn per
  `Temp_OpeningInspiration.png`) — the Blender master stays unpitched (A9 reuse unaffected). Export
  gotchas recorded: keep the FBX node name `ColossalHand_Pristine` (scene mesh reference is a
  name-hashed fileID) and export with `bake_space_transform` (relic rendering uses the raw Mesh asset,
  so the Y-up conversion must live in the vertices). **Open: owner eyeball** (walk-toward, drop view);
  weathered/ruined variants split to **V18**.
- **Proportion pass + palm line (2026-07-12):** owner re-proportioned the hand to near-human
  (middle/palm 0.88 vs human 0.75–0.8; webs/knuckle line moved outward, palm shortened at heel,
  broad palm kept as style) with measurement support from `ArtSource/hand_proportions.py`; one palm
  crease added as a beveled groove strip. Re-exported same-name FBX (subdiv 1, −20° pitch, 3,072 tris,
  watertight). Side effect: master rig now approximate (see amended W2 constraint above).
- **Owner eyeball PASSED — ticket closed (2026-07-15, in-game screenshots vs
  `Docs/Temp_OpeningInspiration.png`):** from spawn, four separated fingers + thumb read
  unmistakably as a hand through the haze (the exact failure that spun this ticket off V4 is gone);
  up close the hand reads as dark stone against the pale sky — the inspiration's value contrast.
  Walk-toward also clean (no size pop — that check closed V16 the same day). The drop-view "hero
  hangs mid-air" item lives with **R6 P4** (already scoped there 2026-07-11), not here. Cage
  cleanup note: a light-touch gridify pass (2026-07-15) removed a wire edge, a wrist-ring notch,
  and merged palm triangle fans — zero silhouette change (verified vertex-identical); the wired
  FBX (`ColossalHand_Pristine.fbx`, exported 2026-07-14) predates it, harmless by construction.

#### V12 — Authored anchor candidate source (guaranteed hero hand) _(opened 2026-07-05 — spun off V4)_
Relic placement is seed-deterministic but not *authorable*: anchors hash-jitter across ±1024u around origin,
so a hand in view of spawn is seed-luck (the 2026-07-05 seed was lucky: two relics ~250–300u out). Add an
**authored anchor** candidate source to the structure-placement pipeline — explicit position + template
(+ optional constraints), merged with / overriding the procedural planner. One mechanism, three consumers:
the guaranteed vista hero hand, quest placement later, and "spawn a known layout" debugging. Fits the
pipeline's existing candidate-source shape — the W1 magic grid is specced as exactly such a variant
(`Structures/MAGIC_GRID_SPEC.md`, `STRUCTURE_PLACEMENT_SPEC.md`). Per the owner decision (2026-07-05,
dev-pins discussion): design it so quests inherit it for free; this is a product feature, **not** a dev pin.
Also feeds the far-field: authored anchors are the data source for hero relic silhouette cards in the
Phase 2 horizon ring (**R5**, `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` §19) — only authored heroes are
far-visible by design.
- **Design settled (2026-07-08, owner discussion) — specced as `STRUCTURE_PLACEMENT_SPEC.md` §9.5.**
  Key decisions: (1) **merge = evaluation order** — locked/modified → authored → procedural; procedural
  candidates spacing-reject against authored anchors via the *existing* tie-break, no new conflict
  resolver. (2) **Data home = scene bootstrap** (`AuthoredAnchorBootstrap`, serialized inspector entries →
  singleton `AuthoredAnchorInput` buffer), after an SO-vs-code-vs-bootstrap discussion: owner dislikes
  ScriptableObject editing ergonomics; bootstrap gives no-recompile inspector tweaking in the hierarchy
  and matches the `RelicVisualBootstrap` → `RelicRenderConfig` pattern. (3) **Identity is seed- and
  position-independent** — `StableAnchorId` = FNV-1a of the `AuthorId` string on its own lane; the same
  authored layout exists in every world, and nudging a position never orphans persistence/quest refs.
  (4) Authored anchors **bypass hard constraints** (guaranteed placement is the feature) — violations log
  warnings; Y snaps to the terrain SDF by default. (5) `StructurePlacementSource.Authored = 3`; template
  is explicit per entry (planner's template assignment skips authored). (6) Debug layouts = second entry
  list on the bootstrap behind new `DebugSettings.EnableAuthoredDebugAnchors`. (7) R5 reads authored
  anchors from the planned buffer (`Source == Authored`), not the authoring component. (8) Hero gets a
  **distinct `relic_hand_hero` template** so scale/material tune independently of background hands.
- **Built (2026-07-08, code complete — compiles clean; EditMode run + scene wiring pending MCP session).**
  New: `AuthoredAnchorInput` (buffer element), `AuthoredAnchorBootstrap` (validating conversion incl.
  duplicate/oversize AuthorId guards; default world entry = `vista_hero_hand` @ (0, 900) +Z, yaw 180),
  `AuthoredAnchorId` hash + authored injection pass in `StructureAnchorPlanningAlgorithm` (old
  `GenerateAnchors` signature kept as delegating overload), authored-source consumption + constraint
  warning pass in `StructureAnchorPlanningSystem`, 7 EditMode tests (`AuthoredAnchorPlanningTests`).
  ~~Remaining: run EditMode suite; scene wiring.~~ **Done + validated (2026-07-08, same session):**
  EditMode suite green (owner-run). Scene wired: `AuthoredAnchorBootstrap` GameObject (world entry =
  `vista_hero_hand` @ (0, 900), yaw 180) + `relic_hand_hero` template on `RelicVisualBootstrap`
  (testAlienHand mesh, `RelicHero.mat`, scale 1200, yOffset −12); scene saved. Play Mode verified via
  Editor.log + positioned MCP captures: hero plans as `Anchor[0] pos=(0, 4, 900)` (authored injected
  first, terrain-snapped Y), all 96 relic anchors realize (template resolved), zero exceptions/warnings.
  Screenshots: `Assets/Screenshots/v12_hero_hand_from_spawn.png`, `v12_hero_hand_close.png`.
  Hardened during validation: a duplicated bootstrap component (MCP tooling stray) created two singleton
  buffers and made the planner's singleton query throw per-frame — bootstrap now reuses an existing
  buffer with a warning instead of creating a second entity. **Open:** owner eyeball from spawn
  (distance/yaw are bootstrap inspector knobs; scale/yOffset on the `relic_hand_hero` template — no
  recompile for any of them). Known caveat: at hero scale the testAlienHand mesh reads as a flat-topped
  boulder — that's **V11** (mesh authoring), not a placement issue.
- **Procedural relics made rare (2026-07-08, owner call after seeing the guaranteed hero):** with the
  hero authored, background relics no longer carry the vista. `Relic.asset` retuned `MinSpacing 80 → 900`
  (> the 600u background-relic fade, so the view disc rarely holds two), `CandidatesPerCell 2 → 1`,
  `MaxSpacing 300 → 1800`. Result: 96 → **6** relics in the ±1024u region (hero + 5 procedural, verified
  in Play Mode; all realize). The hero's own 900u spacing exclusion also clears the spawn vista of
  competitors by construction. Not needed for testing: EditMode tests construct rule data in code, and
  known-layout debugging now uses the authored debug list. Screenshot:
  `Assets/Screenshots/v12_rare_relics_vista.png` — one lone colossus on the horizon, the spec's
  composition. Rarity rationale + deferred cadence decisions (fixed-region caveat, revisit trigger,
  MaxSpacing note) documented in `STRUCTURE_PLACEMENT_SPEC.md` §9.6.
- **From-spawn eyeball PASSED — ticket fully closed (2026-07-15):** owner in-game screenshot from
  spawn shows the lone colossus on the horizon at (0, 900) — the spec's composition. Distance/yaw
  kept as authored; no knob changes needed.

#### V13 — Burning-descent VFX (meteor entry) _(opened 2026-07-08 — owner idea, discussed + specced same day)_
**Spec:** `Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md` (Phases 3–4; shared break-open contract with V14).
First-person flame/ember layer for the sky-drop descent: screen-edge flame tongues streaming opposite
velocity, embers past the camera, optional heat wobble/warm vignette + roar audio. **Ignites on V14's
break-open signal, burns off before landing** (lean: altitude band), handing off to C3's dust burst.
Key decision (owner, 2026-07-08): the meteor is a property of the **arrival sequence, never of
altitude/fall speed** — future high-altitude mechanics fall with C2's wind/speed-line vocabulary, no
flames; promote to an "atmospheric entry" state only if a mechanic ever warrants it. Rides the same
camera-rig mount C2 plans (build emitters shareable; don't block on C2). Stretch: dev-toggle smoke
trail. Perf: camera-parented particles at modest counts — near-camera fill on a vertex-bound scene.
Bonus (not the purpose): masks the residual streamed-window detail cutoff from the drop
(`ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` §6b).

#### V14 — Meteor-interior loading shell (diegetic initial load) _(opened 2026-07-08 — owner idea, discussed + specced same day)_
**Spec:** `Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md` (Phases 1–2; the higher-value slice).
The game has **no loading UI**, but the substance of one already exists: the V7 sky-drop readiness
gate holds the player at ~400u while near-spawn colliders build (8s timeout fallback) — currently in
full view of the worst pop-in the game has. V14 gives that gate a diegetic face: the player starts
**inside the meteor** (full-screen rocky interior + glowing cracks + rumble; one Canvas/quad + one
controller MonoBehaviour — the project's first UI element), and the shell **breaks open when the gate
actually releases** (no fake progress bar — binary for MVP, min-hold ~1.5–2s so a fast load doesn't
flash-open; progress-driven crack glow deferred as polish). Needs a small DOTS→managed **gate-state
bridge** (singleton component the overlay polls — established pattern). Gravity release follows the
shell opening (§5.1 of the spec; one coupled frame acceptable for MVP if decoupling is invasive).
Scope: **initial spawn only** (respawn/fast-travel reuse deferred). Acceptance: player never sees the
world assembling; shell timing tracks real readiness; break-open + V13 ignition read as one beat.

#### V15 — Sky mountain band: rugged silhouette + horizon line + snow _(opened 2026-07-09 — owner observation, discussed + built same day)_
**Spec:** `Rendering/SKY_MOUNTAIN_BAND_SPEC.md` (ACTIVE — full design + dial table).
Owner called out two reads: the mountains "just look like a sine curve" (they literally were — three
sine harmonics) and "blend a little too seamlessly with the terrain" (same ground palette as the
plain). Fix, all inside `ProceduralGradientSky.shader`'s mountain block: **(1)** ridged periodic FBM
silhouette (sharp crests/V-valleys, wraps seamlessly at ±π via integer-lattice `fmod` hashing);
**(2)** second back ridge — taller base, farther fictive haze distance, lighter + horizon-shifted hue
(nearest-darkest depth stacking); **(3)** horizon demarcation line at `viewDir.y = 0` — above it the
range goes darker + toward `_AtmoHorizon` (`_MountainRangeShade`/`_MountainHueShift`), below it the
far-field ground **skirt keeps the ground palette** (load-bearing: the disc alpha-fades into it —
recolor it and the far-clip edge returns); **(4)** snow caps — built, **off by default**
(`_SnowOpacity 0`; round 3 owner call: keep as a toggle, may come in useful, not a priority). All
palette-derived per V9
authority; ~4 octaves of 1D noise per sky pixel — no new assets/draws. Owner's serialized .mat tuning
(variation 0.1442 etc.) carries over; new dials take shader defaults. Future: silhouette *source*
superseded by the Phase-2 seed-driven horizon ring (spec §7) — a reachable mountain biome plugs in
there, post-MVP. **Round 2 (owner feedback on first screenshot, 2026-07-09):** back ridge inverted —
v1 was broader + much taller than the front (backwards from perspective); now finer (1.6× frequency,
smaller variation, base only slightly above front) with the color separation softened (0.35/0.25) so
the ranges read as siblings one atmospheric step apart. Ground-level look owner-approved round 2
("that's great"); snow toggled off round 3. **Remaining eyeball: drop-altitude skirt check**
(below-line ground palette unchanged from ~400u).

#### V16 — Relic size pop-in — LOD made dormant-by-design _(opened 2026-07-09 — owner screenshots during the V15 walk; discussed + built same day)_
**Spec note:** dormancy note at the top of `Structures/RELIC_LOD_IMPOSTOR_SPEC.md`.
Owner saw the hand render small on the horizon, then pop much larger a few seconds into the approach.
Diagnosis: the LOD swap (`RelicLodSelectionSystem`) changes the entity's *world scale* between
`FullScale` and a fixed-target-size `ImpostorScale` — and since billboard impostor art was never
authored, "LOD 1" was the **same mesh** rescaled smaller: zero vertices saved, pure size pop. The
system's original conditions are gone anyway (V12 cut relics ~96 → ~6; scene is vertex-bound by
trees/rocks; far-distance handled by R6 ≤2000u + R5 cards >2000u). Owner question "what's the point
of the LOD system if we don't use it?" → answer: none today, but keep the tested machinery dormant
(per the keep-as-toggle principle) for real billboard art / a heavy V11 hero mesh. Fix:
`RelicRealizationSystem.TemplateParticipatesInLod` — no authored `ImpostorMesh` → single render
entry, no `RelicLodParams`/`RelicLodState`, LOD query matches nothing; loud dormancy comments at the
skip site + system doc; EditMode test `TemplateParticipatesInLod_RequiresAuthoredImpostorMesh` guards
the decision (its failure after authoring impostor art = the feature waking up, not a regression).
**Walk-toward check PASSED — ticket closed (2026-07-15):** owner approached the hero hand from
spawn (~900u) to close range — no size change at any distance.

#### V17 — Mid-field disc variation (kill the uniform band) _(opened 2026-07-09 — owner screenshot, discussed same day)_
**Spec:** `Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md` §12 (full root-cause analysis, dials, acceptance).
Owner: the impostor band between the streamed terrain window (~180u) and the sky mountain band reads
too uniform. Diagnosed from the shader: (1) the 250u binary grass/rock patches foreshorten to nothing
at grazing angles, (2) the flat +Y plane has one normal → one constant lighting value across the whole
band, (3) scatter density drops to zero at the chunk radius. The meteor arrival (V13/V14) only masks
the descent — the vista beat is steady-state viewing, so this needs a real fix.
- **P1 — macro luminance variation** in shared `GroundNoise.hlsl` (~1000–2000u FBM octave, ±~10%
  multiply): applies to terrain + disc identically, so the seam stays aligned by construction. New
  dials join the `TerrainChunkMaterialContractTests` parity guard.
- **P2 — fake relief shading** (disc shader only): pseudo-normal from finite-differenced low-freq
  height FBM replaces the flat +Y in the Lambert term — rolling ground without geometry.
- **P3 — vertex undulation** (optional, judge after P1+P2): real rolling silhouette against the sky
  band; **must** damp to zero inside ~250u of the player so streamed-chunk replacement stays hidden
  (traversal-era liability documented in the spec).
- **Dynamic-proof by design:** P1 multiplies the already-dynamic `_AtmoGround`/`_AtmoRock` palette;
  P2 lights against the live `GetMainLight()` sun, so relief pops at low sun angles automatically
  once the time-of-day pin comes off. This is the permanent implementation, not an MVP hack.
- **Out of scope** (spec §12.4): distant-scatter speckling (superseded by R1/R5), cloud shadows +
  weather tints (weather track — must cover real terrain too, not just the disc).
- **Sequenced (owner decision 2026-07-09) — Build-order step 3:** after the V9 P3 owner eyeball
  (V17 modifies both sides of the terrain↔disc seam that check judges), before V9 P5 (saturation
  is a one-shot global grade; P1 changes the luminance it grades). P3 undulation judged after
  V15's drop-altitude skirt check (same disc→sky-band handoff). Independent of steps 1–2 (R6/V11).
- **P1+P2 BUILT 2026-07-16** (V9 P3 seam eyeball passed 2026-07-15, unblocking this): macro
  luminance octave inside the shared `GroundPaletteMix` (both surfaces, seam-aligned by
  construction; defaults 0.0007 scale ≈ 1400u, ±8%), relief pseudo-normal via `GroundReliefNormal`
  consumed only by the disc shader (defaults 0.002 scale ≈ 500u, strength 0.35; strength 0 =
  exactly the old flat-plane term). Macro dials added to the `TerrainChunkMaterialContractTests`
  parity guard. Remaining: in-editor compile/EditMode pass (editor was locked during the build
  session), owner eyeball per §12.5 (noon vista, seam walk, drop-altitude texture read, sun-angle
  relief spot-check), then V9 P5. P3 undulation still unjudged.

#### V18 — Hero hand weathered/ruined variants _(opened 2026-07-11 — owner ask during the V11 swap session)_
Variants of the V11 hand for background/procedural hands and future set-dressing, honoring the fiction
(relics are **ossified remains of a dead god** — breaks are uneven fractures mid-segment with rubble,
never clean segment removals). **Built in Blender, not Unity:** fracture cuts on the low-poly cage
*before* the subdivision bake (cut + jagged poked cap + rubble ring at the break), so caps inherit the
same subsurf smoothing + displacement noise as intact bone; export each variant as its own FBX
(`ColossalHand_Weathered/_Ruined.fbx` — current files are stale box-era placeholders to overwrite,
same node-name/fileID rule as V11). Unity side is data only: template entries per variant so the
procedural `relic_hand` template can rotate them — per-anchor deterministic variant pick can ride the
existing anchor hash (positions + yaw already hash-vary; only the authored hero is pinned by design).
Optional polish: vertex-color AO/weathering bake + one-line `RelicLit` multiply (cheap, agreed earlier).
Runtime damage is explicitly **not** this ticket — that's W2 (SDF stamp up close).

#### R6 — Landmark draw distance — relics never cull _(opened 2026-07-06, spec written; pulled from backlog 2026-07-07 as Build-order step 1)_
**Spec:** `Rendering/LANDMARK_DRAW_DISTANCE_SPEC.md` (ACTIVE — full design, slices P1–P4, acceptance).
The V9 round-5 thin haze removed the fog wall that used to hide hero relics popping at the 600u far clip.
Fix = the industry "landmarks never cull" pattern: raise the camera far plane to a new
`ProjectFeatureConfig.LandmarkDrawDistance` (~2000u) while the *world* stays short — terrain/scatter only
exist ≤180u anyway, so the far plane wasn't protecting perf. Prerequisite (P2, must land with/before P1):
decouple `_AtmoFarFade` from `Camera.main.farClipPlane` (broadcast the config world distance instead) or
raising the plane silently stretches the disc→skirt handoff to 1500u+. Hero material drops the far-clip
concealer (`AtmoAerialHazeAmount`, like the disc) and gains a dithered edge fade at the landmark distance;
realization gains a ~0.5s spawn dither fade (all relics). Hero templates only — background relics keep the
world distance.
- **Why it leads the Build order (2026-07-07):** the vista's premise — hand much larger, much further
  away, always visible — is impossible under the 600u clip (geometry past it doesn't render at all,
  a fact re-confirmed during the P2/relic validation session). R6 P3 also **supersedes the 2026-07-07
  interim patch** that scales the far-clip concealer by `strength` in `Atmosphere.hlsl` — heroes stop
  taking the concealer entirely.
- **Feeds:** R5 (narrows its card contract to >2000u). **Touches:** V9 HLSL contract, camera bootstraps,
  `TimeOfDayController.PushAtmosphere`, `RelicLit.shader`.
- **P2 → P1 → P3 built (2026-07-07, three commits in rollout order; spec flipped ACTIVE — see its §9
  build record for implementation notes).** `_AtmoFarFade` now sources
  `AtmosphereBroadcast.WorldReferenceDistance` (config-seeded, 600) instead of the camera plane;
  `LandmarkDrawDistance` (default 2000, 0 = off) raises the camera far plane via
  `DerivedLandmarkFarClip` through the config singleton; `RelicLit` drops the concealer for
  `AtmoAerialHazeAmount` + an IGN dither dissolve at the new `_AtmoLandmarkFade` global (ForwardLit +
  DepthOnly clipped in sync) — the interim strength-scaled-concealer patch is reverted/superseded.
  EditMode 221/221 green. **Open:** P4 spawn fade; owner-eyeball validation — landmark permanence
  (walk toward/away from a 1500u hero), P2 ground vista unchanged, background-relic exposure at
  600–1024u (accepted-for-eyeball side effect), plus the V9 carry-overs (noon vista after the disc
  palette swap, hero hands legibility at 250–400u in game view).
- **Partial validation (2026-07-15, owner in-game screenshots + walk-toward):** the hero at 900u —
  past the old 600u clip — renders and persists in normal play with no cull or pop on approach
  (the same walk closed V16); ground vista unchanged; hero legible in game view (V9 carry-overs
  covered). Still open: P4 spawn fade (incl. the drop-altitude hero-hang scope from 2026-07-11),
  permanence at 1500u+ (no authored hero that far yet), background-relic exposure eyeball.
- **P4 scope addition (owner drop screenshot, 2026-07-11):** during the V7 sky-drop the hero hand
  renders suspended against the haze — at drop altitude neither the terrain window nor the disc reads
  as ground under it, so the whole hand hangs mid-air until the player descends. P4's spawn fade
  should therefore be **readiness/altitude-aware for the authored hero** (hold or fade it in until
  the drop's readiness gate releases / the camera is below the band where the skirt reads as ground),
  not just a realization dither. Related viewing geometry: V13's flame layer masks part of the
  descent; V15's remaining drop-altitude skirt check.

---

### Dev Tooling _(pulled into current focus 2026-07-05)_

#### T2 — Dev determinism pins (shared convention + time-of-day pin)
**Decision (2026-07-05):** dynamic/randomized systems get a **pin** in their existing config, added
case-by-case when a system first causes debugging pain — no central dev-mode framework. Convention recorded
in `CLAUDE.md` § Dev Determinism Pins. Precedent: `DebugSettings.UseFixedWFCSeed`. Relic placement needs no
pin (already seed-deterministic); authored placement is product work → **V12**.
- **Time-of-day pin (built 2026-07-05):** `TimeOfDayController.pinTimeOfDay` + `pinnedNormalizedTime`
  (default `0.08` = midday raw cycle time, matching the scene's default start). Motivation: the scene's 240 s
  day cycle drifts midday→night inside a single debugging/screenshot session, which invalidated visual
  comparisons during the 2026-07-05 V4/V8 validation.
- **Enabled in `Basic Terrain Scene`** so visual validation always runs at midday; un-tick the inspector box
  for day/night-cycle work.
- **Verified (2026-07-05):** with the pin on, lighting is identical midday ~2.5 min into Play Mode (the
  unpinned 240 s cycle was full night by then); clouds still animate. Clean console. Done.
- **Unpin path re-verified live (2026-07-15):** owner report "unchecked the pin but the cycle stays
  fixed" diagnosed via MCP — the cycle was actually running (raw time sampled advancing 0.96 → wrap →
  0.52; sun intensity tracking). Two perception traps recorded: **(1) unchecking the pin during Play
  Mode reverts on exit** — the scene serializes `pinTimeOfDay: 1`, so every fresh play session
  re-pins unless the box is unchecked in Edit Mode and the scene saved; **(2)** the remap curve gives
  the overcast Cloudbreak day ~100 s of near-identical grey — "fixed" is easy to misread mid-day
  (dawn/dusk are the visible ~19 s windows). Unfocused-editor throttling compounds both.

---

### Camera Feel _(secondary — slingshot feel toward the relic; was sprint lead pre-2026-06-29)_

> **FPS adaptation:** these tickets were specced against the third-person orbit camera, so the **distance
> dolly/pullback** terms (`TargetDistance`, `BallisticDistanceAdd`) are third-person concepts. In first-person
> the camera is head-locked, so reinterpret those as no-ops; the **FOV punch/narrow, shake, dip, camera-local
> drop, speed lines, and dust burst** all carry over to FPS as-is and are where the feel actually comes from.

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

#### Dev-toggle / deferred (third-person body) _(not MVP-gating — body hidden in first-person play)_

#### A9 — First-person arms viewmodel (the real fix for FPS-only MVP) _(deferred — follows the vista)_
With MVP reversed to first-person only (2026-06-20), the full third-person body is hidden in play
(`PlayerFirstPersonVisibility`) and the A1–A8 clips are invisible except via the dev V-key toggle. The
proper FPS feedback for charge/launch/glide is a dedicated **arms viewmodel**: a first-person arms rig with
FPS-authored clips, shown only in first-person.

The groundwork is already done and forward-compatible — `PlayerFirstPersonVisibility` hides the body in
first-person, so this ticket only adds the arms rig and shows it in the same place (no rework of the body-hide
or camera-mode plumbing).

- **Arms source (decided 2026-07-09, owner):** the V11 colossal-hand master rig + mesh generator
  (`BlenderSource/` hand work-set) is the intended source for the FP hands — same DNA as the vista
  colossus (fiction hook; dovetails with P2 Magic Hand). Duplicate the master, re-pose from the agony
  claw to a relaxed FP pose, re-run the generator at player scale (own FBX, ~0.19m hand — never the
  hero export). Still A9-scope on top of that: convert the segment parent-chain to an armature, add a
  forearm stub (master ends at the wrist), and retune the surface (kill/reduce the 0.35 SegNoise
  displace — megalith noise reads as gravel skin at viewmodel distance; stone-vs-skin material is a
  fiction call).
- **Refined (2026-07-10, owner insight during V11 finishing):** the V11 hand was heavily hand-sculpted
  past what the generator reproduces, so the reuse path is now **rig the baked V11 mesh directly**,
  not regenerate: build the armature from the master-rig segment transforms (they still track every
  joint origin/axis), skin rigidly per segment (correct for the faceted style), and get the relaxed
  FP pose by **rotating bones, never editing verts** — the agony claw stays the rest pose for the
  vista export, both poses live on one asset. Caveats recorded: inner-joint crease welds open into
  crease lines when straightened (reads as anatomy; per-joint two-vert fix if ugly), and poses far
  outside the modeled range (hyperextension) will look worse than near-pose variants. "Apply straight
  pose as rest" is a one-button decision deferred to A9 start. V11 itself ships un-rigged.
- **Rig desync (2026-07-12, V11 proportion pass):** the mesh was re-proportioned to near-human directly
  on the cage (webs/knuckle line moved outward, palm shortened at the heel; middle/palm now 0.88) —
  master-rig segment transforms no longer match the mesh (digit lengths ~40% off, palm box stale, thumb
  hand-rebuilt earlier). "Build the armature from the master-rig transforms" therefore needs a
  **re-fit pass at A9 start**: snap joint heads/tails to the mesh's actual joint creases (the 0.85-crease
  rings mark every fold) rather than trusting rig positions. The mesh is canon; the rig is approximate.
  Proportion tool: `ArtSource/hand_proportions.py` (geodesic digit arcs + palm auto-detect).
- **Rigging phase kickoff (2026-07-12, owner):** A9's asset phase starts now (Unity-side viewmodel
  integration below stays scoped for later). Agreed sequence: (1) **armature first**, built in the
  master `ArtSource/ColossalHand.blend` so both poses live on one asset — per the rig-desync note
  above, fit joint heads/tails to the mesh's actual crease rings, not the stale master-rig transforms;
  skin rigidly per segment; include the wrist/forearm root bone now (cheap, avoids a re-skin when the
  forearm stub is modeled). (2) **Relax the grip by rotating bones only** — never edit verts; the
  agony claw stays the rest pose so the V11 hero export is untouched; expect inner-joint crease welds
  to open when straightened (per-joint two-vert fix if ugly). (3) **Owner tweak pass** on the relaxed
  pose (crease lines, surface/material read at viewmodel distance). FP hand exports as its **own FBX
  at player scale (~0.19m)** — never overwrite `ColossalHand_Pristine.fbx` (name-hashed fileID).
  Deferred until the pose reads in Unity: apply-relaxed-as-rest (the one-button decision above) and
  mirroring for the second hand.
- **Armature built + rigidly skinned (2026-07-12):** `ColossalHand_Armature` in the master blend —
  17 bones (`Wrist → Palm` + 4×3 finger chains + `Thumb_Thenar → F_Thumb_S1/S2`), joints fitted to the
  cage's 0.85-crease rings via geodesic digit labeling (thumb has no crease rings — joints placed from
  its centerline; expect owner nudges there). Every vert weighted 1.0 to exactly one bone; armature
  modifier first in the `ColossalHand_LowPolyHex` stack (before Subdivision). Pose convention:
  **local +X extends, −X curls** (verified numerically on fingertip travel). Joint pivots are safe to
  move in armature edit mode — rest pose deformation is identity, so no re-skin needed.
- **Relaxed grip posed (2026-07-12):** stored as Action **`FP_RelaxedGrip`** (fake-user on; rest pose
  untouched = agony claw). Owner refined joint pivots in edit mode first (rolls re-fit afterward — edit-mode
  bone moves invalidate curl-axis rolls; re-fit before posing numerically). Angles solved per joint against
  measured rest flexion: knuckles ~15–22° flex (agony knuckles were nearly straight — the claw is all
  PIP/DIP hook), PIP 25–35°, DIP 12–18°, index→pinky cascade; thumb ~27°/15°. Unlink the action in the
  Action Editor to see the agony rest; owner tweak pass on this pose is the remaining asset step.
- **Single master file + eventual rest flip (2026-07-12, owner):** rig lives in the master
  `ArtSource/ColossalHand.blend` — one mesh, one armature, two poses (Actions), two export selections —
  so sculpt edits propagate to both the colossus and the FP hand. Hero export becomes mesh-only
  "export selected" (or armature modifier disabled) so the armature node never enters the
  name-hashed-fileID hierarchy. Forearm stub, when modeled, is a separate object included only in the
  FP export selection. **Rest flip EXECUTED (2026-07-12, owner call):** rest pose = owner-tweaked relaxed grip; the claw
  lives in Action **`AgonyClaw`** (fake-user on; per-bone inverse deltas — verified lossless, fingertip
  error 0.0000). Pre-flip backup: `ArtSource/ColossalHand_preflip_2026-07-12.blend`. `FP_RelaxedGrip`
  action now = all-zero neutral snapshot (relaxed is rest). **Consequence: any V11 hero re-export must
  assign `AgonyClaw` first** — exporting at rest now gives the relaxed FP hand, not the vista claw.
- Author/acquire the remaining clips on that rig: slingshot charge pull, launch/release, glide arms-spread, idle/move bob.
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
