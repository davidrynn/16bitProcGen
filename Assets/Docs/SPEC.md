# Deterministic WFC Test - Seed & DeadEnd Rotation
**Type:** Enhancement (Visual/Debug)  
**Scope:** Delta spec - adds missing functionality for rotation testing  
**Status:** Ready for implementation

---

## Objective

Enable deterministic, reproducible WFC test runs with complete rotation support for all tile types.

**Current State:**
- Random seed not initialized → non-deterministic layouts
- DeadEnd (Door) tiles always spawn at 0° rotation → cannot verify rotation variants
- Corridor/Corner rotation works correctly

**Target State:**
- Fixed seed (12345) ensures identical collapse patterns across runs
- DeadEnd tiles rotate based on socket signatures (N/E/S/W open edge)
- All rotation logic consistent across tile types

---

## Scope

### Files Modified (3)
1. `Scripts/DOTS/WFC/HybridWFCSystem.cs` - Add seed initialization
2. `Scripts/DOTS/WFC/DungeonRenderingSystem.cs` - Add DeadEnd rotation function
3. **(Optional)** `Scripts/DOTS/Core/DebugSettings.cs` - Enable debug flags

### Lines Changed: ~45
- HybridWFCSystem: +2 lines (seed init)
- DungeonRenderingSystem: +15 lines (rotation function + call)
- DebugSettings: +2 lines (enable flags)

### Out of Scope
- Socket constraint enforcement (remains disabled)
- Floor/Wall pattern exclusion (manual via commenting)
- Backtracking or improved collapse algorithms
- Persistent seed storage (Inspector/ScriptableObject)

---

## Technical Design

### 1. Seed Initialization
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs`  
**Location:** `OnCreate()` method after line 60

```csharp
// After existing initialization
DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Initialization complete");

// NEW: Initialize random seed for deterministic testing
UnityEngine.Random.InitState(12345);
DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Random seed initialized to 12345 for deterministic testing");
```

**Rationale:**
- `UnityEngine.Random` is global state - must init before any collapse calls
- `OnCreate()` runs once per system lifetime (correct placement)
- Seed 12345 chosen for easy recognition in logs

---

### 2. DeadEnd Rotation Logic
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs`

#### 2A: Add Rotation Function (after line 426)
```csharp
private static quaternion DetermineDeadEndRotation(WFCPattern pat)
{
    // DeadEnd has one open edge ('F'), three closed ('W')
    // Rotate to face the open edge
    if (pat.north == (byte)'F') return quaternion.identity;                 // 0°   - Opens North
    if (pat.east == (byte)'F') return quaternion.Euler(0, math.radians(90f), 0);  // 90°  - Opens East
    if (pat.south == (byte)'F') return quaternion.Euler(0, math.radians(180f), 0); // 180° - Opens South
    if (pat.west == (byte)'F') return quaternion.Euler(0, math.radians(270f), 0);  // 270° - Opens West
    
    // Fallback (should never occur for valid DeadEnd patterns)
    DOTS.Terrain.Core.DebugSettings.LogWarning($"DetermineDeadEndRotation: No open edge found for pattern {pat.patternId}");
    return quaternion.identity;
}
```

**Pattern Matching:**
| Socket Sig | Open Edge | Rotation |
|------------|-----------|----------|
| `FWWW`     | North     | 0°       |
| `WFWW`     | East      | 90°      |
| `WWFW`     | South     | 180°     |
| `WWWF`     | West      | 270°     |

#### 2B: Update Door Case (line 276-278)
**Before:**
```csharp
case DungeonPatternType.Door:
    prefabToSpawn = prefabs.doorPrefab;
    break;
```

**After:**
```csharp
case DungeonPatternType.Door:
    prefabToSpawn = prefabs.doorPrefab;
    rotation = DetermineDeadEndRotation(pat);  // ← ADD THIS LINE
    break;
```

---

### 3. Debug Flag Enablement (Optional)
**File:** `Scripts/DOTS/Core/DebugSettings.cs`  
**Lines:** 12, 15

