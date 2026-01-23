# Deprecated Files Tracking

**Last Updated:** 2025-01-XX  
**Purpose:** Track all deprecated, legacy, and archived files that have been moved to the deprecated assembly definition.

## Overview

This document tracks files that are:
- Marked as `[LEGACY]` or `[Obsolete]` in code comments
- Located in `Archive/` folders
- No longer actively used but kept for reference/backward compatibility
- Excluded from main assemblies via `DOTS.Terrain.Deprecated.asmdef`

## Assembly Definition

All deprecated files are compiled into: **`DOTS.Terrain.Deprecated.asmdef`**
- **Platform:** Editor-only (not included in builds)
- **Auto-referenced:** false (must be explicitly referenced if needed)
- **Purpose:** Prevents deprecated code from being compiled into production builds

---

## Archived Test Files

### Location: `Assets/Scripts/DOTS/Test/Archive/`

#### Root Archive Files (10 files)
1. `BasicComputeShaderTest.cs` - Basic compute shader testing
2. `CollectionsTest.cs` - Unity Collections testing
3. `ComputeShaderDebugTest.cs` - Compute shader debugging utilities
4. `JobsTest.cs` - Unity Jobs system testing
5. `MathematicsTest.cs` - Unity.Mathematics testing
6. `Phase1CompletionTest.cs` - Phase 1 completion verification
7. `PhysicsTest.cs` - Unity Physics testing
8. `SimpleComputeTest.cs` - Simple compute shader test
9. `SimpleRenderingTest.cs` - Simple rendering test
10. `SimpleTerrainTest.cs` - Simple terrain generation test

#### Manual Test Archive Files (19 files)
Location: `Assets/Scripts/DOTS/Test/Archive/Manual/`

1. `BiomeSystemTest.cs` - Biome system manual testing
2. `GlobPhysicsTest.cs` - Glob physics manual testing
3. `HybridGenerationTest.cs` - Hybrid terrain generation testing
4. `ModelAlignmentTest.cs` - Model alignment verification
5. `ModificationSystemTest.cs` - Terrain modification system testing
6. `SimpleTestManager.cs` - Simple rendering test manager (replaced by `WFCSmokeHarness.cs`)
7. `SocketPatternTest.cs` - Socket pattern rotation testing
8. `TerrainDataManagerSystemTest.cs` - Terrain data manager testing
9. `TerrainDataTest.cs` - Terrain data component testing
10. `TerrainEntityTest.cs` - Terrain entity testing
11. `TerrainGenerationTest.cs` - Terrain generation testing
12. `TerrainRefactorTest.cs` - Terrain refactor verification
13. `TerrainRefactorTestSetup.cs` - Terrain refactor test setup helper (depends on legacy `TerrainEntityManager`)
14. `TransformIntegrationTest.cs` - Transform integration testing
15. `WFCDungeonRenderingTest.cs` - WFC dungeon rendering test
16. `WFCSystemTest.cs` - WFC system testing
17. `WFCTestManager.cs` - WFC test manager (depends on deprecated `WFCSystemTest`)
18. `WFCTestSceneSetup.cs` - WFC test scene setup helper (replaced by `WFCSmokeHarness.cs`)
19. `WFCTestSetup.cs` - WFC test setup wrapper (replaced by `WFCSmokeHarness.cs`)

**Total Archived Test Files:** 29 files

---

## Legacy System Files

### Location: `Assets/Scripts/DOTS/Generation/`

1. **`TerrainGenerationSystem.cs`**
   - Status: `[LEGACY]` - Disabled internally
   - Reason: Replaced by SDF-based terrain pipeline
   - Notes: Currently disabled, maintained for backward compatibility

2. **`HybridTerrainGenerationSystem.cs`**
   - Status: `[LEGACY]` - Experimental/obsolete
   - Reason: Replaced by SDF-based terrain pipeline
   - Notes: Contains debug code (spacebar regeneration), experimental system

### Location: `Assets/Scripts/DOTS/Modification/`

3. **`TerrainModificationSystem.cs`**
   - Status: `[LEGACY]` - Uses legacy TerrainData component
   - Reason: Replaced by SDF edit systems
   - Notes: Maintained for backward compatibility with tests

4. **`PlayerModificationComponent.cs`**
   - Status: `[Obsolete]` attribute
   - Reason: Legacy heightmap modification, replaced by SDF edit buffers
   - Notes: Contains `GlobRemovalType` enum (also obsolete)

### Location: `Assets/Scripts/DOTS/Core/`

5. **`TerrainEntityManager.cs`**
   - Status: `[LEGACY]` - Part of legacy terrain system
   - Reason: Uses DOTS.Terrain.TerrainData (legacy component)
   - Notes: Maintained for backward compatibility

### Location: `Assets/Scripts/`

