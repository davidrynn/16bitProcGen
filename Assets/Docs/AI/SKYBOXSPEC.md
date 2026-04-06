# Minimal Procedural Gradient Sky — Technical Spec

_Status: PHASE 2 COMPLETE_
_Last updated: 2026-04-06_
_Depends on: [SKYBOXPLAN.md](SKYBOXPLAN.md)_

---

## 1. Overview

This spec defines the exact data structures, shader interface, coordinate space, gradient math, and data flow for the Phase 1 procedural gradient sky. Everything below is implementation-ready; no design decisions are left open except the Renderer Feature vs. Skybox Material choice noted in the plan.

---

## 2. Data Model

### 2.1 SkySettings Struct

A pure, serializable, value-type struct with no managed references. Usable from MonoBehaviours, ECS components, or ScriptableObjects without modification.

```csharp
namespace DOTS.Rendering.Sky
{
    [System.Serializable]
    public struct SkySettings
    {
        /// Bottom color of the gradient (horizon).
        public Color horizonColor;

        /// Top color of the gradient (zenith / straight up).
        public Color zenithColor;

        /// Controls gradient curve sharpness.
        /// 1.0 = linear. >1.0 = more color near horizon. <1.0 = more color near zenith.
        /// Clamped to [0.01, 10.0] at consumption time.
        public float gradientExponent;

        /// Vertical offset for the horizon line in normalized view-direction space.
        /// 0.0 = geometric horizon (view direction perpendicular to up).
        /// Positive values push the horizon upward.
        /// Clamped to [-0.5, 0.5] at consumption time.
        public float horizonHeight;
    }
}
```

**Defaults (used when no explicit value is set):**

| Field | Default | Unit |
|-------|---------|------|
| `horizonColor` | `(0.85, 0.75, 0.55, 1.0)` — warm gold | Linear color |
| `zenithColor` | `(0.30, 0.50, 0.80, 1.0)` — calm blue | Linear color |
| `gradientExponent` | `1.0` | Unitless |
| `horizonHeight` | `0.0` | Normalized [-0.5, 0.5] |

### 2.2 Shader Property IDs

All properties are set via `MaterialPropertyBlock` or `Material.SetX` using these exact names:

| Shader Property | C# Constant Name | Type |
|-----------------|-------------------|------|
| `_HorizonColor` | `ShaderIDs.HorizonColor` | `Color` (linear) |
| `_ZenithColor` | `ShaderIDs.ZenithColor` | `Color` (linear) |
| `_GradientExponent` | `ShaderIDs.GradientExponent` | `float` |
| `_HorizonHeight` | `ShaderIDs.HorizonHeight` | `float` |

```csharp
namespace DOTS.Rendering.Sky
{
    public static class ShaderIDs
    {
        public static readonly int HorizonColor     = Shader.PropertyToID("_HorizonColor");
        public static readonly int ZenithColor      = Shader.PropertyToID("_ZenithColor");
        public static readonly int GradientExponent = Shader.PropertyToID("_GradientExponent");
        public static readonly int HorizonHeight    = Shader.PropertyToID("_HorizonHeight");
    }
}
```

---

## 3. Coordinate Space

### 3.1 Choice: View-Direction Space

The gradient is computed from the **normalized view direction** of each fragment, specifically its **vertical (Y) component** after normalization.

### 3.2 Rationale

| Alternative | Problem |
|-------------|---------|
| World-space Y position | Sky color changes as camera moves vertically; breaks with infinite terrain |
| Screen-space UV.y | Gradient shifts when camera pitch changes; horizon doesn't stay fixed |
| View-direction Y | Gradient is locked to the camera's orientation; stable under translation; horizon stays at the geometric horizon regardless of player position |

### 3.3 Computing View Direction in the Shader

For a fullscreen triangle / quad pass:

```hlsl
// In vertex shader or fragment shader:
// clipPos = the clip-space position of the fullscreen vertex
float4 viewDir = mul(unity_MatrixInvVP, float4(clipPos.xy, 1.0, 1.0));
viewDir.xyz /= viewDir.w;
float3 dir = normalize(viewDir.xyz - _WorldSpaceCameraPos.xyz);
float y = dir.y; // vertical component, range [-1, 1]
```

For a skybox mesh approach, the view direction comes from the vertex position on the unit cube/sphere, transformed by the inverse view matrix.

---

## 4. Gradient Math

### 4.1 Height Parameterization

Given the vertical component $y$ of the normalized view direction (range $[-1, 1]$):

$$
t(y) = \text{saturate}\!\left(\frac{y - h_0}{h_1 - h_0}\right)
$$

Where:
- $y$ = vertical component of the normalized view direction vector
- $h_0$ = `horizonHeight` (the Y value at which the gradient begins; default `0.0`)
- $h_1$ = zenith height, fixed at `1.0` (straight up)

