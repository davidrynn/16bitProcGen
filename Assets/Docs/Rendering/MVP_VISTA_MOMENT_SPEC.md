# MVP Vista Moment — The Relic Discovery Experience

**Status:** ACTIVE — MVP PRIORITY  
**Phase Fit:** Phase 1 (immediate) — must ship before any other visual polish  
**Last Updated:** 2026-04-26

---

## 1. The Vision

> *The player crests a rise in an open grassy plain. In the far distance, rising from the earth, is a massive ancient stone hand — four gnarled fingers reaching skyward. Mountains rim the horizon. The air hazes with distance. This is the first moment the player understands the world has secrets.*

Reference image: [`../../ChatGPT Image Apr 22, 2026, 09_34_36 PM.png`](../../ChatGPT%20Image%20Apr%2022%2C%202026%2C%2009_34_36%20PM.png)

This is the **MVP "wow moment"** — the thing a player screenshots on day one. Every other visual feature is subordinate to making this land correctly.

The hand structure is not decorative. It contains a maze interior, discoverable by approaching and entering. The exterior vista is what earns the player's curiosity. The interior is what rewards it.

---

## 2. Required Elements

### 2.1 The Relic — Giant Stone Hand

- Four fingers (not five) — deliberate, unsettling, ancient
- Scale: visible from ~200–400 world units away
- Material: weathered stone, pale grey, matching the rocky foreground scatter
- Emerges from the ground — base buried, fingers exposed
- Interior: hollow, maze-like structure accessible via WFC dungeon pipeline
- Existing asset candidates: `Assets/Models/testAlienHand.fbx` (introduced, wired to structure placement), `Odd_Head_Relic_v1.fbx` / `Odd_Head_Relic_var1_smooth.fbx` (wrong shape — head relics, not hand)
- Structure placement: handled by `Assets/Scripts/DOTS/Structures/` pipeline (committed, working) — `Relic.asset` `DefaultTemplateId` set to `relic_hand`

### 2.2 Atmospheric Depth Haze

The image's mood is 60% atmosphere. The grading effect is:
- Foreground: full-saturation green grass, sharp rocks
- Mid-distance: slightly desaturated, slight blue shift
- Far distance: strong blue-grey haze, relic slightly softened but still readable
- Sky: overcast, dramatic cloud coverage (art direction, not tech — URP skybox)

**Current state:** No atmosphere system. URP global volume has basic fog but it's not tuned.

**Implementation path (fastest to ship):**
1. URP Global Volume → Fog (Exponential Height Fog, color = blue-grey `#8FA8C0`, density ~0.004, max distance ~500)
2. Tune fog start/end distance so foreground is clear, horizon is heavily veiled
3. Optionally add a subtle Depth of Field or Chromatic Aberration in the volume for style

This is a half-day task and transforms the mood immediately.

### 2.3 Ground Plane Impostor — World Extent From Altitude

When the player drops from height (~400m sky-drop intro) or is otherwise airborne, SDF terrain chunks only exist within ~256u horizontal radius. Beyond that the world is void. A horizontal flat plane impostor fills this gap.

**Design:** A large disc mesh (64×64 quads, ~1500u radius) on the XZ plane, procedurally shaded in world-space using the same noise octaves as the terrain (grass/rock color thresholds). Radial alpha fade over the inner 40u band hides the seam with real chunks; fog dissolves the outer edge. The system entity follows player XZ each frame — one transform write.

**Spec:** [`GROUND_PLANE_IMPOSTOR_SPEC.md`](GROUND_PLANE_IMPOSTOR_SPEC.md)  
**Cost:** ½–1 day. No texture assets. No SDF pipeline changes.  
**Status:** Specced, not yet implemented.

### 2.4 Background Mountain Imposters

Mountains exist only as a horizon silhouette — they do not need to be real terrain.

**Two options — pick one for MVP:**

| Option | Description | Cost |
|--------|-------------|------|
| **A — Painted Skybox** | Replace or augment current skybox with mountain silhouette painted into it | 2–4 hours, ships fast |
| **B — Horizon Impostor Mesh** | Low-poly ring mesh at ~800u distance, sampled from world seed, textured with mountain profile | 1–2 days; correct but heavyweight for MVP |

**MVP recommendation: Option A.** The `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` is the right long-term system but is Phase 2+ work. For MVP, a painted skybox panel with a mountain silhouette reads as authentic at this art style's fidelity level.

### 2.4 Two Biomes: Plains + Mountains

**Plains (current terrain, needs tuning):**
- Flat to gently rolling, SDF terrain
- Grass scatter (GPU-instanced) — existing baseline stays; only the *streaming rewrite* ([`BIOME_GRASS_STREAMING_MVP_PLAN.md`](../Biomes/BIOME_GRASS_STREAMING_MVP_PLAN.md)) is deferred
- Rock scatter (surface scatter system)
- Muted green palette

**Mountains (new, minimal for MVP):**
- Used as background framing, not as traversable terrain at MVP
- For now: represented by the skybox silhouette (Option A above)
- Phase 2: promote to real biome zone with elevated SDF terrain, rougher noise, rock-dominant scatter

