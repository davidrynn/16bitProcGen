# Test Plan - Deterministic WFC with DeadEnd Rotation
**Target Build:** Seed init + DeadEnd rotation implementation  
**Test Environment:** Unity Editor Play Mode, 5×5 grid with placeholders  
**Expected Duration:** 15 minutes manual testing

---

## Pre-Test Setup

### Prerequisites Checklist
- [ ] Code changes applied per SPEC.md
- [ ] `DungeonManager.dungeonSize` set to `(5, 5)` in Inspector
- [ ] Placeholder prefabs assigned to `DungeonPrefabRegistryAuthoring`:
  - Corridor_Placeholder → corridorPrefab
  - Corner_Placeholder → cornerPrefab
  - Door_Placeholder → doorPrefab
- [ ] Floor/Wall patterns commented out in `WFCBuilder.cs` (lines 124-128)
- [ ] Debug flags enabled in `DebugSettings.cs` (EnableWFCDebug, EnableRenderingDebug)
- [ ] Unity console cleared and visible

---

## Test Case 1: Seed Initialization Verification

### Objective
Verify that random seed is initialized to 12345 on system creation.

### Steps
1. Enter Play Mode
2. Monitor console for initialization messages

### Expected Results
```
[DOTS-WFC] HybridWFCSystem: Initializing...
[DOTS-WFC] HybridWFCSystem: Initialization complete
[DOTS-WFC] HybridWFCSystem: Random seed initialized to 12345 for deterministic testing
```

### Pass Criteria
- ✅ Seed initialization message appears exactly once
- ✅ Message includes seed value "12345"
- ✅ Message appears after "Initialization complete"
- ✅ No errors or warnings related to seed initialization

### Failure Actions
- **Missing message:** Check line added to `HybridWFCSystem.OnCreate()` (after line 60)
- **Multiple messages:** Verify OnCreate() only runs once (should be automatic)

---

## Test Case 2: Deterministic Collapse (Reproducibility)

### Objective
Verify that two consecutive runs produce identical WFC collapse patterns.

### Steps
1. Enter Play Mode
2. Press `G` to trigger generation
3. Wait for completion message
4. Copy/save console collapse logs to file (Run1.txt)
5. Exit Play Mode
6. Re-enter Play Mode
7. Press `G` to trigger generation
8. Copy/save console collapse logs to file (Run2.txt)
9. Compare Run1.txt and Run2.txt

### Expected Results
**Run 1 Sample:**
```
[DOTS-WFC] Cell at (0,0) collapsed to pattern 3 (random collapse)
[DOTS-WFC] Cell at (1,0) collapsed to pattern 7 (entropy=1)
[DOTS-WFC] Cell at (2,0) collapsed to pattern 10 (forced collapse)
...
```

**Run 2 Sample:**
```
[DOTS-WFC] Cell at (0,0) collapsed to pattern 3 (random collapse)  ← SAME
[DOTS-WFC] Cell at (1,0) collapsed to pattern 7 (entropy=1)        ← SAME
[DOTS-WFC] Cell at (2,0) collapsed to pattern 10 (forced collapse) ← SAME
...
```

### Pass Criteria
- ✅ Cell collapse order identical between runs
- ✅ Pattern IDs identical for each cell position
- ✅ Collapse method (random/entropy/forced) identical
- ✅ Final grid layout visually identical

### Failure Actions
- **Different patterns:** Verify seed init runs before first collapse
- **Different order:** Check for any async/parallel operations affecting collapse
- **Floating-point variation:** May occur despite seeding (acceptable if pattern IDs match)

---

## Test Case 3: DeadEnd Rotation - Visual Verification

### Objective
Verify that DeadEnd tiles rotate to face their open edge.

### Steps
1. Enter Play Mode and trigger generation
2. Wait for rendering completion
3. Inspect Scene View for spawned Door_Placeholder objects
4. For each DeadEnd tile:
   - Note position (X, Z coordinates)
   - Check rotation in Inspector (Y rotation angle)
   - Verify placeholder faces expected direction

### Expected Results (Example Grid)
| Position | Pattern ID | Socket Sig | Expected Y Rotation | Visual Orientation     |
|----------|------------|------------|---------------------|------------------------|
| (2, 1)   | 8          | `FWWW`     | 0°                  | Placeholder faces +Z   |
| (4, 3)   | 9          | `WFWW`     | 90°                 | Placeholder faces +X   |
| (1, 4)   | 10         | `WWFW`     | 180°                | Placeholder faces -Z   |
| (3, 0)   | 11         | `WWWF`     | 270°                | Placeholder faces -X   |

