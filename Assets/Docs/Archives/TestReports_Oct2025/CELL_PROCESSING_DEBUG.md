# Cell Processing Debug Logging

## Problem
- **DungeonRenderingSystem processes 0 cells** for rendering
- **No constraint validation messages** appearing in logs
- **Need to understand why** cells are not being processed

## Solution
Added detailed cell state logging to `DungeonRenderingSystem` to understand why 0 cells are processed:

### Code Changes
```csharp
// Enhanced cell processing with detailed state tracking
int processedCells = 0;
int totalCells = 0;
int collapsedCells = 0;
int visualizedCells = 0;
int collapsedNotVisualized = 0;
int sampleCount = 0;

Entities
    .WithAll<WFCCell>()
    .ForEach((Entity entity, ref WFCCell cell) =>
    {
        totalCells++;
        if (cell.collapsed) collapsedCells++;
        if (cell.visualized) visualizedCells++;
        
        // Log sample of cell states for debugging
        if (sampleCount < 5)
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering($"Sample cell at ({cell.position.x},{cell.position.y}) - collapsed: {cell.collapsed}, visualized: {cell.visualized}, pattern: {cell.selectedPattern}");
            sampleCount++;
        }
        
        if (cell.collapsed && !cell.visualized)
        {
            // Process cell for rendering
            SpawnDungeonElement(ref cell);
            cell.visualized = true;
            processedCells++;
        }
    }).WithoutBurst().Run();

// Detailed state breakdown
DOTS.Terrain.Core.DebugSettings.LogRendering($"Processed {processedCells}/{totalCells} cells for rendering");
DOTS.Terrain.Core.DebugSettings.LogRendering($"Cell state breakdown - Total: {totalCells}, Collapsed: {collapsedCells}, Visualized: {visualizedCells}, CollapsedNotVisualized: {collapsedNotVisualized}");
```

### What It Will Log
The enhanced logging will show:

1. **Sample Cell States:**
   ```
   [DOTS-Rendering] Sample cell at (0,0) - collapsed: true, visualized: true, pattern: 3
   [DOTS-Rendering] Sample cell at (1,0) - collapsed: true, visualized: true, pattern: 2
   [DOTS-Rendering] Sample cell at (2,0) - collapsed: true, visualized: true, pattern: 1
   ```

2. **Cell State Breakdown:**
   ```
   [DOTS-Rendering] Processed 0/144 cells for rendering
   [DOTS-Rendering] Cell state breakdown - Total: 144, Collapsed: 140, Visualized: 140, CollapsedNotVisualized: 0
   ```

### Expected Findings
Based on the current behavior, we expect to see:
- **Total: 144** (12x12 grid)
- **Collapsed: 140** (some cells failed to collapse)
- **Visualized: 140** (all collapsed cells already marked as visualized)
- **CollapsedNotVisualized: 0** (no cells need processing)
- **Processed: 0** (no cells processed)

### Why This Happens
The most likely scenario is:
1. **WFC completes** and marks cells as collapsed
2. **Some other system** (possibly DungeonVisualizationSystem) marks cells as visualized
3. **DungeonRenderingSystem** finds no cells to process (all already visualized)
4. **Fallback system** (DungeonVisualizationSystem) handles the rendering

### Next Steps
1. **Run the 12x12 grid** with enhanced logging
2. **Check console for** cell state breakdown
3. **Identify which system** is marking cells as visualized
4. **Fix the rendering flow** to use proper DungeonRenderingSystem

## Files Modified
- `Scripts/DOTS/WFC/DungeonRenderingSystem.cs` - Added detailed cell state logging

## Benefits
- **Shows exact cell states** (collapsed, visualized, pattern)
- **Reveals why 0 cells** are processed
- **Identifies the root cause** of rendering issues
- **Provides data** for fixing the rendering flow
