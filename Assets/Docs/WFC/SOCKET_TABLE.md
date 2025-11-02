# WFC Socket Table - Minimal Rotation Test Set
**Scope:** Corridor, Corner, DeadEnd (Door) — 3 base tiles × 4 rotations = 12 total patterns  
**Purpose:** Audit socket values and rotation behavior for testing

---

## Socket Symbol Legend (Dungeon Domain)
| Symbol | Meaning | Description |
|--------|---------|-------------|
| `F`    | Floor/Open | Edge allows connection (open passage) |
| `W`    | Wall/Closed | Edge blocks connection (solid wall) |

**Note:** Terrain patterns use different symbols (W=Water, S=Sand, G=Grass, R=Rock, N=sNow) but are **not used in dungeon generation**.

---

## Pattern Definitions with Rotations

### 1. CORRIDOR (Straight Passage)
**Base Definition:** `WFCBuilder.cs:130`
```csharp
AddRotated(DungeonPatternType.Corridor, (byte)'F', (byte)'W', (byte)'F', (byte)'W', 1.0f);
//         Type                         North      East       South      West       Weight
```

| Rotation | Pattern ID | Name         | North | East | South | West | Visual Description |
|----------|-----------|--------------|-------|------|-------|------|--------------------|
| 0°       | ID varies | Corridor_0   | `F`   | `W`  | `F`   | `W`  | Vertical passage (N-S open, E-W closed) |
| 90°      | ID varies | Corridor_90  | `W`   | `F`  | `W`   | `F`  | Horizontal passage (E-W open, N-S closed) |
| 180°     | ID varies | Corridor_180 | `F`   | `W`  | `F`   | `W`  | **DUPLICATE of 0°** (vertical) |
| 270°     | ID varies | Corridor_270 | `W`   | `F`  | `W`   | `F`  | **DUPLICATE of 90°** (horizontal) |

**Symmetry Note:** Corridor has **2-fold rotational symmetry** (0°≡180°, 90°≡270°). Only 2 unique socket configurations exist.

---

### 2. CORNER (L-shaped Turn)
**Base Definition:** `WFCBuilder.cs:133`
```csharp
AddRotated(DungeonPatternType.Corner, (byte)'F', (byte)'F', (byte)'W', (byte)'W', 1.0f);
//         Type                       North      East       South      West       Weight
```

| Rotation | Pattern ID | Name       | North | East | South | West | Visual Description |
|----------|-----------|------------|-------|------|-------|------|--------------------|
| 0°       | ID varies | Corner_0   | `F`   | `F`  | `W`   | `W`  | NE corner (opens North & East) |
| 90°      | ID varies | Corner_90  | `W`   | `F`  | `F`   | `W`  | SE corner (opens South & East) |
| 180°     | ID varies | Corner_180 | `W`   | `W`  | `F`   | `F`  | SW corner (opens South & West) |
| 270°     | ID varies | Corner_270 | `F`   | `W`  | `W`   | `F`  | NW corner (opens North & West) |

**Symmetry Note:** Corner has **no rotational symmetry**. All 4 rotations produce **unique socket configurations**.

---

### 3. DEAD END (Corridor End / Doorway)
**Base Definition:** `WFCBuilder.cs:136`  
**Enum Name:** `DungeonPatternType.Door` (acts as corridor terminator)
```csharp
AddRotated(DungeonPatternType.Door, (byte)'F', (byte)'W', (byte)'W', (byte)'W', 0.9f);
//         Type                     North      East       South      West       Weight
```

| Rotation | Pattern ID | Name     | North | East | South | West | Visual Description |
|----------|-----------|----------|-------|------|-------|------|--------------------|
| 0°       | ID varies | Door_0   | `F`   | `W`  | `W`   | `W`  | Opens North only |
| 90°      | ID varies | Door_90  | `W`   | `F`  | `W`   | `W`  | Opens East only |
| 180°     | ID varies | Door_180 | `W`   | `W`  | `F`   | `W`  | Opens South only |
| 270°     | ID varies | Door_270 | `W`   | `W`  | `W`   | `F`  | Opens West only |

**Symmetry Note:** Dead End has **no rotational symmetry**. All 4 rotations produce **unique socket configurations**.

---

## Rotation Algorithm
**Location:** `WFCBuilder.cs:96-121` (function `AddRotated`)

### Clockwise Edge Rotation (90° increments)
```csharp
// Lines 115-119:
byte oldN = rn;
rn = rw;     // North ← West
rw = rs;     // West  ← South
rs = re;     // South ← East
re = oldN;   // East  ← North
```

**Pattern:** Each rotation shifts sockets clockwise around the tile:
- `N` receives value from `W`
- `W` receives value from `S`
- `S` receives value from `E`
- `E` receives value from `N`

---

## Full Pattern Creation Sequence
**Location:** `WFCBuilder.cs:89-138` (function `CreateDungeonMacroTilePatterns`)

