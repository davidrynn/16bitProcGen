# WFC Schema: Implementable Data Structures

This document defines concrete C# data structures for implementing Wave Function Collapse in your DOTS project. These schemas are derived from SmolbeanPlanet3D's working WFC system and adapted for ECS/DOTS architecture.

## Overview

SmolbeanPlanet3D uses a **tile-based constraint solver** where:
- Each map square can be one of several **tile types** (terrain variants)
- Tiles have **directional sockets** (edges labeled with compatibility info)
- The solver uses **adjacency constraints** to collapse possibilities
- **Backtracking** resolves dead ends

This schema enables that solver to run as **pure data + deterministic jobs** in DOTS.

---

## Core Schemas

### 1. WfcModuleDefinition

Represents a single tile variant (e.g., "flat_grass", "cliff_north").

```csharp
public struct WfcModuleDefinition
{
    public int Id;                        // Unique identifier (0-based)
    public FixedString64Bytes Name;       // e.g., "TerrainFlat_GrassGreen"
    
    // Mesh/Visual Reference
    public float MeshRotation;            // Rotation in degrees (0, 90, 180, 270)
    public float HeightAdjustment;        // Vertical offset for mesh placement
    
    // Constraint Metadata
    public int SocketCount;               // Usually 4 (N, E, S, W) for grids
    public int FirstSocketIndex;          // Offset into flattened socket array
    
    // Probability Weighting (optional, for weighted entropy)
    public float SpawnWeight;             // Higher = more likely to spawn (default 1.0)
    public float BiomePriority;           // Biome-specific weight modifier
    
    // Flags
    public bool IsWalkable;               // Can place units here
    public bool AllowsNature;             // Can spawn trees/rocks on top
    public bool HasCliffFace;             // Visual indicator for edge rendering
}
```

**Example Instances:**
- `{ Id=0, Name="Grass_Flat", SocketCount=4, IsWalkable=true, AllowsNature=true, SpawnWeight=2.0 }`
- `{ Id=1, Name="Cliff_North", SocketCount=4, IsWalkable=false, AllowsNature=false, HasCliffFace=true }`
- `{ Id=2, Name="Water_Deep", SocketCount=4, IsWalkable=false, AllowsNature=false, SpawnWeight=0.5 }`

---

### 2. WfcSocketDefinition

Describes the edge compatibility metadata for one tile direction.

```csharp
public struct WfcSocketDefinition
{
    public int DirectionId;               // 0=North, 1=East, 2=South, 3=West (4-tile cardinal grid)
    
    // Socket Identity (used for matching)
    public uint SocketHash;               // Compact representation of edge properties
                                          // e.g., hash of ("flat_edge", "ground_level", "walkable")
    
    // Semantic Tags (for filtering/debugging)
    public FixedString32Bytes SocketType; // e.g., "FlatGround", "Cliff", "Water"
    
    // Optional: Height/Slope Metadata
    public float EdgeHeight;              // Height of this edge (for smooth transitions)
    public float EdgeSlope;               // Slope for cliff/ramp detection
}
```

**Example Instances:**
```
ModuleId=0 (GrassFlat):
  Socket[0] { DirectionId=0(N), SocketType="FlatGround", EdgeHeight=0.0, EdgeSlope=0.0 }
  Socket[1] { DirectionId=1(E), SocketType="FlatGround", EdgeHeight=0.0, EdgeSlope=0.0 }
  Socket[2] { DirectionId=2(S), SocketType="FlatGround", EdgeHeight=0.0, EdgeSlope=0.0 }
  Socket[3] { DirectionId=3(W), SocketType="FlatGround", EdgeHeight=0.0, EdgeSlope=0.0 }

ModuleId=1 (CliffNorth):
  Socket[0] { DirectionId=0(N), SocketType="CliffFace",    EdgeHeight=1.0, EdgeSlope=90.0 }
  Socket[1] { DirectionId=1(E), SocketType="FlatGround",   EdgeHeight=0.0, EdgeSlope=0.0 }
  Socket[2] { DirectionId=2(S), SocketType="FlatGround",   EdgeHeight=0.0, EdgeSlope=0.0 }
  Socket[3] { DirectionId=3(W), SocketType="FlatGround",   EdgeHeight=0.0, EdgeSlope=0.0 }
```

---

### 3. WfcConstraintRule

