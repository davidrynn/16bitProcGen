# WFC Rotation Dataflow - Dry Run Trace
**Purpose:** Step-by-step code walkthrough showing where pattern variants are stored and how rotation is applied  
**Scope:** Read-only analysis without code execution

---

## 1. Pattern Creation & Storage (Compile Time)

### Step 1A: Define Base Patterns with Sockets
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs`  
**Function:** `CreateDungeonMacroTilePatterns()` (lines 89-139)

```csharp
// Line 130: Corridor base definition
AddRotated(DungeonPatternType.Corridor, (byte)'F', (byte)'W', (byte)'F', (byte)'W', 1.0f);
//                                      North=F     East=W    South=F   West=W

// Line 133: Corner base definition
AddRotated(DungeonPatternType.Corner, (byte)'F', (byte)'F', (byte)'W', (byte)'W', 1.0f);
//                                    North=F     East=F    South=W   West=W

// Line 136: DeadEnd (Door) base definition
AddRotated(DungeonPatternType.Door, (byte)'F', (byte)'W', (byte)'W', (byte)'W', 0.9f);
//                                  North=F     East=W    South=W   West=W
```

### Step 1B: Generate 4 Rotated Variants
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs`  
**Function:** `AddRotated` helper (lines 96-121)

```csharp
// Lines 99-120: Rotation loop (4 iterations)
for (int rot = 0; rot < 4; rot++)
{
    // Lines 101-111: Create pattern with current socket values
    var dp = new DungeonPattern
    {
        id = nextId++,                           // ← Auto-increment: 0,1,2,3...
        name = type.ToString() + "_" + rot * 90, // ← "Corridor_0", "Corridor_90", etc.
        type = type,                             // ← DungeonPatternType enum value
        north = rn,                              // ← Current socket values
        east = re,
        south = rs,
        west = rw,
        weight = weight
    };
    result.Add(PatternConversion.ToWFCPattern(dp)); // ← Convert to ECS format

    // Lines 115-119: Rotate sockets clockwise for next iteration
    byte oldN = rn;
    rn = rw;  // North ← West
    rw = rs;  // West  ← South
    rs = re;  // South ← East
    re = oldN; // East ← North
}
```

**Output (after Floor/Wall exclusion):**

| Pattern ID | Type     | Rotation | Name         | N | E | S | W | Socket Sig |
|------------|----------|----------|--------------|---|---|---|---|------------|
| 0          | Corridor | 0°       | Corridor_0   | F | W | F | W | `FWFW`     |
| 1          | Corridor | 90°      | Corridor_90  | W | F | W | F | `WFWF`     |
| 2          | Corridor | 180°     | Corridor_180 | F | W | F | W | `FWFW`     |
| 3          | Corridor | 270°     | Corridor_270 | W | F | W | F | `WFWF`     |
| 4          | Corner   | 0°       | Corner_0     | F | F | W | W | `FFWW`     |
| 5          | Corner   | 90°      | Corner_90    | W | F | F | W | `WFFW`     |
| 6          | Corner   | 180°     | Corner_180   | W | W | F | F | `WWFF`     |
| 7          | Corner   | 270°     | Corner_270   | F | W | W | F | `FWWF`     |
| 8          | DeadEnd  | 0°       | Door_0       | F | W | W | W | `FWWW`     |
| 9          | DeadEnd  | 90°      | Door_90      | W | F | W | W | `WFWW`     |
| 10         | DeadEnd  | 180°     | Door_180     | W | W | F | W | `WWFW`     |
| 11         | DeadEnd  | 270°     | Door_270     | W | W | W | F | `WWWF`     |

---

## 2. Pattern Initialization (Runtime - OnCreate)

### Step 2A: Load Patterns into Blob Asset
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs`  
**Function:** `InitializeWFCData` (lines 326-358)

```csharp
// Line 337: Get pattern list from WFCBuilder
var patterns = WFCBuilder.CreateDungeonMacroTilePatterns();

// Lines 339-354: Convert to blob asset and store in WFCComponent
var patternData = WFCBuilder.CreatePatternData(patterns.ToArray());
wfc.patterns = patternData; // ← Stored in WFCComponent.patterns

