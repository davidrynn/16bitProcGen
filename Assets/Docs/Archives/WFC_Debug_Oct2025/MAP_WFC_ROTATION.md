# WFC Rotation System Map

## Exact Call Path: Pattern Creation → Rotation/Variants → Compatibility Check → Collapse Choice → Render Spawn

### 1. Pattern Creation with Pre-Rotated Variants
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:89-139`

- **Entry Point:** `CreateDungeonMacroTilePatterns()` (line 89)
- **Rotation Generation:** `AddRotated()` helper (lines 96-121)
  - Creates 4 pre-rotated variants per base pattern
  - **Edge Rotation Logic:** Lines 115-119 (clockwise: N←W, W←S, S←E, E←N)
  - **Pattern Naming:** `type.ToString() + "_" + rot * 90` (line 104)
- **System Type:** **PRE-ROTATED VARIANTS** - Each rotation is a separate pattern with rotated edge values

### 2. Compatibility Check
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:268-280`

- **Function:** `PatternsAreCompatible(WFCPattern a, WFCPattern b, int direction)`
- **Logic:** Edge-to-edge matching (a.north == b.south, etc.)
- **Usage:** Called from `PruneWithCollapsedNeighbors()` in `HybridWFCSystem.cs:483,498,513,528`

### 3. Collapse Choice (Pattern Selection)
**File:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:332-432`

- **Entry Point:** `ProcessWFCResults(ref WFCCell cell)` (line 332)
- **Pattern Pruning:** `PruneWithCollapsedNeighbors()` (line 355)
- **Collapse Logic:** Lines 377-422
  - **Entropy=1:** Force collapse (lines 377-384)
  - **Random Collapse:** 50% chance when entropy ≤ 3 (lines 385-403)
  - **Forced Collapse:** 10% chance for any entropy (lines 404-422)
- **Selected Pattern Storage:** `cell.selectedPattern = selectedPattern` (lines 381, 400, 419)

### 4. Render Spawn (Rotation Application)
**File:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:254-347`

- **Entry Point:** `SpawnDungeonElement(ref WFCCell cell)` (line 254)
- **Pattern Lookup:** `blobPatterns[cell.selectedPattern]` (line 262)
- **Rotation Determination:** Lines 265-289
  - **Corridor:** `DetermineCorridorRotation(pat)` (line 282)
  - **Corner:** `DetermineCornerRotation(pat)` (line 287)
- **Transform Application:** Lines 313-319
  ```csharp
  var transform = new LocalTransform {
      Position = new float3(cell.position.x * cellSize, 0, cell.position.y * cellSize),
      Rotation = rotation,  // <-- Rotation applied here
      Scale = 1f
  };
  ```

## Key Symbols and Line References

### AddRotated(...)
- **Location:** `Scripts/DOTS/WFC/WFCBuilder.cs:96-121`
- **Purpose:** Creates 4 pre-rotated variants with edge rotation
- **Edge Rotation:** Clockwise (N←W, W←S, S←E, E←N)

### PatternsAreCompatible(...)
- **Location:** `Scripts/DOTS/WFC/WFCBuilder.cs:268-280`
- **Purpose:** Validates edge compatibility between adjacent patterns
- **Usage:** Called from `HybridWFCSystem.cs:483,498,513,528`

### Collapse Point (Variant/Rotation Choice)
- **Location:** `Scripts/DOTS/WFC/HybridWFCSystem.cs:377-422`
- **Pattern Selection:** `cell.selectedPattern = selectedPattern`
- **Storage:** `WFCCell.selectedPattern` field (line 106 in `WFCComponent.cs`)

### Transform Rotation Application Point
- **Location:** `Scripts/DOTS/WFC/DungeonRenderingSystem.cs:313-319`
- **Rotation Storage:** `LocalTransform.Rotation` field
- **Application:** `ecb.SetComponent(instance, transform)` (line 319)

## Rotation Storage and Flow

### Where CHOSEN ROTATION is Stored
1. **Pattern Level:** Each rotated variant is a separate pattern with rotated edge values
2. **Cell Level:** `WFCCell.selectedPattern` stores the chosen pattern index (line 106 in `WFCComponent.cs`)
3. **Render Level:** `LocalTransform.Rotation` stores the final quaternion (line 316 in `DungeonRenderingSystem.cs`)

### Where ROTATION is Read During Rendering
- **Pattern Lookup:** `blobPatterns[cell.selectedPattern]` (line 262 in `DungeonRenderingSystem.cs`)
- **Rotation Calculation:** `DetermineCorridorRotation(pat)` (line 282) or `DetermineCornerRotation(pat)` (line 287)
- **Transform Assignment:** `Rotation = rotation` (line 316 in `DungeonRenderingSystem.cs`)

## System Architecture: Pre-Rotated Variants

**The system uses PRE-ROTATED VARIANTS, not single archetype + runtime rotation:**

- **Pattern Creation:** 4 variants created with rotated edges (WFCBuilder.cs:96-121)
- **Pattern Selection:** WFC algorithm selects one variant index (HybridWFCSystem.cs:377-422)
- **Rotation Calculation:** Pattern edges analyzed to determine world rotation (DungeonRenderingSystem.cs:404-426)
- **Transform Application:** Quaternion applied to spawned entity (DungeonRenderingSystem.cs:313-319)

**Key Insight:** The system creates pre-rotated variants with rotated edge values, then applies additional rotation during rendering based on those edge values. This creates a potential double-rotation issue where pattern edges are pre-rotated but rendering applies additional rotation.