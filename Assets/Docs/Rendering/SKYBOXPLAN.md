# Minimal Procedural Gradient Sky — Plan

_Status: PHASE 2 COMPLETE · PHASE 3 PLANNED (vista atmosphere — see §9, ticket V6)_
_Last updated: 2026-07-01_

> **Purpose.** Define the smallest viable implementation of a shader-based procedural gradient sky for a stylized low-poly URP game with infinite procedural terrain. The sky must be lightweight, deterministic, and extensible toward biome-driven color, animated clouds, and time-of-day — none of which are in scope for Phase 1.

---

## 1. Scope Definition

### In Scope (Phase 1 — Vertical Slice)

- Single URP-compatible unlit shader producing a smooth vertical color gradient.
- Two configurable colors: **horizon** and **zenith**.
- A **gradient exponent** controlling blend sharpness.
- An optional **horizon height offset** so the gradient can be tuned per scene.
- A `SkySettings` data struct that fully parameterizes the sky (no singletons).
- A render pass or fullscreen quad that draws behind all geometry (Background queue).
- Deterministic output: same `SkySettings` → same visual, every frame, every platform.
- Works with infinite terrain streaming and arbitrary camera position/teleport.

### Out of Scope (Deferred)

| Feature | Reason |
|---------|--------|
| Animated clouds | Phase 2+ — separate cloud layer planned |
| Time-of-day cycle | Phase 2+ — requires lerp between presets |
| Biome color blending | Phase 2+ — requires biome system hookup |
| Physically-based sky | Conflicts with art direction |
| Volumetric rendering | Performance budget violation |
| HDRI / cubemap fallback | Unnecessary for stylized look |
| Stars / moon / sun disc | Phase 3+ |

---

## 2. Non-Goals

- No atmospheric scattering, Rayleigh, or Mie computation.
- No raymarching.
- No runtime texture generation or runtime allocations.
- No dependency on Unity's built-in `Skybox` material or `RenderSettings.skybox`.
- No singleton managers.

---

## 3. Architecture Overview

```
┌─────────────────────────────────────────────────┐
│                  DATA LAYER                      │
│                                                  │
│  SkySettings (pure struct)                       │
│    horizonColor : Color                          │
│    zenithColor  : Color                          │
│    gradientExponent : float                      │
│    horizonHeight    : float (optional offset)    │
│                                                  │
├─────────────────────────────────────────────────┤
│                RENDER LAYER                      │
│                                                  │
│  ProceduralGradientSky.shader (URP Unlit)        │
│    Fullscreen / skybox mesh                      │
│    Reads SkySettings uniforms                    │
│    Outputs color per fragment via gradient math   │
│                                                  │
├─────────────────────────────────────────────────┤
│              INTEGRATION LAYER                   │
│                                                  │
│  SkyController (MonoBehaviour or System)          │
│    Owns SkySettings                              │
│    Pushes uniforms to shader material            │
│    (Future) Receives biome context               │
│                                                  │
└─────────────────────────────────────────────────┘
```

Data flows **downward only**. The shader has zero knowledge of biomes, terrain, or time. The integration layer is the only place that may read external game state in future phases.

---

## 4. Minimal Vertical Slice

Deliverables for Phase 1:

| # | Deliverable | Type |
|---|-------------|------|
| 1 | `SkySettings` struct | C# data |
| 2 | `ProceduralGradientSky.shader` | URP shader (unlit, fullscreen) |
| 3 | `SkyController` | MonoBehaviour (bootstrap-compatible) |
| 4 | Material asset wired to shader | Asset |
| 5 | Validation tests | See SKYBOXTESTS.md |

**Definition of Done:** A single gradient sky renders from horizon to zenith, colors configurable in the Inspector, visually stable during camera movement across unbounded terrain.

---

## 5. Extension Points

| Extension | How | Status |
|-----------|-----|--------|
| Biome color variation | `BiomeSkyMapping` ScriptableObject + `TimeOfDayController.TransitionToPreset()` | **Code done; content/wiring pending — Phase 3 (§9)** |
| Time-of-day | `SkyPreset` keyframes (dawn/noon/dusk/night), `TimeOfDayController` lerps by normalized time | **Phase 2 — Done (runs live)** |
| Cloud layer | `_CLOUDS_ON` shader keyword in `ProceduralGradientSky.shader`; procedural FBM noise, no textures | **Phase 2 — Done** |
| Star field | Additional pass behind gradient with point sprites or noise | Phase 3+ |
| Fog integration | Share `horizonColor` with URP fog settings | **Phase 3 — Active (§9, ticket V6)** |
| Scene-wide color unification | Generalize `_driveFogColor` into an atmosphere authority + global `_Atmo*` uniforms consumed by disc/mountains/terrain | **Proposed — [ATMOSPHERE_COLOR_AUTHORITY_SPEC.md](ATMOSPHERE_COLOR_AUTHORITY_SPEC.md), ticket V9** |

