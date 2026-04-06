# Minimal Procedural Gradient Sky — Test Plan

_Status: DESIGN — Phase 1 test definitions_
_Last updated: 2026-04-06_
_Depends on: [SKYBOXSPEC.md](SKYBOXSPEC.md)_

---

## 1. Overview

This document defines all validation tests for the Phase 1 procedural gradient sky. Tests are grouped into four categories: **visual**, **parameter**, **integration**, and **performance**. Each test specifies inputs, expected behavior, and pass/fail criteria.

Tests are designed so that:
- **Parameter tests** and **math tests** are pure C# (EditMode, NUnit).
- **Visual tests** and **integration tests** are PlayMode or manual inspection.
- **Performance tests** are profiler-based spot checks.

---

## 2. Visual Tests

Visual tests verify the rendered sky looks correct and remains stable under typical gameplay conditions.

### 2.1 Gradient Stable During Camera Translation

| Field | Value |
|-------|-------|
| **Setup** | Default `SkySettings`. Camera at world origin. |
| **Action** | Translate camera by `(1000, 50, 1000)` units over 5 seconds. |
| **Expected** | Sky gradient appears identical in every frame. No color shift, no jitter. |
| **Pass criteria** | Side-by-side screenshots at start and end show no perceptible difference. |
| **Type** | PlayMode / Manual |

### 2.2 Gradient Stable During Camera Rotation

| Field | Value |
|-------|-------|
| **Setup** | Default `SkySettings`. Camera at origin facing forward. |
| **Action** | Rotate camera 360° around Y axis, then ±45° pitch. |
| **Expected** | Gradient wraps smoothly around the horizon. No seams, no discontinuities. Zenith is always directly overhead. |
| **Pass criteria** | Manual inspection, or automated: sample pixel at screen center at 0° pitch → matches `zenithColor` (up) or gradient midpoint (forward). |
| **Type** | PlayMode / Manual |

### 2.3 No Visible Banding

| Field | Value |
|-------|-------|
| **Setup** | `horizonColor = (0.1, 0.1, 0.1)`, `zenithColor = (0.12, 0.12, 0.12)` (low contrast, worst case for banding). `gradientExponent = 1.0`. |
| **Action** | Render at 1080p, inspect gradient region. |
| **Expected** | No visible stair-stepping / color bands. If dithering is enabled, bands are imperceptible. |
| **Pass criteria** | Manual inspection on 8-bit sRGB display. |
| **Type** | Manual |

### 2.4 Horizon Remains Fixed Under Translation

| Field | Value |
|-------|-------|
| **Setup** | Default `SkySettings`. Camera facing horizontal (pitch = 0). |
| **Action** | Move camera up 500 units, then down 500 units. |
| **Expected** | The gradient's horizon line (where `horizonColor` begins blending) stays at the same screen position relative to camera pitch. |
| **Pass criteria** | Pixel at screen center (pitch = 0) produces the same color ± 1/255 before and after translation. |
| **Type** | PlayMode / Automated |

### 2.5 Consistent Across Chunk Streaming

| Field | Value |
|-------|-------|
| **Setup** | Terrain chunk streaming enabled. Default `SkySettings`. |
| **Action** | Walk across 10+ chunk boundaries. Chunks load and unload. |
| **Expected** | Sky is completely unaffected by chunk lifecycle. No flicker, no color change, no frame drops. |
| **Pass criteria** | Manual observation. |
| **Type** | PlayMode / Manual |

---

## 3. Parameter Tests

Pure data tests on `SkySettings` → shader input mapping. Runnable in EditMode (no scene required).

### 3.1 Gradient Exponent Extremes

| Test Case | `gradientExponent` | Expected `t'` at `y = 0.5` (midpoint) |
|-----------|-------------------|----------------------------------------|
| Linear | `1.0` | `0.5` |
| Near-zero (clamped to 0.01) | `0.0` → clamped to `0.01` | `≈ 0.993` (nearly all zenith) |
| High exponent | `10.0` | `≈ 0.00098` (nearly all horizon) |
| Negative (clamped to 0.01) | `-2.0` → clamped to `0.01` | `≈ 0.993` |

**Pass criteria:** Computed `t'` matches expected value within `±0.001`.

### 3.2 Gradient Exponent — Visual Character

