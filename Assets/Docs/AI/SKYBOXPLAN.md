# Minimal Procedural Gradient Sky — Plan

_Status: PHASE 2 COMPLETE_
_Last updated: 2026-04-06_

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
| Biome color variation | `BiomeSkyMapping` ScriptableObject + `TimeOfDayController.TransitionToPreset()` | **Phase 2 — Done** |
| Time-of-day | `SkyPreset` keyframes (dawn/noon/dusk/night), `TimeOfDayController` lerps by normalized time | **Phase 2 — Done** |
| Cloud layer | `_CLOUDS_ON` shader keyword in `ProceduralGradientSky.shader`; procedural FBM noise, no textures | **Phase 2 — Done** |
| Star field | Additional pass behind gradient with point sprites or noise | Phase 3+ |
| Fog integration | Share `horizonColor` with URP fog settings | Phase 3+ |

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

## References

- [TERRAIN_ECS_NEXT_STEPS_SPEC.md](TERRAIN_ECS_NEXT_STEPS_SPEC.md) — SDF terrain pipeline (sky must not depend on terrain height)
- [BIOME_GRASS_STREAMING_MVP_PLAN.md](BIOME_GRASS_STREAMING_MVP_PLAN.md) — biome streaming context for future sky color hookup
- [MASTER_PLAN.md](../MASTER_PLAN.md) — overall project roadmap
