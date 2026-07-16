# Ground Plane Impostor Spec

**Status:** IMPLEMENTED — Mid-field variation planned (§12, ticket V17)
**Phase Fit:** Phase 1 (required for sky-drop intro sequence)
**Last Updated:** 2026-07-09
**Complements:** [`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`](HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) (vertical ring, Phase 2)

---

## 1. Purpose

Render a large flat terrain-colored plane on the XZ axis beyond the procedurally generated chunk radius, so that when the player is at high altitude (e.g. spawning from a sky drop) the world appears to extend to the horizon in all directions rather than ending at a void boundary.

This solves a specific gap in the current architecture: SDF + Surface Nets terrain chunks only exist within ~256u of the player. Beyond that radius there is nothing. From ground level this is hidden by fog. From 300–500m altitude the void is fully visible in a 120° downward FOV.

**Current state:** Core implementation is live and rendering. Next steps: (1) impostor-native XZ-distance haze to replace scene fog (§4.4); (2) day/night lighting response via ambient SH + main light (§4.5).

---

## 2. Industry Context

Known as a **skirt mesh**, **ground plane impostor**, or **infinite ground plane**. Used in:
- No Man's Sky — curved ground impostor for planetary surface from orbit
- Microsoft Flight Simulator — tiered base-layer ground tiles beneath streamed terrain
- Sea of Thieves — distant-island plane beyond stream radius

The flat-plane variant (as opposed to a curved planet surface) is correct for this world scale and camera altitude range (0–600m).

---

## 3. Relationship to Horizon Ring Impostor

| System | Plane | Problem solved | Priority |
|--------|-------|---------------|---------|
| **Ground Plane Impostor** (this spec) | XZ horizontal | "What's beneath me at distance" | MVP — needed for sky drop |
| Horizon Ring Impostor (`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`) | Vertical cylinder | "What's on the horizon silhouette" | Phase 2 |

Build the ground plane first. The ring sits on top of it. Together they produce a convincing world from any altitude.

---

## 4. Visual Design

### 4.1 Camera Perspective

Primary use: player at 300–500m altitude looking mostly downward during a 10-second sky drop.
Secondary use: player at ground level (plane is occluded by terrain and invisible inside inner fade).

From 400m altitude with 70° FOV:
- Visible radius at the horizon: ~1100u
- Near boundary (chunk edge): ~256u
- The impostor covers the annular region 256u → 1500u

### 4.2 Shading

The impostor must produce the same large-scale color distribution as the real terrain: green grass fields, grey rock patches, brown dirt — the same visual language seen at ground level, just reduced to color blobs at distance.

**Approach:** Procedural world-space shader sampling the same noise octaves as the terrain biome system. No texture asset. Two octaves of FBM value noise in world XZ, thresholded to biome color outputs.

Benefits:
- Color-correct match at the seam with real terrain (same noise source)
- No baking step or texture regeneration when seed/player position changes
- At viewing distance (300m+) the pixel density is ~0.5–1 world unit per pixel — texture detail is wasted

### 4.3 The Seam (Transition to Real Chunks)

The inner edge of the impostor (at ~256u from player) overlaps with the outermost real terrain chunks. Transition strategy:

1. **Radial alpha fade (inner):** the impostor fades in over a 40u inner band (256u → 296u from player XZ center). The real terrain is opaque underneath — no visible gap.
2. **Haze fade (outer):** the impostor fades toward a `_HazeColor` starting at ~900u, reaching full haze by ~1400u. This dissolves the disc edge so there is no hard boundary against the sky (see §4.4).
3. **Y placement:** Impostor sits at a fixed world Y approximating the median terrain height (default: `y = 0`, configurable). From 400m altitude the vertical parallax error over hills is imperceptible.

### 4.4 Atmospheric Haze — Impostor-Native (New Direction)

**Decision:** The impostor disc owns its own haze rather than relying on Unity's scene fog.

**Why scene fog was rejected:**
- Unity's `ExponentialSquared` fog is 3D camera-distance based. At 400m altitude, the disc sits ~500m from the camera — `exp(-(0.007 × 500)²) ≈ 0.000005`. The disc is completely invisible.
- Scene fog cannot distinguish "far from camera vertically" from "far from camera horizontally." It works for ground-level horizon haze but breaks entirely for altitude views.
- The fog color (warm orange/tan) creates a jarring seam at the impostor boundary when the disc has no fog applied.