### Pass Criteria
- ✅ At least 2 distinct DeadEnd rotation angles visible (ideally all 4)
- ✅ Rotation matches socket signature per pattern ID
- ✅ Placeholder's forward vector points toward open edge
- ✅ No DeadEnd tiles stuck at 0° when sockets indicate otherwise

### Failure Actions
- **All DeadEnd at 0°:** Check if rotation function call added to Door case (line 277)
- **Wrong rotation:** Verify socket comparison logic in `DetermineDeadEndRotation()`
- **Inverted rotation:** Check if placeholder prefab has non-identity base rotation

---

## Test Case 4: DeadEnd Rotation - Console Log Verification

### Objective
Verify that rendering logs include rotation information for DeadEnd tiles.

### Steps
1. Trigger generation with rendering debug enabled
2. Filter console for `[DOTS-Rendering]` messages
3. Locate logs for DeadEnd spawns (pattern IDs 8-11 after Floor/Wall exclusion)

### Expected Results
```
[DOTS-Rendering] DungeonRenderingSystem: Spawned CorridorEndDoorway at (2, 1) with transform (2, 0, 1)
```

**Enhanced Logging (Optional):**
If additional debug logs added:
```
[DOTS-Rendering] DeadEnd at (2,1): sockets N=F,E=W,S=W,W=W → Rotation 0°
[DOTS-Rendering] DeadEnd at (4,3): sockets N=W,E=F,S=W,W=W → Rotation 90°
```

### Pass Criteria
- ✅ DeadEnd spawn messages appear for all collapsed DeadEnd cells
- ✅ Transform position matches cell grid coordinates
- ✅ (Optional) Socket signature visible in logs
- ✅ No errors or warnings during DeadEnd spawn

### Failure Actions
- **No spawn messages:** Check if rendering system processes Door pattern type
- **Wrong position:** Verify cell position multiplication by cellSize
- **Exception during spawn:** Check pattern array bounds and null prefab handling

---

## Test Case 5: Rotation Regression - Corridor & Corner

### Objective
Verify that Corridor and Corner rotation behavior is unchanged.

### Steps
1. Complete a test run with all tiles
2. Inspect Corridor tiles:
   - Find one with socket `FWFW` (vertical) → Should be 0° rotation
   - Find one with socket `WFWF` (horizontal) → Should be 90° rotation
3. Inspect Corner tiles:
   - Find one with socket `FFWW` (NE) → Should be 0° rotation
   - Find one with socket `WFFW` (SE) → Should be 90° rotation
   - Find one with socket `WWFF` (SW) → Should be 180° rotation
   - Find one with socket `FWWF` (NW) → Should be 270° rotation

### Expected Results
**Corridor Rotation Table:**
| Socket Sig | Expected Rotation | Visual Check            |
|------------|-------------------|-------------------------|
| `FWFW`     | 0°                | Aligned with Z-axis     |
| `WFWF`     | 90°               | Aligned with X-axis     |

**Corner Rotation Table:**
| Socket Sig | Expected Rotation | Visual Check            |
|------------|-------------------|-------------------------|
| `FFWW`     | 0°                | L-shape opens NE        |
| `WFFW`     | 90°               | L-shape opens SE        |
| `WWFF`     | 180°              | L-shape opens SW        |
| `FWWF`     | 270°              | L-shape opens NW        |

### Pass Criteria
- ✅ All Corridor rotations match pre-implementation behavior
- ✅ All Corner rotations match pre-implementation behavior
- ✅ No visual anomalies (inverted, doubled rotations)
- ✅ Console logs show same rotation angles as before

### Failure Actions
- **Changed behavior:** Verify no accidental edits to `DetermineCorridorRotation()` or `DetermineCornerRotation()`
- **Visual glitches:** Check if any transform logic changed outside rotation functions

---

## Test Case 6: Pattern ID Remapping Verification

### Objective
Verify that pattern IDs match expected range after Floor/Wall exclusion.

### Steps
1. Monitor console for collapse logs
2. Record all pattern IDs mentioned
3. Verify range and distribution

### Expected Results
**With Floor/Wall Excluded:**
- Pattern IDs: 0-11 only
- ID 0-3: Corridor variants
- ID 4-7: Corner variants
- ID 8-11: DeadEnd variants

**Pattern Distribution (stochastic, approximate):**
- ~33% Corridor (8-9 cells)
- ~33% Corner (8-9 cells)
- ~33% DeadEnd (8-9 cells)

### Pass Criteria
- ✅ No pattern IDs outside range 0-11
- ✅ All three tile types represented
- ✅ At least 2 rotation variants per tile type visible

### Failure Actions
- **IDs 12-19 present:** Floor/Wall not commented out in `WFCBuilder.cs`
- **Only one tile type:** Check if patterns loaded correctly from blob asset
- **Missing rotation variants:** Increase grid size or check collapse algorithm

