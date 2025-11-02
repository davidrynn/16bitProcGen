# Linear Model Alignment Test

## Problem
- **Need to verify** if models are oriented correctly in their base prefabs
- **Need to compare** actual model orientations with expected socket patterns
- **Need to test** rotation logic by spawning models with specific rotations

## Solution
Created two test scripts to spawn models in a line for visual inspection:

### 1. ModelAlignmentTest.cs
**Purpose:** Spawn all models without rotation to check base orientations

```csharp
public class ModelAlignmentTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTest = false;
    public float spacing = 2.0f;
    public float yOffset = 0.0f;
    
    [Header("Prefab References")]
    public GameObject doorPrefab;
    public GameObject corridorPrefab;
    public GameObject cornerPrefab;
    public GameObject floorPrefab;
    public GameObject wallPrefab;
}
```

**What it does:**
- Spawns all models in a line without any rotation
- Shows base orientations of each model type
- Logs expected orientations for comparison

### 2. SocketPatternTest.cs
**Purpose:** Spawn models with specific socket patterns and rotations

```csharp
public class SocketPatternTest : MonoBehaviour
{
    [Header("Test Settings")]
    public bool runTest = false;
    public float spacing = 3.0f;
    public float yOffset = 0.0f;
    
    [Header("Prefab References")]
    public GameObject doorPrefab;
    public GameObject corridorPrefab;
    public GameObject cornerPrefab;
}
```

**What it does:**
- Spawns models with specific socket patterns (FWWW, WFWW, etc.)
- Applies expected rotations for each pattern
- Shows what the final orientations should look like

## How to Use

### Step 1: Setup Test Scene
1. **Create empty GameObject** in scene
2. **Add ModelAlignmentTest component**
3. **Assign prefab references** in Inspector
4. **Set runTest = true** or use Context Menu

### Step 2: Run Base Orientation Test
1. **Run ModelAlignmentTest** to see base orientations
2. **Check scene view** to see models in a line
3. **Verify orientations** match expected socket patterns

### Step 3: Run Socket Pattern Test
1. **Add SocketPatternTest component** to same GameObject
2. **Assign prefab references** in Inspector
3. **Set runTest = true** or use Context Menu
4. **Check scene view** to see rotated models

## Expected Results

### ModelAlignmentTest Output:
```
=== MODEL ALIGNMENT TEST STARTING ===
Spawning all models in a line without rotation to check base orientations
Spawned Door (Base) at position (0, 0, 0) - Expected: One open side should face +Z
Spawned Corridor (Base) at position (2, 0, 0) - Expected: Two opposite open sides should face +Z and -Z
Spawned Corner (Base) at position (4, 0, 0) - Expected: Two adjacent open sides should face +Z and +X
Spawned Floor (Base) at position (6, 0, 0) - Expected: All sides open (FFFF)
Spawned Wall (Base) at position (8, 0, 0) - Expected: All sides closed (WWWW)
=== MODEL ALIGNMENT TEST COMPLETE ===
```

### SocketPatternTest Output:
```
=== SOCKET PATTERN TEST STARTING ===
=== DOOR/DEADEND PATTERNS ===
Spawned Door FWWW at (0, 0, 0) with rotation (0, 0, 0) - Should face +Z (forward)
Spawned Door WFWW at (3, 0, 0) with rotation (0, 90, 0) - Should face +X (right)
Spawned Door WWFW at (6, 0, 0) with rotation (0, 180, 0) - Should face -Z (backward)
Spawned Door WWWF at (9, 0, 0) with rotation (0, 270, 0) - Should face -X (left)
=== CORRIDOR PATTERNS ===
Spawned Corridor FWFW at (12, 0, 0) with rotation (0, 0, 0) - Should face +Z and -Z
Spawned Corridor WFW at (15, 0, 0) with rotation (0, 90, 0) - Should face +X and -X
=== CORNER PATTERNS ===
Spawned Corner FFWW at (18, 0, 0) with rotation (0, 0, 0) - Should face +Z and +X
Spawned Corner WFFW at (21, 0, 0) with rotation (0, 90, 0) - Should face +X and -Z
Spawned Corner WWFF at (24, 0, 0) with rotation (0, 180, 0) - Should face -Z and -X
Spawned Corner FWWF at (27, 0, 0) with rotation (0, 270, 0) - Should face -X and +Z
```

## What to Look For

### In ModelAlignmentTest:
1. **Door model:** Should have one open side facing +Z (forward)
2. **Corridor model:** Should have two opposite open sides facing +Z and -Z
3. **Corner model:** Should have two adjacent open sides facing +Z and +X
4. **Floor model:** Should have all sides open
5. **Wall model:** Should have all sides closed

### In SocketPatternTest:
1. **FWWW pattern:** Should face +Z after 0° rotation
2. **WFWW pattern:** Should face +X after 90° rotation
3. **WWFW pattern:** Should face -Z after 180° rotation
4. **WWWF pattern:** Should face -X after 270° rotation

## Troubleshooting

### If Models Don't Match Expected Orientations:
1. **Base models are incorrectly oriented** in prefabs
2. **Socket definitions don't match** model geometry
3. **Rotation logic needs adjustment**

### If Rotations Don't Work:
1. **Rotation logic is incorrect** in DungeonRenderingSystem
2. **Quaternion calculations are wrong**
3. **Model pivot points are incorrect**

## Files Created
- `Scripts/DOTS/Test/ModelAlignmentTest.cs` - Base orientation test
- `Scripts/DOTS/Test/SocketPatternTest.cs` - Socket pattern rotation test

## Benefits
- **Visual verification** of model orientations
- **Easy comparison** between expected and actual orientations
- **Clear identification** of orientation problems
- **No complex WFC setup** required
- **Immediate visual feedback** in scene view
