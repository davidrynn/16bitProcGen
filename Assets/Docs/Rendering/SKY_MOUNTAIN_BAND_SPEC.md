# Sky Mountain Band — Silhouette Ruggedness + Band Composition

**Status:** ACTIVE — built 2026-07-09, pending owner visual validation (ticket **V15**)
**Phase Fit:** Phase 1 (MVP vista polish) — interim system; silhouette *source* superseded in Phase 2 by the seed-driven horizon impostor
**Last Updated:** 2026-07-09
**Owner:** David Rynn
**Ticket:** V15 (`../Tickets/done/vista-moment.md`)

**Purpose:** Make the skybox mountain band read as a rugged, layered, *distinct* distant range instead
of a smooth sine ridge that blends seamlessly into the terrain. Everything lives in the mountain block
of `Assets/Resources/Shaders/ProceduralGradientSky.shader` — no new assets, no new draw calls, dialable
back to (near) the old look. Owner-approved through round 3 (2026-07-09): ruggedness + layering
confirmed "definitely an improvement"; back ridge retuned finer (round 2); snow caps kept as an off-by-default toggle (round 3).

---

## 1. Why This Exists

Owner observations (2026-07-09):

1. The mountains "just look like a sine curve" — correct literally: the ridgeline was three overlapping
   sine harmonics (`0.60·sin(az) + 0.30·sin(2.3az) + 0.10·sin(4.7az)`). Smooth harmonics can only
   produce rolling hills; real ridgelines need sharp peaks and asymmetry.
2. The mountains "blend a little too seamlessly with the terrain" — also structural: the band used the
   *same* `_AtmoRock`/`_AtmoGround` palette as the terrain and ground disc (V9 P4), so nothing
   separated the range from the plain it sits behind.

## 2. Design

Four changes, all inside the sky shader's mountain block:

### 2.1 Ridged periodic FBM silhouette

The sine harmonics are replaced by `MountainRidgedFBM`: 4 octaves of 1D **periodic value noise** over
azimuth with the ridged transform `(1 − |2n − 1|)²` — sharp crests, V-shaped valleys.

- **Wrap constraint:** azimuth is remapped to `t01 ∈ [0,1)` and each octave hashes its lattice index
  through `fmod(i, period)`, so the ridge wraps seamlessly at ±π. This only holds for **integer**
  lattice counts — the FBM rounds its base frequency internally so any dial value stays seamless.
- `_MountainRidgeFreq` (default 6) sets how many major massifs ring the horizon.

### 2.2 Second (back) ridge

