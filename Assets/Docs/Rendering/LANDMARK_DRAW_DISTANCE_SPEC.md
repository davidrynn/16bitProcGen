# Landmark Draw Distance Spec

**Status:** ACTIVE — P2/P1/P3 built 2026-07-07; **P4 spawn fade built 2026-07-16** (fade math
EditMode-tested, BRG plumbing play-verified; the 0.5s dissolve itself is a visual — eyeball on
next relic-streaming session). Remaining validation: permanence at 1500u+, drop-altitude hero hang.
**Last Updated:** 2026-07-07
**Owner:** Rendering / Vista
**Phase:** Post-V9-tuning follow-up (ticket R6; bridges to R5)
**Keywords:** pop-in, far clip, draw distance, landmark, hero relic, layer cull distances, dither fade,
aerial perspective, far fade, vista

---

## 1. Purpose

Hero-scale relics currently pop in and out at the camera far clip (~600u). The old concealment was a
thick fog wall; the V9 round-5 haze re-tune (owner call, 2026-07-06 — thin 10u-scale-height ground
layer, see `../Tickets/vista-moment.md`) deliberately removed that wall, so the pop is now naked.

This spec adopts the industry-standard answer for landmark-scale objects — **landmarks never cull** —
by raising the camera far plane while keeping the *world's* effective draw distance where it is, plus
a dither fade for the two remaining pop moments (hero draw-distance edge, streaming realization).
It covers the **600u → ~2000u** band; beyond that, hero relics hand off to the R5 silhouette cards in
the Phase-2 horizon ring (`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` §19).

**Why this is cheap here:** the far clip is not protecting the expensive content. Terrain chunks and
scatter (92% of scene verts — see `RENDER_PERF_PROFILE_REPORT.md`) only *exist* within the ~180u chunk
window; the only geometry between 600u and 2000u is a handful of relic meshes on the cheap `RelicLit`
shader and more of the flat ground disc. Raising the far plane buys landmark permanence for near-zero
vertex cost.

## 2. Prior art (why "never cull," not "more fog")

- **Vista meshes with no cull:** Elden Ring's Erdtree, BotW's Hyrule Castle / Death Mountain, classic
  WoW mountains — hero landmarks are exempt from draw-distance budgets and use a cheap far shader.
- **LOD chains ending in an impostor card** (octahedral impostors — Fortnite, Horizon Zero Dawn) —
  right for *mass* content (trees), overkill for a few authored heroes; our card variant is R5.
- **Spawn dither fade** (nearly every open-world streamer): objects that appear inside the view fade
  over a fraction of a second instead of popping between frames.
- **Fog wall** — the crudest tool; explicitly retired by the round-5 tune and not coming back.

## 3. Current state (2026-07-06, all verified in code)

| Fact | Where |
|---|---|
| Far clip 600u from `VistaCameraFarClip` → `DerivedCameraFarClip` | `ProjectFeatureConfig.cs:182,216`; applied in `PlayerEntityBootstrap.cs:340`, `PlayerCameraBootstrap_WithVisuals.cs:219`; copied into ECS at `DotsSystemBootstrap.cs:499` |
| `_AtmoFarFade` global = `Camera.main.farClipPlane` each frame | `TimeOfDayController.PushAtmosphere` (line ~300) |
| Disc hides the far clip by alpha-fading over `0.75×_AtmoFarFade → _AtmoFarFade` (450–600u), revealing the skybox ground skirt | `GroundPlaneImpostor.shader` + `Atmosphere.hlsl` `AtmoFarClipHaze` |
| Hero relic shading uses `ApplyAerialPerspective`, which **includes** the far-clip concealer — beyond ~600u it fogs fully to horizon | `RelicLit.shader`, `Atmosphere.hlsl` §5.3 |
| Relic LOD swap distances already derive from `camera.farClipPlane` | `RelicVisualBootstrap.cs:157-170` |
| Relic anchors realize across ±1024u; hero template = `relic_hand` (authored heroes forthcoming via V12) | structure placement pipeline |

Pop sources today: **(a)** far-clip crossing — a relic 600u+ away appears/disappears as the player
moves; **(b)** realization pop — an anchor realizes inside the view when streaming catches up.

## 4. Design

### P1 — Raised far plane, short world

- New config on `ProjectFeatureConfig`: `LandmarkDrawDistance` (default **2000**, `0` = disabled →
  everything behaves as today). `VistaCameraFarClip` (600) is **redefined as the world reference
  distance** — where the ordinary world visually ends — and keeps driving fog ratios.
