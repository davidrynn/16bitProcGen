# Document Index

**Last Updated:** 2026-04-11

> **New here?** Start with [MASTER_PLAN.md](MASTER_PLAN.md) — it has the project vision, current status, phase roadmap, and a curated document map.

Quick-reference index of all project documentation. Docs are organized by category with status and links.

---

## Active Specs

| Document | Status | Description |
|----------|--------|-------------|
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | ACTIVE | Master bug/issue tracker |
| [AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md](AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md) | ACTIVE | SDF + Surface Nets terrain implementation roadmap |
| [AI/TerrainHeightMaps/TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md](AI/TerrainHeightMaps/TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md) | PHASE C IN PROGRESS | Phase A ✅ Phase B ✅ Phase C (visual) 🔄 — all systems implemented, awaiting Play Mode visual confirmation |
| [AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_PLAN.md](AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_PLAN.md) | DESIGN | Rollout plan for generalizing tree-only placement into reusable surface scatter families |
| [AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md](AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SPEC.md) | DESIGN | Runtime contract for chunk-scattered trees, bushes, rocks, ore nodes, and similar discrete props |
| [AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SCHEMA.md](AI/TerrainHeightMaps/TERRAIN_SURFACE_SCATTER_SCHEMA.md) | DESIGN | First ECS/data breakdown for tree-plus-rock surface scatter lifecycle |
| [AI/TerrainHeightMaps/TERRAIN_TREE_PLACEMENT_SPEC.md](AI/TerrainHeightMaps/TERRAIN_TREE_PLACEMENT_SPEC.md) | DESIGN | Tree-specific placement behavior within the broader surface scatter layer |
| [AI/TERRAIN_EDIT_PLAYER_SAFETY_LOCAL_GRID_SPEC.md](AI/TERRAIN_EDIT_PLAYER_SAFETY_LOCAL_GRID_SPEC.md) | PROPOSED | Player-overlap edit guard + deterministic chunk-local grid guarantees |
| [AI/PLAYER_PIT_MUDINESS_HYPOTHESIS_TEST_PLAN.md](AI/PLAYER_PIT_MUDINESS_HYPOTHESIS_TEST_PLAN.md) | ACTIVE | Test-first hypothesis matrix for pit-wall mudiness root-cause validation |
| [AI/PERSISTENCE_SPEC.md](AI/PERSISTENCE_SPEC.md) | DESIGN | World persistence — edit journals, entity/NPC/player state (Phase 4) |
| [AI/BIOME_GRASS_STREAMING_MVP_PLAN.md](AI/BIOME_GRASS_STREAMING_MVP_PLAN.md) | DESIGN | Biome-based grass streaming MVP plan for infinite terrain |
| [AI/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md](AI/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) | DESIGN (DEFERRED) | Seed-driven far-horizon impostor plan for mountain/hill/sea silhouette rendering |
| [AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) | ROOT CAUSE FIXED | Fall-through debug: missing PhysicsWorldIndex |
| [AI/TERRAIN_EDIT_CONTROLS_SPEC.md](AI/TERRAIN_EDIT_CONTROLS_SPEC.md) | IMPLEMENTED | Terrain edit raycast fix, Input System migration, reticle |
| [AI/PLAYER_BOOTSTRAP_FIX_SPEC.md](AI/PLAYER_BOOTSTRAP_FIX_SPEC.md) | ACTIVE | Player bootstrap reliability for DOTS tests |
| [AI/DOTS_Terrain_LOD_Implementation_Checklist.md](AI/DOTS_Terrain_LOD_Implementation_Checklist.md) | ACTIVE | Gap-to-spec execution checklist for current LOD implementation |
| [AI/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md](AI/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md) | ACTIVE | Terrain banding visual artifact diagnostic |
| [AI/TERRAIN_SEAM_DEBUG_SPEC_v1.md](AI/TERRAIN_SEAM_DEBUG_SPEC_v1.md) | COMPLETE | Terrain seam/ring pattern investigation |
| [AI/TERRAIN_SEAM_DEBUG_MESH_SPEC.md](AI/TERRAIN_SEAM_DEBUG_MESH_SPEC.md) | COMPLETE | Mesh seam validator findings |
| [SDF_SurfaceNets_ECS_Overview.md](SDF_SurfaceNets_ECS_Overview.md) | CURRENT | SDF terrain architecture overview |

