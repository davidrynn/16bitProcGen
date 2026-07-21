# Document Index

**Last Updated:** 2026-07-09

> **New here?** Start with [MASTER_PLAN.md](MASTER_PLAN.md) — it has the project vision, current status, phase roadmap, and a curated document map.

Quick-reference index of all project documentation. Docs are organized by feature-area folder (see [DOCUMENTATION_SYSTEM_SPEC.md](DOCUMENTATION_SYSTEM_SPEC.md) §6.4) with status and links.

---

## Design / Vision

| Document | Status | Description |
|----------|--------|-------------|
| [MASTER_PLAN.md](MASTER_PLAN.md) | ACTIVE | Project vision, design pillars, current status, and phase roadmap ("what / when") |
| [GAME_DESIGN.md](GAME_DESIGN.md) | DESIGN (v0.1) | The GDD — what makes it fun, core loop, the travel-is-fun north star, WFC fast-build, progression ("why it's fun"). Minimal, iterating |

## Terrain

| Document | Status | Description |
|----------|--------|-------------|
| [Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md](Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md) | ACTIVE | SDF + Surface Nets terrain implementation roadmap |
| [Terrain/WORLD_STRUCTURE_SPEC.md](Terrain/WORLD_STRUCTURE_SPEC.md) | DESIGN | World macro-structure authority (`H`): one seeded heightfield sampled by terrain, disc, sky band/ring — spine for mountains, lakes, persistence, and the WFC/pocket-interior resume; owner-ratified decisions 2026-07-18 |
| [Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md](Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md) | DESIGN | Vertical chunk streaming for underground caves, tunnels, and dungeons — tiered Level 1–3 roadmap |
| [Terrain/TERRAIN_VOXEL_CHUNK_EDIT_SPEC.md](Terrain/TERRAIN_VOXEL_CHUNK_EDIT_SPEC.md) | PROPOSED | Minecraft-style cube edits snapped to a voxel/chunk-aligned grid, deterministic across chunk boundaries |
| [Terrain/TERRAIN_STRATEGY_PLAN.md](Terrain/TERRAIN_STRATEGY_PLAN.md) | DESIGN | Sequencing plan: from prototype height signal to biome-aware world generation on the SDF pipeline |
| [Terrain/TERRAIN_MVP_PRIORITY_NOTE.md](Terrain/TERRAIN_MVP_PRIORITY_NOTE.md) | DESIGN | Priority guidance: intended order of importance for the terrain-system MVP |
| [Terrain/TERRAIN_EDIT_PLAYER_SAFETY_LOCAL_GRID_SPEC.md](Terrain/TERRAIN_EDIT_PLAYER_SAFETY_LOCAL_GRID_SPEC.md) | PROPOSED | Player-overlap edit guard + deterministic chunk-local grid guarantees |
| [Terrain/TERRAIN_BINARY_EDIT_LAYER_SPEC.md](Terrain/TERRAIN_BINARY_EDIT_LAYER_SPEC.md) | PROPOSED | Binary voxel edit layer: hard-edged boxy edits via separate mask + face extraction, no SDF density rebuild, additive to existing SDF pipeline. |
| [Terrain/DOTS_Terrain_LOD_SPEC.md](Terrain/DOTS_Terrain_LOD_SPEC.md) | ACTIVE | Terrain LOD system spec — sole source of truth for LOD (absorbed `AI/TERRAIN_LOD_SPEC.md` + `AI/DOTS_Terrain_LOD_Plan.md` on archiving, 2026-07-02) |
| [Terrain/DOTS_Terrain_LOD_Implementation_Checklist.md](Terrain/DOTS_Terrain_LOD_Implementation_Checklist.md) | ACTIVE | Gap-to-spec execution checklist for current LOD implementation |
| [Terrain/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md](Terrain/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md) | ACTIVE | Terrain banding visual artifact diagnostic |
| [Terrain/SDF_SurfaceNets_ECS_Overview.md](Terrain/SDF_SurfaceNets_ECS_Overview.md) | CURRENT | SDF terrain architecture overview |

### Terrain / Scatter

