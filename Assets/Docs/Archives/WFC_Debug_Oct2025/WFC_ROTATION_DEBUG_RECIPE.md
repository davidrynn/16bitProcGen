# WFC Rotation Debug Run Recipe

## Test Setup: 5×5 Grid with Fixed Seed

### 1. Scene Setup
1. **Open Scene:** `Scenes/Test.unity` or `Scenes/SampleScene.unity`
2. **Add DebugController:** Create empty GameObject → Add `DebugController` component
3. **Configure DebugController:**
   - ✅ `enableDebugLogging = true`
   - ✅ `enableWFCDebug = true` 
   - ✅ `enableRenderingDebug = true`
   - ✅ `enableTestSystems = false` (use production systems)

### 2. Enable Debug Flags
**File:** `Scripts/DOTS/Core/DebugController.cs`

**In Unity Inspector, set:**
```csharp
DebugController component:
├── enableDebugLogging = true
├── enableWFCDebug = true  
├── enableRenderingDebug = true
├── enableTerrainDebug = false
├── enableWeatherDebug = false
├── enableTestDebug = false
└── enableTestSystems = false
```

**Alternative: Runtime Toggle**
- Press **D** key to enable all debug logging (if `DebugTestController` is present)
- Or use `DebugController.EnableAllDebug()` context menu

### 3. Fixed Seed Configuration
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:31`

**Temporarily modify line 31:**
```csharp
// Change from:
private bool enableDebugLogs = true;

// To:
private bool enableDebugLogs = true;
private int fixedSeed = 12345; // Add this line
```

**Then modify random calls (lines 385, 404, 426):**
```csharp
// Change from:
UnityEngine.Random.Range(0f, 1f)

// To:
UnityEngine.Random.Range(0f, 1f) // Will use fixed seed
```

**Set seed before generation:**
```csharp
// Add to HybridWFCSystem.OnCreate() around line 63:
UnityEngine.Random.InitState(fixedSeed);
```

### 4. Grid Size Configuration
**File:** `Scripts/DOTS/WFC/DungeonManager.cs` or create DungeonGenerationRequest

**Set 5×5 grid:**
```csharp
var request = new DungeonGenerationRequest {
    isActive = true,
    position = float3.zero,
    size = new int2(5, 5),  // 5x5 grid
    cellSize = 1.0f
};
```

### 5. Expected Debug Output

#### Pattern Creation Logs
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:96-121`
```
[DOTS-WFC] HybridWFCSystem: Created 20 dungeon patterns and 0 constraints
```

#### Collapse Logs  
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:362-365`
```
[DOTS-WFC] HybridWFCSystem: Cell at (0,0): entropy=20, possible=20, collapsed=False
[DOTS-WFC] HybridWFCSystem: Cell at (1,0): entropy=20, possible=20, collapsed=False
[DOTS-WFC] HybridWFCSystem: Cell at (2,0): entropy=20, possible=20, collapsed=False
```

#### Pattern Selection Logs
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:377-422`
```
[DOTS-WFC] HybridWFCSystem: Cell at (2,1) collapsed to pattern 5 (random collapse)
```

#### Rotation Application Logs
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:217, 337`
```
[DOTS] DungeonRenderingSystem: Spawning element for cell (2,1) pattern=5
[DOTS] DungeonRenderingSystem: Spawned RoomEdge at (2,1) with transform (2,0,1)
```

### 6. Debug Toggle Locations

#### Enable WFC Debug Only
**File:** `Scripts/DOTS/Core/DebugController.cs:130-143`
```csharp
[ContextMenu("Enable WFC Debug Only")]
public void EnableWFCDebugOnly()
```

#### Enable Rendering Debug
**File:** `Scripts/DOTS/Core/DebugSettings.cs:15`
```csharp
public static bool EnableRenderingDebug = false; // Set to true
```

#### Force Log Specific Messages
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:591-594`
```csharp
private void DebugLog(string message, bool force = false)
{
    DOTS.Terrain.Core.DebugSettings.LogWFC($"HybridWFCSystem: {message}", force);
}
```

### 7. Key Debug Points to Monitor

#### Pattern Rotation Creation
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:96-121`
- Watch for 4 variants per pattern type
- Verify edge rotation: N←W, W←S, S←E, E←N

#### Collapse Selection
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:377-422`
- Monitor `cell.selectedPattern` values
- Check entropy reduction from 20 → 1

#### Rotation Calculation
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:365-426`
- Watch `DetermineWallRotation()` calls
- Verify quaternion values: `(0, 90°, 0)` or `(0, 0°, 0)`

#### Transform Application
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:313-319`
- Check `LocalTransform.Rotation` values
- Verify position calculation: `(x*cellSize, 0, y*cellSize)`

### 8. Expected Rotation Flow for Wall at (2,1)

1. **Pattern Creation:** Wall_0, Wall_90, Wall_180, Wall_270 created
2. **Pattern Selection:** Wall_90 (pattern index 5) selected
3. **Edge Analysis:** Pattern has N='F', E='W', S='F', W='F'
4. **Neighbor Check:** Check walls at (2,2), (2,0), (3,1), (1,1)
5. **Rotation Decision:** Based on neighbor pattern types
6. **Transform Application:** `quaternion.Euler(0, 90°, 0)` applied
7. **Visualization:** GameObject rotated to match transform

### 9. Troubleshooting

#### No Debug Output
- Check `DebugController` is in scene
- Verify `enableDebugLogging = true`
- Ensure `EnableWFCDebug = true` in `DebugSettings`

#### No WFC Generation
- Check for `DungeonGenerationRequest` component
- Verify `isActive = true`
- Ensure `WFCComponent` exists in scene

#### Rotation Issues
- Compare pattern edge values vs. expected orientation
- Check neighbor detection in `IsWallAt()`
- Verify quaternion application in `LocalTransform`