## Project-Level Docs

| Document | Description |
|----------|-------------|
| [MASTER_PLAN.md](MASTER_PLAN.md) | **Project overview — vision, status, phase roadmap, doc map** |
| [DOCUMENTATION_SYSTEM_SPEC.md](DOCUMENTATION_SYSTEM_SPEC.md) | Canonical documentation structure, metadata, and AI-discovery rules |
| [/CLAUDE.md](/CLAUDE.md) | Claude Code project instructions |
| [/.github/copilot-instructions.md](/.github/copilot-instructions.md) | GitHub Copilot instructions |
| [PROJECT_NOTES.md](PROJECT_NOTES.md) | Current work session notes and TODO |
| [PROJECT_STRUCTURE_DOTS.md](PROJECT_STRUCTURE_DOTS.md) | DOTS-first folder layout |

## Code Guides

| Document | Description |
|----------|-------------|
| [/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md](/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) | Bootstrap pattern guide with physics setup |
| [/Assets/Scripts/DOTS/Test/Testing_Documentation.md](/Assets/Scripts/DOTS/Test/Testing_Documentation.md) | Complete test catalog |
| [Unity6_Compatibility_Notes.md](Unity6_Compatibility_Notes.md) | Unity 6 DOTS compatibility fixes |
| [ArtAndDOTS_Pipeline.md](ArtAndDOTS_Pipeline.md) | 16-bit art + DOTS integration |

## Audits & Reports

| Document | Description |
|----------|-------------|
| [/TERRAIN_SYSTEMS_CODE_AUDIT.md](/TERRAIN_SYSTEMS_CODE_AUDIT.md) | Redundant/obsolete terrain code analysis |
| [/ISystem_Usage_Report.md](/ISystem_Usage_Report.md) | ISystem usage patterns audit |
| [AI/TERRAIN_SEAM_DEBUG_IMPLEMENTATION_REPORT.md](AI/TERRAIN_SEAM_DEBUG_IMPLEMENTATION_REPORT.md) | Seam debug implementation report |
| [CURSOR_NamespaceFlattening_Refactor.md](CURSOR_NamespaceFlattening_Refactor.md) | Namespace refactor (COMPLETE) |

## WFC Dungeon

| Document | Description |
|----------|-------------|
| [WFC_Dungeon_Test_Plan.md](WFC_Dungeon_Test_Plan.md) | WFC dungeon test strategy |
| [WFC/MAP_WFC.md](WFC/MAP_WFC.md) | WFC system map and components |
| [WFC/SOCKET_TABLE.md](WFC/SOCKET_TABLE.md) | WFC socket table for rotation tests |

## Archives

| Folder | Contents |
|--------|----------|
| [Archives/FirstPersonController/](Archives/FirstPersonController/) | Archived first-person controller docs |
| [Archives/TerrainDesign/](Archives/TerrainDesign/) | Early terrain design explorations |
| [Archives/Fixes/](Archives/Fixes/) | Test planning archives |
| [Archives/WFC_Debug_Oct2025/](Archives/WFC_Debug_Oct2025/) | Oct 2025 WFC rotation debug campaign |
| [Archives/TestReports_Oct2025/](Archives/TestReports_Oct2025/) | Oct 2025 test campaign reports |
| [DebugTraces/](DebugTraces/) | Historical WFC debug traces |
| [AI/TERRAIN_SEAM_DEBUG_SPEC_OBSOLETE.md](AI/TERRAIN_SEAM_DEBUG_SPEC_OBSOLETE.md) | Superseded by v1 |
| [Archives/DOTS_Migration_Plan.md](Archives/DOTS_Migration_Plan.md) | Legacy hybrid terrain migration (superseded by SDF pipeline) |
| [Archives/AI_Instructions.md](Archives/AI_Instructions.md) | AI assistant standards (superseded by CLAUDE.md) |
| [Archives/PROJECT_NOTES_2025-11.md](Archives/PROJECT_NOTES_2025-11.md) | Nov 2025 work session notes (camera system, test org) |