Defines which tiles can be adjacent in which directions.

```csharp
public struct WfcConstraintRule
{
    // Identity
    public int ModuleIdA;                 // Source tile
    public int DirectionId;               // Direction from A (0=N, 1=E, 2=S, 3=W)
    public int ModuleIdB;                 // Target tile that can neighbor A in that direction
    
    // Constraint Type
    public enum ConstraintType { SocketMatch, HeightRange, SlopeRange, Custom }
    public ConstraintType Type;
    
    // Constraint Parameters (union-like; use based on Type)
    public uint SocketHashRequired;       // For SocketMatch: required hash
    public float MinHeightDelta;          // For HeightRange: acceptable height difference range
    public float MaxHeightDelta;
    public float MaxSlopeDelta;           // For SlopeRange: max slope difference
    
    // Priority / Weight
    public float PreferenceWeight;        // Higher = collapse chooses this first (if valid)
}
```

**Example Rules:**
```
// Flat ground can neighbor flat ground on any side
{ ModuleIdA=0, Direction=N, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=0xABCD1234 }
{ ModuleIdA=0, Direction=E, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=0xABCD1234 }
{ ModuleIdA=0, Direction=S, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=0xABCD1234 }
{ ModuleIdA=0, Direction=W, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=0xABCD1234 }

// Cliff North can have flat ground on all sides except north
{ ModuleIdA=1, Direction=E, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=... }
{ ModuleIdA=1, Direction=S, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=... }
{ ModuleIdA=1, Direction=W, ModuleIdB=0, Type=SocketMatch, SocketHashRequired=... }

// Flat ground cannot have CliffFace north
// (implicit: if no rule exists, adjacency is forbidden)
```

---

### 4. WfcRunConfig

Configuration for a single WFC solve execution.

```csharp
public struct WfcRunConfig
{
    // World Parameters
    public int MapWidthTiles;
    public int MapHeightTiles;
    public int Seed;                      // For reproducible Random
    
    // Solve Strategy
    public enum CollapseHeuristic { UniformRandom, MinimumEntropy, WeightedEntropy }
    public CollapseHeuristic Heuristic;
    
    public enum BacktrackMode { None, Shallow, Deep }
    public BacktrackMode Backtracking;    // How aggressively to undo failed assignments
    
    // Constraints
    public bool EnforceHeight;            // Validate height deltas between neighbors
    public bool EnforceSlope;             // Validate slope deltas
    public bool AllowIslands;             // Allow disconnected landmasses
    
    // Output Behavior
    public bool LogDiagnostics;           // Write solve steps to debug log
    public int MaxIterations;             // Prevent infinite loops (fallback limit)
    public int MaxBacktracks;             // Backtrack failure threshold
}
```

**Example Configuration:**
```csharp
var config = new WfcRunConfig
{
    MapWidthTiles = 256,
    MapHeightTiles = 256,
    Seed = 42,
    Heuristic = CollapseHeuristic.WeightedEntropy,  // Use module weights
    Backtracking = BacktrackMode.Deep,
    EnforceHeight = true,
    EnforceSlope = true,
    AllowIslands = false,
    LogDiagnostics = false,
    MaxIterations = 500000,
    MaxBacktracks = 1000
};
```

---

### 5. WfcConstraintTable (Runtime Acceleration)

Precomputed lookup for fast adjacency checks.

```csharp
public struct WfcConstraintTable
{
    // Flat array: [moduleA * (NumModules * 4) + direction * NumModules + moduleB]
    // Contains: 0 if forbidden, 1 if allowed, >1 if weight
    public NativeArray<byte> ValidAdjacency;
    
    // Metadata
    public int ModuleCount;
    public int DirectionCount;            // Usually 4
}
```

**Purpose**: During collapse, instead of scanning WfcConstraintRule array each time, use:
```csharp
var index = (moduleA * (count * 4)) + (direction * count) + moduleB;
var allowed = table.ValidAdjacency[index] > 0;
```

---

## Asset Authoring Workflow

### Stage 1: Extraction (Pre-Solve)

Create the module/socket definitions from your mesh library:

1. **Enumerate mesh variants** (e.g., 28 terrain meshes in SmolbeanPlanet3D)
2. **Assign socket types** manually or via mesh boundary analysis tool
   - SmolbeanPlanet3D uses `TerrainDataEditor` + `BoundariesMatch()` to derive edge compatibility