```csharp
public static bool EnableWFCDebug = true;       // ← Change from false
public static bool EnableRenderingDebug = true; // ← Change from false
```

**Alternative:** Leave manual toggle for user control.

---

## Definition of Done

### Functional Requirements
- ✅ Seed initialization logs appear in console on play mode entry
- ✅ Two consecutive runs with identical setup produce identical collapse patterns
- ✅ DeadEnd tiles spawn with rotation matching their socket signature
- ✅ All 4 DeadEnd rotation variants visible across 25-cell grid (stochastic, but likely)
- ✅ Corridor and Corner rotation unchanged (no regression)

### Code Quality
- ✅ Rotation function follows existing pattern (DetermineCorridorRotation/DetermineCornerRotation)
- ✅ No compiler warnings or errors
- ✅ Debug logs include rotation information
- ✅ Fallback handling for invalid patterns

### Testing
- ✅ Manual test: 5×5 grid with placeholders
- ✅ Console verification: Seed init message appears
- ✅ Visual verification: DeadEnd placeholders face different directions
- ✅ Determinism verification: Identical logs on repeat runs

---

## Implementation Sequence

1. **Add seed initialization** (HybridWFCSystem.cs)
   - Insert after line 60 in OnCreate()
   - Test: Verify log message appears

2. **Add DeadEnd rotation function** (DungeonRenderingSystem.cs)
   - Insert after line 426 (after DetermineCornerRotation)
   - Follow existing function signature pattern

3. **Update Door case** (DungeonRenderingSystem.cs)
   - Modify line 276-278 to call rotation function
   - Test: Verify DeadEnd tiles rotate

4. **Enable debug flags** (DebugSettings.cs) - Optional
   - Change lines 12, 15 to true
   - Or: Leave for manual toggle

5. **Verify with test run** (see TEST_PLAN.md)

---

## Risks & Mitigations

### Risk: Seed Changes Existing Behavior
**Likelihood:** Medium  
**Impact:** Low (visual only, no gameplay)  
**Mitigation:** 
- Seed only affects random collapse decisions
- No changes to pattern definitions or constraints
- Easy rollback: remove 2 lines

### Risk: DeadEnd Rotation Conflicts with Prefab
**Likelihood:** Low  
**Impact:** Low (visual misalignment)  
**Mitigation:**
- Placeholder prefabs designed with neutral orientation
- Rotation applied consistently with Corridor/Corner
- Visual inspection during test run

### Risk: Performance Regression
**Likelihood:** Very Low  
**Impact:** Negligible  
**Mitigation:**
- Rotation function called once per cell (25 times total)
- Simple conditional logic (4 if-statements)
- Same pattern as existing rotation functions

---

## Dependencies

### Prerequisites
- Placeholder prefabs exist and are assigned to DungeonPrefabRegistryAuthoring
- Grid size set to 5×5 in DungeonManager
- Floor/Wall patterns commented out (manual step per LIMIT_TILESET.diff)

### No External Dependencies
- Uses existing Unity.Mathematics quaternion functions
- No new packages or assets required

---

## Notes

### Why Fixed Seed Instead of Parameter?
**Decision:** Hardcode seed 12345 for now.

**Rationale:**
- Simplest implementation (2 lines)
- Sufficient for testing/debugging
- Future enhancement: Add seed field to WFCComponent or DungeonGenerationRequest

**Upgrade Path:**
```csharp
// Future: Add to DungeonGenerationRequest
public struct DungeonGenerationRequest
{
    public int randomSeed;  // ← Add field
    // ...
}

// In OnUpdate() or OnStartRunning():
if (request.randomSeed != 0)
{
    UnityEngine.Random.InitState(request.randomSeed);
}
```

### Why Not Use Unity.Mathematics.Random?
**Decision:** Continue using UnityEngine.Random.

**Rationale:**
- Existing code uses UnityEngine.Random (5 call sites)
- Changing would require refactoring all random calls
- UnityEngine.Random.InitState() provides determinism
- Out of scope for this delta spec

**Future:** Consider migrating to Unity.Mathematics.Random for job safety.

---

**End of Spec** — Ready for implementation per TEST_PLAN.md validation.

