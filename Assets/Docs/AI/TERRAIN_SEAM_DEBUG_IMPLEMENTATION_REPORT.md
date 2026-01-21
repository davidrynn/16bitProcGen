# Terrain Seam Debug Implementation Report
**Branch:** `debug/terrain-seams-v1`  
**Date:** 2026-01-20  
**Spec:** TERRAIN_SEAM_DEBUG_SPEC_v1.md  

## Implementation Summary

All required components from the spec have been successfully implemented to investigate terrain seam issues (vertical 90° walls / ring patterns) by determining whether they originate from density sampling mismatches or later meshing/rendering stages.

## Components Created

### 1. TerrainDebugConfig (Singleton)
**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfig.cs`

Debug-only singleton component with fields:
- `bool Enabled` - Master switch for debug behavior
- `bool FreezeStreaming` - Stops player-based streaming
- `int2 FixedCenterChunk` - Fixed center when streaming is frozen
- `int StreamingRadiusInChunks` - Radius around fixed center
- `float SeamEpsilon` - Threshold for density mismatches (default 0.001)
- `bool EnableSeamLogging` - Controls Console logging of mismatches

**Default behavior:** When `Enabled == false`, all systems operate normally.

### 2. TerrainChunkDebugState (Component)
**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainChunkDebugState.cs`

Lightweight lifecycle tracking component:
- `int2 ChunkCoord` - Chunk coordinate
- `byte Stage` - Lifecycle stage (0-5):
  - 0 = Spawned
  - 1 = NeedsDensity
  - 2 = DensityReady
  - 3 = NeedsMesh
  - 4 = MeshReady
  - 5 = Uploaded

**Integration:** Added to chunks when debug mode is enabled; updated by streaming, density, mesh build, and upload systems.

### 3. TerrainDebugConfigAuthoring
**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfigAuthoring.cs`

MonoBehaviour authoring component for scene setup:
- Provides inspector controls for all debug config fields
- Bakes to `TerrainDebugConfig` singleton at runtime
- Allows easy debugging in playmode scenes

### 4. Enhanced TerrainChunkDensityBlob
**File:** `Assets/Scripts/DOTS/Terrain/SDF/Chunks/TerrainChunkDensity.cs`

Added metadata for world-space mapping:
- `int3 Resolution` - Grid resolution
- `float3 WorldOrigin` - Chunk world-space origin
- `float VoxelSize` - Voxel size in world units
- `GetWorldPosition(x, y, z)` - Maps grid indices to world position
- `GetDensity(x, y, z)` - Safe density value accessor

**Backward compatible:** Existing blob creation updated to populate metadata.

## Systems Modified

### 1. TerrainChunkStreamingSystem
**File:** `Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs`

**Changes:**
- Checks for `TerrainDebugConfig` singleton at start of OnUpdate
- When `FreezeStreaming == true`: uses `FixedCenterChunk` and `StreamingRadiusInChunks` instead of player position
- Adds `TerrainChunkDebugState` component to spawned chunks when debug enabled
- Refactored streaming logic into `ProcessStreamingWindow()` helper method

**Determinism:** Fixed center + radius produces deterministic chunk set across runs.

### 2. TerrainChunkDensitySamplingSystem
**File:** `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs`

**Changes:**
- Populates `TerrainChunkDensityBlob` metadata (Resolution, WorldOrigin, VoxelSize) during blob creation
- Updates `TerrainChunkDebugState.Stage` to `StageDensityReady` when density blob is complete

### 3. TerrainChunkMeshBuildSystem
**File:** `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs`

**Changes:**
- Updates `TerrainChunkDebugState.Stage` to `StageMeshReady` after mesh blob creation

### 4. TerrainChunkMeshUploadSystem
**File:** `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs`

**Changes:**
- Updates `TerrainChunkDebugState.Stage` to `StageUploaded` after mesh upload and render component setup

## New System: TerrainSeamValidatorSystem
**File:** `Assets/Scripts/DOTS/Terrain/Debug/TerrainSeamValidatorSystem.cs`

**Purpose:** Compare shared border density values between adjacent chunks to detect sampling mismatches.

**Behavior:**
- Only runs when `TerrainDebugConfig.Enabled == true`
- Updates after `TerrainChunkDensitySamplingSystem`
- For each chunk with density data:
  - Finds east neighbor `(cx+1, cz)` and north neighbor `(cx, cz+1)`
  - Compares density values at shared borders
  - Counts samples above `SeamEpsilon` threshold
- **Logs format:** `[SEAM_MISMATCH] A(cx,cz) ↔ B(cx+1,cz) (East/North) maxΔ=... samples=.../... above ε=...`

**Border comparison logic:**
- East border: compares `A[xMax, y, z]` with `B[0, y, z]`
- North border: compares `A[x, y, zMax]` with `B[x, y, 0]`
- No modifications to density or mesh data

**Burst compatible:** Fully Burst-compiled for performance.

## Test: TerrainChunkBorderContinuityTests
**File:** `Assets/Scripts/DOTS/Tests/PlayMode/TerrainChunkBorderContinuityTests.cs`

**Test:** `BorderContinuity_2x2Grid_NoSeamMismatches`

**Setup:**
- Creates isolated test World
- Enables `TerrainDebugConfig` with frozen streaming
- Spawns deterministic 2×2 chunk grid (radius=1 around (0,0))
- Resolution: 17×17×17, VoxelSize: 1.0

**Execution:**
- Runs streaming system to spawn chunks
- Runs density sampling system (10 frames to ensure completion)
- Runs validator system

**Assertions:**
- At least 4 chunks spawned
- All chunks have density data
- **Zero seam mismatches above epsilon (0.001)**

**Manual validation:** Test includes `ValidateSeamsManually()` that counts mismatches independently of validator system logging.

## Bootstrap Integration

### ProjectFeatureConfig
**File:** `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs`

Added toggle:
```csharp
[Header("Terrain Debug Systems")]
public bool EnableTerrainSeamValidatorSystem = false;
```

**Default:** Disabled to avoid overhead in normal gameplay.

### DotsSystemBootstrap
**File:** `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`

Added conditional system registration:
```csharp
if (config.EnableTerrainSeamValidatorSystem)
{
    var handle = world.CreateSystem<DOTS.Terrain.Debug.TerrainSeamValidatorSystem>();
    simGroup.AddSystemToUpdateList(handle);
    Debug.Log("[DOTS Bootstrap] TerrainSeamValidatorSystem enabled and added to SimulationSystemGroup.");
}
```

## How to Use

### Option 1: Playmode Scene Debugging
1. Add `TerrainDebugConfigAuthoring` MonoBehaviour to scene
2. Set `Enabled = true`
3. Configure `FreezeStreaming`, `FixedCenterChunk`, `StreamingRadiusInChunks`
4. Set `SeamEpsilon = 0.001` (or desired threshold)
5. Set `EnableSeamLogging = true`
6. Enable `EnableTerrainSeamValidatorSystem` in ProjectFeatureConfig asset
7. Enter playmode
8. Check Console for `[SEAM_MISMATCH]` warnings

### Option 2: PlayMode Test
1. Open Test Runner (Window → General → Test Runner)
2. Select PlayMode tab
3. Run `TerrainChunkBorderContinuityTests.BorderContinuity_2x2Grid_NoSeamMismatches`
4. Check test results and Console output

### Option 3: Manual World Creation
```csharp
var debugConfig = new TerrainDebugConfig
{
    Enabled = true,
    FreezeStreaming = true,
    FixedCenterChunk = int2.zero,
    StreamingRadiusInChunks = 2,
    SeamEpsilon = 0.001f,
    EnableSeamLogging = true
};
entityManager.CreateSingleton(debugConfig);
```

## Next Steps (Per Spec)

### Required Before Any Fixes
1. **Run scene with debug enabled** - Add `TerrainDebugConfigAuthoring` to a test scene
2. **Run PlayMode test** - Execute `BorderContinuity_2x2Grid_NoSeamMismatches`
3. **Report findings:**
   - Do seam mismatches occur?
   - Do mismatches align with visible seams?
   - What is the magnitude of maxΔ?
   - Do ring patterns align with chunk Stage differences?

### Interpretation Guide (from spec)
- **Mismatches detected** → Bug is in sampling / origin math / off-by-one logic
- **No mismatches** → Bug is in surface nets, normals, winding, or upload
- **Ring patterns align with chunk Stage differences** → Streaming/pipeline visibility issue

## Files Modified

### New Files
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfig.cs`
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainChunkDebugState.cs`
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainDebugConfigAuthoring.cs`
- `Assets/Scripts/DOTS/Terrain/Debug/TerrainSeamValidatorSystem.cs`
- `Assets/Scripts/DOTS/Tests/PlayMode/TerrainChunkBorderContinuityTests.cs`

