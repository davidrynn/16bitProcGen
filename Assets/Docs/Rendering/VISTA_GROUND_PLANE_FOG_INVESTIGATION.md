# Vista — Ground Plane Impostor & Fog Investigation

**Status:** INVESTIGATING
**Opened:** 2026-06-29
**Owner:** David
**Relates to:** Tickets V1 (ground plane impostor), V2 (atmospheric fog).
**Source specs:** [`GROUND_PLANE_IMPOSTOR_SPEC.md`](GROUND_PLANE_IMPOSTOR_SPEC.md), [`MVP_VISTA_MOMENT_SPEC.md`](MVP_VISTA_MOMENT_SPEC.md), [`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`](HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md)

> Working doc, not a spec. Captures what the impostor + fog **actually do today** vs. what they
> should do, so V1/V2 are re-scoped from evidence (screenshots) rather than from the stale
> "unbuilt" assumption in `Tickets/vista-moment.md`. Promote decisions into the source specs once confirmed.

---

## 1. Goal (intent, restated 2026-06-29)

From altitude (sky-drop / vista view) the world should read as **one continuous ground reaching
the horizon** — no void, no hard edges, no perceptible seam between:

- the **real SDF terrain** (chunks, ~180–256u around the player), and
- the **ground plane impostor** that extends it outward, and
- the **horizon / skybox** (V3 mountain panel; later the seed-driven horizon ring).

Two specific complaints driving this investigation:

1. **The impostor reads as a square, and a "square from height" artifact appears.** It should be a
   **disc** that visually reaches the horizon, with the real mesh filling seamlessly into it and the
   impostor filling seamlessly into the horizon — no gaps anywhere in that chain.
2. **The fog is unclear** — it doesn't read as a clean atmospheric haze tying the layers together.

---

## 2. What actually renders today (code reality)

### 2.1 Fog
- **Source:** Unity built-in `RenderSettings` fog, applied at runtime by
  `DotsSystemBootstrap.ApplyDistanceFog()` from the `ProjectFeatureConfig` "Distance Fog" block.
  Settings: **ExponentialSquared**, color warm-tan `#CCAD99` (0.80, 0.68, 0.60), **density 0.007**.
  (`EnableDistanceFog: 1` in `ProjectFeatureConfig.asset`.)
- The grey fog serialized in `Basic Terrain Scene.unity` RenderSettings (0.5, Exp²) is **overridden**
  at startup by the config applier — the serialized grey is stale and misleading.
- `OasisFogVolumeComponent` in `DefaultVolumeProfile.asset` is **off** (`Density: 0`) — a dormant
  third-party leftover, not part of the atmosphere.
- `FogEffect.prefab` (WeatherSystem) is a **localized particle** effect, not global atmosphere.
- URP applies built-in fog **only to shaders that opt in** (`#pragma multi_compile_fog` + `MixFog`).
  Terrain/scatter URP-Lit shaders do (e.g. `DOTSVertexColorUnlitClip.shader`). **The impostor shader
  does not** (see 2.2).