The function creates 5 base tile types in this order (with IDs auto-incrementing):
1. **Floor** (line 124): FFFF - Open all sides (4 patterns: IDs 0-3)
2. **Wall** (line 127): WFFF - One closed side (4 patterns: IDs 4-7)
3. **Corridor** (line 130): FWFW - Two opposite closed sides (4 patterns: IDs 8-11)
4. **Corner** (line 133): FFWW - Two adjacent closed sides (4 patterns: IDs 12-15)
5. **Door** (line 136): FWWW - Three closed sides (4 patterns: IDs 16-19)

**Total Patterns:** 20 (5 base types × 4 rotations)

---

## Minimal Test Set Summary
To test **only Corridor, Corner, DeadEnd** with rotation:

| Base Type | Unique Configs | Total Patterns | Pattern IDs (typical) |
|-----------|----------------|----------------|----------------------|
| Corridor  | 2              | 4              | 8-11                 |
| Corner    | 4              | 4              | 12-15                |
| DeadEnd   | 4              | 4              | 16-19                |
| **TOTAL** | **10**         | **12**         | **8-19**             |

---

## How to Limit Generation (Read-Only Note)

**Location:** `WFCBuilder.cs:89-138`

To temporarily test with only Corridor/Corner/DeadEnd:

### Option 1: Comment Out Unwanted Tiles
```csharp
// Lines 124-127: Comment out Floor and Wall
// AddRotated(DungeonPatternType.Floor, (byte)'F', (byte)'F', (byte)'F', (byte)'F', 1.0f);
// AddRotated(DungeonPatternType.Wall, (byte)'W', (byte)'F', (byte)'F', (byte)'F', 1.0f);

// Lines 130-136: Keep these active
AddRotated(DungeonPatternType.Corridor, (byte)'F', (byte)'W', (byte)'F', (byte)'W', 1.0f);
AddRotated(DungeonPatternType.Corner, (byte)'F', (byte)'F', (byte)'W', (byte)'W', 1.0f);
AddRotated(DungeonPatternType.Door, (byte)'F', (byte)'W', (byte)'W', (byte)'W', 0.9f);
```

**Effect:** Pattern IDs would start at 0 instead of 8, yielding 12 total patterns (IDs 0-11).

---

## Compatibility Check Function
**Location:** `WFCBuilder.cs:268-280` (function `PatternsAreCompatible`)

Two patterns are compatible if their **opposing edges match**:
```csharp
case 0: return a.north == b.south;  // Tile A's north touches Tile B's south
case 1: return a.east == b.west;    // Tile A's east touches Tile B's west
case 2: return a.south == b.north;  // Tile A's south touches Tile B's north
case 3: return a.west == b.east;    // Tile A's west touches Tile B's east
```

**Examples:**
- Corridor_0 (`F W F W`) can connect North to any tile with South=`F`
- Corner_0 (`F F W W`) **cannot** connect South to Corridor_0's North (W ≠ F)
- DeadEnd_0 (`F W W W`) can connect North to Corridor_0's South (`F` = `F`)

---

## Socket Summary Table (All 3 Tiles, All Rotations)

| Pattern      | Rot | N | E | S | W | Socket Signature | Unique? |
|--------------|-----|---|---|---|---|------------------|---------|
| Corridor     | 0°  | F | W | F | W | `FWFW`           | Yes     |
| Corridor     | 90° | W | F | W | F | `WFWF`           | Yes     |
| Corridor     | 180°| F | W | F | W | `FWFW`           | No (=0°)|
| Corridor     | 270°| W | F | W | F | `WFWF`           | No (=90°)|
| Corner       | 0°  | F | F | W | W | `FFWW`           | Yes     |
| Corner       | 90° | W | F | F | W | `WFFW`           | Yes     |
| Corner       | 180°| W | W | F | F | `WWFF`           | Yes     |
| Corner       | 270°| F | W | W | F | `FWWF`           | Yes     |
| DeadEnd      | 0°  | F | W | W | W | `FWWW`           | Yes     |
| DeadEnd      | 90° | W | F | W | W | `WFWW`           | Yes     |
| DeadEnd      | 180°| W | W | F | W | `WWFW`           | Yes     |
| DeadEnd      | 270°| W | W | W | F | `WWWF`           | Yes     |

**Total Unique Configurations:** 10 (2 Corridor + 4 Corner + 4 DeadEnd)  
**Total Patterns Generated:** 12 (system creates all rotations regardless of duplicates)

---

## Additional Socket Symbols (Not Used in Dungeons)
**Location:** `WFCBuilder.cs:162-174` (function `CreateDefaultTerrainPatterns`)

These symbols appear in the terrain pattern system but are **separate** from dungeon generation:
- `W` = Water (terrain context, **not** Wall)
- `S` = Sand
- `G` = Grass
- `R` = Rock
- `N` = sNow (using 'N' to avoid conflict)

**Important:** Dungeon patterns use **only `F` and `W`**. Terrain symbols are in a different domain (`PatternDomain.Terrain` vs `PatternDomain.Dungeon`).

---

**End of Audit** — All socket values, rotations, and definitions confirmed from source code.

