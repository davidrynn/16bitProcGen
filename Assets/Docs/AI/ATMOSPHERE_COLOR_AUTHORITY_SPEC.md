# Atmosphere Color Authority Spec

**Status:** PROPOSED
**Last Updated:** 2026-07-02
**Owner:** Rendering / Vista
**Phase:** Vista MVP (ties ticket V9; consumed by V3, V8)
**Keywords:** atmosphere, palette, aerial perspective, hue unification, global shader uniforms, impostor, fog, time-of-day, biome

---

## 1. Purpose

Establish a **single source of truth for the scene's atmospheric palette** — horizon, zenith,
ground, rock, sun, fog, and a global saturation term — that every distance-facing visual surface
*consumes* rather than *owns*. Today each surface defines its color independently and on a different
update model, so they drift apart (visibly "off" at the seams) and cannot respond together to the
time-of-day cycle. This spec defines the authority, the broadcast mechanism, and the shared
aerial-perspective rule that unifies the sky, ground-plane impostor disc, mountain horizon impostor,
and terrain into one hue-consistent picture.

This is the architectural generalization of the already-shipped `SkyController._driveFogColor`
coupling (fog color follows the sky horizon) — extended from one output to all surfaces.

---

## 2. Problem Statement (current state, 2026-07-02)

The scene has four color-bearing surfaces, each sourced by a **different mechanism**, two of which are
frozen and cannot track the day/night cycle:

| Surface | Color mechanism today | Tracks time-of-day? | Authoritative source |
|---|---|---|---|
| Sky (gradient) | runtime material uniforms on a cloned material | ✅ dynamic | `SkyPreset` keyframes → `SkyController.PushSkyUniforms` |
| Fog | `RenderSettings.fogColor` | ✅ dynamic | horizon color (`SkyController._driveFogColor`) |
| Ground disc impostor | **shader-default** `_GrassColor`/`_RockColor` on a runtime material | ❌ frozen | `GroundPlaneImpostor.shader` literals |
| Mountain horizon impostor | **shader-default** `_MountainColor` | ❌ frozen | `ProceduralGradientSky.shader` literal / not yet built as a panel |
| SDF terrain mesh | shared **material asset** (Synty grass **albedo texture** × white `_BaseColor`) | ❌ frozen | `Generic_Grass.mat` + baked texture |

Consequences:

- **Sky + fog move; ground + terrain + mountains do not.** As the cycle runs, dawn/dusk warm the sky
  and fog while the ground/disc/mountains stay locked at their noon-baked look. The static desync you
  see today (disc slightly off from terrain) is the frozen case of the same bug.
- **The terrain is not palette-driven at all.** Its color lives in a baked Synty albedo texture, not a
  tint slot. This is why the previously-written `SyncTerrainColor` (in `GroundPlaneImpostorBootstrap`)
  was disabled: it reads the terrain tint from `_BaseColor`, but `_BaseColor` is **white** `(1,1,1)`,
  so the sync trips its own luminance > 0.85 "broken white fallback" guard and refuses. The old
  approach was architecturally doomed — you cannot read a tint from a slot that does not hold the color.
- **No shared infrastructure exists.** There is no `Shader.SetGlobalColor`/`SetGlobalVector` anywhere
  in runtime code, no shared HLSL include, and no atmosphere/palette singleton. This is greenfield.

### Reference values (current)

- Sky preset horizons (all warm during day): dawn `(0.95,0.60,0.35)`, noon `(0.85,0.75,0.55)`,
  dusk `(0.90,0.45,0.25)`, night `(0.08,0.08,0.15)` (Plains uses the cool **Cloudbreak** preset instead).
- Disc: `_GrassColor (0.40,0.46,0.26)`, `_RockColor (0.28,0.32,0.23)`, `_RockThreshold 0.60`.
- Mountain: `_MountainColor (0.22,0.25,0.20)` (flat grey-green).
- Terrain: `Generic_Grass.mat` `_BaseColor (1,1,1)` × Synty grass albedo texture.
- Fog: config `FogMode` Exp², `FogDensity 0.0022`, `FogColor (0.62,0.74,0.85)` (overwritten each frame
  by the horizon).

---

## 3. Scope

**In scope:**

1. A single **atmosphere authority** that evaluates the palette every frame and broadcasts it.
2. A **global-shader-uniform** broadcast contract (`_Atmo*`) that any shader can sample.
3. A shared **`Atmosphere.hlsl`** include exposing the palette globals and one
   `ApplyAerialPerspective()` function.
