# WFC Rotation Debug Run Recipe

## 5×5 Grid with Fixed Seed Setup

### 1. Enable Debug Flags
**File:** `Scripts/DOTS/Core/DebugController.cs`
- Add `DebugController` component to scene GameObject
- Set in Inspector:
  - `enableDebugLogging = true`
  - `enableWFCDebug = true`
  - `enableRenderingDebug = true`

### 2. Set Fixed Seed
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:31`
- Add after line 31: `private int fixedSeed = 42;`
- Add to `OnCreate()` around line 63: `UnityEngine.Random.InitState(fixedSeed);`

### 3. Configure Grid Size
**File:** `Scripts/DOTS/WFC/DungeonManager.cs` or create `DungeonGenerationRequest`
- Set `size = new int2(5, 5)`
- Set `cellSize = 1.0f`

### 4. Run and Monitor
- Press Play in Unity
- Watch Console for debug output:
  ```
  [DOTS-WFC] HybridWFCSystem: Created 20 dungeon patterns
  [DOTS-WFC] HybridWFCSystem: Cell at (2,1) collapsed to pattern 5
  [DOTS] DungeonRenderingSystem: Spawned Corner at (2,1)
  ```

### 5. Debug Toggle Locations
- **WFC Debug:** `Scripts/DOTS/Core/DebugSettings.cs:12` - `EnableWFCDebug`
- **Rendering Debug:** `Scripts/DOTS/Core/DebugSettings.cs:15` - `EnableRenderingDebug`
- **Runtime Toggle:** Press 'D' key if `DebugTestController` present

### 6. Expected Output
- **Pattern Count:** 20 total (5 types × 4 rotations each)
- **Collapse Pattern:** Cells collapse to specific pattern indices
- **Rotation Application:** Corners show 0°, 90°, 180°, 270° rotations
- **Visual Result:** 5×5 grid with visible tile rotations