---

## Test Case 7: Full Integration Test

### Objective
End-to-end validation of complete feature set.

### Setup
- 5×5 grid
- All three tile types enabled (Corridor, Corner, DeadEnd)
- Seed 12345
- Placeholders assigned

### Steps
1. Clear console
2. Enter Play Mode
3. Trigger generation (press `G`)
4. Wait for completion message: "RENDERING COMPLETE! All 25 cells processed."
5. Verify each checklist item below

### Validation Checklist
- [ ] **Seed Init:** Message appears in console
- [ ] **Determinism:** Run twice, pattern IDs match
- [ ] **Corridor Rotation:** At least 1 vertical and 1 horizontal
- [ ] **Corner Rotation:** At least 2 different L-shape orientations
- [ ] **DeadEnd Rotation:** At least 2 different facing directions
- [ ] **No Errors:** Zero exceptions or errors in console
- [ ] **Visual Quality:** All 25 cells have placeholders, no floating objects
- [ ] **Grid Alignment:** All tiles at Y=0, spaced by cellSize (1.0)

### Pass Criteria
All 8 checklist items passed.

### Failure Actions
See individual test cases for specific failure diagnostics.

---

## Test Case 8: Edge Case - Fallback Handling

### Objective
Verify graceful fallback if invalid pattern data encountered.

### Steps
1. (Manual code modification) Temporarily create a DeadEnd pattern with invalid sockets (all 'W'):
   ```csharp
   // In WFCBuilder.CreateDungeonMacroTilePatterns()
   AddRotated(DungeonPatternType.Door, (byte)'W', (byte)'W', (byte)'W', (byte)'W', 0.9f);
   ```
2. Run generation
3. Check console for fallback warning
4. Revert code change

### Expected Results
```
[DOTS] DetermineDeadEndRotation: No open edge found for pattern 8
[DOTS-Rendering] DungeonRenderingSystem: Spawned CorridorEndDoorway at (...) with transform (...)
```

Tile spawns at 0° rotation (fallback).

### Pass Criteria
- ✅ Warning message logged
- ✅ System continues without crash
- ✅ DeadEnd tile spawns (even if incorrectly rotated)

### Note
This test is optional. The production code should never have invalid patterns.

---

## Performance Validation

### Metrics to Monitor
- Frame time during WFC generation: < 50ms per frame (target)
- Total generation time (25 cells): < 5 seconds
- Memory allocation: No significant spikes
- Console spam: < 200 log messages per run

### Expected Impact
- **Seed init:** Negligible (one-time call)
- **DeadEnd rotation:** < 1ms total (25 calls × ~0.04ms each)
- **Overall:** No measurable performance change

### Pass Criteria
- ✅ No frame drops below 30 FPS during generation
- ✅ Generation completes in under 10 seconds
- ✅ No memory leaks (stable memory graph over multiple runs)

---

## Test Summary Template

```
═══════════════════════════════════════════════════════════════
WFC DETERMINISTIC TEST - RESULTS
═══════════════════════════════════════════════════════════════
Date: _____________
Unity Version: _____________
Build: Seed Init + DeadEnd Rotation

TEST RESULTS:
─────────────────────────────────────────────────────────────
[PASS/FAIL] TC1: Seed Initialization
[PASS/FAIL] TC2: Deterministic Collapse
[PASS/FAIL] TC3: DeadEnd Visual Rotation
[PASS/FAIL] TC4: DeadEnd Console Logs
[PASS/FAIL] TC5: Corridor/Corner Regression
[PASS/FAIL] TC6: Pattern ID Range
[PASS/FAIL] TC7: Full Integration
[PASS/FAIL] TC8: Fallback Handling (Optional)

OVERALL: [PASS/FAIL]
─────────────────────────────────────────────────────────────

NOTES:
- Pattern distribution: ___% Corridor, ___% Corner, ___% DeadEnd
- Rotation variants observed: Corridor (___/2), Corner (___/4), DeadEnd (___/4)
- Generation time: ___ seconds
- Issues encountered: _______________

═══════════════════════════════════════════════════════════════
```

---

## Regression Testing (Future Runs)

### When to Re-Test
- After any changes to WFC collapse algorithm
- After changes to pattern definitions (socket values)
- After prefab updates
- Before merging to main branch

### Automated Testing (Future Enhancement)
Consider implementing:
- Unit test for `DetermineDeadEndRotation()` function
- Integration test that captures pattern IDs and compares to baseline
- Visual regression test using screenshot comparison

---

**End of Test Plan** — Execute test cases in sequence for comprehensive validation.