### Modified Files
- `Assets/Scripts/DOTS/Terrain/SDF/Chunks/TerrainChunkDensity.cs` - Added metadata to blob
- `Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs` - Debug mode support
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs` - Metadata population, debug stage tracking
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs` - Debug stage tracking
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs` - Debug stage tracking
- `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` - Added validator toggle
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` - Validator registration

## Compilation Status
✅ All files compile without errors  
✅ No Burst safety violations  
✅ No missing component errors  

## Spec Compliance Checklist
- ✅ 2.1 TerrainDebugConfig singleton with all required fields
- ✅ 2.2 Streaming system respects FreezeStreaming and FixedCenterChunk
- ✅ 3.1 TerrainChunkDebugState component with stage field
- ✅ 3.2 Stage updates in streaming, density, mesh build, and upload systems
- ✅ 4.1 TerrainChunkDensityBlob metadata for world position mapping
- ✅ 4.2 TerrainSeamValidatorSystem compares shared borders
- ✅ 5.1 PlayMode test: BorderContinuity_2x2Grid_NoSeamMismatches
- ✅ Non-goal: No algorithm changes, no fixes attempted

## Known Limitations
1. **Test execution:** PlayMode tests must be run through Unity Test Runner (not dotnet test CLI)
2. **Manual bootstrap:** Validator system requires `EnableTerrainSeamValidatorSystem = true` in ProjectFeatureConfig
3. **Y-axis borders not validated:** Only East (X+) and North (Z+) borders checked (sufficient for detecting grid mismatches)
4. **No visual overlays:** Debug data only available via Console logs and stage component inspection

## Recommendations
1. Run the PlayMode test first to establish baseline seam behavior in controlled environment
2. If test passes (no mismatches), focus investigation on Surface Nets meshing implementation
3. If test fails (mismatches detected), investigate `TerrainChunkDensitySamplingJob` and chunk origin calculations
4. Use `TerrainChunkDebugState.Stage` to correlate visible ring patterns with pipeline stage differences
5. Consider adding DebugSettings logging toggles if validator system needs more granular output control

---

**Status:** ✅ All spec requirements implemented  
**Ready for:** Testing and data collection phase  
**Blocked on:** Execution of PlayMode test and scene debugging to generate findings report