| Document | Status | Description |
|----------|--------|-------------|
| [Terrain/Scatter/TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md](Terrain/Scatter/TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md) | PHASE C IN PROGRESS | Phase A ✅ Phase B ✅ Phase C (visual) 🔄 — all systems implemented, awaiting Play Mode visual confirmation |
| [Terrain/Scatter/TERRAIN_PLAINS_TREE_VARIANT_YAW_SPEC.md](Terrain/Scatter/TERRAIN_PLAINS_TREE_VARIANT_YAW_SPEC.md) | ACTIVE | Deterministic 3-variant plains tree mesh selection plus per-instance Y-axis yaw rotation |
| [Terrain/Scatter/TERRAIN_SURFACE_SCATTER_PLAN.md](Terrain/Scatter/TERRAIN_SURFACE_SCATTER_PLAN.md) | DESIGN | Rollout plan for generalizing tree-only placement into reusable surface scatter families |
| [Terrain/Scatter/SURFACE_SCATTER_LOD_SPEC.md](Terrain/Scatter/SURFACE_SCATTER_LOD_SPEC.md) | ACTIVE | Distance-based near/far mesh swap for scatter trees & rocks inside RenderMeshInstanced path — targets the vertex-bound finding in the render perf report |
| [Terrain/Scatter/SCATTER_LOD_SPEED_BIAS_SPEC.md](Terrain/Scatter/SCATTER_LOD_SPEED_BIAS_SPEC.md) | DESIGN | Shrinks the scatter LOD swap distance as player speed rises — drops scatter detail during fast airborne movement (extends SURFACE_SCATTER_LOD_SPEC) |
| [Terrain/Scatter/TERRAIN_SURFACE_SCATTER_SPEC.md](Terrain/Scatter/TERRAIN_SURFACE_SCATTER_SPEC.md) | DESIGN | Runtime contract for chunk-scattered trees, bushes, rocks, ore nodes, and similar discrete props |
| [Terrain/Scatter/TERRAIN_SURFACE_SCATTER_SCHEMA.md](Terrain/Scatter/TERRAIN_SURFACE_SCATTER_SCHEMA.md) | DESIGN | First ECS/data breakdown for tree-plus-rock surface scatter lifecycle |
| [Terrain/Scatter/TERRAIN_TREE_PLACEMENT_SPEC.md](Terrain/Scatter/TERRAIN_TREE_PLACEMENT_SPEC.md) | DESIGN | Tree-specific placement behavior within the broader surface scatter layer |
| [Terrain/Scatter/GRASS_ECS_SPEC.md](Terrain/Scatter/GRASS_ECS_SPEC.md) | PLANNING (DEFERRED) | GPU-instanced, edit-reactive grass via DrawMeshInstancedIndirect; post-MVP, staged behind core terrain and trees |

## Biomes

| Document | Status | Description |
|----------|--------|-------------|
| [Biomes/Windswept_Colossus_Plains_Biome_Spec.md](Biomes/Windswept_Colossus_Plains_Biome_Spec.md) | ACTIVE | Procedural biome definition for the MVP plains — terrain/grass/scatter parameters in system units, MVP vs post-MVP pipeline status, relic seating hooks, biome-selection stub |
| [Biomes/TERRAIN_BIOME_NOISE_SPEC.md](Biomes/TERRAIN_BIOME_NOISE_SPEC.md) | DESIGN | Behavior spec for biome-aware terrain generation in the active SDF + Surface Nets pipeline |
| [Biomes/TERRAIN_BIOME_NOISE_SCHEMA.md](Biomes/TERRAIN_BIOME_NOISE_SCHEMA.md) | DESIGN | Concrete data model for the biome-noise behavior (sibling of the strategy plan + noise spec) |
| [Biomes/BIOME_TERRAIN_FIELD_SPEC.md](Biomes/BIOME_TERRAIN_FIELD_SPEC.md) | DESIGN | Phase 2 world-field driven terrain: WorldSample, region classifier, per-region shaping, rare features |
| [Biomes/BIOME_GRASS_STREAMING_MVP_PLAN.md](Biomes/BIOME_GRASS_STREAMING_MVP_PLAN.md) | DESIGN | Biome-based grass streaming MVP plan for infinite terrain |