### Phase 2 Cloud Decision

Clouds are implemented as a **shader keyword** (`_CLOUDS_ON`) inside the gradient sky shader rather than a separate render pass. Rationale:
- Single draw call, single material — no URP Renderer Feature complexity
- Avoids `ScriptableRenderPass` API fragility across URP versions
- No per-frame `FindFirstObjectByType` in the render pipeline
- Total ALU cost with clouds enabled: ~50 instructions (well within budget)
- Toggle via `SkyController.CloudsEnabled` which calls `material.EnableKeyword`/`DisableKeyword`

---

## 6. Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Color banding on low-bit displays | Medium | Low | Use `half` precision + dithering keyword in shader; test on 8-bit target |
| Horizon line visible as hard edge | Low | Medium | Gradient exponent tuning; default exponent ≤ 2.0 |
| URP version breaks custom fullscreen pass | Low | Medium | Use `RenderPassEvent.BeforeRenderingOpaques` or camera background approach; keep shader simple |
| Performance regression from fullscreen pass | Very Low | Low | Single pass, no texture, no branching → negligible |
| Conflicts with Unity `RenderSettings.skybox` | Low | Low | Disable built-in skybox; use camera clear color or custom render feature |

---

## 7. Performance Expectations

| Metric | Target |
|--------|--------|
| GPU cost | < 0.05 ms on integrated GPU (gradient only); < 0.1 ms with clouds |
| CPU cost per frame | 0 allocations, ~0 µs (uniform push only on change) |
| Texture memory | 0 bytes (no textures — clouds use procedural FBM noise) |
| Draw calls | 1 fullscreen triangle (clouds are same draw call via keyword) |
| Shader ALU | ~10 instructions gradient only; ~50 with `_CLOUDS_ON` |
| Compatible with | Mobile / WebGL / low-end desktop |

---

## 8. Shader Approach Summary

### Coordinate Space

**View-direction space** (normalized view ray per fragment).

Rationale: the gradient must remain fixed relative to the camera's orientation, not world position. Using the vertical component of the view direction ensures the sky is stable regardless of camera translation — critical for infinite terrain.

### Gradient Model

1. Compute normalized height parameter from the fragment's view direction:

$$
t(y) = \text{saturate}\!\left(\frac{y - h_0}{h_1 - h_0}\right)
$$

Where:
- $y$ = vertical component of the normalized view direction
- $h_0$ = horizon height parameter (default 0)
- $h_1$ = zenith height parameter (default 1)

2. Apply sharpness exponent:

$$
t' = t^{k}
$$

Where $k$ = `gradientExponent` (default 1.0; values > 1 push color toward horizon; values < 1 push toward zenith).

3. Interpolate color:

