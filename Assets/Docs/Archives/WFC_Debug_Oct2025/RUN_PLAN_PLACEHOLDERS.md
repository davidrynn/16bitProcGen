# WFC Placeholder Rotation Test - Run Plan
**Goal:** Validate rotation behavior with Corridor/Corner/DeadEnd using simple placeholder prefabs  
**Test Grid:** 5×5  
**Reproducibility:** Seed 12345 (requires manual implementation)

---

## 1. Grid Size Configuration

### DungeonManager Settings
**File:** `Scripts/DOTS/WFC/DungeonManager.cs`  
**Lines:** 14-16

```csharp
public float3 dungeonPosition = new float3(0, 0, 0);
public int2 dungeonSize = new int2(16, 16);  // ← Change to (5, 5)
public float cellSize = 1.0f;
```

**Action Required:**
- Inspector: Set `Dungeon Size` to `X=5, Y=5`
- Or: Edit line 15 directly to `new int2(5, 5)`

---

## 2. Random Seed (Manual Implementation Required)

**Problem:** The system uses `UnityEngine.Random` without seed initialization.

**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs`  
**Lines:** 382, 396, 401, 415, 423

Current random calls:
```csharp
// Line 382: Random collapse decision
UnityEngine.Random.Range(0f, 1f) < 0.5f

// Line 396: Select pattern from possible set
possiblePatterns[UnityEngine.Random.Range(0, possibleCount)]

// Line 401: Force collapse probability
UnityEngine.Random.Range(0f, 1f) < 0.1f

// Line 415: Select pattern for forced collapse
possiblePatterns[UnityEngine.Random.Range(0, possiblePatterns.Length)]

// Line 423: Debug logging throttle
UnityEngine.Random.Range(0f, 1f) < 0.1f
```

**Temporary Workaround (Add to OnCreate or OnStartRunning):**
```csharp
// In HybridWFCSystem.OnCreate() after line 33:
UnityEngine.Random.InitState(12345);
DOTS.Terrain.Core.DebugSettings.LogWFC("HybridWFCSystem: Random seed set to 12345");
```

**Note:** This is NOT currently implemented. Without seeding, results will vary between runs.

---

## 3. Placeholder Prefab Wiring

### DungeonPrefabRegistryAuthoring Configuration
**File:** `Scripts/Authoring/DungeonPrefabRegistryAuthoring.cs`  
**Lines:** 12-16 (MonoBehaviour fields)

```csharp
public GameObject corridorPrefab;   // ← Assign: Corridor_Placeholder
public GameObject cornerPrefab;     // ← Assign: Corner_Placeholder
public GameObject roomEdgePrefab;   // ← Assign: (optional, Floor/Wall won't be used)
public GameObject roomFloorPrefab;  // ← Assign: (optional, Floor/Wall won't be used)
public GameObject doorPrefab;       // ← Assign: Door_Placeholder (used as DeadEnd)
```

**Prefab Locations:**
- `Prefabs/WFCPlaceholders/Corridor_Placeholder.prefab`
- `Prefabs/WFCPlaceholders/Corner_Placeholder.prefab`
- `Prefabs/WFCPlaceholders/Door_Placeholder.prefab`

**Inspector Setup:**
1. Locate scene object with `DungeonPrefabRegistryAuthoring` component
2. Drag prefabs into corresponding slots:
   - **Corridor Prefab:** `Corridor_Placeholder`
   - **Corner Prefab:** `Corner_Placeholder`
   - **Door Prefab:** `Door_Placeholder`
3. Room Edge/Floor can be left empty or assigned (won't spawn if patterns excluded)

### Baker Conversion
**File:** `Scripts/Authoring/DungeonPrefabRegistryAuthoring.cs`  
**Lines:** 31-77

The baker converts GameObject references to Entity prefabs:
```csharp
// Lines 43-46: Corridor conversion
if (authoring.corridorPrefab != null)
{
    corridorEntity = GetEntity(authoring.corridorPrefab, TransformUsageFlags.Renderable);
}
```

**Verification:**
- Check console for: `"DungeonRenderingSystem (Macro-only): Waiting for DungeonPrefabRegistry"`
- If this message persists, the baker hasn't run or prefabs aren't assigned

---

## 4. Tileset Limiting (Read-Only)

### Exclude Floor and Wall Patterns
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs`  
**Function:** `CreateDungeonMacroTilePatterns()` (lines 89-139)

**Lines to COMMENT OUT (DO NOT EDIT YET):**

```csharp
// Lines 124-125: Floor pattern (FFFF)
// AddRotated(DungeonPatternType.Floor, (byte)'F', (byte)'F', (byte)'F', (byte)'F', 1.0f);

// Lines 127-128: Wall pattern (WFFF)
// AddRotated(DungeonPatternType.Wall, (byte)'W', (byte)'F', (byte)'F', (byte)'F', 1.0f);
```

**Lines to KEEP ACTIVE:**

```csharp
// Line 130: Corridor (FWFW)
AddRotated(DungeonPatternType.Corridor, (byte)'F', (byte)'W', (byte)'F', (byte)'W', 1.0f);

// Line 133: Corner (FFWW)
AddRotated(DungeonPatternType.Corner, (byte)'F', (byte)'F', (byte)'W', (byte)'W', 1.0f);

// Line 136: DeadEnd/Door (FWWW)
AddRotated(DungeonPatternType.Door, (byte)'F', (byte)'W', (byte)'W', (byte)'W', 0.9f);
```

**Effect:**
- Current: 20 patterns (5 types × 4 rotations, IDs 0-19)
- After: 12 patterns (3 types × 4 rotations, IDs 0-11)