## Rendering

| Document | Status | Description |
|----------|--------|-------------|
| [Rendering/MVP_VISTA_MOMENT_SPEC.md](Rendering/MVP_VISTA_MOMENT_SPEC.md) | **ACTIVE — MVP PRIORITY** | Vista discovery experience: atmospheric haze + mountain horizon + relic hand; gap analysis + ordered implementation |
| [Rendering/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md](Rendering/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md) | INVESTIGATING | Working doc: what the ground-plane impostor + fog actually do today vs. spec, re-scoping tickets V1/V2 from screenshot evidence |
| [Rendering/SKYBOXPLAN.md](Rendering/SKYBOXPLAN.md) | PHASE 2 COMPLETE · PHASE 3 PLANNED | Procedural gradient sky plan; Phase 3 = vista atmosphere (ticket V6). Companion SPEC/TESTS archived in `Archives/Skybox_2026/` |
| [Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md](Rendering/GROUND_PLANE_IMPOSTOR_SPEC.md) | **ACTIVE — MVP PRIORITY** | Horizontal ground-plane impostor for sky-drop sequence; terrain-colored flat disc beyond chunk radius; §12 = mid-field variation plan (macro luminance + fake relief + optional undulation, ticket V17) |
| [Rendering/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md](Rendering/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) | DESIGN (DEFERRED) | Seed-driven far-horizon impostor plan for mountain/hill/sea silhouette rendering (Phase 2; builds after ground plane) |
| [Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md](Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md) | ACTIVE (MVP slice built) | Single atmosphere/palette authority + global `_Atmo*` uniforms + shared aerial-perspective HLSL; unifies sky, ground disc, mountain impostor, terrain & fog color under one time-of-day/biome source (ticket V9; consumed by V3, V8) |
| [Rendering/LANDMARK_DRAW_DISTANCE_SPEC.md](Rendering/LANDMARK_DRAW_DISTANCE_SPEC.md) | PROPOSED | Hero-relic pop-in fix: raise camera far plane while the world stays short ("landmarks never cull"), decouple `_AtmoFarFade` from the far plane, dithered edge/spawn fades; covers 600→2000u, hands off to R5 cards beyond (ticket R6) |
| [Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md](Rendering/METEOR_ARRIVAL_SEQUENCE_SPEC.md) | PROPOSED | Diegetic opening beat: meteor-interior loading shell over the sky-drop readiness gate (V14) + burning-descent VFX (V13); break-open on real readiness, C3 dust handoff, arrival-sequence trigger only |
| [Rendering/SKY_MOUNTAIN_BAND_SPEC.md](Rendering/SKY_MOUNTAIN_BAND_SPEC.md) | ACTIVE (owner-approved at ground level) | Skybox mountain band: ridged FBM silhouette + finer back ridge + horizon demarcation line + snow-cap toggle (off by default); interim until the Phase-2 seed-driven horizon ring (ticket V15) |
| [Rendering/RENDER_PERF_PROFILE_REPORT.md](Rendering/RENDER_PERF_PROFILE_REPORT.md) | REPORT | Basic Terrain Scene profiling: scene is vertex-bound (trees/rocks = 92% of verts), NOT fill-rate bound — low-res rendering gives no FPS win; LODs are the lever |

## Player

| Document | Status | Description |
|----------|--------|-------------|
| [Player/PLAYER_CHARACTER_VISUAL_SWAP_SPEC.md](Player/PLAYER_CHARACTER_VISUAL_SWAP_SPEC.md) | PROPOSED | Synty SM_Chr_Male_01 + Kevin Iglesias animations — capsule visual swap, Animator bridge, per-state clip mapping |
| [Player/PLAYER_LANDING_ANIMATION_SPEC.md](Player/PLAYER_LANDING_ANIMATION_SPEC.md) | PROPOSED | Tiered landing animations (light/hard/slide) + visual floor clamp to fix slingshot terrain clipping |
| [Player/PLAYER_PIT_MUDINESS_HYPOTHESIS_TEST_PLAN.md](Player/PLAYER_PIT_MUDINESS_HYPOTHESIS_TEST_PLAN.md) | ACTIVE | Test-first hypothesis matrix for pit-wall mudiness root-cause validation |
| [Player/PLAYER_BOOTSTRAP_FIX_SPEC.md](Player/PLAYER_BOOTSTRAP_FIX_SPEC.md) | ACTIVE | Player bootstrap reliability for DOTS tests |
| [Player/SPACE_FIGURE_SPEC.md](Player/SPACE_FIGURE_SPEC.md) | ACTIVE | Humanoid block-figure model spec (BoxPlayer rig, Unity Humanoid Avatar mapping) — moved from repo root |

