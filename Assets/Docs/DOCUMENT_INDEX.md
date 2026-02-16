# Document Index

**Last Updated:** 2026-02-15

Quick-reference index of all project documentation. Docs are organized by category with status and links.

---

## Active Specs

| Document | Status | Description |
|----------|--------|-------------|
| [KNOWN_ISSUES.md](KNOWN_ISSUES.md) | ACTIVE | Master bug/issue tracker |
| [AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md](AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md) | ACTIVE | SDF + Surface Nets terrain implementation roadmap |
| [AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) | ROOT CAUSE FIXED | Fall-through debug: missing PhysicsWorldIndex |
| [AI/TERRAIN_EDIT_CONTROLS_SPEC.md](AI/TERRAIN_EDIT_CONTROLS_SPEC.md) | IMPLEMENTED | Terrain edit raycast fix, Input System migration, reticle |
| [AI/PLAYER_BOOTSTRAP_FIX_SPEC.md](AI/PLAYER_BOOTSTRAP_FIX_SPEC.md) | ACTIVE | Player bootstrap reliability for DOTS tests |
| [AI/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md](AI/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md) | ACTIVE | Terrain banding visual artifact diagnostic |
| [AI/TERRAIN_SEAM_DEBUG_SPEC_v1.md](AI/TERRAIN_SEAM_DEBUG_SPEC_v1.md) | COMPLETE | Terrain seam/ring pattern investigation |
| [AI/TERRAIN_SEAM_DEBUG_MESH_SPEC.md](AI/TERRAIN_SEAM_DEBUG_MESH_SPEC.md) | COMPLETE | Mesh seam validator findings |
| [SDF_SurfaceNets_ECS_Overview.md](SDF_SurfaceNets_ECS_Overview.md) | CURRENT | SDF terrain architecture overview |

## Project-Level Docs

| Document | Description |
|----------|-------------|
| [/CLAUDE.md](/CLAUDE.md) | Claude Code project instructions |
| [/.github/copilot-instructions.md](/.github/copilot-instructions.md) | GitHub Copilot instructions |
| [AI_Instructions.md](AI_Instructions.md) | AI assistant standards |
| [PROJECT_NOTES.md](PROJECT_NOTES.md) | Work session tracking |
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
| [DOTS_Migration_Plan.md](DOTS_Migration_Plan.md) | Legacy hybrid terrain migration (superseded by SDF pipeline) |
