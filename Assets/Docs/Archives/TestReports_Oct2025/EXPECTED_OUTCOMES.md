# WFC Placeholder Test - Expected Outcomes
**Test Configuration:** 5×5 grid, 3 tile types (Corridor/Corner/DeadEnd), Seed 12345  
**Purpose:** Validate socket values and rotation behavior match the defined patterns

---

## 1. Socket/Rotation Checklist

### Corridor Tile - 2 Unique Configurations
**Base Pattern:** `FWFW` (North Open, East Closed, South Open, West Closed)

| Rotation | Socket Signature | North | East | South | West | Visual Orientation        |
|----------|------------------|-------|------|-------|------|---------------------------|
| 0°       | `FWFW`          | F     | W    | F     | W    | Vertical passage (N-S)    |
| 90°      | `WFWF`          | W     | F    | W     | F    | Horizontal passage (E-W)  |
| 180°     | `FWFW`          | F     | W    | F     | W    | **Duplicate of 0°**       |
| 270°     | `WFWF`          | W     | F    | W     | F    | **Duplicate of 90°**      |

**Unique Sockets:** 2 (only 0° and 90° differ)

**Expected Behavior:**
- ✅ Vertical corridors (0°/180°) should face Z-axis (no rotation)
- ✅ Horizontal corridors (90°/270°) should rotate 90° around Y-axis
- ✅ Placeholders with directional markers (arrows/colors) should clearly show orientation

---

### Corner Tile - 4 Unique Configurations
**Base Pattern:** `FFWW` (North Open, East Open, South Closed, West Closed)

| Rotation | Socket Signature | North | East | South | West | Visual Orientation        |
|----------|------------------|-------|------|-------|------|---------------------------|
| 0°       | `FFWW`          | F     | F    | W     | W    | NE corner (opens N & E)   |
| 90°      | `WFFW`          | W     | F    | F     | W    | SE corner (opens S & E)   |
| 180°     | `WWFF`          | W     | W    | F     | F    | SW corner (opens S & W)   |
| 270°     | `FWWF`          | F     | W    | W     | F    | NW corner (opens N & W)   |

**Unique Sockets:** 4 (all rotations differ)

**Expected Behavior:**
- ✅ Each rotation creates a distinct L-shape orientation
- ✅ Open edges should align with adjacent open tiles (if constraints existed)
- ✅ Rotation should progress clockwise: 0° → 90° → 180° → 270°

---

### DeadEnd Tile (Door) - 4 Unique Configurations
**Base Pattern:** `FWWW` (North Open, East Closed, South Closed, West Closed)

| Rotation | Socket Signature | North | East | South | West | Visual Orientation        |
|----------|------------------|-------|------|-------|------|---------------------------|
| 0°       | `FWWW`          | F     | W    | W     | W    | Opens North only          |
| 90°      | `WFWW`          | W     | F    | W     | W    | Opens East only           |
| 180°     | `WWFW`          | W     | W    | F     | W    | Opens South only          |
| 270°     | `WWWF`          | W     | W    | W     | F    | Opens West only           |

**Unique Sockets:** 4 (all rotations differ)

**Expected Behavior:**
- ✅ Each rotation faces a single cardinal direction
- ✅ The open edge should point toward the connected neighbor (if constraints existed)
- ✅ Border cells may favor outward-facing DeadEnds (open edge toward grid interior)

---

## 2. Visual Expectations (5×5 Grid, Seed 12345)

### Grid Layout Assumptions
**Without Seed Implementation:** Results will vary, but statistical expectations apply:
- **25 cells total**
- **3 pattern types** = ~33% each on average (without constraints)
- **4 rotations per type** = ~25% each on average

**With Seed 12345 (if implemented):** Specific reproducible layout.

### Minimum Rotation Coverage
For a successful test, we should observe:

