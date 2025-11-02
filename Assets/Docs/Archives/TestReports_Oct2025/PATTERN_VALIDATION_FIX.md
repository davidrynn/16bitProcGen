# Pattern Validation Fix

## Problem
- **IndexOutOfRangeException** in `DungeonRenderingSystem.SpawnDungeonElement` at line 262
- **Root Cause:** WFC generates invalid pattern indices (-1) for larger grids (12x12)
- **Symptom:** Crashes when trying to access `blobPatterns[cell.selectedPattern]` with `selectedPattern = -1`

## Solution
Added pattern validation safety check in `DungeonRenderingSystem.SpawnDungeonElement()`:

### Code Changes
```csharp
// Validate pattern index before array access
var wfc = SystemAPI.GetSingleton<WFCComponent>();
ref var blobPatterns = ref wfc.patterns.Value.patterns;

if (cell.selectedPattern < 0 || cell.selectedPattern >= blobPatterns.Length)
{
    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Invalid pattern {cell.selectedPattern} for cell at {cell.position}, skipping spawn (valid range: 0-{blobPatterns.Length - 1})");
    DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonRenderingSystem: Cell state - collapsed: {cell.collapsed}, visualized: {cell.visualized}");
    return; // Skip this cell instead of crashing
}

// Look up selected pattern to derive type and edges
var pat = blobPatterns[cell.selectedPattern];
```

### Benefits
1. **Prevents Crashes:** No more `IndexOutOfRangeException` when WFC generates invalid patterns
2. **Debug Information:** Logs invalid patterns and cell state for debugging
3. **Graceful Degradation:** Skips invalid cells instead of crashing entire system
4. **Maintains Functionality:** Valid cells still get spawned with proper rotation logic

## Expected Results
- **5x5 Grid:** Should work as before (no invalid patterns)
- **12x12 Grid:** Should not crash, but will show warning messages for invalid patterns
- **Console Output:** Will show which cells have invalid patterns and their state
- **Visual Result:** Valid cells will render with proper rotations, invalid cells will be skipped

## Next Steps
1. **Test with 12x12 grid** to verify no crashes
2. **Check console logs** for invalid pattern messages like:
   ```
   [DOTS-Rendering] DungeonRenderingSystem: Invalid pattern -1 for cell at (4,0), skipping spawn (valid range: 0-11)
   [DOTS-Rendering] DungeonRenderingSystem: Cell state - collapsed: true, visualized: false
   ```
3. **Debug WFC algorithm** to understand why it generates pattern -1
4. **Fix WFC pattern generation** to ensure valid patterns (0-11) are always generated

## Files Modified
- `Scripts/DOTS/WFC/DungeonRenderingSystem.cs` - Added pattern validation safety check