**Behavior:**
- At $y = h_0$: $t = 0$ → pure `horizonColor`
- At $y = 1$: $t = 1$ → pure `zenithColor`
- At $y < h_0$: $t = 0$ → `horizonColor` extends below the horizon (no mirroring)

### 4.2 Exponent Curve

$$
t' = t^{k}
$$

Where $k$ = `gradientExponent`, clamped to $[0.01, 10.0]$.

| $k$ value | Visual effect |
|-----------|---------------|
| `0.25` | Zenith color dominates; rapid transition near horizon |
| `1.0` | Linear gradient |
| `2.0` | Horizon color extends further upward; transition near zenith |
| `4.0` | Strongly horizon-heavy; zenith color only at very top |

### 4.3 Color Interpolation

$$
C_{\text{sky}} = \text{lerp}\!\left(C_{\text{horizon}},\; C_{\text{zenith}},\; t'\right)
$$

Expanded per-channel:

$$
C_{\text{sky}} = C_{\text{horizon}} \cdot (1 - t') + C_{\text{zenith}} \cdot t'
$$

All colors are in **linear space**. The URP pipeline handles the final linear-to-sRGB conversion.

### 4.4 Below-Horizon Behavior

When $y < h_0$, the `saturate` clamp yields $t = 0$, so the fragment is pure `horizonColor`. This is intentional: the area below the horizon is covered by terrain geometry and is rarely visible. A solid horizon color avoids artifacts if the camera briefly sees below the terrain edge.

---

## 5. Shader Specification

### 5.1 Shader Properties Block

```hlsl
Properties
{
    _HorizonColor     ("Horizon Color", Color)  = (0.85, 0.75, 0.55, 1.0)
    _ZenithColor      ("Zenith Color", Color)   = (0.30, 0.50, 0.80, 1.0)
    _GradientExponent ("Gradient Exponent", Range(0.01, 10.0)) = 1.0
    _HorizonHeight    ("Horizon Height", Range(-0.5, 0.5)) = 0.0
}
```

### 5.2 Tags and Render State

```hlsl
Tags
{
    "RenderType" = "Background"
    "Queue" = "Background"
    "RenderPipeline" = "UniversalPipeline"
}

ZWrite Off
Cull Off
```

- **Queue = Background**: renders before all opaque geometry.
- **ZWrite Off**: sky is infinitely far; never writes depth.
- **Cull Off**: fullscreen triangle has no meaningful face orientation.

### 5.3 Fragment Shader Pseudocode

```hlsl
half4 frag(Varyings input) : SV_Target
{
    float3 viewDir = normalize(input.viewDirWS);
    float y = viewDir.y;

    float h0 = _HorizonHeight;
    float h1 = 1.0;

    float t = saturate((y - h0) / (h1 - h0));
    t = pow(t, _GradientExponent);

    half4 color = lerp(_HorizonColor, _ZenithColor, t);
    return color;
}
```

**Instruction count estimate:** ~8-10 ALU ops. No texture fetches. No branching. Constant-time.

### 5.4 Anti-Banding (Optional Enhancement)

If banding is visible on 8-bit displays, add screen-space dithering:

```hlsl
// Interleaved gradient noise (Jimenez 2014)
float dither = frac(52.9829189 * frac(dot(input.positionCS.xy, float2(0.06711056, 0.00583715))));
color.rgb += (dither - 0.5) / 255.0;
```

This adds < 3 ALU ops and eliminates visible banding.

---

## 6. Data Flow

### 6.1 Initialization Flow

```
1. Scene loads
2. SkyController.Awake()
   a. Load or create SkySettings (from ScriptableObject or hardcoded defaults)
   b. Find or create Material with ProceduralGradientSky shader
   c. Push SkySettings → Material uniforms (once)
3. Sky renders on first frame
```

### 6.2 Runtime Update Flow

```
1. SkyController detects SkySettings changed (dirty flag or inspector edit)
2. Clamp values:
   a. gradientExponent = clamp(gradientExponent, 0.01, 10.0)
   b. horizonHeight    = clamp(horizonHeight, -0.5, 0.5)
3. Push four uniforms to Material:
   a. material.SetColor(ShaderIDs.HorizonColor, settings.horizonColor)
   b. material.SetColor(ShaderIDs.ZenithColor, settings.zenithColor)
   c. material.SetFloat(ShaderIDs.GradientExponent, settings.gradientExponent)
   d. material.SetFloat(ShaderIDs.HorizonHeight, settings.horizonHeight)
4. No per-frame work if SkySettings has not changed
```

### 6.3 Future Biome Integration Flow (DOTS-driven, design target)

```
1. DOTS biome system determines current biome context for player position
2. DOTS bridge/adapter raises a lightweight biome signal (BiomeType)
3. `TimeOfDayController.ApplyBiome(BiomeType)` resolves biome → `SkyPreset` (+ optional cloud override)
4. `TimeOfDayController` lerps current sky/cloud state → target state over N seconds
5. Uniform push occurs per-frame during transition only
```

This flow intentionally avoids a dependency on `LegacyWeatherSystem`. The only required runtime seam is an external caller invoking `TimeOfDayController.ApplyBiome(BiomeType)`.

---

## 7. Integration Points

### 7.1 SkyController (MonoBehaviour)

| Responsibility | Details |
|---------------|---------|
| Owns sky `Material` | Created at startup or assigned in Inspector |
| Applies runtime data | Receives evaluated sky/cloud data from `TimeOfDayController` |
| Pushes uniforms | When runtime sky/cloud state changes |
| No biome logic | Biome resolution stays in `TimeOfDayController`/mapping layer |

### 7.2 Cloud Keyword Integration

Clouds are controlled via shader keyword `_CLOUDS_ON` on the same sky material:

| Responsibility | Details |
|---------------|--------|
| Keyword toggle | `SkyController.CloudsEnabled` calls `EnableKeyword`/`DisableKeyword` |
| Cloud uniforms | Pushed to the same material alongside gradient uniforms |
| No second pass | Clouds composited inside the fragment shader via alpha blend |
| Procedural noise | 4-octave FBM value noise, no texture dependencies |

### 7.3 Camera Configuration

- Camera clear flags: **Solid Color** or **Don't Clear** (the sky pass covers the background).
- If using skybox material approach: Camera clear flags → **Skybox**, material assigned to `RenderSettings.skybox`.

### 7.4 Biome Hook (Future DOTS)

- `TimeOfDayController.ApplyBiome(BiomeType biome, float durationSeconds = -1f)` is the stable hook for biome-driven sky changes.
- Current state: no hard dependency on legacy weather systems.
- Future DOTS implementation should call this method from a MonoBehaviour bridge that listens to ECS biome state changes.

---

## 8. File Layout

```
Assets/
├── Scripts/
│   └── Rendering/
│       └── Sky/
│           ├── SkySettings.cs            // Pure data struct (gradient params)
│           ├── CloudSettings.cs          // Pure data struct (cloud params)
│           ├── ShaderIDs.cs              // Static shader property ID cache
│           ├── SkyController.cs          // MonoBehaviour — pushes uniforms, keyword toggle
│           ├── SkyPreset.cs              // ScriptableObject — 4 keyframes (dawn/noon/dusk/night)
│           ├── TimeOfDayController.cs    // MonoBehaviour — drives time cycle + biome transitions
│           ├── BiomeSkyMapping.cs        // ScriptableObject — biome → SkyPreset lookup
│           └── Editor/
│               └── SkyAssetCreator.cs    // Editor menu items for asset creation
├── Resources/
│   ├── Shaders/
│   │   └── ProceduralGradientSky.shader  // URP sky shader (gradient + optional clouds via _CLOUDS_ON)
│   ├── Materials/
│   │   └── ProceduralGradientSky.mat    // Material asset
│   └── Sky/
│       ├── DefaultSkyPreset.asset       // Default time-of-day keyframes
│       └── DefaultBiomeSkyMapping.asset // Biome → preset lookup
└── Docs/
    └── AI/
        ├── SKYBOXPLAN.md
        ├── SKYBOXSPEC.md
        └── SKYBOXTESTS.md
```

---

## 9. Assumptions

1. The project uses URP (Universal Render Pipeline) with linear color space.
2. The camera never needs to render below the geometric horizon for extended periods (terrain fills below-horizon).
3. A single sky configuration is active at a time (no split-screen with different skies).
4. `SkySettings` changes are infrequent (biome transitions, not per-frame animation).
5. The shader runs on Shader Model 3.0+ hardware.
6. Biome-driven sky updates will be sourced by DOTS systems, not `LegacyWeatherSystem`.

---

## 10. Determinism Guarantee

Given identical `SkySettings` values:
- The same pixel on screen produces the same color every frame.
- Moving the camera in translation (not rotation) does not change the sky.
- No random values, noise textures, or time-dependent inputs in Phase 1.
- Reproducible across platforms (standard `pow`, `lerp`, `saturate` intrinsics).

---

## References

- [SKYBOXPLAN.md](SKYBOXPLAN.md) — scope, architecture, risks
- [SKYBOXTESTS.md](SKYBOXTESTS.md) — validation test plan
- [TERRAIN_ECS_NEXT_STEPS_SPEC.md](TERRAIN_ECS_NEXT_STEPS_SPEC.md) — terrain pipeline (sky is independent)
- [BIOME_GRASS_STREAMING_MVP_PLAN.md](BIOME_GRASS_STREAMING_MVP_PLAN.md) — biome context for future Phase 2