**Impostor-native haze approach:**
- `_OuterFadeStart` / `_OuterFadeEnd` shader properties control the fade zone in XZ world distance from the player.
- `_HazeColor` property (to be added) matches the sky horizon tint — set independently of Unity's fog system.
- At ground level: haze dissolves the disc edge so there is no visible ring at the 1400u boundary.
- From altitude: the fade is XZ-distance-based, so the disc remains fully visible regardless of camera height.

**Scene fog (`EnableDistanceFog`):** Disable once the impostor haze is tuned. The impostor handles distant haze for the area it covers. Terrain geometry within 256u gets whatever post-process or ambient occlusion treatment is appropriate there.

Shader fade zone defaults:
| Property | Default | Description |
|----------|---------|-------------|
| `_InnerFadeStart` | 256u | Disc begins fading in (inside = real terrain) |
| `_InnerFadeEnd` | 296u | Disc at full opacity |
| `_OuterFadeStart` | 900u | Disc begins fading toward haze color |
| `_OuterFadeEnd` | 1400u | Disc fully dissolved into haze |

### 4.5 Day/Night Lighting Response

The disc currently outputs a flat unlit biome colour. This looks wrong once a day/night cycle exists — at night the disc stays fully bright while the rest of the world darkens, creating a glowing ground plane.

**Requirement:** disc colour must scale with the world's ambient light and sun elevation, with zero coupling to whatever day/night system is eventually implemented.

**Approach — ambient SH + main directional light:**
```hlsl
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Ambient sky contribution to an upward-facing (+Y) surface.
// SampleSH reads Unity's spherical harmonic probes, which any day/night system
// updates automatically via RenderSettings or the sky material.
half3 ambient = SampleSH(half3(0, 1, 0));

// Sun contribution: flat disc has constant +Y normal, so illumination = sun elevation.
// When the sun dips below the horizon, sunElevation → 0 and this term drops out.
Light mainLight  = GetMainLight();
float sunElevation = saturate(mainLight.direction.y);
half3 sunContrib   = half3(mainLight.color) * sunElevation;

half3 litColor = (half3)biomeColor * saturate(ambient + sunContrib);
```

**Why this decouples cleanly:**
- `SampleSH` and `GetMainLight` are standard URP built-ins, updated every frame by the engine from `RenderSettings` / the active directional light.
- Any day/night implementation — whether it's a rotating `GameObject`, a `WeatherSystem` extension, or a shader-driven sky — ultimately writes to the same ambient and directional light state.
- No component reference, no event subscription, no per-frame system callback needed.

**Minimum ambient floor:** apply a small `max(ambient, 0.05)` so the disc is never fully black at midnight — matches how real terrain surfaces behave under moonlight/stars.

**No day/night system exists yet** in this project. The shader change can be landed now so the disc is ready when one is added. When implemented, test:
- Midday: disc is full terrain colour, well lit
- Sunset: disc takes on warm orange tint from sun angle + ambient sky
- Night: disc darkens proportionally to terrain geometry

---

## 5. Technical Design

### 5.1 Mesh

A single full disc mesh in the XZ plane:
- Outer radius: 1500u
- Subdivisions: 64×64 uniform grid (not polar — world-space noise sampling stays consistent)
- Generated at runtime once in `GroundPlaneImpostorBootstrap.Start()`
- Winding: `v0, v2, v1` gives +Y normal, visible from above with `Cull Back`
- No UVs needed — shader uses world XZ position for noise sampling

### 5.2 ECS Architecture

**Namespace:** `DOTS.Impostors` (not `DOTS.World` — avoids shadowing `Unity.Entities.World`)

**Component: `GroundPlaneImpostorTag`**
Marker tag. One entity in the world.

**Managed component: `GroundPlaneImpostorConfig`** (class IComponentData)
```csharp
class GroundPlaneImpostorConfig : IComponentData
{
    public float    WorldY;
    public float    InnerFadeStart;
    public float    InnerFadeEnd;
    public float    OuterRadius;
    public Material ImpostorMaterial;  // runtime instance, enableInstancing = true
    public Mesh     ImpostorMesh;      // generated disc, passed to RenderMeshInstanced
}
```

