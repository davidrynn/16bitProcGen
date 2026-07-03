# Documentation Reorganization Changelog

---

## 2026-07-02 — Codebase Cleanup Doc Reorder Round (Batch 19, `AI/CODEBASE_SIMPLIFICATION_PLAN.md` §6.3 rows D1–D8)

**Purpose:** Archive concluded/superseded docs and de-duplicate the LOD doc cluster as part of the broader codebase simplification pass.

### Archived (21 docs total, into 8 new `Assets/Docs/Archives/*_2026` subfolders)

- **`Archives/SeamDebug_2026/`** (D1) — `TERRAIN_SEAM_DEBUG_SPEC_OBSOLETE.md`, `TERRAIN_SEAM_DEBUG_SPEC_v1.md`, `TERRAIN_SEAM_DEBUG_MESH_SPEC.md`, `TERRAIN_SEAM_DEBUG_IMPLEMENTATION_REPORT.md` — moved from `AI/`; concluded investigation lineage.
- **`Archives/Skybox_2026/`** (D2) — `SKYBOXSPEC.md`, `SKYBOXTESTS.md` — moved from `AI/`; Phase 1/2 complete, forward work lives in `AI/ATMOSPHERE_COLOR_AUTHORITY_SPEC.md`. `AI/SKYBOXPLAN.md` stays active.
- **`Archives/ResolvedBugfixes_2026/`** (D3) — `PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md`, `TERRAIN_EDIT_CONTROLS_SPEC.md`, `CAMERA_IDENTITY_FIX_SPEC.md` — moved from `AI/`; all resolved/implemented.
- **`Archives/StructurePlacement_2026/`** (D4) — `RELIC_RENDER_REFACTOR_SPEC.md` — moved from `AI/STRUCTURE PLACEMENT/`; superseded by `RELIC_LOD_IMPOSTOR_SPEC.md` §8.
- **`Archives/TerrainHeightMaps_2026/`** (D5) — `TERRAIN_PLAINS_NOISE_ALGORITHM.md` — moved from `AI/TerrainHeightMaps/`; values absorbed by `TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md`.
- **`Archives/RootLegacy_2026/`** (D6) — `CURSOR_NamespaceFlattening_Refactor.md`, `multiplayer_evaluation_spec.md`, `ISystem_Usage_Report.md`, `StructureReview.md`, `Unity6_Compatibility_Notes.md`, and the whole `DebugTraces/` folder — moved from `Assets/Docs/` root; stale/complete records. `StructureReview.md`'s one still-open finding (legacy non-DOTS `Assets/Scripts/Terrain/` tree) was folded into `CODEBASE_SIMPLIFICATION_PLAN.md` §6.7 as row S16 before archiving.
- **`Archives/MVP_Ideation_2026/`** (D7) — `mvp_feature_list.md`, `wfc_good_vs_evil_rules.md` — moved from `mvp/`; stale ideation docs.
- **`Archives/LOD_2026/`** (D8) — `TERRAIN_LOD_SPEC.md`, `DOTS_Terrain_LOD_Plan.md` — moved from `AI/`; superseded by root `DOTS_Terrain_LOD_SPEC.md`, which is now the sole LOD source of truth (see next section).

Every archived doc above received a `Status: ARCHIVED (2026-07-02)` header line, with a `Superseded By` / historical-reference note where applicable, per `DOCUMENTATION_SYSTEM_SPEC.md` §6.3.

### Folder rename (D7)

- `Assets/Docs/mvp/` → `Assets/Docs/Biomes/` (now contains only `Windswept_Colossus_Plains_Biome_Spec.md`, the one live biome spec).

### LOD doc cluster de-duplication (D8)

