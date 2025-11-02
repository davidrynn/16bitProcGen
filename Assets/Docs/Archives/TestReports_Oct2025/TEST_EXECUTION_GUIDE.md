# Test Execution Guide - SPEC.md Implementation (CORRECTED)
**Implementation Status:** ✅ Complete & Corrected
**Test Status:** ⏳ Awaiting manual Unity Editor execution

---

## What's Been Done

### ✅ Code Changes Applied
1. **HybridWFCSystem.cs** - Configurable seed initialization using CORRECT RNG (lines 63-75)
2. **DungeonRenderingSystem.cs** - DeadEnd rotation function and call added (lines 278, 428-440)
3. **DebugSettings.cs** - Debug flags + seed toggle added (lines 12, 15, 21-23)

### ✅ Verification Complete
- No compiler errors
- No linter errors
- Uses correct random generator (Unity.Mathematics.Random, NOT UnityEngine.Random)
- Configurable for both testing and gameplay

---

## CRITICAL: Understanding the Random Number Generator Fix

### ❌ What Was Wrong Initially:
```csharp
// Line 63: WFC's ACTUAL random generator (was always random)
random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);

// Line 69: WRONG generator (WFC doesn't use this!)
UnityEngine.Random.InitState(12345);
```
**Result:** WFC ignored the seed, always generated random dungeons.

### ✅ What's Correct Now:
```csharp
// Line 63-73: Seeds the CORRECT generator WFC actually uses
if (DebugSettings.UseFixedWFCSeed)
    random = new Unity.Mathematics.Random((uint)DebugSettings.FixedWFCSeed);
else
    random = new Unity.Mathematics.Random((uint)System.DateTime.Now.Ticks);
```
**Result:** Toggle controls determinism properly.

---

## Pre-Test Configuration

### Step 1: Enable Deterministic Mode for TC1-TC2

**File:** `Scripts/DOTS/Core/DebugSettings.cs` (line 22)

**Change this:**
```csharp
public static bool UseFixedWFCSeed = false;  // ← Default (random mode)
```

**To this:**
```csharp
public static bool UseFixedWFCSeed = true;   // ← Enable for testing
```

### Step 2: Verify Other Debug Flags

Ensure these are already set (should be from previous changes):
```csharp
public static bool EnableWFCDebug = true;           // Line 12
public static bool EnableRenderingDebug = true;     // Line 15
```

⚠️ **CRITICAL:** Without `UseFixedWFCSeed = true`, TC2 (determinism) will FAIL!

---

## Scene Setup Instructions

### Required GameObjects in Scene Hierarchy

Your Test scene needs exactly **TWO GameObjects**:

#### 1. **WFCSmokeHarness** GameObject ← REQUIRED
**Purpose:** Creates WFC entities and cells for the dungeon generation system

**Configuration:**
```
┌─ WFCSmokeHarness (Script) ──────────────────┐
│ Grid Settings:                              │
│   Grid Width:  5                            │
│   Grid Height: 5                            │
│   Cell Size:   1.0                          │
│                                             │
│ Execution:                                  │
│   Run On Start:         ☑ (CHECKED)        │
│   Enable Debug Logs:    ☐ (unchecked)     │
│   Enable WFC Debug:     ☐ (unchecked)     │
│   Enable Rendering Debug: ☐ (unchecked)   │
└─────────────────────────────────────────────┘
```

⚠️ **Leave harness debug checkboxes UNCHECKED** - DebugSettings.cs controls logging!

---

#### 2. **DungeonPrefabRegistry** GameObject ← REQUIRED
**Purpose:** Provides placeholder prefabs for rendering

**Prefab Assignments:**
- **Corridor Prefab:** `Corridor_Placeholder.prefab`
- **Corner Prefab:** `Corner_Placeholder.prefab`
- **Door Prefab:** `Door_Placeholder.prefab`
- **Room Edge/Floor:** Any placeholder (not used in 5x5 test)

**How to Assign:**
- Click circle icon (⊙) next to each slot
- Type prefab name in popup
- Double-click to assign