| `gradientExponent` | Visual description |
|--------------------|--------------------|
| `0.5` | Sky quickly becomes zenith color; horizon band is thin |
| `1.0` | Even, linear blend |
| `2.0` | Horizon color extends ~70% up the sky |
| `4.0` | Almost entirely horizon color with thin zenith cap |

**Type:** Manual visual spot check.

### 3.3 Color Edge Cases

| Test Case | Input | Expected |
|-----------|-------|----------|
| Black to white | `horizon = (0,0,0,1)`, `zenith = (1,1,1,1)` | Clean grayscale gradient, no NaN or artifacts |
| Same color | `horizon = zenith = (0.5, 0.5, 0.8, 1)` | Solid flat color, no artifacts |
| Full alpha transparency | `horizon.a = 0`, `zenith.a = 0` | Transparent sky (if blending enabled); no crash |
| HDR values | `horizon = (2,1,0,1)` | Renders without error; tonemapping handles output |
| Near-black | `horizon = zenith = (0.001, 0.001, 0.001, 1)` | Very dark sky; no NaN |

**Pass criteria:** No shader errors, no `NaN` pixels, output color within expected range.

### 3.4 Horizon Height Offset

| `horizonHeight` | Expected behavior |
|-----------------|-------------------|
| `0.0` (default) | Gradient starts at geometric horizon (`viewDir.y = 0`) |
| `0.3` | Gradient starts higher; more of the lower sky is pure `horizonColor` |
| `-0.3` | Gradient starts below horizon; zenith color reaches further down |
| `0.5` (max) | Nearly the entire sky is `horizonColor`; only directly overhead is zenith |
| `-0.5` (min) | Gradient fills almost entire view even when looking slightly downward |
| Out of range (`1.0`) | Clamped to `0.5` before shader push |

**Pass criteria:** Computed `t` at `y = h0` equals `0.0`. Clamping applied correctly.

### 3.5 SkySettings Defaults

| Test Case | Expected |
|-----------|----------|
| Default-constructed `SkySettings` | All fields at specified defaults from spec |
| Serialization round-trip | JsonUtility or binary → deserialize → fields match original |

**Type:** EditMode / NUnit.

---

## 4. Integration Tests

Tests verifying the sky works correctly alongside other game systems.

### 4.1 Works With Terrain Chunk Loading

| Field | Value |
|-------|-------|
| **Setup** | Sky active. Terrain generation system running. |
| **Action** | Camera moves to trigger chunk generation and destruction. |
| **Expected** | Sky rendering is completely independent of terrain lifecycle. No frame spikes attributable to sky, no visual changes. |
| **Pass criteria** | Profiler shows zero correlation between chunk events and sky render time. |
| **Type** | PlayMode |

### 4.2 No Dependency on Terrain Height

| Field | Value |
|-------|-------|
| **Setup** | Sky active. No terrain loaded (empty scene). |
| **Action** | Camera looks up, down, around. |
| **Expected** | Sky renders correctly. No errors, no dependency on any terrain component. |
| **Pass criteria** | Sky visible and correct with zero entities containing terrain components. |
| **Type** | PlayMode / Automated |

### 4.3 Works After Player Teleport

| Field | Value |
|-------|-------|
| **Setup** | Camera at `(0, 10, 0)`. Default `SkySettings`. |
| **Action** | Teleport camera to `(50000, 500, 50000)` in a single frame. |
| **Expected** | Sky is visually identical before and after teleport. No flicker, no 1-frame glitch. |
| **Pass criteria** | Screenshot comparison ± 1/255 per channel at matching pitch/yaw. |
| **Type** | PlayMode / Automated |

### 4.4 Works With URP Post-Processing

| Field | Value |
|-------|-------|
| **Setup** | URP post-processing volume active (bloom, color grading). |
| **Action** | Render scene with sky. |
| **Expected** | Post-processing applies to sky output correctly. No render order issues. |
| **Pass criteria** | Bloom visible on bright horizon; color grading affects sky. |
| **Type** | PlayMode / Manual |

### 4.5 SkyController Absent — Graceful Fallback