A second FBM line — **same mountain type as the front, but finer**, because distance compresses
apparent wavelength and height: 1.6× the front's noise frequency, smaller variation
(`_Mountain2Variation` 0.09 vs the front's ~0.14), and only a slightly higher base
(`_Mountain2BaseHeight` 0.03 vs 0.023 — enough to peek through the front saddles). Farther fictive
haze distance (`_Mountain2Distance` 1500 vs 900). Composited back-to-front. Depth stacking:
**nearest ridge darkest**, back ridge one *subtle* atmospheric step lighter and toward the horizon
hue (fixed constants 0.35/0.25 in-shader; the dials give enough tuning surface).

> **Round 2 (owner feedback 2026-07-09):** v1 had the back ridge broader (0.8× frequency) and much
> taller (base 0.045) with a strong color separation (0.5/0.4) — it read as a bigger, paler range
> behind foothills, backwards from perspective. The ranges should read as siblings, one step apart.

### 2.3 Horizon demarcation line + range color

A gate at `viewDir.y = 0` (`_MountainLineSoftness` controls crispness) splits the front layer:

- **Below the line — the ground skirt (unchanged, load-bearing):** the band continues below the
  horizon as the far-field ground skirt the ground disc alpha-fades into
  (`GroundPlaneImpostor.shader` / `AtmoWorldEdgeHaze` handoff). It **must keep the ground palette**
  (`lerp(_AtmoRock, _AtmoGround, hills) × _MountainShade`) — recolor it and the far-clip edge shows
  again from altitude.
- **Above the line — the mountain range:** `lerp(_AtmoRock, _AtmoHorizon, _MountainHueShift) ×
  _MountainRangeShade` — darker (0.45 vs 0.55) and pulled toward the horizon hue, so the range reads
  as a separate distant mass. Distant ranges reading darker/cooler than the foreground is standard
  aerial perspective and a classic 16-bit composition (flat dark silhouette band, hard base line).

The gate itself **is** the visible horizon line the owner asked for.

### 2.4 Snow caps — built, OFF by default (toggle)

One global snow-line elevation angle (`_SnowLineHeight`): any band pixel above it lerps toward the
snow color, so only peaks that rise past the line get caps — coverage follows ridge height for free
(one smoothstep). Snow hue derives from `_AtmoHorizon` pushed toward white (never a literal), so it
dims at night and warms at dusk with the palette. Applied **before** haze so distant snow recedes
with everything else.

**`_SnowOpacity` defaults to 0 — snow is dormant** (round 3, owner call 2026-07-09: keep the
functionality as a toggle, it may come in useful; just not a priority. At faint opacities it's
largely washed out by haze anyway — expect to need 0.5+ plus possibly a lower `_SnowLineHeight` to
see it clearly).

## 3. Color Authority Compliance

Per `ATMOSPHERE_COLOR_AUTHORITY_SPEC.md`: **no private color literals.** Every new hue derives from
the broadcast palette:

| Surface | Derivation |
|---|---|
| Skirt (below line) | `lerp(_AtmoRock, _AtmoGround, hills) × _MountainShade` (unchanged) |
| Front range | `lerp(_AtmoRock, _AtmoHorizon, _MountainHueShift) × _MountainRangeShade` |
| Back range | front hue pushed 0.35 further toward `_AtmoHorizon`, shade lifted 0.25 toward 1 |
| Snow (off by default) | `lerp(_AtmoHorizon, white, 0.65)` — dims at night, warms at dusk with the palette |

Both ridges keep the existing haze machinery (`AtmoHeightHazeAmount` + `_MountainHazeFloor` with the
zero-haze dev-pin gate; ground-plane-clamped ray from altitude).

## 4. Dials (all on `ProceduralGradientSky.mat`)

New properties (existing dials unchanged; serialized owner tuning on the .mat carries over):

| Property | Default | Meaning |
|---|---|---|
| `_MountainRidgeFreq` | 6 | major massifs around the horizon (rounded internally; back ridge runs 1.6×) |
| `_Mountain2BaseHeight` / `_Mountain2Variation` | 0.03 / 0.09 | back ridge height envelope (finer + only slightly higher than front) |
| `_Mountain2Distance` | 1500 | back ridge fictive haze distance |
| `_MountainRangeShade` | 0.45 | range darkness above the line (`_MountainShade` now skirt-only) |
| `_MountainHueShift` | 0.30 | range pull toward `_AtmoHorizon` |
| `_MountainLineSoftness` | 0.0015 | horizon line crispness |
| `_SnowLineHeight` / `_SnowSoftness` / `_SnowOpacity` | 0.045 / 0.012 / **0 (off)** | snow toggle — raise opacity to enable |

`_MountainHueShift 0` + `_MountainRangeShade = _MountainShade` ≈ the old single-band coloring
(silhouette shape stays ridged — the sine code is gone).

**Trap:** `SkyController` runtime-copies the sky material — Play Mode inspector tuning edits the copy
and is lost on exit. Tune on the asset, or copy values out before stopping.

## 5. Performance

~4–5 extra octaves of 1D value noise + a few lerps per *sky* pixel (background queue, most of it
occluded by terrain at ground level). The shader already runs 4-octave 2D FBM for clouds per pixel.
No new assets, textures, or draw calls. Not measurable on a vertex-bound scene
(`project_perf_vertex_bound`).

## 6. Validation

- Compile: no shader errors in Editor.log / console after refresh.
- **Ground-level screenshot:** rugged twin-ridge silhouette, visible horizon line where the range
  meets the plain, no seam scanning the full 360°. _(Confirmed by owner, round 2 screenshot 2026-07-09.)_
- **Drop-altitude screenshot (~400u):** skirt still ground-colored below the line (no far-clip edge
  regression — the seamless blend being removed was previously load-bearing *at* the line), band
  composition holds from above.
- Day/night sweep is owner-eyeball (snow + range hues track the palette by construction).

## 7. Supersession Path

Phase 2's `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` replaces the *silhouette source* (sampled real
world height per azimuth instead of authored noise) — at that point §2.1/§2.2 of this spec are
superseded, while §2.3's horizon-line/skirt composition and §3's palette rules carry forward to the
generated horizon texture. A reachable mountain **biome** (owner intent, post-MVP) plugs into that
system as the deterministic world-height function; nothing in this spec blocks or prejudices it —
this band is disposable fiction by design.

## 8. Related Docs

- `ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` — palette authority the band consumes (§5.4 sky band)
- `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` — Phase 2 replacement for the silhouette source
- `MVP_VISTA_MOMENT_SPEC.md` §2.4 — mountains-as-skybox MVP decision this refines
- `GROUND_PLANE_IMPOSTOR_SPEC.md` — the disc whose far edge hands off to the below-line skirt
- `../Tickets/done/vista-moment.md` — ticket V15