- Camera bootstraps set `farClipPlane = max(DerivedCameraFarClip, LandmarkDrawDistance)`.
- **No per-layer culling in the first slice.** `Camera.layerCullDistances` (dedicated `Landmark`
  layer, world layers clamped to 600u) is documented as the *containment* option (§P4) if profiling
  ever shows cost — with the explicit risk that Entities Graphics/BRG honoring `layerCullDistances`
  must be verified before relying on it. Given the ≤180u chunk window, we expect not to need it.
- Depth precision: near 0.3 → far 2000 is a ~6700:1 ratio — comfortably fine with reversed-Z.
- Shadows are untouched (URP shadow distance is independent); far landmarks render unshadowed
  silhouettes, which matches the vista language.

### P2 — Decouple `_AtmoFarFade` from the camera far plane *(prerequisite for P1)*

`TimeOfDayController.PushAtmosphere` must broadcast `farFade = DerivedCameraFarClip` (the world
reference distance, via config) instead of `Camera.main.farClipPlane`. Otherwise raising the far
plane silently stretches the disc's alpha handoff to 1500–2000u and the `distanceHaze` ramp with it,
retuning the whole vista as a side effect. After P2, the disc→skirt handoff stays at 450–600u and the
ground-level look is byte-identical regardless of the camera far plane.
EditMode test: `AtmosphereBroadcast` receives the config distance, not the camera plane.

### P3 — Landmark far path in `RelicLit`

`ApplyAerialPerspective`'s far-clip concealer exists to hide the **world** edge; a landmark drawn
beyond it must not be erased by it. For the hero material:

- Replace the haze call with `AtmoAerialHazeAmount` (height + distance floor, **no concealer**) —
  the same no-concealer contract the disc already uses.
- Add a **dithered edge fade** over the last ~10% before `LandmarkDrawDistance` (screen-door /
  interleaved-gradient dither in the fragment shader — `RelicLit` is opaque + ZWrite, so alpha
  blending is not an option). Crossing 2000u is then a dissolve, not a pop.
- Grounding: a relic at 600–2000u stands on the skybox skirt (the disc has alpha-faded out by 600u).
  The skirt is palette-matched land and the height-haze term veils the relic's lowest meters hardest,
  which naturally seats it. Judged acceptable by design; if eyeballing disagrees, the fallback is
  pushing the disc's alpha handoff outward for landmark-bearing bearings — decide only on evidence.
- Parallax: the skirt is static wallpaper behind a parallaxing relic — acceptable beyond 600u (small
  angular motion), the same acceptance R5 already makes for its cards.

### P4 — Spawn fade-in (streaming pop, all relics)

When realization spawns a relic inside the view frustum, dither it in over ~0.5s (per-instance fade
progress as a BRG instanced material property, driven by the realization system). This fixes pop
source (b) for background relics too, independent of draw distance. Separable slice — can ship last.

### Out from here: R5

Beyond `LandmarkDrawDistance`, fictionally-kilometers-away heroes are the horizon ring's job
(silhouette cards from V12 authored anchors). This spec deliberately narrows R5's problem: the cards
only need to cover **>2000u**, where a static card is indistinguishable from geometry. Note for R5:
its §19 handoff assumption ("haze reaches ~full at the far clip") predates the round-5 thin haze and
must be revisited — the dithered edge fade from P3 is the replacement handoff mechanism.

## 5. Scope

Hero/landmark relic templates only (`relic_hand` today; V12 authored heroes when they exist).
Background relics (`relic_head`, `stone_outcrop`) keep the world draw distance — they are set
dressing, not landmarks, and multiplying long-range meshes erodes the "few cheap meshes" premise.

## 6. Non-Goals

- R5 horizon-ring cards (separate ticket; this spec feeds it a narrower contract).
- Relic mesh LODs (R1) — orthogonal; `RelicVisualBootstrap` swap distances already track the far
  plane and will just work.
- Re-thickening the haze. The thin layer is an owner-approved aesthetic decision.
- Terrain/scatter draw-distance changes of any kind.

## 7. Acceptance Criteria

1. A hero relic 1500u away is visible from spawn and neither appears nor disappears as the player
   walks/slingshots toward or away from it; at ~2000u it dissolves via dither, not a pop.
2. Ground-level vista and sky-drop are **byte-identical** with `LandmarkDrawDistance = 0` vs today
   (P2 decoupling verified by screenshot diff), and unchanged at ground level with it enabled except
   for the newly visible landmarks.
3. Relics realizing inside the view fade in over ~0.5s (P4).
4. No measurable FPS regression (scene stays vertex-bound on ≤180u scatter; verify with the profiler
   workflow from `RENDER_PERF_PROFILE_REPORT.md`).
5. EditMode: broadcast `farFade` sources from config, not the camera far plane.

## 8. Rollout

P2 (decouple, zero visual change) → P1 (raise plane, landmarks appear) → P3 (edge dither) →
P4 (spawn fade). P2 must land with or before P1; P3/P4 are independently shippable.

