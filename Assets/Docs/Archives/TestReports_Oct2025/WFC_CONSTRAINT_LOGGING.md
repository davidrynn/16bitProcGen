# WFC Constraint Validation Logging

## Problem
- **WFC constraint violations** causing improper dungeon layouts
- **Corners connecting to deadends** on both open sides
- **Corridors not aligning properly** with neighbors
- **Need to identify exactly where** constraint violations occur

## Solution
Added comprehensive WFC constraint validation logging to `DungeonRenderingSystem`:

### Code Changes
```csharp
// In SpawnDungeonElement method:
// Validate WFC constraints with neighbors
ValidateWFCConstraints(ref cell, pat, ref blobPatterns);

// New methods added:
- ValidateWFCConstraints(ref WFCCell, WFCPattern, ref BlobArray<WFCPattern>) - Main validation function
- CheckNeighborConstraint(int2, WFCPattern, ref BlobArray<WFCPattern>, int2, string, byte, string) - Checks individual neighbor constraints
- GetNeighborSocket() - Gets socket value for specific direction
- GetSocketString() - Creates readable socket representation
```

### What It Logs
The system now logs **WFC CONSTRAINT VIOLATION** messages when:

1. **Open socket faces closed socket:**
   ```
   WFC CONSTRAINT VIOLATION: Cell at (2,3) has North open (F) but neighbor at (2,4) has South closed (W)
   - This pattern: 3 sockets=FWWW
   - Neighbor pattern: 2 sockets=WWFW
   ```

2. **Closed socket faces open socket:**
   ```
   WFC CONSTRAINT VIOLATION: Cell at (1,1) has East closed (W) but neighbor at (2,1) has West open (F)
   - This pattern: 2 sockets=WFWW
   - Neighbor pattern: 1 sockets=WWFW
   ```

### Expected Output
When you run the 12x12 grid, you should see messages like:
```
[DOTS-Rendering] WFC CONSTRAINT VIOLATION: Cell at (3,4) has North open (F) but neighbor at (3,5) has South closed (W)
[DOTS-Rendering]   - This pattern: 3 sockets=FWWW
[DOTS-Rendering]   - Neighbor pattern: 2 sockets=WWFW
[DOTS-Rendering] WFC CONSTRAINT VIOLATION: Cell at (3,4) has East open (F) but neighbor at (4,4) has West closed (W)
[DOTS-Rendering]   - This pattern: 3 sockets=FWWW
[DOTS-Rendering]   - Neighbor pattern: 2 sockets=WFWW
```

### What This Reveals
The constraint violations will show:
1. **Which cells** have constraint violations
2. **What patterns** are being used
3. **What socket configurations** are causing the violations
4. **Whether the issue is** in pattern definitions or WFC algorithm

### Next Steps
1. **Run the 12x12 grid** and check console for constraint violations
2. **Analyze the violations** to understand the pattern
3. **Check if socket definitions** match the actual model geometry
4. **Fix the WFC algorithm** or pattern definitions based on findings

## Files Modified
- `Scripts/DOTS/WFC/DungeonRenderingSystem.cs` - Added constraint validation logging

## Benefits
- **Identifies exact constraint violations** instead of guessing
- **Shows pattern types and socket configurations** for debugging
- **Helps determine if issue is** in WFC algorithm or pattern definitions
- **Provides concrete data** for fixing the constraint satisfaction