### Player / Movement

| Document | Status | Description |
|----------|--------|-------------|
| [Player/Movement/MOVEMENT_PLANNING.md](Player/Movement/MOVEMENT_PLANNING.md) | ACTIVE | Movement system design spec — slingshot, glide, thermals, visual feedback, camera behavior, prototype order |
| [Player/Movement/AAA_MOVEMENT_CHECKLIST.md](Player/Movement/AAA_MOVEMENT_CHECKLIST.md) | ACTIVE | 21-point playtest evaluation rubric for traversal feel |
| [Player/Movement/MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md](Player/Movement/MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md) | PROPOSED | ECS components, systems, update ordering, camera resolver architecture |
| [Player/Movement/SLINGSHOT_ANIMATION_CONTROLLER_SPEC.md](Player/Movement/SLINGSHOT_ANIMATION_CONTROLLER_SPEC.md) | SPEC | Wire the three authored slingshot clips into the Animator state machine — no gameplay/physics changes |
| [Player/Movement/SPEED_SHAKE_SPEC.md](Player/Movement/SPEED_SHAKE_SPEC.md) | DESIGN | Velocity-driven continuous camera shake for sense of speed (movement feel polish) |

## Structures

| Document | Status | Description |
|----------|--------|-------------|
| [Structures/STRUCTURE_PLACEMENT_PLAN.md](Structures/STRUCTURE_PLACEMENT_PLAN.md) | DESIGN | Rollout plan for deterministic semantic structure placement (dungeons, villages, relics, ruins) |
| [Structures/STRUCTURE_PLACEMENT_SPEC.md](Structures/STRUCTURE_PLACEMENT_SPEC.md) | DESIGN | Runtime contract for region-scale anchors, hard spacing constraints, and structure-family realization |
| [Structures/MAGIC_GRID_SPEC.md](Structures/MAGIC_GRID_SPEC.md) | DESIGN | Analytic world-space XZ magic lattice: power-source nodes, WFC-build-on-node affordance, sparse claimed-node alignment state, per-template NodeAffinity, two-sources-one-pipeline with the free placer + universal influence query; chunk-decoupled, additive brightness cue (air-warp deferred) |
| [Structures/RELIC_LOD_IMPOSTOR_SPEC.md](Structures/RELIC_LOD_IMPOSTOR_SPEC.md) | IMPLEMENTED | Distance-based full-mesh ↔ impostor swap for large relics; supersedes archived `RELIC_RENDER_REFACTOR_SPEC.md` §8. Far-plane clipping now masked by distance fog. |
| [Structures/RELIC_BILLBOARD_IMPOSTOR_SPEC.md](Structures/RELIC_BILLBOARD_IMPOSTOR_SPEC.md) | DESIGN | Pre-baked camera-facing billboard as LOD 1 impostor for relics (atlas bake tool + Y-axis facing system); future work for distant vistas |

## WFC Dungeon

| Document | Description |
|----------|-------------|
| [WFC/WFC_Dungeon_Test_Plan.md](WFC/WFC_Dungeon_Test_Plan.md) | WFC dungeon test strategy |
| [WFC/MAP_WFC.md](WFC/MAP_WFC.md) | WFC system map and components |
| [WFC/SOCKET_TABLE.md](WFC/SOCKET_TABLE.md) | WFC socket table for rotation tests |

## Multiplayer