4. Wiring the four surfaces as consumers: sky, ground disc, mountain impostor, and a terrain tint path.
5. The **aerial-perspective rule** shared by disc and mountains (desaturate + lerp toward horizon by
   distance), differing only in falloff strength.
6. Resolving the ground-color source-of-truth decision (see §6).

**Out of scope (see §9):** unifying the disc and mountain *C#/DOTS bootstraps* into one system; a
biome-transition blend animation; per-material artistic overrides beyond the palette; volumetric or
height-based fog (that is V8 / a separate concern, though it consumes the same `_AtmoHorizon`).

---

## 4. Non-Goals

- No physically-based atmospheric scattering; the palette stays art-directed keyframes.
- No new singleton competing with `TimeOfDayController` — the authority *is* that controller, extended.
- No per-surface private color copies survive; if a surface still owns a literal after this, it is a bug.
- No replacement of the Synty terrain texture with procedural/vertex-color terrain (that is Option C,
  documented but not adopted for MVP).
- No gameplay/ECS-layer color logic; this is entirely a managed rendering concern.

---

## 5. Architecture

Two tiers. **Tier 1 is the durable contract and must be built carefully; Tier 2 implementations stay
deliberately thin and are free to grow.** The governing principle: *share the interface, not
necessarily the implementation.*

```
┌──────────────────────────── TIER 1 — AUTHORITY (build well) ────────────────────────────┐
│                                                                                          │
│  AtmosphereController  (extends TimeOfDayController; managed, rendering layer)            │
│    • evaluates the active SkyPreset at the current time-of-day (already does)             │
│    • derives the full palette: horizon, zenith, ground, rock, sun, saturation            │
│    • broadcasts once per frame:                                                           │
│        Shader.SetGlobalColor / SetGlobalFloat  →  _Atmo* uniforms                         │
│        RenderSettings.fogColor = horizon        (existing _driveFogColor behavior)        │
│                                                                                          │
│  Atmosphere.hlsl  (shared include — the single definition of the palette + aerial math)  │
│    • declares the _Atmo* uniforms                                                         │
│    • float3 ApplyAerialPerspective(float3 color, float viewDist, float strength)          │
└──────────────────────────────────────────────────────────────────────────────────────────┘
                                          │ (all consumers #include Atmosphere.hlsl)
        ┌─────────────────┬───────────────┼────────────────┬─────────────────────┐
        ▼                 ▼               ▼                ▼                     ▼
   Sky shader      Ground disc      Mountain impostor   Terrain tint          Fog
 (horizon/zenith)  (_AtmoGround/    (_AtmoGround base,  (_AtmoGround × Synty  (RenderSettings
                    _AtmoRock,       strong aerial →     albedo, mild          .fogColor =
                    mild aerial)     desaturated)        time-of-day tint)     horizon)
```

### 5.1 Tier 1 — the authority

- **Home:** extend `TimeOfDayController` (it already evaluates the palette per frame and owns
  `SkyController`). Do **not** add a rival singleton. Keep it managed — `Shader.SetGlobal*` and
  `RenderSettings` are managed APIs, and this is rendering, not gameplay, so it correctly stays out of
  the ECS world.
- **Per-frame work:** after evaluating the current `SkySettings`, push the derived palette to global
  uniforms and keep driving `RenderSettings.fogColor`. One update per frame; only push on change if we
  want to shave cost (palette changes are continuous under a live cycle, so unconditional push is fine).

### 5.2 Global uniform contract (`_Atmo*`)

Declared once in `Atmosphere.hlsl`, set once per frame by the authority. Colors in **linear** space.

| Uniform | Type | Semantics |
|---|---|---|
| `_AtmoHorizon` | `half4` | Horizon/haze color — the hue everything converges toward at distance. Same value drives fog. |
| `_AtmoZenith` | `half4` | Top-of-sky color (sky shader primarily). |
| `_AtmoGround` | `half4` | Base ground/grass tint for disc, mountains, and the terrain tint multiply. |
| `_AtmoRock` | `half4` | Base rock tint for disc/mountains. |
| `_AtmoSun` | `half4` | Sun/key-light color for cheap directional warmth (rgb) + intensity (a). |
| `_AtmoSaturation` | `half` | Global saturation scalar (1 = full). Lets the whole scene desaturate under overcast without editing every color. |
| `_AtmoFarFade` | `half` | Reference distance at which aerial perspective reaches full — typically the camera far clip. Lets shaders normalize `viewDist`. |

> Add uniforms only when a consumer needs them; do not speculatively broadcast unused channels.

### 5.3 The shared aerial-perspective rule

