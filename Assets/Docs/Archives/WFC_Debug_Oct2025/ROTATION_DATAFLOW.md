# WFC Rotation Data Flow Analysis

## Example: One Collapsed Cell Analysis

### Cell Position: (2, 1) - Wall Pattern

#### 1. Pattern Selection
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:377-384`  
**Selected Pattern Index:** 5 (Wall_90 variant)  
**Pattern Data:** `blobPatterns[5]` from `DungeonRenderingSystem.cs:262`

#### 2. Tile Type and Variant
**File:** `Scripts/DOTS/WFC/WFCComponent.cs:22-29`  
- **Tile Type:** `DungeonPatternType.Wall` (value: 1)
- **Variant:** Wall_90 (90° rotation from base)
- **Pattern Name:** "Wall_90" (from `WFCBuilder.cs:104`)

#### 3. Sockets Before/After Rotation
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:127, 114-120`

**Base Wall Pattern (Wall_0):**
- North: 'W' (closed)
- East: 'F' (open) 
- South: 'F' (open)
- West: 'F' (open)

**After 90° Rotation (Wall_90):**
- North: 'F' (was West)
- East: 'W' (was North) 
- South: 'F' (was East)
- West: 'F' (was South)

#### 4. Transform Rotation Applied During Spawn
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:365-402`

**Rotation Calculation Process:**
1. **Neighbor Check:** `IsWallAt()` calls at lines 373-376
2. **Orientation Logic:** Lines 378-401
3. **Final Rotation:** `quaternion.Euler(0, math.radians(90f), 0)` (line 385 or 397)

**Applied Transform:**
```csharp
// From DungeonRenderingSystem.cs:313-319
var transform = new LocalTransform {
    Position = new float3(2 * cellSize, 0, 1 * cellSize),
    Rotation = quaternion.Euler(0, math.radians(90f), 0), // 90° Y-axis
    Scale = 1f
};
```

## Rotation Mismatch Analysis

### Potential Issues Identified

#### 1. Double Rotation Problem
**Location:** Pattern edges are pre-rotated, but rendering applies additional rotation
- **Pattern Level:** Wall_90 has edges already rotated 90°
- **Render Level:** `DetermineWallRotation()` may apply another 90° rotation
- **Result:** Potential 180° total rotation

#### 2. Socket vs Transform Mismatch
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:365-402`
- **Socket Logic:** Based on pattern edge values (pre-rotated)
- **Transform Logic:** Based on neighbor positions (world-space)
- **Mismatch:** Pattern edges don't match world orientation expectations

#### 3. Corner Rotation Inconsistency
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:414-426`
- **Pattern Creation:** Corner_0 has N/E open, S/W closed
- **Render Logic:** Maps NE→0°, ES→90°, SW→180°, WN→270°
- **Issue:** Pattern edge rotation doesn't align with render mapping

## Specific File:Line References

### Pattern Creation Rotation
- **WFCBuilder.cs:114-120:** Edge rotation logic
- **WFCBuilder.cs:104:** Pattern naming with rotation suffix

### Pattern Selection
- **HybridWFCSystem.cs:381:** `cell.selectedPattern = selectedPattern`
- **HybridWFCSystem.cs:262:** Pattern lookup in rendering

### Rotation Application
- **DungeonRenderingSystem.cs:273:** Wall rotation call
- **DungeonRenderingSystem.cs:316:** Transform rotation assignment
- **DungeonRenderingSystem.cs:385:** 90° rotation application

### Visualization Rotation
- **DungeonVisualizationSystem.cs:273-292:** GameObject rotation application
- **DungeonVisualizationSystem.cs:291:** Full transform rotation for walls/doors/corners

## Debug Points for Rotation Issues

### 1. Pattern Edge Verification
**Check:** Pattern edges match expected orientation after rotation
**Location:** `WFCBuilder.cs:114-120`

### 2. Neighbor-Based Rotation
**Check:** `DetermineWallRotation()` logic matches pattern edge expectations  
**Location:** `DungeonRenderingSystem.cs:365-402`

### 3. Transform Application
**Check:** Final quaternion matches intended world orientation
**Location:** `DungeonRenderingSystem.cs:313-319`

### 4. Visualization Consistency
**Check:** GameObject rotation matches DOTS transform rotation
**Location:** `DungeonVisualizationSystem.cs:273-292`