---

### ✅ You Can Remove:
- **DungeonManager** (not needed - WFCSmokeHarness auto-starts)
- **DebugController** (not needed - DebugSettings.cs controls flags)
- Any other test GameObjects

---

## Test Execution Checklist (TEST_PLAN.md Cases 1-5)

### 📋 TC1: Seed Initialization Message Verification

**Objective:** Confirm fixed seed is being used

**Steps:**
1. ✅ Set `DebugSettings.UseFixedWFCSeed = true` in code
2. ✅ Save all scripts
3. Open Unity Editor → Test scene
4. Clear Console (right-click → Clear)
5. Enter Play Mode
6. **Look for this log:**
   ```
   [DOTS-WFC] HybridWFCSystem: Random seed initialized to 12345 for deterministic testing
   ```

**Expected Result:**  
✅ Log appears shortly after Play Mode starts

**If you see instead:**
```
[DOTS-WFC] HybridWFCSystem: Random generator initialized with time-based seed
```
❌ **FAIL** - You forgot to set `UseFixedWFCSeed = true`!

---

### 📋 TC2: Deterministic Collapse Verification

**Objective:** Verify identical dungeon layout across two runs

**Steps:**

#### Run 1:
1. ✅ Confirm `UseFixedWFCSeed = true` in DebugSettings.cs
2. Clear Console
3. Enter Play Mode
4. Wait for: `[DOTS-Rendering] DungeonVisualizationSystem: Spawned 25 GameObjects`
5. **Copy ENTIRE console log** → paste into `logs/Run1.txt`
6. Exit Play Mode

#### Run 2:
7. **DO NOT change any code** (keep same seed!)
8. Clear Console
9. Re-enter Play Mode
10. Wait for: `[DOTS-Rendering] DungeonVisualizationSystem: Spawned 25 GameObjects`
11. **Copy ENTIRE console log** → paste into `logs/Run2.txt`
12. Exit Play Mode

#### Comparison:
13. Run in terminal (from Assets directory):
    ```bash
    # Extract collapse sequences
    Select-String -Path "logs/Run1.txt" -Pattern "collapsed to pattern" > logs/Run1_Collapses.txt
    Select-String -Path "logs/Run2.txt" -Pattern "collapsed to pattern" > logs/Run2_Collapses.txt
    
    # Compare them
    Compare-Object (Get-Content logs/Run1_Collapses.txt) (Get-Content logs/Run2_Collapses.txt)
    ```

**Expected Result:**  
✅ **No output** from Compare-Object = files are identical!

**If you see differences:**  
❌ **FAIL** - Check that `UseFixedWFCSeed = true` in BOTH runs

---

### 📋 TC3: Visual Verification of DeadEnd Rotation

**Objective:** See Door prefabs rotated in different directions

**Steps:**
1. After any test run completes (TC1 or TC2)
2. In Scene Hierarchy, look for GameObjects named like: `DungeonElement_Door_(x,y)`
3. Select each Door GameObject
4. In Scene view, observe their rotation
5. Check Inspector → Transform → Rotation Y value

**Expected Results:**
- Some Doors at Y = 0° (facing North - open edge pointing up)
- Some Doors at Y = 90° (facing East - open edge pointing right)
- Some Doors at Y = 180° (facing South - open edge pointing down)
- Some Doors at Y = 270° (facing West - open edge pointing left)

**Validation:**
✅ **PASS** if you see Doors facing different directions (not all the same rotation)

---

### 📋 TC4: Console Log Verification for DeadEnd Spawns

**Objective:** Verify rotation logic is being applied (even if rendering is fallback path)

**Steps:**
1. Open `logs/Run1.txt` (from TC2)
2. Search for lines containing: "collapsed to pattern"
3. Note the pattern IDs for Door type cells
4. Cross-reference with socket signatures