The single shared behavior between disc and mountains. Distant surface color pulls toward the horizon
hue **and** loses saturation as distance grows — the JC3-style hue unification that makes the
disc-edge / mountain-base / sky-horizon seam disappear.

```hlsl
// strength ∈ [0,1] dials how aggressively this surface hazes (disc: low, mountains: high)
float3 ApplyAerialPerspective(float3 color, float viewDist, float strength)
{
    float t = saturate(viewDist / _AtmoFarFade) * strength;      // 0 near → 1 far
    float3 desat = lerp(color, Luminance(color).xxx, (1 - _AtmoSaturation) + t * 0.5);
    return lerp(desat, _AtmoHorizon.rgb, t);                     // converge to horizon at distance
}
```

- **Disc** calls it with a **low** `strength` — near/mid ground keeps its color, only the far edge hazes
  (complements the existing `MixFog` on the disc, which stays).
- **Mountains** call it with a **high** `strength` — the far backdrop reads correctly **desaturated**
  and horizon-tinted (this is the corrected reading of "mountains should be desaturated"). Their base
  hue comes from `_AtmoGround`/`_AtmoRock` instead of the dead `_MountainColor` literal.

Same function, different `strength`. That is the concrete "disc and mountains share logic" — DRY at the
shader level, which is the right layer since both are fragment-colored.

### 5.4 Consumers

- **Sky shader:** read `_AtmoHorizon`/`_AtmoZenith` (or keep its dedicated uniforms; the authority sets
  both consistently).
- **Ground disc (`GroundPlaneImpostor.shader`):** replace `_GrassColor`/`_RockColor` literals with
  `_AtmoGround`/`_AtmoRock`; call `ApplyAerialPerspective(color, viewDist, discStrength)` before the
  existing `MixFog`.
- **Mountain impostor:** `#include Atmosphere.hlsl`; base color from `_AtmoGround`/`_AtmoRock`, then
  `ApplyAerialPerspective(..., mountainStrength)`. Replaces the flat `_MountainColor`.
- **Terrain:** apply `_AtmoGround` as a **tint multiply** so it breathes with the cycle (see §6 Option B).

---

## 6. Decision — ground color: authored vs. generated

The terrain color currently lives in a baked Synty texture that ignores the palette entirely. Sky and
fog are palette-driven and dynamic. **These are incompatible: a day/night cycle cannot warm the sky and
fog while the ground stays a fixed noon texture.** The terrain must participate. Three options:

| Option | Ground truth | Authority action | Verdict |
|---|---|---|---|
| **A. Texture is truth** | Synty albedo | Sample the texture's average once, feed it in as `_AtmoGround`; disc/mountains match. | Cheapest, but terrain still won't shift with time-of-day. Rejected as the endpoint. |
| **B. Palette tints texture** ← **CHOSEN** | Palette | Push `_AtmoGround` as a tint multiply onto terrain **and** disc/mountains uniformly (≈white/neutral at noon, warm at dusk, cool-dark at night). | Terrain now breathes with the cycle; keeps the Synty look at noon. The only option where all surfaces stay locked as the cycle runs. |
| **C. Procedural terrain** | Palette | Replace Synty texture with vertex-color/noise terrain reading the palette directly. | Full control, but expensive; overkill for MVP. Deferred. |

**Chosen: Option B.** Keep the Synty albedo as the noon look, but let the authority apply a shared
time-of-day tint to every ground surface. Cheap first step that proves the pattern with no shadergraph
surgery: have the authority write the **same** `_AtmoGround` value into both the terrain material's
`_BaseColor` and the disc's grass tint each frame — instantly syncing disc↔terrain and inverting the
old broken sync (one authority pushes one tint into both, instead of the disc reading terrain's white
slot). Full version routes `_AtmoGround` through the Synty shadergraph as an albedo multiply.

---

## 7. Compute & Art Budget

- **Authority:** one managed pass per frame — a handful of `SetGlobalColor`/`SetGlobalFloat` calls plus
  the existing fog-color write. Negligible CPU, zero allocation.
- **Shaders:** `ApplyAerialPerspective` is a couple of `lerp`s + a luminance dot — a few ALU ops per
  fragment. Well inside the low-poly / low-res / compute-light intent. No new textures, no branches, no
  extra passes.
- **Art intent preserved:** stylized, hue-unified, aerial-perspective look (per `Temp_OpeningInspiration.png`
  and the JC3 reference); the palette stays art-directed keyframes, not simulation.

---

## 8. Rollout Phases

1. **P1 — Authority + contract.** Add `Atmosphere.hlsl` (uniform declarations + `ApplyAerialPerspective`),
   extend `TimeOfDayController` to broadcast `_Atmo*` and keep driving fog. No visual change yet.