Root `Assets/Docs/DOTS_Terrain_LOD_SPEC.md` was confirmed as the actual source of truth (the implementation checklist already pointed at it). `AI/TERRAIN_LOD_SPEC.md` and `AI/DOTS_Terrain_LOD_Plan.md` were content-diffed against it; unique still-relevant content (confirmed implementation namespace `DOTS.Terrain.LOD`, the edit-authoritative LOD0 promotion policy for far edited chunks, the `NativeParallelHashMap`-based neighbor-clamp approach, and the `DebugSettings.LogLod` flag) was merged into a new "Merged from Superseded Docs" section in the root spec before both AI/ docs were archived. `DOTS_Terrain_LOD_Implementation_Checklist.md` remains active alongside the root spec.

### Index

`DOCUMENT_INDEX.md` updated: every doc moved above had its entry's path corrected (or moved to the Archives table); no other entries were touched.

---

## 2025-11-05 — Initial Reorganization Round

**Purpose:** Organized and consolidated project documentation for better maintainability

### Summary of Changes

### 1. Archived FirstPerson Controller Documentation

**Reason:** Current work focuses on simpler camera follow system (per PROJECT_NOTES.md)

**Moved to:** `Assets/Docs/Archives/FirstPersonController/`

**Files Archived:**
- `README_FirstPerson.md`
- `FirstPersonController_Implementation_Summary.md`
- `FirstPerson_Fix_SystemOrdering.md` (from Archives/Fixes)
- `player-movement-pipeline.md` (from Archives/Fixes)
- `PlayerMovement_Testing_Context.md` (from Archives/Fixes)

---

### 2. Consolidated Bootstrap Documentation

**Reason:** 7 separate markdown files with overlapping content was excessive

**Action:** Consolidated into single comprehensive guide

**Result:**
- **Created:** `Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md` (comprehensive)
- **Updated:** `Assets/Scripts/Player/Bootstrap/README.md` (brief overview, points to guide)
- **Deleted:** 6 redundant files:
  - `BOOTSTRAP_SETUP_GUIDE.md`
  - `SCENE_SETUP_CHECKLIST.md`
  - `RENDERING_EXPLANATION.md`
  - `PHYSICS_SETUP_GUIDE.md`
  - `PHYSICS_QUICK_START.md`
  - `QUICK_COMPARISON.md`

**Before:**
```
Bootstrap/
├── README.md (195 lines)
├── BOOTSTRAP_SETUP_GUIDE.md (100 lines)
├── SCENE_SETUP_CHECKLIST.md (99 lines)
├── RENDERING_EXPLANATION.md (196 lines)
├── PHYSICS_SETUP_GUIDE.md (282 lines)
├── PHYSICS_QUICK_START.md (205 lines)
└── QUICK_COMPARISON.md (167 lines)
Total: 7 files, ~1,244 lines
```

**After:**
```
Bootstrap/
├── README.md (58 lines, quick reference)
└── BOOTSTRAP_GUIDE.md (508 lines, comprehensive)
Total: 2 files, 566 lines
```

---

### 3. Moved UNITY6_FIXES.md to Documentation Folder

**Reason:** Should be in documentation folder, not project root

**Action:**
- **Moved from:** `C:\UnityWorkspace\16bitProcGen\UNITY6_FIXES.md`
- **Moved to:** `Assets/Docs/Unity6_Compatibility_Notes.md`
- **Renamed:** For clearer naming convention

---

### 4. Renamed and Archived SPEC.md

**Reason:** Misleading name - not a project spec, but a specific WFC test spec from October 2025

**Action:**
- **Moved from:** `Assets/Docs/SPEC.md`
- **Moved to:** `Assets/Docs/Archives/TestReports_Oct2025/WFC_Seed_DeadEnd_SPEC.md`
- **Renamed:** For accurate description of contents

---

### 5. Updated References

**Updated Files:**
- `Assets/Docs/AI_Instructions.md` - Fixed bootstrap guide reference
- `Assets/Scripts/Player/Bootstrap/README.md` - Points to new consolidated guide

**No broken links remain**

---

## Documentation Structure (After Reorganization)