// Lines 360-374: Initialize cells with all patterns possible
for (int y = 0; y < gridSize.y; y++)
{
    for (int x = 0; x < gridSize.x; x++)
    {
        var cellEntity = EntityManager.CreateEntity();
        var cell = new WFCCell
        {
            position = new int2(x, y),
            collapsed = false,
            selectedPattern = -1,                    // ← -1 = not collapsed yet
            possiblePatternsMask = allPatternsMask,  // ← All 12 patterns possible (bits 0-11 set)
            // ...
        };
        EntityManager.AddComponentData(cellEntity, cell);
    }
}
```

**State After Initialization:**
- `WFCComponent.patterns`: Blob array of 12 `WFCPattern` structs (IDs 0-11)
- 25 `WFCCell` entities: Each with `possiblePatternsMask = 0b111111111111` (all 12 patterns possible)

---

## 3. Cell Collapse (Runtime - WFC Generation)

### Step 3A: Select Pattern ID from Possible Set
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs`  
**Function:** `TryCollapseCell` (lines 361-428)

```csharp
// Line 366: Count possible patterns from bitmask
int possibleCount = WFCCellHelpers.CountPossiblePatterns(cell.possiblePatternsMask);

// Lines 374-380: Collapse if only 1 pattern left
if (possibleCount == 1)
{
    int selectedPattern = WFCCellHelpers.GetFirstPossiblePattern(cell.possiblePatternsMask);
    cell.selectedPattern = selectedPattern;  // ← Store pattern ID (0-11)
    cell.collapsed = true;
}

// Lines 382-399: Random collapse (50% chance if ≤3 patterns possible)
else if (possibleCount <= 3 && UnityEngine.Random.Range(0f, 1f) < 0.5f)
{
    // Build array of possible pattern IDs
    int[] possiblePatterns = new int[possibleCount];
    int index = 0;
    for (int i = 0; i < 32; i++)
    {
        if (WFCCellHelpers.IsPatternPossible(ref cell, i))
        {
            possiblePatterns[index++] = i;  // ← Collect IDs: [0,5,8] for example
        }
    }
    // Pick random ID from possible set
    int selectedPattern = possiblePatterns[UnityEngine.Random.Range(0, possibleCount)];
    cell.selectedPattern = selectedPattern;  // ← Store chosen ID
    cell.collapsed = true;
}
```

**Key Field:** `WFCCell.selectedPattern` (int)
- **Type:** Index into `WFCComponent.patterns` blob array
- **Range:** 0-11 (after Floor/Wall exclusion)
- **Meaning:** Encodes both tile type AND rotation variant
- **Storage:** Updated in-place on the `WFCCell` component

---

## 4. Example Trace: Corridor at (1,0)

### Scenario: Cell (1,0) collapses to Corridor_90

**Step 4A: Collapse Decision**
```csharp
// HybridWFCSystem.cs:396
cell.position = int2(1, 0)
cell.possiblePatternsMask = 0b000000001010  // Patterns 1 and 3 possible (both horizontal corridors)
possiblePatterns = [1, 3]                    // Array of possible IDs
selectedPattern = possiblePatterns[Random.Range(0, 2)] = 1  // ← Chose pattern ID 1
cell.selectedPattern = 1                     // ← STORED
cell.collapsed = true
```

**Step 4B: Pattern Lookup at Render Time**
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:262`

```csharp
var wfc = SystemAPI.GetSingleton<WFCComponent>();
ref var blobPatterns = ref wfc.patterns.Value.patterns;  // ← Load blob array
var pat = blobPatterns[cell.selectedPattern];            // ← blobPatterns[1]

// Pattern 1 contains:
pat.patternId = 1
pat.type = (int)DungeonPatternType.Corridor  // ← Type: 3
pat.north = (byte)'W'                        // ← Sockets after 90° rotation
pat.east = (byte)'F'
pat.south = (byte)'W'
pat.west = (byte)'F'
// Socket signature: WFWF (horizontal corridor)
```

**Step 4C: Determine Rotation**
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:282-283, 404-412`

```csharp
// Line 282: Switch on pattern type
case DungeonPatternType.Corridor:
    rotation = DetermineCorridorRotation(pat);  // ← Call rotation function

// Line 404-412: DetermineCorridorRotation logic
private static quaternion DetermineCorridorRotation(WFCPattern pat)
{
    bool openNS = pat.north == (byte)'F' && pat.south == (byte)'F';  // W && W = false
    bool openEW = pat.east == (byte)'F' && pat.west == (byte)'F';    // F && F = true ✓
    
    if (openNS) return quaternion.identity;           // Not this case
    if (openEW) return quaternion.Euler(0, math.radians(90f), 0);  // ← THIS PATH (90° rotation)
    return quaternion.identity;
}
```

**Result:** `rotation = quaternion.Euler(0, 1.5708f, 0)` (90° around Y-axis)