**System: `GroundPlaneImpostorSystem`** (`PresentationSystemGroup`, `SystemBase`)
- Runs each frame in `PresentationSystemGroup`
- Reads player `LocalToWorld` position
- Pushes `_PlayerXZ`, `_InnerFadeStart`, `_InnerFadeEnd` to the material
- Issues `Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrixBuffer)`

**Why `Graphics.RenderMeshInstanced` instead of Entities.Graphics:**
Entities.Graphics uses `BatchRendererGroup` which mandates a `DOTS_INSTANCING_ON` shader variant on every custom shader it draws. Custom URP shaders do not include this variant by default, and adding it requires non-trivial changes. `Graphics.RenderMeshInstanced` bypasses `BatchRendererGroup` entirely — no variant required. The single draw call overhead is negligible for one disc.

**Requirement:** `material.enableInstancing = true` must be set on the runtime material. `Graphics.RenderMeshInstanced` throws `InvalidOperationException` if this flag is false, even when the shader has `#pragma multi_compile_instancing`. These are two independent requirements.

**Bootstrap: `GroundPlaneImpostorBootstrap`** (MonoBehaviour)
- `Start()`: finds shader by name, generates disc mesh, creates runtime material with `enableInstancing = true`, creates ECS entity
- Uses `[SerializeField] private bool _enabled = true` toggle (no `ProjectFeatureConfig` reference — avoids circular assembly dependency)
- Logs entity creation with `forceLog: true` so spawn is always visible in console

### 5.3 Shader

**File:** `Assets/Shaders/GroundPlaneImpostor.shader`
**Shader name:** `"Ground/GroundPlaneImpostor"`
**Queue:** `AlphaTest` (opaque-like, clips via `clip()`)

Key pragma choices:
- `#pragma multi_compile_instancing` — required for `RenderMeshInstanced`
- No `#pragma multi_compile_fog` — URP 3D fog breaks from altitude (see §4.4)

Properties in `CBUFFER_START(UnityPerMaterial)` (SRP Batcher compatible):
```hlsl
float4 _GrassColor;
float4 _RockColor;
float4 _PlayerXZ;      // .xy = player world XZ; set each frame by system
float  _NoiseScale;
float  _RockThreshold;
float  _InnerFadeStart;
float  _InnerFadeEnd;
float  _OuterFadeStart;  // present; blend inactive until _HazeColor added
float  _OuterFadeEnd;
// TODO (Phase D): float4 _HazeColor;
```

Fragment logic (current — Phase C):
```hlsl
float dist = length(IN.worldPos.xz - _PlayerXZ.xy);
float innerAlpha = smoothstep(_InnerFadeStart, _InnerFadeEnd, dist);
clip(innerAlpha - 0.01);

float3 biomeColor = lerp(_GrassColor.rgb, _RockColor.rgb,
                         step(_RockThreshold, FBM2(IN.worldPos.xz * _NoiseScale)));
return half4((half3)biomeColor, 1.0);  // flat, unlit
```

Fragment logic (target — Phase D, haze + day/night):
```hlsl
// Outer haze
float outerBlend = 1.0 - smoothstep(_OuterFadeStart, _OuterFadeEnd, dist);
half3 color = lerp(_HazeColor.rgb, (half3)biomeColor, outerBlend);

// Day/night — ambient SH + sun elevation (see §4.5)
half3 ambient    = max(SampleSH(half3(0,1,0)), half3(0.05,0.05,0.05));
Light mainLight  = GetMainLight();
half3 sunContrib = half3(mainLight.color) * saturate(mainLight.direction.y);
color *= saturate(ambient + sunContrib);

return half4(color, 1.0);
```

### 5.4 Feature Flag

`ProjectFeatureConfig.EnableGroundPlaneImpostor` (default: `true`) — controls system registration in `DotsSystemBootstrap`. The Bootstrap MonoBehaviour has its own `_enabled` bool for scene-level toggling without touching the config asset.

---

## 6. Files