$$
C_{\text{sky}} = \text{lerp}(C_{\text{horizon}},\; C_{\text{zenith}},\; t')
$$

All operations are constant-time, branch-free, and Burst/GPU friendly.

### Rendering Strategy

Two viable approaches (decide during implementation):

| Approach | Pros | Cons |
|----------|------|------|
| **URP Renderer Feature + fullscreen pass** | Clean integration, no scene object needed | Requires URP scripting API knowledge |
| **Skybox material on camera** | Simplest setup; Unity handles draw order | Couples to `RenderSettings.skybox`; less control |

**Decision:** Skybox material on camera via `RenderSettings.skybox`. The URP Renderer Feature approach was evaluated during Phase 2 cloud implementation and rejected — it adds API fragility (RenderGraph migration on URP 17.2), per-frame managed lookups, and manual Renderer asset wiring for zero visual or performance benefit in this project.

---

## 9. Phase 3 — Vista Atmosphere (biome content + fog integration)

_Added 2026-07-01. Tracked as ticket **V6** — **§9.1 implemented 2026-07-01** (`CloudbreakSkyPreset.asset`,
`SkyController` fog-color driving, `DefaultBiomeSkyMapping` populated/assigned). §9.2 multi-biome bridge still
deferred. Working notes / evidence: `VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`._

**Motivation.** The MVP vista ("look at" the giant hand across a hazy Highlands plain) exposed two gaps:
the day/night sky horizon is warm/orange at every daytime (art), and the fog color is static so it can't
match the sky across the cycle. Phase 2 shipped the *mechanisms* (time-of-day cycle, biome mapping, clouds)
but the biome mapping was never populated or wired, and fog integration was left at Phase 3+.

**Art target.** `Docs/Temp_OpeningInspiration.png` — a cool, broken-overcast highland day: towering cumulus
with grey storm-cells and shafts of broken sun, muted green-gold grass, distant mountains dissolved into pale
blue-grey haze (strong aerial perspective). Named palette: **"Cloudbreak."** Zero orange.

**Scope (single biome).** The MVP has one biome (Windswept Colossus Plains), so biome *switching* is out of
scope — build the content + fog for one biome and leave the switch bridge as a marked seam.

### 9.1 Deliverables

1. **Plains "Cloudbreak" `SkyPreset` asset.** Cool overcast palette; suggested starting values:
   - zenith `~(0.45, 0.52, 0.60)`, horizon `~(0.68, 0.72, 0.74)`
   - clouds: high coverage, bright white with grey undersides
   - sun: soft, reduced intensity, slightly cool
   - Tune all four keyframes (dawn/noon/dusk/night) so **no daytime horizon is orange** (current preset
     horizons are all warm: dawn `0.95,0.60,0.35`; noon `0.85,0.75,0.55`; dusk `0.90,0.45,0.25`).
   - Set as `TimeOfDayController.activePreset` and `BiomeSkyMapping.fallbackPreset`.
2. **Fog tracks the sky** (promotes the Extension-Points "Fog integration" row). Drive `RenderSettings.fogColor`
   (and optionally density) from the evaluated `SkySettings.horizonColor` each frame in `SkyController`
   (alongside the existing `PushSkyUniforms`). This is the core JC3-style hue-unification: haze always equals
   the horizon, so the ground plain + scatter + ground-plane impostor all dissolve into one matching band.
   - Supersedes the static color set once at startup in `DotsSystemBootstrap.ApplyDistanceFog` (keep config
     for enable/mode/density; let the sky own the color). Fog enable + Exp² + impostor fog support already
     landed under V1/V2.
3. **Populate + assign `DefaultBiomeSkyMapping.asset`** — Plains entry + fallback = Cloudbreak; assign to the
   `TimeOfDayController`. Makes the biome path live and correct for one biome, ready to extend.

### 9.2 Deferred seam (multi-biome)

Nothing calls `TimeOfDayController.ApplyBiome()` yet — there is no DOTS→sky "current biome" signal. Build this
managed bridge when a second biome exists to transition to; verify at that point how the current biome is
represented/detected at runtime. Until then the fallback (Cloudbreak) is always active.

> **Generalization (2026-07-02).** The `_driveFogColor` coupling below is the first instance of a broader
> pattern: one authority evaluates the palette and every distance-facing surface consumes it. That
> generalization — global `_Atmo*` uniforms + a shared aerial-perspective HLSL consumed by the ground disc,
> mountain impostor, and terrain tint — is specced in
> [ATMOSPHERE_COLOR_AUTHORITY_SPEC.md](ATMOSPHERE_COLOR_AUTHORITY_SPEC.md) (ticket **V9**). `TimeOfDayController`
> is the intended home for that authority.

### 9.3 Decision log

- Keep the **live day/night cycle** for the vista rather than pinning a fixed time — fog-tracks-sky makes the
  cycle read correctly at all times, removing the reason to pin. (2026-07-01)
- Sky/haze colors are **biome-dependent** by design (per `BiomeSkyMapping`). (2026-07-01)
- Highlands fiction retained; the ocean/archipelago approach to hiding the impostor edge was explored and
  **rejected**. (2026-07-01)

---

## References

- [TERRAIN_ECS_NEXT_STEPS_SPEC.md](../Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md) — SDF terrain pipeline (sky must not depend on terrain height)
- [BIOME_GRASS_STREAMING_MVP_PLAN.md](../Biomes/BIOME_GRASS_STREAMING_MVP_PLAN.md) — biome streaming context for future sky color hookup
- [MASTER_PLAN.md](../MASTER_PLAN.md) — overall project roadmap
