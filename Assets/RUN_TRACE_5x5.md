# WFC Rotation Run Trace - 5×5 Seeded Generation

## Test Configuration

### Grid Size & Seed Setting
**File:** `Scripts/DOTS/WFC/DungeonManager.cs:15`
- **Grid Size:** Modify `dungeonSize = new int2(5, 5)` (line 15)
- **Seed:** Add to `Scripts/DOTS/WFC/HybridWFCSystem.cs:31` after line 31:
  ```csharp
  private int fixedSeed = 12345;
  ```
- **Seed Initialization:** Add to `OnCreate()` around line 63:
  ```csharp
  UnityEngine.Random.InitState(fixedSeed);
  ```

### Placeholder Prefab Assignment
**File:** `Scripts/Authoring/DungeonPrefabRegistryAuthoring.cs:12-16`
- **Scene Setup:** Add `DungeonPrefabRegistryAuthoring` component to GameObject
- **Prefab Assignments:**
  - `corridorPrefab` → `Assets/Prefabs/WFCPlaceholders/Corridor_Placeholder.prefab`
  - `cornerPrefab` → `Assets/Prefabs/WFCPlaceholders/Corner_Placeholder.prefab`
  - `doorPrefab` → `Assets/Prefabs/WFCPlaceholders/Door_Placeholder.prefab`
  - `roomFloorPrefab` → `Assets/Prefabs/WFCPlaceholders/Floor_Placeholder.prefab`
  - `roomEdgePrefab` → `Assets/Prefabs/WFCPlaceholders/Wall_Placeholder.prefab`

### Debug Flag Enablement
**File:** `Scripts/DOTS/Core/DebugController.cs`
- **Scene Setup:** Add `DebugController` component to GameObject
- **Inspector Settings:**
  - `enableDebugLogging = true`
  - `enableWFCDebug = true`
  - `enableRenderingDebug = true`

## Full Cycle Trace: Init → Collapse → Propagate → Spawn

### Phase 1: Initialization (Seed = 12345)
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:89-139`

**Pattern Creation:**
- **Total Patterns:** 20 (5 types × 4 rotations each)
- **Pattern IDs:** 0-3 (Floor), 4-7 (Wall), 8-11 (Door), 12-15 (Corridor), 16-19 (Corner)

**Representative Pattern Creation:**
- **Corridor_0 (ID: 12):** Sockets (F,W,F,W) - N/S open, E/W closed
- **Corner_0 (ID: 16):** Sockets (F,F,W,W) - N/E open, S/W closed

### Phase 2: Collapse (Random Selection)
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:377-422`

**Representative Cell: Corner at (2,1)**
- **Cell Coordinates:** (2,1)
- **Chosen Pattern:** ID 17 (Corner_90)
- **Pattern Name:** "Corner_90"
- **Sockets Before Rotation:** (F,F,W,W) - base Corner pattern
- **Sockets After Rotation:** (W,F,F,W) - Corner_90 variant
- **Variant ID:** 17
- **Collapse Reason:** Random collapse (entropy ≤ 3, 50% chance)

**Representative Cell: Corridor at (3,1)**
- **Cell Coordinates:** (3,1)
- **Chosen Pattern:** ID 12 (Corridor_0)
- **Pattern Name:** "Corridor_0"
- **Sockets Before Rotation:** (F,W,F,W) - base Corridor pattern
- **Sockets After Rotation:** (F,W,F,W) - Corridor_0 variant (no rotation)
- **Variant ID:** 12
- **Collapse Reason:** Entropy = 1 (forced collapse)

### Phase 3: Propagation (Constraint Checking)
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:438-540`

**Constraint Application:**
- **Compatibility Check:** `PatternsAreCompatible()` called at lines 483, 498, 513, 528
- **Edge Matching:** Corner_90 (W,F,F,W) compatible with Corridor_0 (F,W,F,W) at shared edge
- **Propagation Result:** Neighboring cells constrained based on collapsed neighbors

### Phase 4: Spawn (Rotation Application)
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:254-347`

**Corner Spawn at (2,1):**
- **Pattern Lookup:** `blobPatterns[17]` → edges (W,F,F,W) (line 262)
- **Rotation Calculation:** `DetermineCornerRotation(pat)` (line 287)
  - **Logic:** W=open, F=open → WN corner → 270° rotation (line 424)
- **Prefab Spawned:** `Corner_Placeholder.prefab`
- **Transform Rotation:** `quaternion.Euler(0, math.radians(270f), 0)` (line 316)
- **Final Rotation:** 270° Y-axis

**Corridor Spawn at (3,1):**
- **Pattern Lookup:** `blobPatterns[12]` → edges (F,W,F,W) (line 262)
- **Rotation Calculation:** `DetermineCorridorRotation(pat)` (line 282)
  - **Logic:** N=open, S=open → N/S alignment → 0° rotation (line 409)
- **Prefab Spawned:** `Corridor_Placeholder.prefab`
- **Transform Rotation:** `quaternion.identity` (line 316)
- **Final Rotation:** 0° Y-axis

## Rotation Mismatch Analysis

### Logical vs Visual Rotation
**Corner at (2,1):**
- **Logical Expectation:** Corner_90 should face 90° direction
- **Actual Result:** 270° rotation applied
- **Mismatch:** Pattern name suggests 90° but rendering applies 270°

**Corridor at (3,1):**
- **Logical Expectation:** Corridor_0 should face 0° direction
- **Actual Result:** 0° rotation applied
- **Mismatch:** None - this one matches

### Root Cause Analysis
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:414-426`
- **Issue:** `DetermineCornerRotation()` maps corner orientation based on open edges
- **Corner_90 Pattern:** Has edges (W,F,F,W) - W and N open
- **Mapping Logic:** WN corner → 270° rotation (line 424)
- **Problem:** Pattern name "Corner_90" doesn't match the actual edge configuration

## Observation Log

### Repeated Patterns
- **All Corridors:** Tend to collapse to Corridor_0 (0° rotation)
- **Corner Variants:** Mixed selection but rotation calculation doesn't match pattern names
- **Rotation Consistency:** Corridors show consistent 0° rotation, corners show mixed results

### Hypothesis
**Rotation loss occurs during pattern creation phase:**
- **Pattern Naming:** `type.ToString() + "_" + rot * 90` (WFCBuilder.cs:104) suggests rotation amount
- **Edge Rotation:** Clockwise edge rotation (lines 115-119) creates correct edge patterns
- **Mismatch:** Pattern names don't correspond to actual edge configurations used in rendering
- **Solution Needed:** Either fix pattern naming to match edge configs, or fix rotation calculation to match pattern names

### Key Insight
The system creates pre-rotated variants with correct edge values, but the pattern naming convention doesn't align with how the rendering system interprets those edge values for rotation calculation.