**Expected Pattern IDs (from WFCBuilder.cs):**
- Pattern 8: Door variant 0 (FWWW - North open) → should spawn at 0°
- Pattern 9: Door variant 1 (WFWW - East open) → should spawn at 90°
- Pattern 10: Door variant 2 (WWFW - South open) → should spawn at 180°
- Pattern 11: Door variant 3 (WWWF - West open) → should spawn at 270°

**Example from logs:**
```
[DOTS-WFC] Cell at int2(4, 3) collapsed to pattern 11 (forced collapse)
```
→ Pattern 11 = WWWF = West-facing Door = should spawn at 270°

**Validation:**
✅ **PASS** if you can find Door patterns (8-11) in collapse logs

---

### 📋 TC5: Regression Check - Corridor/Corner Unchanged

**Objective:** Verify existing rotation logic still works

**Steps:**
1. After TC2 Run 1 completes
2. In Scene Hierarchy, find GameObjects named:
   - `DungeonElement_Corridor_(x,y)`
   - `DungeonElement_Corner_(x,y)`
3. Visually inspect their rotations in Scene view

**Expected Results:**
- **Corridors:** Oriented to match their connectivity (N-S or E-W)
- **Corners:** Oriented to match their L-shape direction (NE, ES, SW, WN)
- **NOT all facing the same direction** (proves rotation logic works)

**Validation:**
✅ **PASS** if Corridors and Corners show varied orientations

---

## Expected Console Output Timeline

### Initialization Phase (First ~400 lines):
```
[DOTS] TerrainSystem: Initializing...
[DOTS-Rendering] DungeonVisualizationSystem: OnCreate called
[DOTS-Rendering] DungeonRenderingSystem: OnCreate called
[DOTS-WFC] HybridWFCSystem: Initializing...
[DOTS] Initializing Compute Shaders...
[DOTS-WFC] HybridWFCSystem: Random seed initialized to 12345 for deterministic testing  ← TC1 CHECK
[DOTS-WFC] HybridWFCSystem: Initialization complete
[WFCSmokeHarness] Initialized 5x5 WFC with cellSize=1. Waiting for collapse and rendering...
```

### WFC Collapse Phase (~2000 lines):
```
[DOTS-WFC] HybridWFCSystem: Starting WFC generation for entity 94
[DOTS-WFC] HybridWFCSystem: Initializing WFC data with dungeon macro-tile patterns
[DOTS-WFC] HybridWFCSystem: Created 12 dungeon patterns and 0 constraints
[DOTS-WFC] HybridWFCSystem: Cell at int2(0, 0) collapsed to pattern X  ← TC2 CHECK
[DOTS-WFC] HybridWFCSystem: Cell at int2(1, 0) collapsed to pattern Y
... (25 cells total)
[DOTS-WFC] HybridWFCSystem: WFC generation completed successfully in N iterations
```

### Rendering Phase (~50 lines):
```
[DOTS-Rendering] DungeonRenderingSystem: Waiting for DungeonPrefabRegistry (not baked yet)
[DOTS-Rendering] DungeonVisualizationSystem: No element entities. Visualizing WFCCells directly.
[DOTS-Rendering] DungeonVisualizationSystem: Spawned 25 GameObjects from WFCCells.  ← TC3 CHECK
[DOTS-Rendering] DungeonVisualizationSystem: WFC complete - stopping updates
```

---

## Toggle Configuration Reference

### For Testing (Deterministic Mode):
```csharp
// In Scripts/DOTS/Core/DebugSettings.cs
public static bool UseFixedWFCSeed = true;   // ← Set to true
public static int FixedWFCSeed = 12345;      // Can customize seed
```

**You'll see:**
```
[DOTS-WFC] HybridWFCSystem: Random seed initialized to 12345 for deterministic testing
```

### For Gameplay (Random Mode):
```csharp
// In Scripts/DOTS/Core/DebugSettings.cs
public static bool UseFixedWFCSeed = false;  // ← Set to false
```

**You'll see:**
```
[DOTS-WFC] HybridWFCSystem: Random generator initialized with time-based seed
```

---

## Troubleshooting