3. **Generate WfcModuleDefinition array** (0-based)
4. **Generate WfcSocketDefinition array** (flattened: all sockets for all modules)

**Pseudocode:**
```csharp
var modules = new List<WfcModuleDefinition>();
var sockets = new List<WfcSocketDefinition>();

foreach (var mesh in terrainMeshes)
{
    var module = new WfcModuleDefinition { Id = modules.Count, Name = mesh.name };
    module.FirstSocketIndex = sockets.Count;
    modules.Add(module);
    
    // Derive 4 sockets from mesh boundaries
    for (int dir = 0; dir < 4; dir++)
    {
        var socket = ComputeSocketFromMesh(mesh, dir);
        sockets.Add(socket);
    }
}

config.ModuleDefinitions = modules.ToArray();
config.SocketDefinitions = sockets.ToArray();
```

### Stage 2: Constraint Building

Determine which tiles can neighbor which:

**Option A: Heuristic-based** (fast, less control)
```csharp
for (int a = 0; a < moduleCount; a++)
{
    for (int dir = 0; dir < 4; dir++)
    {
        for (int b = 0; b < moduleCount; b++)
        {
            if (SocketsCompatible(modules[a], dir, modules[b], (dir + 2) % 4))
            {
                AddConstraintRule(a, dir, b, ...);
            }
        }
    }
}
```

**Option B: Editor authoring** (more control, manual work)
- Create a Scriptable Asset with constraint list
- TerrainDataEditor UI to toggle valid adjacencies
- Export to constraint rule array

### Stage 3: Precomputation

Build the constraint table for runtime lookups:

```csharp
var table = new WfcConstraintTable { ModuleCount = modules.Length };
table.ValidAdjacency = new NativeArray<byte>(modules.Length * 4 * modules.Length);

// Fill table from constraint rules
foreach (var rule in config.ConstraintRules)
{
    var index = (rule.ModuleIdA * (count * 4)) + (rule.DirectionId * count) + rule.ModuleIdB;
    table.ValidAdjacency[index] = 1; // or weight value for weighted solve
}
```

---

## Integration with DOTS

### Run WFC as a Job

```csharp
var config = new WfcRunConfig { /* ... */ };
var modules = new NativeArray<WfcModuleDefinition>(...);
var sockets = new NativeArray<WfcSocketDefinition>(...);
var constraints = new WfcConstraintTable { /* ... */ };

var job = new WaveFunctionCollapseJob
{
    Config = config,
    Modules = modules,
    Constraints = constraints,
    OutputTiles = new NativeArray<int>(config.MapWidthTiles * config.MapHeightTiles, Allocator.TempJob),
};

job.Schedule().Complete();

// Read results from job.OutputTiles[y * width + x]
```

### Determinism

Use a **seeded Xorshift128** PRNG (not Unity.Random.Range) to ensure reproducibility:

```csharp
public struct Xorshift128
{
    private uint x, y, z, w;
    
    public Xorshift128(uint seed)
    {
        x = seed; y = 1; z = 2; w = 3;
    }
    
    public int Range(int min, int max)
    {
        // ... true xorshift implementation
    }
}

// In WFC job:
var prng = new Xorshift128((uint)config.Seed);
// Use prng.Range() instead of Random.Range()
```

---

## Reference Implementation Notes

**Source Files (SmolbeanPlanet3D):**
- [WaveFunctionCollapse.cs](../../Assets/Scripts/Procedural/WaveFunctionCollapse.cs) — Core solver algorithm
- [NeighbourData.cs](../../Assets/Scripts/Procedural/NeighbourData.cs) — Constraint metadata (adjacency rules)
- [Edge.cs](../../Assets/Scripts/Procedural/Edge.cs) — Socket representation (edge matching)
- [TerrainDataEditor.cs](../../Assets/Editor/TerrainDataEditor.cs) — Authoring tool for socket generation

**Key Limitations to Address:**
1. **No weighted entropy** in original — add by tracking min entropy weight in collapse step
2. **Unweighted random selection** — switch to `Random.value < cumulative[i] / sum`
3. **Global Unity.Random** — replace with deterministic Xorshift128 per-job
4. **Limited backtracking** — consider breadth-first state queue if performance allows

---

**Generated**: April 9, 2026 | **Source**: SmolbeanPlanet3D WFC system analysis