**Pattern ID Remapping:**
| Current IDs | New IDs | Type     | Rotations           |
|-------------|---------|----------|---------------------|
| 8-11        | 0-3     | Corridor | 0°, 90°, 180°, 270° |
| 12-15       | 4-7     | Corner   | 0°, 90°, 180°, 270° |
| 16-19       | 8-11    | DeadEnd  | 0°, 90°, 180°, 270° |

---

## 5. Debug Flag Configuration

### Enable WFC Debug Logging
**File:** `Scripts/DOTS/Core/DebugSettings.cs`  
**Line:** 12

```csharp
public static bool EnableWFCDebug = false;  // ← Change to true
```

**Effect:** Enables logs from:
- `HybridWFCSystem` initialization, cell collapse, propagation
- Pattern selection and entropy reduction

### Enable Rendering Debug Logging
**File:** `Scripts/DOTS/Core/DebugSettings.cs`  
**Line:** 15

```csharp
public static bool EnableRenderingDebug = false;  // ← Change to true
```

**Effect:** Enables logs from:
- `DungeonRenderingSystem` prefab instantiation
- Position/rotation calculations
- Pattern-to-prefab mapping

### Runtime Toggle (Alternative)

You can toggle these flags at runtime via inspector if a MonoBehaviour wrapper exists, or use a debug controller.

**Check for existing wrapper:**
- `Scripts/DOTS/Core/DebugController.cs` (may provide inspector access)
- `Scripts/DOTS/Core/DebugTestController.cs` (test harness variant)

---

## 6. Execution Checklist

### Pre-Run Setup
- [ ] Set `DungeonManager.dungeonSize` to `(5, 5)` in inspector
- [ ] Assign placeholder prefabs to `DungeonPrefabRegistryAuthoring`
- [ ] Comment out Floor/Wall lines in `WFCBuilder.cs` (lines 124-125, 127-128)
- [ ] Set `EnableWFCDebug = true` in `DebugSettings.cs` (line 12)
- [ ] Set `EnableRenderingDebug = true` in `DebugSettings.cs` (line 15)
- [ ] **(Optional)** Add `UnityEngine.Random.InitState(12345)` to `HybridWFCSystem.OnCreate()`

### Run Process
1. Enter Play Mode
2. Press `G` key to trigger generation (or enable `Generate On Start`)
3. Monitor console for WFC collapse logs
4. Wait for rendering completion message
5. Inspect scene view for spawned placeholders

### Expected Console Output
```
[DOTS-WFC] HybridWFCSystem: Initializing...
[DOTS-WFC] HybridWFCSystem: Random seed set to 12345 (if added)
[DOTS-WFC] Cell at (0,0) collapsed to pattern 2 (random collapse)
[DOTS-WFC] Cell at (1,0) collapsed to pattern 5 (entropy=1)
...
[DOTS-Rendering] DungeonRenderingSystem: Spawned Corridor at (1, 0) with transform (1, 0, 0)
[DOTS-Rendering] DungeonRenderingSystem: RENDERING COMPLETE! All 25 cells processed.
```

---

## 7. Known Limitations

### Random Seed
- **Not implemented:** Runs will produce different layouts each time
- **Workaround:** Add seed initialization manually (see Section 2)
- **Future:** Add seed parameter to `WFCComponent` or `DungeonGenerationRequest`

### Pattern Selection Algorithm
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs`  
**Lines:** 374-428 (function `TryCollapseCell`)

Current algorithm uses multiple random branches:
- 50% chance to collapse when 3 or fewer patterns remain (line 382)
- 10% chance to force collapse otherwise (line 401)

**Implication:** Without constraints, cells may collapse unpredictably even with the same seed due to floating-point timing variations.

### Constraint System
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs`  
**Lines:** 144-152 (function `CreateBasicDungeonConstraints`)

```csharp
// Note: Current HybridWFCSystem does not apply constraints during propagation.
// We keep a placeholder list to remain API-compatible.
```

**Implication:** Socket compatibility is NOT enforced. Cells may select patterns that create socket mismatches (F-W adjacency).

---

## 8. Rotation Application Points

### Pattern Storage at Collapse
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs`  
**Lines:** 378, 397, 416

```csharp
cell.selectedPattern = selectedPattern;  // ← Stores pattern ID (includes rotation)
cell.collapsed = true;
```

**Field:** `WFCCell.selectedPattern` (int)  
**Meaning:** Index into `WFCComponent.patterns` blob array (0-11 after limiting, 0-19 normally)

### Transform Rotation at Spawn
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs`  
**Lines:** 254-346 (function `SpawnDungeonElement`)

```csharp
// Line 262: Load pattern from blob
var pat = blobPatterns[cell.selectedPattern];

// Lines 282-287: Determine rotation by pattern type
case DungeonPatternType.Corridor:
    rotation = DetermineCorridorRotation(pat);  // Lines 404-412
    break;
case DungeonPatternType.Corner:
    rotation = DetermineCornerRotation(pat);    // Lines 414-426
    break;

// Lines 313-318: Apply transform
var transform = new LocalTransform
{
    Position = new float3(cell.position.x * cellSize, 0, cell.position.y * cellSize),
    Rotation = rotation,  // ← Quaternion calculated from sockets
    Scale = 1f
};
```

**Key Functions:**
- `DetermineCorridorRotation(pat)` - lines 404-412: Checks N/S vs E/W openings
- `DetermineCornerRotation(pat)` - lines 414-426: Maps NE/ES/SW/WN to 0°/90°/180°/270°

---

**End of Run Plan** — All file:line references confirmed for read-only analysis.

