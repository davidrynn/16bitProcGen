# WFC Rotation Debug Watchpoints

## 6-10 Precise Watchpoints for Play Mode

### 1. Pattern Creation Phase
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:96-121`
- **Watchpoint:** `AddRotated()` function execution
- **Variables:** `rn, re, rs, rw` (rotated edge values)
- **Example:** Corner_90 creation - watch edge rotation from (F,F,W,W) to (W,F,F,W)

### 2. Pattern Storage Phase  
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:101-112`
- **Watchpoint:** `DungeonPattern` creation in loop
- **Variables:** `dp.north, dp.east, dp.south, dp.west, dp.name`
- **Example:** Corner_90 pattern with name "Corner_90" and edges (W,F,F,W)

### 3. Collapse Selection Phase
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:377-422`
- **Watchpoint:** `cell.selectedPattern = selectedPattern` assignment
- **Variables:** `cell.selectedPattern, possibleCount, cell.position`
- **Example:** Cell at (2,1) collapses to pattern index 5 (Corner_90)

### 4. Pattern Lookup Phase
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:260-262`
- **Watchpoint:** Pattern retrieval from blob
- **Variables:** `cell.selectedPattern, pat.north, pat.east, pat.south, pat.west`
- **Example:** Pattern 5 retrieved with edges (W,F,F,W)

### 5. Rotation Calculation Phase
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:414-426`
- **Watchpoint:** `DetermineCornerRotation(pat)` execution
- **Variables:** `pat.north, pat.east, pat.south, pat.west, rotation quaternion`
- **Example:** Corner with (W,F,F,W) → ES corner → 90° rotation

### 6. Transform Application Phase
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:313-319`
- **Watchpoint:** `LocalTransform` creation and assignment
- **Variables:** `transform.Rotation, transform.Position`
- **Example:** Transform with Rotation = quaternion.Euler(0, 90°, 0)

## Concrete Example: Corner@90° at Position (2,1)

### Build Phase
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:133, 96-121`
- **Base Pattern:** Corner with (F,F,W,W) - N/E open, S/W closed
- **After 90° Rotation:** (W,F,F,W) - W/N open, E/S closed
- **Pattern Name:** "Corner_90"
- **Pattern ID:** 5 (assuming 0-3 are Floor variants, 4-7 are Wall variants, 8-11 are Corridor variants, 12-15 are Corner variants)

### Collapse Phase
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:381, 400, or 419`
- **Selected Pattern:** `cell.selectedPattern = 5`
- **Stored in:** `WFCCell.selectedPattern` field

### Spawn Phase
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:262, 287, 316**
- **Pattern Lookup:** `pat = blobPatterns[5]` → edges (W,F,F,W)
- **Rotation Calculation:** `DetermineCornerRotation(pat)` → ES corner → 90° rotation
- **Transform Application:** `Rotation = quaternion.Euler(0, math.radians(90f), 0)`

### Expected Values
- **Sockets Before Rotation:** (F,F,W,W) - base Corner pattern
- **Sockets After Rotation:** (W,F,F,W) - Corner_90 variant
- **Chosen Variant/Rotation:** Pattern index 5 (Corner_90)
- **Final Transform Rotation:** quaternion.Euler(0, 90°, 0)

## Additional Watchpoints

### 7. Compatibility Check Phase
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:483, 498, 513, 528`
- **Watchpoint:** `PatternsAreCompatible()` calls during pruning
- **Variables:** `a.north, b.south, direction, compatibility result`

### 8. Entropy Calculation Phase
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:358-359`
- **Watchpoint:** `WFCCellHelpers.CountPossiblePatterns()` call
- **Variables:** `cell.possiblePatternsMask, possibleCount`

### 9. Visualization Phase
**File:** `Scripts/DOTS/WFC/DungeonVisualizationSystem.cs:273-292`
- **Watchpoint:** GameObject rotation application
- **Variables:** `unityQuaternion, go.transform.rotation`

### 10. Debug Logging Phase
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:383, 402, 421`
- **Watchpoint:** Collapse logging statements
- **Variables:** `cell.position, selectedPattern, collapse reason`
