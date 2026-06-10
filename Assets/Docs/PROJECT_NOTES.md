# Project Notes — Scratchpad

**Last Updated:** 2026-04-23

Lightweight scratchpad for current session notes, quick TODOs, and observations that don't yet belong in a spec or bug report. Anything durable should migrate to the appropriate document below.

---

## Quick Links

| What | Where |
|------|-------|
| Project vision, status, roadmap | [MASTER_PLAN.md](MASTER_PLAN.md) |
| Bug/issue tracker | [KNOWN_ISSUES.md](KNOWN_ISSUES.md) |
| Full document index | [DOCUMENT_INDEX.md](DOCUMENT_INDEX.md) |
| SDF terrain architecture | [SDF_SurfaceNets_ECS_Overview.md](SDF_SurfaceNets_ECS_Overview.md) |
| Terrain next-steps spec | [AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md](AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md) |
| Claude/AI instructions | [/CLAUDE.md](/CLAUDE.md) |

---

## Terrain Render Configuration (2026-05-12)

Configuration decisions made to align terrain rendering with industry standard open-world games.

### Render Distance

`ProjectFeatureConfig.HighRenderDistance`: **240u → 300u** (the High preset is the only preset used in the asset).

- 20-chunk streaming radius (was 16)
- ~1681 active chunks (was ~1089)
- LOD0: 0–100u · LOD1: 100–200u · LOD2: 200–300u
- Camera far clip: 600u — terrain covers 50% of camera range (was 40%)
- Gap from 300–600u is covered by ground plane impostor + fog

**Rationale:** Industry standard puts LOD2 at 400–800m, but the tree/rock render systems submit one `RenderMeshInstanced` batch per chunk (not per mesh type globally), so chunk count directly drives draw call overhead. At 4225 chunks (480u), FPS dropped to ~18 with 98.5% CPU main-thread usage and terrain edit raycasts failed (collider build queue overwhelmed). 300u is the practical ceiling for the current per-chunk rendering architecture. Fix: consolidate tree/rock instancing into a global pool per mesh type; then revisit pushing to 480u.

### Rebuild Budgets

`TerrainLodSettings.Default` changes:
- `MaxDensityRebuildsPerFrame`: **6 → 16**
- `MaxMeshRebuildsPerFrame`: **6 → 16**
- `MaxColliderRebuildsPerFrame`: **4 → 8**

At 16/frame and 60fps, the full 4225-chunk world can fill density in ~4.4s and mesh in another ~4.4s, fitting within the 8s sky-drop gravity hold. Center chunks (player landing zone) build first.

### Current Asset State (`ProjectFeatureConfig.asset`)
- `TerrainRenderDistancePreset: 2` (High = 480u)
- `VistaCameraFarClip: 600`
- `FogMode: 3` (ExponentialSquared), `FogDensity: 0.005`
- `FogColor: {r: 0.56, g: 0.66, b: 0.75}` (blue-grey, matches horizon haze)
- `EnableGroundPlaneImpostor: 1` (fills 480–1500u gap)
- `EnableSkyDropSpawn: 1`, `SkyDropSpawnHeight: 400`, `SkyDropGravityHoldSeconds: 8`

---

## Current Session (2026-04-23)

**MVP Vista Moment — vision captured and set as top priority.**

Reference image: [`../ChatGPT Image Apr 22, 2026, 09_34_36 PM.png`](../ChatGPT%20Image%20Apr%2022%2C%202026%2C%2009_34_36%20PM.png)

A grassy plain, a distant gigantic ancient stone hand (four fingers) rising from the ground, mountains on the horizon, atmospheric haze creating depth. The hand contains a maze inside. This is the MVP "first vista" moment — the thing a player screenshots.

Gap analysis from session:
- Structure placement pipeline: already built (untracked `DOTS/Structures/`)
- Atmospheric haze: **nothing done** — URP volume untouched, half-day fix
- Mountain horizon: **nothing done** — painted skybox panel is the MVP path
- 4-finger hand mesh: **missing** — no suitable asset, art task needed
- Biome work: no second biome needed for MVP; mountains are skybox silhouette

New doc: [`AI/MVP_VISTA_MOMENT_SPEC.md`](AI/MVP_VISTA_MOMENT_SPEC.md) — captures full gap analysis and priority order.
Updated: `MASTER_PLAN.md` vision section + immediate next steps now lead with vista work.
Updated: `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md` — clarified as Phase 2, not MVP.

## Current Session (2026-04-03)

- Fixed BUG-009: reversed normals at CSG seams after terrain edits (full 3D gradient in Surface Nets)
- Observed possible collision issue on vertical planes after edits — needs investigation (likely collider rebuild lag from `MaxCollidersPerFrame = 4`, not winding-related)

## Current Session (2026-04-11)

- Added first Surface Scatter ECS/data breakdown doc for tree-plus-rock rollout:
	- `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SCHEMA.md`
- Cross-linked the new Surface Scatter schema from:
	- `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_PLAN.md`
	- `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md`
	- `Assets/Docs/AI/TerrainHeightMaps/TERRAIN_TREE_PLACEMENT_SPEC.md`
	- `Assets/Docs/DOCUMENT_INDEX.md`
- Validation status update for player grounding regression:
	- Targeted CLI batch run still exits with code 1 and no XML output in current editor state.
	- In-editor Unity MCP test run succeeded for:
		- `DOTS.Player.Test.PlayerWallContactCommandPlayModeTests.CeilingOnly_EmbeddedRecovery_DoesNotGroundPlayer`
	- Result: passed (1/1), confirming ceiling-only embedded recovery does not ground player.

## Parking Lot

_Items noticed during work that deserve follow-up but aren't urgent._

- BUG-008 (edit buffer grows without bound) will become noticeable at high edit counts; consider clear-after-bake or per-chunk accumulated density
- BUG-009 remaining minor factors: `minDensity == 0f` symmetric case, `hasSurface` fallback vertex when `crossingCount == 0`
- BUG-010 (grass not removed after edits) — straightforward fix outlined in KNOWN_ISSUES, blocked on GRASS_ECS_SPEC Phase 3

## Human Notes

- Need to figure out how to keep interest for finding treasure — too much, not enough, too random, not exciting
- Movement: WASD should change relative to camera facing, not world axes (partially addressed by `PlayerMovementSystem` yaw-relative input)
- Need to pare down code and get a deeper connection to movement system

- explore idea of good evil, changing existing structures as opposed to defining how it's built, AND defining how it's built based on material. Also explore how WFC will work with limited resources, how does structure change as add more to it?

- More ideas for structures - towers, one semi-invisible or mirror or both. Other structures are just odd pulsing disturbances in view but nothing there. Underground mine.
 
- A big idea is indication of something of interest. How to do this without being too obvious but giving an idea. And at the same time not being annoying, having to make player go through an entire rig-a-ma-roll just to get a map or something. I think LoZ did something with wind, or a slight flame in the correct direction, or bell sounds.  Are can be case by case - sounds of deep rushing air coming from an underground mine for example. but how to indicate that more closely? Maybe resource indicator? For large object that's obvious. Difficult because some of the joy is the searching, like in minecraft you just have to dig, there's no way around it, and sometimes it works and sometimes it doesn't.

- How to make progression feel worthwhile. Games that fail: Conan exiles, LOTR Moria. Games that succeed: Minecraft, Planet Crafters. Make it off planet? Get to the core?

- How silly is the game? Should guy put him in bunny suit?