| Field | Value |
|-------|-------|
| **Setup** | Scene with sky shader/material but no `SkyController` in scene. |
| **Action** | Enter play mode. |
| **Expected** | Sky renders with shader default values. No `NullReferenceException`. |
| **Pass criteria** | No errors in console. Sky visible with built-in property defaults. |
| **Type** | PlayMode / Automated |

---

## 5. Performance Tests

### 5.1 Shader GPU Cost

| Field | Value |
|-------|-------|
| **Setup** | 1080p, sky only (no terrain). |
| **Measure** | GPU time for sky pass via Frame Debugger / RenderDoc. |
| **Target** | < 0.05 ms on integrated GPU (Intel UHD 630 class). |
| **Pass criteria** | Measured GPU time below threshold. |
| **Type** | Manual / Profiler |

### 5.2 No CPU Overhead When Settings Unchanged

| Field | Value |
|-------|-------|
| **Setup** | `SkyController` active, `SkySettings` not changed after init. |
| **Measure** | CPU profiler on `SkyController.Update()` (if exists) or LateUpdate. |
| **Target** | 0 allocations. ≤ 0.001 ms per frame. |
| **Pass criteria** | Profiler shows no GC alloc, negligible CPU time. |
| **Type** | PlayMode / Profiler |

### 5.3 Uniform Push Cost on Change

| Field | Value |
|-------|-------|
| **Setup** | Trigger `SkySettings` change every frame for 100 frames. |
| **Measure** | CPU time for `Material.SetColor` / `Material.SetFloat` calls. |
| **Target** | < 0.01 ms per change. 0 allocations. |
| **Pass criteria** | Profiler confirms target. |
| **Type** | PlayMode / Profiler |

### 5.4 No Frame Rate Impact in Full Scene

| Field | Value |
|-------|-------|
| **Setup** | Full scene: terrain, player, sky. |
| **Measure** | FPS with sky enabled vs. disabled (camera clear to solid color). |
| **Target** | Difference < 0.5 ms at 1080p. |
| **Pass criteria** | A/B comparison. |
| **Type** | PlayMode / Profiler |

---

## 6. Test Implementation Notes

### 6.1 EditMode Tests (NUnit)

Location: `Assets/Scripts/Rendering/Sky/Tests/Editor/`

Covers:
- Section 3.1 (exponent extremes — pure math)
- Section 3.3 (color edge cases — struct validation)
- Section 3.4 (horizon height clamping — pure math)
- Section 3.5 (defaults and serialization)

These tests instantiate `SkySettings`, compute expected `t'` values using the spec's formulas, and assert results. No scene, no GPU.

### 6.2 PlayMode Tests (NUnit + Unity Test Framework)

Location: `Assets/Scripts/Rendering/Sky/Tests/PlayMode/`

Covers:
- Section 2.4 (horizon stability — automated pixel sample)
- Section 4.2 (no terrain dependency)
- Section 4.3 (teleport stability)
- Section 4.5 (graceful fallback)

These tests create a minimal scene programmatically (camera + sky material), render one or more frames, and assert pixel values or absence of errors.

### 6.3 Manual Tests

Covers:
- Section 2.1, 2.2, 2.3, 2.5 (visual quality)
- Section 3.2 (visual character of exponents)
- Section 4.1, 4.4 (integration with full systems)
- Section 5.x (profiler measurements)

Documented as a checklist for manual QA passes.

---

## 7. Manual QA Checklist

Run this checklist before merging any sky-related change:

- [ ] Sky renders a smooth gradient from horizon to zenith
- [ ] Camera translation does not shift gradient
- [ ] Camera rotation wraps gradient smoothly (no seams)
- [ ] No visible banding at 1080p on 8-bit display
- [ ] Gradient exponent slider produces expected visual changes in Inspector
- [ ] Horizon height slider shifts gradient origin up/down
- [ ] Sky renders without terrain in scene
- [ ] Sky unaffected by chunk loading/unloading
- [ ] No console errors or warnings from sky system
- [ ] GPU cost < 0.05 ms in Frame Debugger
- [ ] No GC allocations from sky system in Profiler

---

## References

- [SKYBOXSPEC.md](SKYBOXSPEC.md) — data model and gradient math definitions
- [SKYBOXPLAN.md](SKYBOXPLAN.md) — scope and architecture
- [Testing_Documentation.md](../../Scripts/DOTS/Test/Testing_Documentation.md) — project test harness catalog