**Step 4D: Apply Transform**
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:313-319`

```csharp
var instance = EntityManager.Instantiate(prefabs.corridorPrefab);  // ← Clone prefab entity

float cellSize = 1.0f;  // From WFCComponent.cellSize
var transform = new LocalTransform
{
    Position = new float3(cell.position.x * cellSize, 0, cell.position.y * cellSize),
    //         = new float3(1 * 1.0f, 0, 0 * 1.0f)
    //         = (1, 0, 0)
    Rotation = rotation,  // ← quaternion.Euler(0, 90°, 0)
    Scale = 1f
};
ecb.SetComponent(instance, transform);  // ← Apply via command buffer
```

**Final State:**
- **GameObject Position:** `(1, 0, 0)` (1 unit east, ground level)
- **GameObject Rotation:** `(0°, 90°, 0°)` Euler = Facing +X axis (horizontal corridor)
- **Socket Compatibility:** E/W open (F), N/S closed (W)

---

## 5. Example Trace: Corner at (2,1)

### Scenario: Cell (2,1) collapses to Corner_180

**Step 5A: Collapse Decision**
```csharp
cell.position = int2(2, 1)
cell.selectedPattern = 6  // ← Corner_180 (SW orientation)
cell.collapsed = true
```

**Step 5B: Pattern Lookup**
```csharp
var pat = blobPatterns[6];  // Pattern ID 6

// Pattern 6 contains:
pat.type = (int)DungeonPatternType.Corner  // ← Type: 4
pat.north = (byte)'W'                      // ← Sockets after 180° rotation
pat.east = (byte)'W'
pat.south = (byte)'F'
pat.west = (byte)'F'
// Socket signature: WWFF (SW corner)
```

**Step 5C: Determine Rotation**
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:287, 414-426`

```csharp
// Line 287: Switch on pattern type
case DungeonPatternType.Corner:
    rotation = DetermineCornerRotation(pat);  // ← Call rotation function

// Line 414-426: DetermineCornerRotation logic
private static quaternion DetermineCornerRotation(WFCPattern pat)
{
    bool n = pat.north == (byte)'F';  // W == F ? false
    bool e = pat.east == (byte)'F';   // W == F ? false
    bool s = pat.south == (byte)'F';  // F == F ? true ✓
    bool w = pat.west == (byte)'F';   // F == F ? true ✓
    
    if (n && e) return quaternion.identity;                        // false && false = skip
    if (e && s) return quaternion.Euler(0, math.radians(90f), 0); // false && true = skip
    if (s && w) return quaternion.Euler(0, math.radians(180f), 0); // true && true = ✓ THIS PATH
    if (w && n) return quaternion.Euler(0, math.radians(270f), 0); // skip
    return quaternion.identity;
}
```

**Result:** `rotation = quaternion.Euler(0, 3.14159f, 0)` (180° around Y-axis)

**Step 5D: Apply Transform**
```csharp
var instance = EntityManager.Instantiate(prefabs.cornerPrefab);

var transform = new LocalTransform
{
    Position = new float3(2 * 1.0f, 0, 1 * 1.0f),  // ← (2, 0, 1)
    Rotation = quaternion.Euler(0, 180°, 0),       // ← 180° rotation
    Scale = 1f
};
ecb.SetComponent(instance, transform);
```

**Final State:**
- **GameObject Position:** `(2, 0, 1)`
- **GameObject Rotation:** `(0°, 180°, 0°)` Euler = L-shape opens toward S and W
- **Socket Compatibility:** S/W open (F), N/E closed (W)

---

## 6. Rotation Determination Functions Summary

### Corridor Rotation Logic
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:404-412`

```csharp
private static quaternion DetermineCorridorRotation(WFCPattern pat)
{
    bool openNS = pat.north == (byte)'F' && pat.south == (byte)'F';
    bool openEW = pat.east == (byte)'F' && pat.west == (byte)'F';
    
    if (openNS) return quaternion.identity;           // 0° - Vertical
    if (openEW) return quaternion.Euler(0, math.radians(90f), 0);  // 90° - Horizontal
    return quaternion.identity;                       // Fallback (should not occur)
}
```

**Mapping:**
| Socket Sig | openNS | openEW | Rotation |
|------------|--------|--------|----------|
| `FWFW`     | true   | false  | 0°       |
| `WFWF`     | false  | true   | 90°      |

---

### Corner Rotation Logic
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:414-426`