| File | Status |
|------|--------|
| `Assets/Scripts/DOTS/World/GroundPlaneImpostorBootstrap.cs` | ✅ Implemented |
| `Assets/Scripts/DOTS/World/GroundPlaneImpostorSystem.cs` | ✅ Implemented |
| `Assets/Scripts/DOTS/World/GroundPlaneImpostorTag.cs` | ✅ Implemented |
| `Assets/Scripts/DOTS/World/GroundPlaneImpostorConfig.cs` | ✅ Implemented |
| `Assets/Shaders/GroundPlaneImpostor.shader` | ✅ Implemented |
| `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` | ✅ Updated |
| `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` | ✅ Updated |

No `.mat` file — material is created at runtime by the bootstrap.

---

## 7. Implementation Checklist

### Phase A — Scaffold ✅
- [x] Add `EnableGroundPlaneImpostor` to `ProjectFeatureConfig`
- [x] Create `GroundPlaneImpostorTag.cs`
- [x] Create `GroundPlaneImpostorConfig.cs`
- [x] Create `GroundPlaneImpostorBootstrap.cs`
- [x] Wire bootstrap into scene (`GroundPlaneImpostor` GameObject)
- [x] Register `GroundPlaneImpostorSystem` in `DotsSystemBootstrap`

### Phase B — Shader ✅
- [x] Create `GroundPlaneImpostor.shader` — URP unlit, no scene fog
- [x] Noise sampling in world XZ (2 octave FBM value noise)
- [x] Biome color blend (grass / rock threshold)
- [x] Radial inner alpha fade (smoothstep 256→296u)
- [x] Outer fade properties (`_OuterFadeStart`, `_OuterFadeEnd`) — properties present, blend pending haze color
- [x] `material.enableInstancing = true` in bootstrap

### Phase C — Runtime System ✅
- [x] `GroundPlaneImpostorSystem` in `PresentationSystemGroup`
- [x] Player XZ tracked each frame via `LocalToWorld`
- [x] `Graphics.RenderMeshInstanced` draw call with cached `Matrix4x4[]` buffer

### Phase D — Haze + Day/Night Lighting (Next)

**Haze:**
- [ ] Add `_HazeColor` property to shader and `CBUFFER`
- [ ] Wire outer fade blend: `lerp(_HazeColor, biomeColor, outerBlend)`
- [ ] Expose `_HazeColor` default matching sky horizon tint
- [ ] Disable `EnableDistanceFog` in `ProjectFeatureConfig` once haze is tuned
- [ ] Validate from 200m and 400m altitude — no hard disc edge visible

**Day/Night Lighting:**
- [ ] Add `#include "...ShaderLibrary/Lighting.hlsl"` to shader
- [ ] Replace flat `biomeColor` output with `biomeColor * saturate(ambient + sunContrib)` (see §4.5)
- [ ] Add minimum ambient floor (`max(ambient, 0.05)`) so disc never goes fully black
- [ ] Validate: disc brightness tracks `RenderSettings.ambientLight` changes at runtime

### Phase E — Validation
- [ ] Raise camera to 200m: smooth terrain-colored disc visible beyond chunk boundary
- [ ] Raise camera to 400m: disc fills downward FOV, seam with real terrain invisible
- [ ] Orbit camera at 400m: no direction shows void
- [ ] Confirm outer haze dissolves far edge (no hard disc boundary)
- [ ] Ground level: disc invisible (inner fade hides it), no seam or Z-fight with terrain
- [ ] Disable `EnableGroundPlaneImpostor`: no entity, no render, no errors

### Phase F — Codex Review
- [ ] Run Codex review on all files in §6
- [ ] Address findings, commit

---

## 8. Test Plan

### EditMode
- [ ] `GroundPlaneImpostor_EntityCreated_WhenFlagEnabled`
- [ ] `GroundPlaneImpostor_NoEntity_WhenFlagDisabled`
- [ ] `GroundPlaneImpostor_Config_DefaultValues_InExpectedRange`

### PlayMode / Manual
- [ ] Plane centers on player XZ (move player laterally, plane follows)
- [ ] Inner fade hides seam with terrain chunks at all camera angles
- [ ] Visible from 200m, 400m altitude with no hard boundary
- [ ] No performance regression (frame time stable with plane active)

---

## 9. Success Criteria