## 9. Build record (2026-07-07)

P2 → P1 → P3 landed as three commits in rollout order; P4 remains open. Implementation notes
where the build refined the design:

- **P2:** the reference distance is a static `AtmosphereBroadcast.WorldReferenceDistance`
  (default 600) seeded from `ProjectFeatureConfig.DerivedCameraFarClip` by `DotsSystemBootstrap` —
  pushed in because `Core.asmdef` (Rendering.Sky) cannot reference `DOTS.Core.Authoring`
  (reverse dependency). EditMode contract test drives the controller's `OnValidate` re-broadcast
  path against a `Camera.main` with a different far plane.
- **P1:** the max() resolves once in `ProjectFeatureConfig.DerivedLandmarkFarClip` and flows to
  both camera bootstraps through the existing `ProjectFeatureConfigSingleton.CameraFarClipPlane`
  (that field now means "what the camera uses"; the un-raised world reference reaches shaders via
  the P2 broadcast). New `_AtmoLandmarkFade` global = max(world reference, landmark distance),
  so a disabled feature (0) collapses the P3 dissolve to the world edge — it then doubles as the
  hero's far-clip concealment with no special-case branch.
- **P3:** dither = interleaved gradient noise `clip()` in `RelicLit`'s ForwardLit **and DepthOnly**
  passes (the depth prepass must dissolve in sync or depth-reading effects ghost the hero).
  `ApplyAerialPerspective`'s concealer reverted to full strength (the 2026-07-07 interim
  strength-scaled patch is superseded); the function keeps zero consumers until V9 P3 terrain tint.
- **Known accepted side effect (owner call, 2026-07-07):** background relics (Unlit gray) realized
  at 600–1024u become visible under the raised plane with no cull in this slice — judged on
  screenshots; the containment options (per-template cull in the relic LOD system, or
  `layerCullDistances`) remain follow-ups if the eyeball disagrees.
- **P4 (2026-07-16):** per-instance `RelicSpawnFade` IComponentData (`[MaterialProperty]` →
  `_RelicSpawnFade`, material default 1 so non-ECS uses render solid) added at 0 by
  `RelicRealizationSystem` on every spawn — all realizations fade, not just in-frustum ones
  (off-screen fades finish unseen; no camera test needed). `RelicSpawnFadeSystem` (Burst ISystem,
  same `EnableRelicRealizationSystem` bootstrap flag) advances it over 0.5s; `RelicLit` clips
  against `min(edgeFade, spawnFade)` in ForwardLit **and DepthOnly** (same sync rule as P3), with
  the matching `UnityPerMaterial` cbuffer added to DepthOnly for SRP-batcher pass parity.
  ShadowCaster deliberately not dithered, matching P3. Note: RelicLit-only — background relics on
  Unlit gray still hard-pop on realization; acceptable under the same owner call as above.
- **P3 amendment — haze pre-melt, two rounds (2026-07-17):** owner reported the edge dissolve's
  stipple reading clearly on a distant hand inside the dissolve band. Root cause: the hero
  exemption's reduced `_AerialStrength` keeps landmarks legible right into the band, so the dither
  clipped a still-readable object. Round 1 forced haze to t = 1 before the band — wrong: at t = 1
  `ApplyAerialHaze` lands on `_AtmoHorizon` (near-white on overcast presets) while the backdrop
  behind a landmark is the darker sky mountain band, so the relic flashed white (owner report).
  Round 2 (final): `AtmoLandmarkHazeRamp` caps at **0.65** over 0.7–0.85 × `_AtmoLandmarkFade`
  (identity below 0.7 — vista legibility untouched) to cut contrast only, and the edge fade band
  widened from the last 10% to the last **20%** — the dither's clipped pixels reveal the *true*
  per-pixel backdrop (band, clouds), making the dither itself the background-blend mechanism until
  R5 silhouette cards exist. Never full-melt a landmark to a guessed color. ForwardLit-only color
  change; DepthOnly stays clip-synced via the shared `AtmoLandmarkEdgeFade`. The "keep it longer"
  dial remains `ProjectFeatureConfig.LandmarkDrawDistance` (all band ratios scale with it).

## Related Docs

- `ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` — `_Atmo*` contract; §5.3 concealer semantics this spec adjusts
- `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` §19 — R5 cards (the >2000u continuation)
- `GROUND_PLANE_IMPOSTOR_SPEC.md` — disc; unchanged, but its alpha handoff depends on P2
- `../Tickets/vista-moment.md` — V9 rounds 3–5 build record (skirt, alpha far-clip, thin haze)
- `RENDER_PERF_PROFILE_REPORT.md` — vertex-bound evidence behind the "cheap far plane" premise