```csharp
private static quaternion DetermineCornerRotation(WFCPattern pat)
{
    bool n = pat.north == (byte)'F';
    bool e = pat.east == (byte)'F';
    bool s = pat.south == (byte)'F';
    bool w = pat.west == (byte)'F';
    
    if (n && e) return quaternion.identity;                 // 0°   - NE corner
    if (e && s) return quaternion.Euler(0, math.radians(90f), 0);  // 90°  - SE corner
    if (s && w) return quaternion.Euler(0, math.radians(180f), 0); // 180° - SW corner
    if (w && n) return quaternion.Euler(0, math.radians(270f), 0); // 270° - WN corner
    return quaternion.identity;                             // Fallback
}
```

**Mapping:**
| Socket Sig | n | e | s | w | Open Edges | Rotation |
|------------|---|---|---|---|------------|----------|
| `FFWW`     | T | T | F | F | N & E      | 0°       |
| `WFFW`     | F | T | T | F | E & S      | 90°      |
| `WWFF`     | F | F | T | T | S & W      | 180°     |
| `FWWF`     | T | F | F | T | W & N      | 270°     |

---

### DeadEnd/Door Rotation Logic
**Note:** DeadEnd does NOT use a special rotation function in current code.

**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:276-278`

```csharp
case DungeonPatternType.Door:
    prefabToSpawn = prefabs.doorPrefab;
    // No rotation assignment → defaults to quaternion.identity (line 257)
    break;
```

**Issue:** DeadEnd rotation is NOT calculated based on sockets!
- **Current Behavior:** All Door patterns spawn at 0° rotation regardless of socket orientation
- **Expected Behavior:** Should rotate to face open edge (N/E/S/W)

**Suggested Fix (Not Implemented):**
```csharp
case DungeonPatternType.Door:
    prefabToSpawn = prefabs.doorPrefab;
    rotation = DetermineDeadEndRotation(pat);  // ← Missing function
    break;

private static quaternion DetermineDeadEndRotation(WFCPattern pat)
{
    if (pat.north == (byte)'F') return quaternion.identity;                 // 0°
    if (pat.east == (byte)'F') return quaternion.Euler(0, math.radians(90f), 0);  // 90°
    if (pat.south == (byte)'F') return quaternion.Euler(0, math.radians(180f), 0); // 180°
    if (pat.west == (byte)'F') return quaternion.Euler(0, math.radians(270f), 0);  // 270°
    return quaternion.identity;
}
```

---

## 7. Dataflow Summary Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ COMPILE TIME: WFCBuilder.CreateDungeonMacroTilePatterns()      │
│ ─────────────────────────────────────────────────────────────── │
│ Base: Corridor FWFW                                            │
│   ↓ AddRotated() rotates sockets 4 times                       │
│ Output: [Corridor_0(FWFW), Corridor_90(WFWF), ...]             │
└────────────────────────┬────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────────┐
│ RUNTIME INIT: HybridWFCSystem.InitializeWFCData()              │
│ ─────────────────────────────────────────────────────────────── │
│ Load patterns → WFCComponent.patterns (Blob Asset)             │
│ Create cells → WFCCell.selectedPattern = -1 (not collapsed)    │
└────────────────────────┬────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────────┐
│ RUNTIME COLLAPSE: HybridWFCSystem.TryCollapseCell()            │
│ ─────────────────────────────────────────────────────────────── │
│ Choose from possible patterns                                   │
│ Store: cell.selectedPattern = 1 ← Pattern ID                   │
│       cell.collapsed = true                                     │
└────────────────────────┬────────────────────────────────────────┘
                         ↓
┌─────────────────────────────────────────────────────────────────┐
│ RUNTIME RENDER: DungeonRenderingSystem.SpawnDungeonElement()   │
│ ─────────────────────────────────────────────────────────────── │
│ Read: pat = blobPatterns[cell.selectedPattern]                 │
│ Extract: pat.type, pat.north, pat.east, pat.south, pat.west    │
│ Calculate: rotation = DetermineCorridorRotation(pat)           │
│ Apply: transform.Rotation = rotation                            │
│        EntityManager.Instantiate(prefab) + SetComponent()       │
└─────────────────────────────────────────────────────────────────┘
```

**Key Storage Points:**
1. **Pattern Definition:** `WFCPattern.north/east/south/west` (byte) - Stored in blob asset
2. **Pattern Selection:** `WFCCell.selectedPattern` (int) - Stores chosen pattern ID
3. **Rotation Calculation:** Local variable `quaternion rotation` - Derived from sockets at spawn time
4. **Final Transform:** `LocalTransform.Rotation` - Applied to spawned entity

---

**End of Dry Run Trace** — All dataflow points documented with exact file:line references.