- [x] No `InvalidOperationException` from `RenderMeshInstanced`
- [x] No `DOTS_INSTANCING_ON` error
- [ ] Player spawning at 400m altitude sees terrain-colored ground extending to the haze horizon
- [ ] No visible void between chunk boundary and horizon
- [ ] Seam between real terrain and impostor plane invisible under normal play
- [ ] Outer disc edge dissolves naturally into sky — no hard ring visible from any altitude
- [ ] Feature togglable via `EnableGroundPlaneImpostor` with no side effects

---

## 10. Future Extensions

- **Vertex displacement** — drive Y per-vertex from the same noise function to add coarse height variation (hills visible from orbit).
- **Biome-aware coloring** — pass biome weights to shader to match per-biome palettes.
- **Compositing with horizon ring** — when `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` is implemented (Phase 2), the ring sits above this plane and handles the vertical silhouette.

## 11. Color authority (supersedes deleted `SyncTerrainColor`)

**Built (2026-07-07, V9 P2).** The disc no longer owns its palette: `_GrassColor`/`_RockColor` material
properties were removed and the fragment samples the global `_AtmoGround`/`_AtmoRock` uniforms broadcast
per frame by the atmosphere authority (**[ATMOSPHERE_COLOR_AUTHORITY_SPEC.md](ATMOSPHERE_COLOR_AUTHORITY_SPEC.md)**,
ticket **V9**). `SyncTerrainColor` is deleted — it was architecturally doomed, reading the terrain tint
from the Synty material's `_BaseColor`, which is white (the real color is in the albedo texture); the
authority pushes one tint to all ground surfaces instead. The dead `_HazeColor` property went with it
(unused since the disc's `MixFog` → height-aware `ApplyAerialHaze` conversion in the V9 MVP slice).
`AtmosphereSettings.Default.groundColor/rockColor` equal the disc's former literals, so the noon look is
unchanged by construction; the disc now follows biome-preset palette blends automatically.

---

## 12. Mid-field variation (ticket V17) — kill the uniform band

**Status:** P1+P2 BUILT 2026-07-16 (pending in-editor compile/test pass + owner eyeball);
P3 not built — judged after V15's drop-altitude skirt check. Opened 2026-07-09 from an owner
screenshot. As built: `GroundMacroLuminance` lives inside `GroundPaletteMix` (consumers can't
fork it, so the seam can't drift), `GroundReliefNormal` in `GroundNoise.hlsl` consumed only by the disc shader.
Dial defaults: `_MacroNoiseScale 0.0007` (~1400u), `_MacroStrength 0.08` (±8%),
`_ReliefScale 0.002` (~500u), `_ReliefStrength 0.35`; `_ReliefStrength 0` reduces exactly to
the old flat-plane lighting. Macro dials parity-guarded in `TerrainChunkMaterialContractTests`.
**Problem:** at ground level the disc reads as a featureless flat-green band between the streamed
terrain window (~180u) and the sky mountain band. The meteor arrival sequence (V13/V14) only masks
the descent — the vista beat itself is steady-state standing-on-the-plain viewing, so the band is in
frame the entire time. This section is the fix.

### 12.1 Root causes (why the existing noise doesn't read)

1. **Grazing-angle foreshortening.** The grass/rock mix is a binary `step()` over 2-octave value
   noise at `_NoiseScale 0.004` (~250u patch wavelength). From eye height, hundreds of units of disc
   compress into a few dozen pixels of screen — the patches flatten into invisible slivers.
2. **Constant lighting.** The disc is a flat +Y plane: one normal everywhere, so the
   `ambient + sun` multiply is a single constant across the whole band. Real terrain gets most of
   its texture from slope-varying Lambert shading; the disc has none.
3. **Scatter density cliff.** Grass/trees/rocks stop at the chunk radius, so detail density drops
   to zero at ~180u even when hues match perfectly.

The aerial haze is smooth and monotonic — it tints, it cannot texture.

**Sequencing (owner decision 2026-07-09, TICKETS.md Build order step 3):** land P1+P2 *after* the
V9 P3 owner eyeball (they modify both sides of the terrain↔disc seam that check judges) and
*before* V9 P5 (saturation is a one-shot global grade; P1 changes the luminance distribution it
grades). P3 undulation is judged after V15's drop-altitude skirt check — both shape the same
disc→sky-band handoff.

### 12.2 Design — three parts, in build order

**P1 — Macro luminance variation** _(shared `GroundNoise.hlsl` — applies to terrain AND disc)_
One additional FBM octave at a much larger wavelength (~1000–2000u) multiplying the post-mix color
by roughly ±8–10% — the "soil moisture / dappled plain" trick. Lives inside the shared mix so the
terrain window picks it up identically and the ~180u seam stays aligned **by construction** (same
rule as the existing patch noise: world-XZ-continuous, never forked per consumer). New dials
(`_MacroNoiseScale`, `_MacroStrength`) follow the existing parity rule — keep them equal across
disc + terrain materials; extend `TerrainChunkMaterialContractTests`' dial-parity guard to cover
them.

**P2 — Fake relief shading** _(disc shader only — terrain has real normals)_
Derive a pseudo-normal by finite-differencing a low-frequency height FBM and Lambert-light the disc
with it instead of the flat +Y normal. Reads as rolling ground without geometry (~3 extra noise
samples). Helper lives in `GroundNoise.hlsl` for reuse; **applied only in
`GroundPlaneImpostor.shader`** — feeding it to `TerrainLit` would fight the mesh's actual normals.
Dials: `_ReliefScale`, `_ReliefStrength`.

**P3 — Vertex undulation** _(optional; judge after P1+P2)_
Displace disc vertex Y in the vertex shader with a low-frequency world-space FBM. The 64×64 grid
over 3000u (~47u vertex spacing) supports wavelengths ≥300u; amplitude ~10–20u. This is the only
part that breaks the razor-straight silhouette where the disc meets the sky band. **Guard (required):
damp displacement to zero inside ~250u of the player** so the real-terrain depth occlusion at the
seam still works, and keep amplitude small relative to haze beyond — when traversal becomes core
(glide M1 / chain slingshot M2), streamed chunks replace fake undulation at the window edge, and
the swap must land where aerial perspective already blurs it.

### 12.3 Why this survives the dynamic world (design constraint, not luck)

- **Palette:** the disc consumes global `_AtmoGround`/`_AtmoRock` (§11). P1 is a scalar multiply on
  top of the already-dynamic color — biome/time-of-day palette shifts flow through untouched.
- **Lighting:** the fragment already calls `GetMainLight()`/`SampleSH()` live. P2's relief responds
  to sun direction automatically — low sun rakes the fake undulation and the relief pops; noon
  flattens it. Both correct, zero rework when the time-of-day pin comes off.
- Perf: the scene is vertex-bound (`RENDER_PERF_PROFILE_REPORT.md`); a few extra fragment noise
  samples on the disc are effectively free, and P3 adds nothing meaningful (~4k verts).

### 12.4 Non-goals (explicitly out of scope)

- **Distant-scatter speckling** (noise-driven dark specks faking far shrubs): superseded by real
  far-scatter representation — R1 LODs and the R5 / Phase-2 horizon-ring impostor stack. Don't
  build a throwaway.
- **Scrolling cloud shadows:** a weather-track feature, not an impostor patch. Done here it would
  be fake (no clouds casting it), fight the determinism-pin validation workflow, and — done right —
  must cover **real terrain too** or cloud shadows would exist only past 180u. Revisit when
  `DOTS.Terrain.Weather` drives visuals.
- **Weather states on the disc** (snow/wet tint): tracked with the weather work, same reasoning.

### 12.5 Acceptance

- From eye height at the noon pin, the mid-field band shows visible large-scale tonal variation —
  no flat single-green wall between the terrain window and the sky band.
- The ~180u terrain↔disc seam stays invisible: macro variation is world-continuous across it
  (verify by walking the seam and via a positioned top-down capture).
- From drop altitude (~400u), variation reads as ground texture, not banding or tiling.
- P2 relief visibly responds to sun elevation once time-of-day is unpinned (spot-check by moving
  the pin) — no re-tune required.
- All new dials default to a subtle look and live as material properties (no recompile to tune).
- P3 only: no visible pop where streamed chunks replace the undulated disc during normal-speed
  traversal at ground level.