6. **`LegacyWeatherSystem.cs`**
   - Status: Legacy MonoBehaviour-based weather system
   - Reason: Replaced by DOTS weather systems
   - Notes: Old GameObject-based implementation

---

## Component Files (Still in Use but Marked Obsolete)

These files are still referenced but marked with `[Obsolete]` attributes:

1. **`PlayerModificationComponent.cs`** - Contains obsolete `GlobRemovalType` enum
2. **`TerrainData.cs`** - Legacy component, may have obsolete members

---

## Migration Notes

### From Legacy to Current Systems

| Legacy System | Current Replacement |
|--------------|-------------------|
| `TerrainGenerationSystem` | `TerrainChunkDensitySamplingSystem` (SDF) |
| `HybridTerrainGenerationSystem` | SDF-based terrain pipeline |
| `TerrainModificationSystem` | `TerrainEditInputSystem` + SDF edit buffers |
| `TerrainEntityManager` | `TerrainBootstrapAuthoring` |
| `LegacyWeatherSystem` | DOTS weather systems |
| `PlayerModificationComponent` | SDF edit buffers |
| `TerrainRefactorTestSetup` | N/A (legacy non-SDF terrain testing, no replacement needed) |
| `WFCTestManager` | `WFCSmokeHarness.cs` (simpler visual testing) |
| `WFCTestSceneSetup` | `WFCSmokeHarness.cs` (simpler visual testing) |
| `WFCTestSetup` | `WFCSmokeHarness.cs` (simpler visual testing) |
| `SimpleTestManager` | `WFCSmokeHarness.cs` (simpler visual testing) |
| `WFCSystemTest` | `WFCSystemTests.cs` (automated NUnit tests) |

---

## Maintenance Guidelines

1. **Do NOT add new code to deprecated files** - Use current systems instead
2. **Do NOT remove deprecated files without migration plan** - They may be referenced by archived tests
3. **Update this document** when files are moved to/from deprecated status
4. **Test deprecated code separately** - Use `DOTS.Terrain.Deprecated.asmdef` for testing if needed

---

## Assembly Definition Details

### DOTS.Terrain.Deprecated.asmdef
- **Location:** `Assets/Scripts/DOTS/Test/Archive/DOTS.Terrain.Deprecated.asmdef`
- **Scope:** All files in `Assets/Scripts/DOTS/Test/Archive/` and subfolders
- **Platform:** Editor-only (`includePlatforms: ["Editor"]`)
- **Auto-referenced:** false (must be explicitly referenced)
- **Effect:** Files in Archive folder are excluded from `DOTS.Terrain` assembly

### Files Still in Active Assemblies

The following legacy files remain in active assemblies but are marked as `[LEGACY]` or `[Obsolete]`:

#### In DOTS.Terrain Assembly:
- `TerrainGenerationSystem.cs` - Still referenced, marked `[LEGACY]`
- `HybridTerrainGenerationSystem.cs` - Still referenced, marked `[LEGACY]`
- `TerrainModificationSystem.cs` - Still referenced, marked `[LEGACY]`
- `TerrainEntityManager.cs` - Still referenced, marked `[LEGACY]`
- `PlayerModificationComponent.cs` - Still referenced, marked `[Obsolete]`

**Note:** These files remain in the active assembly because they may still be referenced by other code. They are marked as legacy/obsolete to discourage new usage.

#### In Core Assembly:
- `LegacyWeatherSystem.cs` - Legacy MonoBehaviour weather system
  - **Status:** Not marked with attributes, but name indicates legacy
  - **Location:** `Assets/Scripts/LegacyWeatherSystem.cs`
  - **Action:** Consider moving to deprecated assembly or marking as obsolete

## Verification Checklist

- [x] All archived test files listed
- [x] All legacy system files identified
- [x] Assembly definition created (`DOTS.Terrain.Deprecated.asmdef`)
- [x] Archive folder files excluded from `DOTS.Terrain` assembly
- [x] Documentation updated
- [x] SocketPatternTest.cs updated to use DebugSettings
- [x] WFCTestSceneSetup.cs archived (moved to Archive/Manual/)
- [x] WFCTestSetup.cs archived (moved to Archive/Manual/)
- [x] WFCTestManager.cs archived (moved to Archive/Manual/)
- [x] SimpleTestManager.cs archived (moved to Archive/Manual/)
- [x] TerrainRefactorTestSetup.cs archived (moved to Archive/Manual/)
- [ ] Verify no build errors after changes
- [ ] Verify deprecated code not included in builds
- [ ] Consider moving LegacyWeatherSystem.cs to deprecated assembly

---

## Notes

- Deprecated files are **Editor-only** - they will not be compiled into builds
- Files can still be accessed for reference or testing in the Unity Editor
- If deprecated code needs to be used, explicitly reference `DOTS.Terrain.Deprecated` assembly
- Consider removing deprecated files entirely after migration period (6+ months)