| Document | Status | Description |
|----------|--------|-------------|
| [Multiplayer/MULTIPLAYER_SPEC.md](Multiplayer/MULTIPLAYER_SPEC.md) | DESIGN | Multiplayer readiness: Pre-MVP hygiene, MVP command architecture, Post-MVP arena PvP path. Supersedes multiplayer_evaluation_spec.md |

## Audio

| Document | Status | Description |
|----------|--------|-------------|
| [Audio/AUDIO_SPEC.md](Audio/AUDIO_SPEC.md) | DESIGN (proposed) | MVP sound — event-driven managed audio layer (`GameAudio` façade + pooled sources), reuses VFX/gameplay signals. MVP sound list (comet roar, landing, wind) + trigger hooks; open decisions in §8 |

## Persistence

| Document | Status | Description |
|----------|--------|-------------|
| [Persistence/PERSISTENCE_SPEC.md](Persistence/PERSISTENCE_SPEC.md) | DESIGN (post-MVP vision) | Full 5-layer persistence vision + offline sim (entity/NPC/player deltas). **MVP scope is authoritative in [Terrain/WORLD_STRUCTURE_SPEC.md](Terrain/WORLD_STRUCTURE_SPEC.md) §9** — SDF-delta saves + config-hash header; this doc is the roadmap beyond it |

## Process

| Document | Status | Description |
|----------|--------|-------------|
| [Process/CODEBASE_SIMPLIFICATION_PLAN.md](Process/CODEBASE_SIMPLIFICATION_PLAN.md) | PLANNED | Codebase cleanup workflow (naming fixes, dead-code archiving, doc ordering) — token-efficient 3-phase process; living home for rename maps, archive lists, and batch logs |
| [Process/THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md](Process/THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md) | ACTIVE | Asset pre-screen, sandbox validation, and fit scoring workflow for third-party content |
| [Process/ArtAndDOTS_Pipeline.md](Process/ArtAndDOTS_Pipeline.md) | ACTIVE | 16-bit art + DOTS integration guide |

## Testing

| Document | Description |
|----------|-------------|
| [Testing/Codex_PlayModeSmokeTestPlan.md](Testing/Codex_PlayModeSmokeTestPlan.md) | Play Mode smoke test plan for `Smoke_BasicPlayable.unity` |
| [Testing/PlayerMovementTestPlan.md](Testing/PlayerMovementTestPlan.md) | Player movement test plan (moved from Player/Test in R48 consolidation) |
| [Testing/SmokeSceneSetup.md](Testing/SmokeSceneSetup.md) | Setup notes for the smoke-test scene |

## Tickets

| Document | Status | Description |
|----------|--------|-------------|
| [Tickets/TICKETS.md](Tickets/TICKETS.md) | ACTIVE | Slim ticket board — status tables for the current work-set + backlog, linking into the detail docs below |
| [Tickets/done/vista-moment.md](Tickets/done/vista-moment.md) | DONE | Completed work-set — MVP Vista Moment (closed 2026-07-21); Vista/Camera Feel/Animation build history |
| [Tickets/backlog.md](Tickets/backlog.md) | ACTIVE | Backlog ticket detail, not yet pulled into a work-set |
| [Tickets/done/](Tickets/done/) | — | Completed work-set docs move here untouched (empty until the first work-set finishes) |

## Project-Level Docs

| Document | Description |
|----------|-------------|
| [MASTER_PLAN.md](MASTER_PLAN.md) | **Project overview — vision, status, phase roadmap, doc map** |
| [DOCUMENTATION_SYSTEM_SPEC.md](DOCUMENTATION_SYSTEM_SPEC.md) | Canonical documentation structure, metadata, and AI-discovery rules |
| [/CLAUDE.md](/CLAUDE.md) | Claude Code project instructions |
| [/.github/copilot-instructions.md](/.github/copilot-instructions.md) | GitHub Copilot instructions |
| [PROJECT_NOTES.md](PROJECT_NOTES.md) | Current work session notes and TODO |
| [PROJECT_STRUCTURE_DOTS.md](PROJECT_STRUCTURE_DOTS.md) | DOTS-first folder layout |
| [DOCUMENTATION_CHANGELOG.md](DOCUMENTATION_CHANGELOG.md) | Record of documentation reorganization rounds |
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | Master bug/issue tracker |