2. **P2 — Disc consumes.** Swap disc literals for `_AtmoGround`/`_AtmoRock` + aerial call. Re-enable the
   sync intent via the authority (delete the doomed `SyncTerrainColor` `_BaseColor` read).
3. **P3 — Terrain tint (Option B).** Push `_AtmoGround` into terrain (`_BaseColor` cheap path first,
   shadergraph multiply for the full version). Verify disc↔terrain seam matches at ground level.
4. **P4 — Mountains consume (folds V3).** Mountain horizon impostor uses the shared include with high
   aerial `strength`; base hue from `_AtmoGround`/`_AtmoRock`. Verify disc-edge = mountain-base = horizon.
5. **P5 — Saturation / overcast pass.** Tune `_AtmoSaturation` per preset (Cloudbreak = lower) once all
   surfaces read it.

V8 (distance-graded fog) is independent but consumes `_AtmoHorizon`; it can proceed in parallel.

---

## 9. Deliberately NOT shared yet

Share the interface, not the implementation. The authority and `Atmosphere.hlsl` are the durable
contract — build them well. But **do not** prematurely unify the disc and mountain C#/DOTS bootstraps
into a single "AtmosphericImpostor" system: the disc is a flat, player-following plane and the mountain
panel is a static-ish horizon ring; forcing them into one system now buys nothing and their geometry
differs. Let them stay two thin bootstraps that happen to consume the same globals + include. When a
**third** impostor type appears, that is the signal to extract the shared system — not before.

---

## 10. Related Docs

- [SKYBOXPLAN.md](SKYBOXPLAN.md) — procedural sky, time-of-day cycle, and the `_driveFogColor`
  fog-tracks-sky coupling this spec generalizes (§9 Phase 3).
- [GROUND_PLANE_IMPOSTOR_SPEC.md](GROUND_PLANE_IMPOSTOR_SPEC.md) — the ground disc; primary consumer, and
  home of the disabled `SyncTerrainColor` this spec supersedes.
- [HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md](HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) — the mountain horizon
  impostor; consumer with strong aerial falloff (ties ticket V3).
- [MVP_VISTA_MOMENT_SPEC.md](MVP_VISTA_MOMENT_SPEC.md) — umbrella MVP vista experience this serves.
- [VISTA_GROUND_PLANE_FOG_INVESTIGATION.md](VISTA_GROUND_PLANE_FOG_INVESTIGATION.md) — working notes /
  evidence for the color-desync diagnosis.
- [../mvp/Windswept_Colossus_Plains_Biome_Spec.md](../mvp/Windswept_Colossus_Plains_Biome_Spec.md) —
  biome palette source for the Plains (Cloudbreak).
- Tickets: `Assets/Docs/TICKETS.md` — **V9** (this authority), **V3** (mountain panel, folds into P4),
  **V8** (distance-graded fog, consumes `_AtmoHorizon`).

---

## 11. Acceptance Criteria

1. A single authority broadcasts the `_Atmo*` uniforms each frame; no runtime surface holds a private
   base-color literal (disc `_GrassColor`/`_RockColor` and mountain `_MountainColor` are gone as sources).
2. Advancing the time-of-day cycle visibly shifts **sky, fog, terrain, disc, and mountains together** —
   no surface stays frozen at noon.
3. At the horizon, the disc's outer edge, the mountain base, and the sky horizon read as one continuous
   hue band (no seam) at multiple times of day.
4. Distant mountains read **desaturated and horizon-tinted** (aerial perspective), not flat grey and not
   over-saturated.
5. `SyncTerrainColor`'s `_BaseColor`-read approach is removed; disc↔terrain match is driven by the
   authority instead.
6. Added ALU/CPU cost is within the compute-light budget (no new passes/textures; a few ops per fragment).
7. Switching the active biome preset (e.g. Plains Cloudbreak) recolors all consumers consistently through
   the same palette.

---

## 12. Open Questions

- **Disc vs. mountain `strength` values** — tune empirically at altitude; start disc ≈ 0.25, mountains ≈ 0.85.
- **Terrain tint path** — ship the cheap `_BaseColor` write first, or go straight to the shadergraph
  albedo multiply? (Lean: `_BaseColor` first to validate, then shadergraph.)
- **Saturation authoring** — per-preset `_AtmoSaturation` keyframe, or derived from cloud coverage?
- **Scatter (trees/rocks/pebbles)** — they already `MixFog`; do they also need `_AtmoGround`/saturation,
  or is fog coupling enough for MVP? (Lean: fog-only for now; revisit if they pop against the tinted ground.)