```
Assets/Docs/
├── AI_Instructions.md ⭐ (Main AI development guidelines)
├── PROJECT_NOTES.md ⭐ (Current work session notes, TODOs)
├── DOCUMENTATION_CHANGELOG.md (This file)
│
├── Unity6_Compatibility_Notes.md (Moved from root)
├── DOTS_Migration_Plan.md
├── ArtAndDOTS_Pipeline.md
├── WFC_Dungeon_Test_Plan.md
│
├── WFC/
│   ├── MAP_WFC.md
│   └── SOCKET_TABLE.md
│
├── DebugTraces/
│   ├── README.md
│   ├── ConsoleLogs.txt
│   └── RUN_TRACE_5x5.md
│
├── Archives/
│   ├── FirstPersonController/ ⭐ (Newly organized)
│   │   ├── README_FirstPerson.md
│   │   ├── FirstPersonController_Implementation_Summary.md
│   │   ├── FirstPerson_Fix_SystemOrdering.md
│   │   ├── player-movement-pipeline.md
│   │   └── PlayerMovement_Testing_Context.md
│   │
│   ├── TestReports_Oct2025/
│   │   ├── WFC_Seed_DeadEnd_SPEC.md ⭐ (Renamed from SPEC.md)
│   │   ├── README.md
│   │   ├── (and 11 other test report files...)
│   │   └── logs/
│   │
│   ├── WFC_Debug_Oct2025/
│   │   └── (7 WFC debug files)
│   │
│   ├── TerrainDesign/
│   │   └── (3 terrain design files)
│   │
│   └── Fixes/
│       └── TERRAIN_REFACTOR_TEST_PLAN.md
│
└── DevLog - NotesOnAI.txt (Kept as is - active dev log)

Assets/Scripts/Player/Bootstrap/
├── README.md ⭐ (Simplified, points to guide)
├── BOOTSTRAP_GUIDE.md ⭐ (New comprehensive guide)
├── PlayerCameraBootstrap.cs
├── PlayerCameraBootstrap_WithVisuals.cs
├── SimplePlayerMovementSystem.cs
└── Tests/
```

---

## Statistics

**Files Moved:** 10  
**Files Deleted:** 6 (redundant markdown)  
**Files Created:** 2 (BOOTSTRAP_GUIDE.md, DOCUMENTATION_CHANGELOG.md)  
**Files Renamed:** 2 (UNITY6_FIXES.md, SPEC.md)  
**References Updated:** 2 files

**Total Documentation Reduction:** ~678 lines removed through consolidation

---

## Guidelines for Future Documentation

### ✅ DO
- Keep a single comprehensive guide per topic
- Use clear, descriptive names
- Archive outdated implementation docs
- Update references when moving files
- Keep PROJECT_NOTES.md current with session work
- Store personal dev notes in root Docs (DevLog)

### ❌ DON'T
- Create multiple overlapping guides
- Use generic names like "SPEC.md" for specific features
- Leave documentation at project root
- Create documentation without clear purpose
- Let outdated docs accumulate in main folders

---

## Active Documentation Files

### For Development (Start Here)
1. **`PROJECT_NOTES.md`** - Current work session, TODOs, recent completions
2. **`AI_Instructions.md`** - DOTS-first development principles and standards

### For Bootstrap/Player Setup
1. **`Bootstrap/BOOTSTRAP_GUIDE.md`** - Complete bootstrap setup guide
2. **`Bootstrap/README.md`** - Quick reference

### For Unity 6 Migration
1. **`Unity6_Compatibility_Notes.md`** - Fixes for Unity 6 compatibility

### For WFC/Dungeon Generation
1. **`WFC_Dungeon_Test_Plan.md`** - Testing procedures
2. **`WFC/MAP_WFC.md`** - WFC algorithm documentation
3. **`WFC/SOCKET_TABLE.md`** - Socket definitions

---

**Reorganization completed by:** AI Assistant  
**Approved by:** User  
**Status:** ✅ Complete