## Code Guides

| Document | Description |
|----------|-------------|
| [/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md](/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) | Bootstrap pattern guide with physics setup |
| [/Assets/Scripts/DOTS/Test/Testing_Documentation.md](/Assets/Scripts/DOTS/Test/Testing_Documentation.md) | Complete test catalog |

## Audits & Reports

| Document | Description |
|----------|-------------|
| [/TERRAIN_SYSTEMS_CODE_AUDIT.md](/TERRAIN_SYSTEMS_CODE_AUDIT.md) | Redundant/obsolete terrain code analysis |

## Archives

| Folder | Contents |
|--------|----------|
| [Archives/ManualTestScripts_2026/](Archives/ManualTestScripts_2026/) | Manual test-harness catalog + notes (harnesses deleted in cleanup round 2, plan A22) |
| [Archives/FirstPersonController/](Archives/FirstPersonController/) | Archived first-person controller docs |
| [Archives/TerrainDesign/](Archives/TerrainDesign/) | Early terrain design explorations |
| [Archives/Fixes/](Archives/Fixes/) | Test planning archives |
| [Archives/WFC_Debug_Oct2025/](Archives/WFC_Debug_Oct2025/) | Oct 2025 WFC rotation debug campaign |
| [Archives/TestReports_Oct2025/](Archives/TestReports_Oct2025/) | Oct 2025 test campaign reports |
| [Archives/DOTS_Migration_Plan.md](Archives/DOTS_Migration_Plan.md) | Legacy hybrid terrain migration (superseded by SDF pipeline) |
| [Archives/AI_Instructions.md](Archives/AI_Instructions.md) | AI assistant standards (superseded by CLAUDE.md) |
| [Archives/PROJECT_NOTES_2025-11.md](Archives/PROJECT_NOTES_2025-11.md) | Nov 2025 work session notes (camera system, test org) |
| [Archives/SeamDebug_2026/](Archives/SeamDebug_2026/) | Concluded terrain seam-debug lineage: OBSOLETE spec, v1 density spec, mesh spec, implementation report (2026-07-02, doc cleanup D1) |
| [Archives/Skybox_2026/](Archives/Skybox_2026/) | Skybox Phase 1/2 spec + test plan, superseded by `Rendering/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md` (2026-07-02, doc cleanup D2) |
| [Archives/ResolvedBugfixes_2026/](Archives/ResolvedBugfixes_2026/) | Resolved bugfix specs: player terrain fall-through, terrain edit controls, camera identity mismatch (2026-07-02, doc cleanup D3) |
| [Archives/StructurePlacement_2026/](Archives/StructurePlacement_2026/) | `RELIC_RENDER_REFACTOR_SPEC.md`, superseded by `RELIC_LOD_IMPOSTOR_SPEC.md` §8 (2026-07-02, doc cleanup D4) |
| [Archives/TerrainHeightMaps_2026/](Archives/TerrainHeightMaps_2026/) | Plains noise algorithm spec, values absorbed by the MVP tree checklist (2026-07-02, doc cleanup D5) |
| [Archives/RootLegacy_2026/](Archives/RootLegacy_2026/) | Stale root-level records: namespace refactor, multiplayer evaluation, ISystem usage report, structure review, Unity 6 compat notes, plus the whole `DebugTraces/` folder (2026-07-02, doc cleanup D6) |
| [Archives/MVP_Ideation_2026/](Archives/MVP_Ideation_2026/) | Stale MVP ideation docs from the former `mvp/` folder, now `Biomes/` (2026-07-02, doc cleanup D7) |
| [Archives/LOD_2026/](Archives/LOD_2026/) | Superseded LOD spec + plan; content merged into root `Terrain/DOTS_Terrain_LOD_SPEC.md` before archiving (2026-07-02, doc cleanup D8) |

## External Reference

| Folder | Contents |
|--------|----------|
| [Reference/External/SmolbeanPlanet3D/](Reference/External/SmolbeanPlanet3D/) | External reference material from an unrelated project (SmolbeanPlanet3D) — patterns/salvage notes only, not project documentation |