### 2.2 Ground plane impostor
- **Live and enabled:** `EnableGroundPlaneImpostor: 1`; the `GroundPlaneImpostorBootstrap` GameObject
  is in `Basic Terrain Scene.unity` with `_enabled: 1`. (V1 is **built**, contradicting
  `Tickets/vista-moment.md`'s "mostly unbuilt".)
- **Mesh:** `GenerateDiscMesh()` actually builds a **square** 64×64 uniform grid spanning −1500..+1500u
  (3000u across). It is not a disc; roundness is meant to come from the shader's radial alpha fade.
- **Render path:** `GroundPlaneImpostorSystem` (PresentationSystemGroup, SystemBase) issues one
  `Graphics.RenderMeshInstanced` centered on player XZ each frame; pushes `_PlayerXZ`,
  `_InnerFadeStart`, `_InnerFadeEnd`.
- **Shader** (`Ground/GroundPlaneImpostor`): transparent, world-XZ FBM noise → grass/rock color,
  ambient SH + attenuated sun lighting. Radial fades:
  - inner fade `_InnerFadeStart/End` — **disabled** (bootstrap forces 0/0; terrain depth-occludes instead)
  - outer fade `_OuterFadeStart 900` → `_OuterFadeEnd 1400` — fades alpha to **0 (transparent → skybox)**
  - `_HazeColor` is declared in the CBUFFER but **never referenced** in the fragment.
- **No fog:** no `#pragma multi_compile_fog`, no `MixFog`. The impostor never participates in the
  scene fog.

---

## 3. Divergences (spec/intent → actual)

| # | Intended | Actual | Likely visual effect |
|---|----------|--------|----------------------|
| D1 | Disc mesh (spec §5.1) | Square grid mesh | Square silhouette / corners if fade doesn't clip them in view |
| D2 | Inner fade 256→296u (spec §4.4) | Inner fade disabled (0/0) | Disc opaque from player out; depends on terrain depth-occlusion at the seam |
| D3 | Outer edge fades to `_HazeColor` (spec §4.4 Phase D) | `_HazeColor` unused; outer fade → transparent (raw sky) | Disc edge meets skybox directly, no haze blend |
| D4 | Disable scene fog once impostor haze tuned; impostor owns its haze (spec §4.4) | Scene fog ON **and** impostor has no fog | Two uncoordinated atmospheres; geometry hazes, impostor doesn't → seam at the boundary |
| **D5** | Outer fade dissolves edge at 900–1400u | **Camera far clip = 600u** (`VistaCameraFarClip`) | **Fade zone is entirely beyond the far clip — the disc is hard-cut at 600u, never dissolves, and isn't hazed.** Prime suspect for "gap" + "unclear fog". |

> **D5 is the headline.** The impostor's graceful-fade design (900–1400u) and the vista camera far
> clip (600u) were set independently and never reconciled. Within the 600u the camera can see, the
> disc is fully opaque (fade starts at 900); at the far plane it's clipped with a hard edge that the
> fog doesn't cover (impostor has no fog). Real terrain inside the disc *does* fog, so the two don't
> match.

---

## 4. Hypotheses for the two complaints

- **"Square from height":** most likely the **finite terrain-chunk region** (~180–256u of real,
  fogged geometry) read as a square block sitting inside the impostor — OR the square impostor mesh
  corners (D1) if they fall inside the view frustum. **Needs a screenshot to disambiguate.**
- **"Unclear fog":** combination of D4 + D5 — geometry fogs to warm-tan while the impostor neither
  fogs nor blends to a haze color, and the disc is hard-clipped at 600u. So there is no single clean
  haze band tying terrain → impostor → sky. Fog density 0.007 (Exp²) also saturates fairly close
  (~99% fogged by 300u), which may read as "murky near, then a hard edge" rather than "sharp
  foreground, veiled horizon."

---

## 5. Verification plan (screenshots → MCP)

Capture from the running game (Play Mode, `Basic Terrain Scene`) at several altitudes and label each:

1. **Ground level**, looking toward horizon — is the fog band clean? Any disc ring / seam visible?
2. **~200u altitude**, 70° FOV, looking down ~45° — where does real terrain end and impostor begin?
   Is the boundary a square?
3. **~400u altitude**, looking down — is there a void anywhere? Is the disc edge a hard ring/square?
   Does it reach the horizon or stop short (far-clip cut at 600u)?
4. **Orbit at 400u** — does any compass direction show void or a straight edge?

For each, note: (a) is the square the chunk region or the impostor mesh; (b) is there a visible gap
between impostor edge and horizon/skybox; (c) does fog read as haze or as a tint/edge.

**MCP option:** drive the Unity Editor via the `unity-mcp-skill` to enter Play Mode, reposition the
camera to the altitudes above, and capture viewport screenshots — so the investigation is repeatable
and doesn't depend on manual framing. Decide capture method before starting (see open questions).

---

## 6. Open questions (resolve from screenshots)

1. Is the "square from height" the **terrain-chunk region** or the **impostor mesh corners**? (D1 vs §4)
2. Should V1 switch to a **true radial/polar disc mesh**, or keep the square grid and rely on a
   correctly-clipping radial shader fade? (Disc mesh is cleaner but changes noise-sampling grid.)
3. **Reconcile the radii:** should the impostor outer fade end at/just inside the **600u far clip**
   (so it dissolves within view), or should the far clip be pushed out to meet the 900–1400u fade?
   These two numbers must agree — that's the core of D5.
4. **One atmosphere or two:** keep scene fog and make the impostor receive it (add `multi_compile_fog`
   + `MixFog`), OR follow the spec and move haze fully into the impostor shader (`_HazeColor`,
   disable scene fog)? Spec §4.4 argues the latter for altitude correctness.
5. Fog feel: is density 0.007 too aggressive near the camera for "foreground sharp / horizon veiled"?

---

## 7. Findings log

_(Append dated entries with screenshots / measurements as the investigation proceeds.)_

- **2026-06-29** — Static code/asset audit complete (sections 2–4 above). No runtime capture yet.
  Next: screenshots at the four altitudes in §5.
- **2026-07-01** — Runtime capture via Unity MCP (`manage_camera` positioned screenshots). Resolutions:
  - **No literal gap/void.** From altitude the green impostor meets the sky directly; the perceived "gap"
    was a hard, high-contrast seam (green land vs warm sky) with zero aerial haze.
  - **The "tan/orange" is the skybox horizon, not fog.** Fog was already disabled in config
    (`EnableDistanceFog: 0`). Proven by forcing the camera clear to solid blue — the tan vanished at ground
    level (positioned MCP captures always render the skybox, so they kept showing it).
  - **Root cause of the orange = the live day/night cycle.** `TimeOfDayController` (on `TerrainBootstrap`,
    240s cycle) evaluates `Assets/Resources/Sky/DefaultSkyPreset.asset` and pushes colors into `SkyController`
    every frame; all daytime horizons are warm. Editing `SkySettings.Default` did nothing (reverted).
  - **Biome-sky architecture exists but is empty/unwired** — `BiomeSkyMapping` + `ApplyBiome()` implemented;
    `DefaultBiomeSkyMapping.asset` has null fallback + no entries; no `ApplyBiome` caller.
  - **Applied & kept:** enabled gentle Exp² fog (`ProjectFeatureConfig.asset`: color `(0.62,0.74,0.85)`,
    density `0.0015`) + `multi_compile_fog`/`MixFog` in `GroundPlaneImpostor.shader`. The plain now hazes
    cleanly into the horizon. Camera clear restored to Skybox; the `SkySettings.Default` probe reverted.
  - **Decision → plan.** Keep Highlands (no ocean); keep the live day/night cycle; make fog color track the
    sky horizon; author a **Plains "Cloudbreak"** preset (cool broken-overcast, per
    `Docs/Temp_OpeningInspiration.png`); colors biome-dependent. Full plan: **`SKYBOXPLAN.md` §9**, ticket
    **V6**. Most §6 open questions are now moot (no gap; single atmosphere via tracking fog; square = skybox).