#### Corridor Rotations
- ✅ **At least 1 vertical corridor** (0° or 180°) with placeholder facing Z-axis
- ✅ **At least 1 horizontal corridor** (90° or 270°) with placeholder facing X-axis
- ⚠️ Since 0°≡180° and 90°≡270°, we only need to confirm 2 visual orientations

#### Corner Rotations
- ✅ **At least 1 corner at 0°** (NE opening) - L-shape pointing top-right
- ✅ **At least 1 corner at 90°** (SE opening) - L-shape pointing bottom-right
- ⚠️ Ideally, all 4 rotations appear in 25 cells (62.5% probability with uniform distribution)

#### DeadEnd Rotations
- ✅ **At least 1 DeadEnd facing North** (0°)
- ✅ **At least 1 DeadEnd facing East** (90°)
- ⚠️ Border constraints may skew distribution (edges prefer inward-facing DeadEnds)

### Placeholder Visual Verification
**Prefab Markers:**
- `Corridor_Placeholder`: Purple cylinder with directional arrow
- `Corner_Placeholder`: Blue L-shaped mesh with colored ends
- `Door_Placeholder`: Brown cube with single open face

**Checklist:**
- [ ] All 25 cells have spawned GameObjects
- [ ] At least 2 distinct corridor orientations (vertical/horizontal)
- [ ] At least 2 distinct corner orientations (different L-shapes)
- [ ] At least 2 distinct DeadEnd orientations (different facing directions)
- [ ] No floating/misaligned placeholders (all at Y=0, grid-aligned XZ)

---

## 3. Socket Mismatch Analysis

### Expected Mismatches (Constraints Disabled)
**File:** `Scripts/DOTS/WFC/WFCBuilder.cs:148`
> "Current HybridWFCSystem does not apply constraints during propagation."

**Implication:** Neighbors may have incompatible sockets.

**Example Mismatch:**
```
Cell A (Corridor 0°): FWFW
Cell B (East of A):   (any pattern)

Expected: Cell B's West socket = 'W' (matches A's East='W')
Reality:  Cell B may have West='F' → socket mismatch
```

**Visual Indicators:**
- Corridor `F` edge adjacent to another tile's `W` edge = gap/misalignment
- Corner `F` edges not connecting to neighboring `F` edges = isolated turns

**What to Document:**
- Count of compatible vs. incompatible adjacencies
- Percentage of cells with at least 1 socket mismatch
- Whether generation completes despite mismatches (should succeed)

---

## 4. Console Log Expectations

### Initialization Phase
```
[DOTS-WFC] HybridWFCSystem: Initializing...
[DOTS-WFC] HybridWFCSystem: Random seed set to 12345 (if seed added)
[DOTS-WFC] HybridWFCSystem: Initialization complete
[DOTS-Rendering] DungeonRenderingSystem: OnCreate called
[DOTS-Rendering] DungeonRenderingSystem (Macro-only): Waiting for DungeonPrefabRegistry
```

### Generation Phase (WFC Collapse)
```
[DOTS-WFC] Starting WFC generation for entity 12345
[DOTS-WFC] Cell at (0,0) collapsed to pattern 3 (random collapse)
[DOTS-WFC] Cell at (1,0) collapsed to pattern 7 (entropy=1)
[DOTS-WFC] Cell at (2,0) collapsed to pattern 10 (forced collapse)
...
[DOTS-WFC] All cells collapsed: checking completion
[DOTS-WFC] WFC generation COMPLETE for entity 12345
```

**Expected Pattern IDs (after Floor/Wall exclusion):**
- 0-3: Corridor rotations
- 4-7: Corner rotations
- 8-11: DeadEnd rotations

### Rendering Phase
```
[DOTS-Rendering] DungeonRenderingSystem (Macro-only): WFC state isCollapsed=True
[DOTS-Rendering] DungeonRenderingSystem (Macro-only): Spawning element for cell (0,0) pattern=3
[DOTS-Rendering] DungeonRenderingSystem: Spawned Corridor at (0, 0) with transform (0, 0, 0)
[DOTS-Rendering] DungeonRenderingSystem (Macro-only): Spawning element for cell (1,0) pattern=7
[DOTS-Rendering] DungeonRenderingSystem: Spawned Corner at (1, 0) with transform (1, 0, 0)
...
[DOTS-Rendering] DungeonRenderingSystem: RENDERING COMPLETE! All 25 cells processed.
```