The biome system (`BiomeComponent`) is marked legacy. Do not revive it for MVP. Instead, add a simple height-based noise parameter variant to `SDFTerrainFieldSettings` for rougher "mountain-adjacent" terrain in the middleground if needed.

---

## 3. Gap Analysis — Current vs. Target

| Element | Current State | Gap |
|---------|--------------|-----|
| Structure placement pipeline | ✅ Committed (`DOTS/Structures/`), 22/22 tests pass | — |
| Relic LOD + impostor | ✅ Implemented (`RelicRealizationSystem`, `RelicLodSelectionSystem`) | Wire hand mesh; billboard impostor art asset pending |
| Hand mesh | `testAlienHand.fbx` introduced, wired as `DefaultTemplateId: relic_hand` | Visual validation; may need scale/yOffset tuning |
| Ground plane impostor | ❌ Not started | Spec'd — see `GROUND_PLANE_IMPOSTOR_SPEC.md`; needed for sky-drop intro |
| Atmospheric haze | Fog enabled (`ExponentialSquared`, density 0.005, warm) but not tuned for vista mood | Tune color toward blue-grey, increase density, adjust start/end |
| Background mountains | None | Painted skybox panel for MVP |
| Plains terrain | SDF + rock/grass scatter | Working, needs visual tuning |
| Mountain biome | Legacy / disabled | Skybox covers MVP; Phase 2 for real terrain |
| Interior maze | WFC pipeline exists | Wire relic anchor to WFC dungeon interior generation |

---

## 4. Priority Order (MVP Sequence)

These are ordered by impact-per-hour — do the cheap wins first.

1. **Ground plane impostor** — flat terrain-colored disc beyond chunk radius; enables sky-drop intro, no void visible from altitude (½–1 day) — see [`GROUND_PLANE_IMPOSTOR_SPEC.md`](GROUND_PLANE_IMPOSTOR_SPEC.md)
2. **Atmospheric fog tuning** — shift fog color toward blue-grey, tighten density, tune start distance so foreground is sharp and horizon is veiled (½ day)
3. **Mountain skybox panel** — painted silhouette in skybox (½–1 day)
4. **Hand mesh validation** — confirm `testAlienHand.fbx` renders correctly at scene scale; tune `scale` and `yOffset` in `RelicVisualBootstrap` inspector
5. **Interior maze** — connect relic anchor to WFC dungeon interior generation
6. **Visual tuning pass** — lighting, fog density, rock/grass scatter density in foreground

---

## 5. What This Is Not

- This is not a biome streaming system. Mountains are skybox for MVP.
- This is not full atmospheric scattering. URP exponential fog is sufficient.
- This is not a procedural hand generator. One authored hand mesh, placed deterministically.
- The `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` seed-driven *vertical* horizon system is Phase 2. The `GROUND_PLANE_IMPOSTOR_SPEC.md` *horizontal* ground plane is MVP — different systems solving different problems.
- The ground plane impostor is not a terrain replacement. It sits beneath the real chunks and is invisible at ground level.

---

## 6. Success Criteria

The MVP vista moment is complete when:

- [ ] Player can be dropped from ~400m altitude and see terrain-colored ground extending to the fog horizon (ground plane impostor)
- [ ] No void is visible in any direction from altitude — the world appears to go on
- [ ] Player spawns/lands in a plains area and can see a giant stone hand in the distance
- [ ] Distance haze makes the relic look far away and slightly mysterious
- [ ] Mountains (or mountain-like silhouettes) frame the horizon
- [ ] Player can walk/slingshot toward the relic
- [ ] Relic transitions from full mesh to billboard impostor as player moves away (LOD swap)
- [ ] Player can enter the relic and find a maze interior
- [ ] The scene reads as "ancient, vast, strange" — not a tech demo

---

## 7. Related Documents

- [`GROUND_PLANE_IMPOSTOR_SPEC.md`](GROUND_PLANE_IMPOSTOR_SPEC.md) — **MVP** horizontal ground-plane impostor (sky-drop world extent)
- [`RELIC_LOD_IMPOSTOR_SPEC.md`](../Structures/RELIC_LOD_IMPOSTOR_SPEC.md) — LOD/impostor system for far-relic rendering
- [`STRUCTURE_PLACEMENT_SPEC.md`](../Structures/STRUCTURE_PLACEMENT_SPEC.md) — Anchor planning and structure realization
- [`HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`](HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) — Phase 2 vertical horizon ring (mountain silhouettes, deferred)
- [`../Player/Movement/MOVEMENT_PLANNING.md`](../Player/Movement/MOVEMENT_PLANNING.md) — Slingshot traversal toward the relic
- [`../WFC/WFC_Dungeon_Test_Plan.md`](../WFC/WFC_Dungeon_Test_Plan.md) — WFC dungeon system test strategy (interior maze pipeline)
- [`../WFC/MAP_WFC.md`](../WFC/MAP_WFC.md) — WFC system map, components, and socket contracts (entry point for relic interior integration)