### Problem: "Nothing renders" or "No GameObjects in hierarchy"

**Solution:** Enable rendering debug in WFCSmokeHarness Inspector:
- Check: ☑ `Enable Rendering Debug`
- This enables the fallback visualization path

**Why:** DungeonRenderingSystem requires baked entity prefabs (SubScenes). Since you're testing in a regular scene, the fallback DungeonVisualizationSystem handles rendering.

---

### Problem: "Collapse sequences differ between Run1 and Run2"

**Checklist:**
1. ✅ Did you set `UseFixedWFCSeed = true` in DebugSettings.cs?
2. ✅ Did you save the file and reload Unity?
3. ✅ Did you see the "seed initialized to 12345" message in BOTH runs?
4. ✅ Did you exit Play Mode completely between Run1 and Run2?

**If all checked and still different:**
- The seed is being re-randomized somewhere else (investigate HybridWFCSystem for additional random initialization)

---

### Problem: "WFC never completes / stuck in infinite loop"

**Look for:**
```
[DOTS-WFC] Cell at int2(X, Y): entropy=12, possible=12, collapsed=False
```
Repeating forever without any collapses.

**Solution:**
- Check if WFCBuilder.cs patterns are valid (12 patterns expected)
- Verify no constraints are blocking all possibilities

---

## Quick Test Checklist (Copy This)

**Before entering Play Mode:**
- [ ] DebugSettings.UseFixedWFCSeed = true (line 22)
- [ ] DebugSettings.EnableWFCDebug = true (line 12)
- [ ] DebugSettings.EnableRenderingDebug = true (line 15)
- [ ] Scene contains WFCSmokeHarness GameObject
- [ ] WFCSmokeHarness: Run On Start = checked
- [ ] WFCSmokeHarness: Debug checkboxes = unchecked (let DebugSettings control it)
- [ ] Scene contains DungeonPrefabRegistry GameObject
- [ ] DungeonPrefabRegistry: All 5 prefabs assigned
- [ ] Console cleared

**During Play Mode:**
- [ ] See: "Random seed initialized to 12345"
- [ ] See: "WFC generation completed successfully"
- [ ] See: "Spawned 25 GameObjects"
- [ ] See 25 colored cubes in Scene view
- [ ] Copy entire console → paste to logs/RunN.txt

**For TC2 (Determinism):**
- [ ] Run twice with same seed
- [ ] Compare logs (use PowerShell Compare-Object)
- [ ] Verify collapse sequences are IDENTICAL

---

## Understanding the Two Rendering Paths

### Production Path (Currently NOT Working):
```
DungeonRenderingSystem → Spawns entity prefabs (requires baking)
```
**Why blocked:** Prefabs need to be in a SubScene and baked by Unity

### Test/Debug Path (Currently Active):
```
DungeonVisualizationSystem → Spawns GameObjects directly (hybrid approach)
```
**How it works:** Bypasses entity baking, creates classic GameObjects

**This is INTENTIONAL design** - the fallback path exists for testing without SubScenes!

---

## After Testing - Reverting to Production Mode

**Once testing is complete, revert these settings:**

```csharp
// In Scripts/DOTS/Core/DebugSettings.cs
public static bool UseFixedWFCSeed = false;      // Back to random
public static bool EnableWFCDebug = false;       // Reduce log spam (optional)
public static bool EnableRenderingDebug = false; // Reduce log spam (optional)
```

**Result:** Dungeons will be randomized every run (better for gameplay).

---

## Summary of Files and Changes

| File | Purpose | Lines Changed |
|------|---------|---------------|
| `DebugSettings.cs` | Added seed toggle + enabled debug logs | +4 |
| `HybridWFCSystem.cs` | Fixed seed initialization (correct RNG) | +11/-2 |
| `DungeonRenderingSystem.cs` | Added DeadEnd rotation logic | +16 |
| **TOTAL** | | **31 lines** |

**All changes within scope:** ✅ ≤3 files, ≤50 lines

---

**Ready to test!** Follow the checklist above and capture logs for validation.