**Rotation-Specific Logs (if enhanced debugging added):**
```
[DOTS-Rendering] Corridor at (1,0): sockets N=W,E=F,S=W,W=F → Horizontal rotation (90°)
[DOTS-Rendering] Corner at (2,1): sockets N=W,E=W,S=F,W=F → SW orientation (180°)
[DOTS-Rendering] DeadEnd at (4,4): sockets N=W,E=W,F=W,W=F → Facing West (270°)
```

---

## 5. Failure Modes & Diagnostics

### Failure: No Placeholders Spawn
**Symptoms:**
- Console: "DungeonRenderingSystem: WFC not collapsed yet"
- Scene: Empty grid

**Diagnosis:**
- Check: WFC collapse probability too low (lines 382, 401 in `HybridWFCSystem.cs`)
- Check: Cell count stuck at 0 collapsed
- Fix: Increase collapse probabilities or add forced collapse after N iterations

---

### Failure: All Placeholders Face Same Direction
**Symptoms:**
- All Corridor placeholders vertical (no 90° rotation)
- All Corners show same L-shape

**Diagnosis:**
- Check: Pattern rotation logic in `DetermineCorridorRotation` (line 404)
- Check: Socket values in spawned patterns match expected signatures
- Debug: Log `pat.north/east/south/west` values at spawn time

**Expected Socket Distribution:**
For Corridor (2 configs):
- 50% should be `FWFW` (vertical)
- 50% should be `WFWF` (horizontal)

For Corner (4 configs):
- 25% each: `FFWW`, `WFFW`, `WWFF`, `FWWF`

---

### Failure: Rotation Angles Don't Match Sockets
**Symptoms:**
- Corridor with `FWFW` sockets rotated 90° (should be 0°)
- Corner with `FFWW` sockets rotated 180° (should be 0°)

**Diagnosis:**
- Check: `DetermineCorridorRotation` logic (lines 407-411)
- Check: `DetermineCornerRotation` logic (lines 417-425)
- Expected mappings:
  - Corridor `FWFW` (N/S open) → `quaternion.identity` (0°)
  - Corridor `WFWF` (E/W open) → `quaternion.Euler(0, math.radians(90f), 0)`
  - Corner `FFWW` (N&E open) → `quaternion.identity` (0°)
  - Corner `WFFW` (E&S open) → `quaternion.Euler(0, math.radians(90f), 0)`

---

## 6. Success Criteria

### Minimum Requirements
- ✅ All 25 cells spawn a placeholder GameObject
- ✅ At least 2 distinct Corridor orientations visible
- ✅ At least 2 distinct Corner orientations visible
- ✅ At least 2 distinct DeadEnd orientations visible
- ✅ Console logs show pattern IDs 0-11 (no Floor/Wall patterns 12-19)
- ✅ No runtime errors or exceptions

### Full Success
- ✅ All minimum requirements met
- ✅ Socket signatures logged and match `SOCKET_TABLE.md` definitions
- ✅ Transform rotations match expected quaternions for each socket type
- ✅ Corridor 0°/180° sockets both produce vertical orientation
- ✅ Corridor 90°/270° sockets both produce horizontal orientation
- ✅ All 4 Corner rotations appear in the 25-cell grid
- ✅ All 4 DeadEnd rotations appear in the 25-cell grid

### Bonus Validation
- ✅ Reproducible results with seed 12345
- ✅ Manual socket compatibility check (adjacent tiles logged with edge values)
- ✅ Rotation symmetry confirmed (Corridor 0°≡180° visually identical)

---

**End of Expected Outcomes** — Use this checklist during test execution to validate rotation behavior.

