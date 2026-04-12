# Procedural Generation Pipeline: 5-Stage Breakdown

SmolbeanPlanet3D's world generation follows a **5-stage sequential pipeline**. This document describes each stage, its entry points, and how to adapt the pipeline for your DOTS project.

## Pipeline Overview

```
┌─────────────────────────────────────────────────────────────┐
│ Stage 1: Height Field Generation                            │
│ (Perlin noise + island falloff + mountain curve)            │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ Stage 2: Tile Constraint Solving                            │
│ (Wave Function Collapse with directional adjacency rules)   │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ Stage 3: Geometry Realization                               │
│ (Instantiate/mesh tiles, apply rotations, set colliders)    │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ Stage 4: Secondary Feature Placement                        │
│ (Trees, rocks, grass patterns, water bodies)                │
└─────────────────────────────────────────────────────────────┘
                           ↓
┌─────────────────────────────────────────────────────────────┐
│ Stage 5: Dynamic Polish & Feedback                          │
│ (Wear textures, vegetation density, LOD setup)              │
└─────────────────────────────────────────────────────────────┘
```

---

## Stage 1: Height Field Generation

**Purpose**: Create a base elevation map that influences terrain tile selection.

**Key Algorithm**: Layered Perlin noise with island curve + mountain boost

### Entry Point
`GameMapCreator.cs` → `GenerateHeightMap()`

### Inputs
```csharp
public struct HeightFieldConfig
{
    public int MapWidth;
    public int MapHeight;
    public int Seed;                          // For reproducible Perlin noise
    
    // Noise Parameters
    public float PrimaryScale;                // Large-scale landform
    public float PrimaryAmplitude;
    public float SecondaryScale;              // Mid-scale detail (hills)
    public float SecondaryAmplitude;
    public float TertiaryScale;               // Fine-scale roughness
    public float TertiaryAmplitude;
    
    // Island Generation
    public float IslandFalloff;               // How quickly edges drop to ocean
    public float FalloffExponent;             // Control falloff curve shape
    
    // Height Remapping
    public float SeaLevel;                    // Threshold for water vs. ground
    public float MountainPeak;                // Stretch range to [SeaLevel, MountainPeak]
    public float MountainBoost;               // Exaggerate high mountains
}
```

### Algorithm Summary

**Layer 1: Large landforms**
```csharp
var noise1 = Mathf.PerlinNoise(x / primaryScale, y / primaryScale) * primaryAmplitude;
```

**Layer 2: Mid-scale features (hills)**
```csharp
var noise2 = Mathf.PerlinNoise(x / secondaryScale, y / secondaryScale) * secondaryAmplitude;
```

**Layer 3: Fine details**
```csharp
var noise3 = Mathf.PerlinNoise(x / tertiaryScale, y / tertiaryScale) * tertiaryAmplitude;
```

**Combine**:
```csharp
var baseHeight = noise1 + noise2 + noise3;
```

**Apply island falloff** (optional; prevents floating continents):
```csharp
var falloffFactor = ComputeIslandFalloff(x, y, mapWidth, mapHeight, falloffExponent);
var falloffHeight = baseHeight * falloffFactor;
```

**Stretch to target range**:
```csharp
var normalizedHeight = (falloffHeight - minHeight) / (maxHeight - minHeight);
var finalHeight = Mathf.Lerp(seaLevel, mountainPeak, normalizedHeight);

// Optional: Mountain boost (exaggerate peaks)
if (finalHeight > seaLevel)
    finalHeight += Mathf.Pow(finalHeight, mountainBoost);

heightMap[y, x] = finalHeight;
```

### Output
```csharp
public struct HeightMap
{
    public float[,] Heights;                  // Raw elevation [0, 1] or [seaLevel, mountainPeak]
    public int MapWidth;
    public int MapHeight;
    public float Seed;
}
```

### Notes
- **Determinism**: Use a seeded Perlin3D implementation (or table-based lookup) instead of `Mathf.PerlinNoise` if targeting strict reproducibility across platforms
- **Performance**: Perlin noise scales linearly with map size; pre-compute for large worlds
- **Parameters**: Common values in SmolbeanPlanet3D:
  - Island falloff = 0.4
  - Primary scale = 100.0 (large continents)
  - Secondary scale = 30.0 (rolling hills)
  - Tertiary scale = 10.0 (roughness detail)

---

## Stage 2: Tile Constraint Solving

**Purpose**: Determine which terrain tile variant occupies each map square, respecting mesh boundary compatibility.

**Key Algorithm**: Wave Function Collapse (unweighted entropy collapse)

### Entry Point
`WaveFunctionCollapse.cs` → `Solve()`

### Inputs
See [wfc-schema.md](wfc-schema.md) for full schema definitions.

```csharp
var config = new WfcRunConfig
{
    MapWidthTiles = mapData.GameMapWidth,
    MapHeightTiles = mapData.GameMapHeight,
    Seed = mapData.Seed,
    Heuristic = CollapseHeuristic.MinimumEntropy,  // Current (could be WeightedEntropy)
    Backtracking = BacktrackMode.Shallow,
    EnforceHeight = true,                          // Consider height deltas
    EnforceSlope = false,                          // Don't enforce slope currently
};
```

### Algorithm Summary (Simplified)

```
1. Initialize: Every tile can be any module (full possibility set)

2. Loop until solved or failed:
   a. Select uncollapsed tile with minimum entropy (fewest possibilities)
   b. Randomly pick one valid possibility
   c. Collapse tile to that module
   d. Propagate constraints to neighbors:
      - North neighbor: remove modules that violate northward constraint
      - East neighbor: remove modules that violate eastward constraint
      - South neighbor: remove modules that violate southward constraint
      - West neighbor: remove modules that violate westward constraint
   e. If any neighbor has 0 possibilities → Backtrack
      (Remove recently placed tile, try next possibility)

3. Return tile grid [y, x] = moduleId
```

###Output
```csharp
public struct WfcResult
{
    public int[] TileModules;                 // Flat array: [y * width + x] = moduleId
    public int[,] TileModules2D;              // 2D view for convenience
    public int Width;
    public int Height;
    public int SolveIterations;               // Diagnostics
    public int BacktrackCount;
    public bool Failed;                       // Did solve fail to complete?
}
```

### Notes
- **Current Limitations**:
  - No weighted entropy (all valid tiles equally likely)
  - Backtracking is shallow (may fail on complex maps)
  - No pre-filtering by height constraints
- **Recommendations for 16bitProcGen**:
  - Add `SpawnWeight` field to WfcModuleDefinition and use weighted collapse
  - Pre-filter module possibilities based on height map
  - Implement breadth-first backtracking for more resilience

---

## Stage 3: Geometry Realization

**Purpose**: Convert tile indices into 3D game objects with meshes, colliders, and navmesh.

### Entry Point
`GridManager.cs` → `CreateGrid()`

### Inputs
- WFC result (tile indices)
- Module definitions (mesh paths, rotations, height offsets)
- Collider/navmesh settings

### Algorithm Summary

```
For each tile (y, x):
  1. Get moduleId from WFC result
  2. Load mesh asset for that module (e.g., "TerrainMeshesAutoGenerated/Mesh_27.asset")
  3. Instantiate GameObject:
     - Parent to grid container
     - Set position: (x * tileSize, heightMap[y, x], y * tileSize)
     - Apply module.MeshRotation (0/90/180/270°)
     - Apply module.HeightAdjustment offset
  4. Add MeshFilter + MeshRenderer (cached materials)
  5. Add MeshCollider (baked for static terrain)
  6. Add to NavMesh baking (if walkable)
```

### Key Parameters

```csharp
public struct GeometryRealizationConfig
{
    public float TileSize;                    // World-space tile dimension (e.g., 10 units)
    public Material[] TerrainMaterials;       // Indexed by module color/variant
    public bool BakeMeshColliders;            // Expensive but required for physics
    public bool BakeNavMesh;                  // Defer to post-realization pass
    public float ColliderHeightOffset;        // Adjust collider to fit mesh
}
```

### Output
- `GridManager.currentGrid` (spatial container)
- Per-tile GameObjects with enabled colliders and renderers
- Baked NavMesh (separate baking pass after all tiles placed)

### Bottlenecks in Original
- **Per-tile GameObject**: 256×256 grid = 65,536 GameObjects (memory + update overhead)
- **Solution for ECS**: Use Entities Graphics + instancing, or chunked meshes

### Notes
- SmolbeanPlanet3D instantiates one GameObject per tile (necessary for MonoBehaviour components)
- For DOTS, consider:
  - **Chunked meshes**: Group 16×16 tiles per chunk mesh (4096 chunks for 256×256 world)
  - **Entities Graphics**: Single draw call per chunk via instancing
  - **Hybrid approach**: Keep some interactive tiles as entities, batch static terrain

---

## Stage 4: Secondary Feature Placement

**Purpose**: Scatter natural features (trees, rocks, water) using noise-based placement with collision detection.

### Entry Points
- `TreeGenerator.cs` → `GenerateTrees()`
- `RockGenerator.cs` → `GenerateRocks()`
- Water bodies: Pre-computed from height map (height < seaLevel)
- Grass patterns: Driven by texture (see Stage 5)

### Algorithm Summary (for Trees)

```
For each tile position:
  1. Sample height map + noise to determine tree probability
  2. If random() < probability AND terrain is walkable:
     a. Raycast down to find ground
     b. Check collision radius (e.g., 2 units) for existing trees
     c. If clear: Instantiate tree prefab
     d. Add to grid spatial hash for fast collision checks
```

### Parameters

```csharp
public struct TreePlacementConfig
{
    public int TargetTreeCount;               // e.g., 5000
    public float TreeDensityScale;            // Modulate base probability
    public float TreeCollisionRadius;         // Avoid overlaps
    public float TreeSpacingMinimum;          // Minimum distance to other trees
    
    public float NoiseHeartbeat;              // Noise frequency for clustering
    public float NoisePersistence;            // Roughness of clusters
    
    public string[] TreePrefabPaths;           // Tree variants (e.g., "Prefabs/Tree4", "Prefabs/Tree5")
}
```

### Output
- Scattered GameObject instances (trees, rocks, grass patches)
- Spatial hash for collision queries (used by animal pathfinding, construction placement)

### Notes
- **Determinism**: Use seeded Xorshift PRNG for placement order
- **Performance**: Raycasts can be expensive; batch or spatial-hash pre-filter
- **Scalability**: In DOTS, use burst-compiled jobs with NativeHashMap for spatial queries

---

## Stage 5: Dynamic Polish & Feedback

**Purpose**: Apply runtime texture updates and visual feedback that evolve with gameplay.

### Entry Points
- `GroundWearManager.cs` → Tracks damage texture feedback
- `GrassInstancer.cs` → Regenerates grass based on ground state
- Day/night cycle (skybox + lighting)
- Wind effects (grass bending, particle direction)

### Ground Wear System

```csharp
public struct GroundWearState
{
    public Texture2D WearTexture;              // Red channel = wear intensity
    public float RegrowthsPerSecond;           // How fast wear fades naturally
    
    // Per-timePeriod:
    public Color32[] Pixels;
    public void ApplyWear(Vector3 position, float radius, float intensity)
    {
        // Paint red channel downward in a circle
        for (int i in pixelsInRadius)
            pixels[i].r -= intensity;
    }
    
    public void UpdateRegrowth(float deltaTime)
    {
        // Fade red channel back to 0 over time
        var regrowth = regrowthsPerSecond * deltaTime * 255;
        for (int i = 0; i < pixels.Length; i++)
            pixels[i].r = Mathf.Clamp(pixels[i].r + regrowth, 0, 255);
    }
}
```

### Grass Instancing

```csharp
public struct GrassInstanceBatch
{
    public Mesh BladeMesh;                    // Single grass blade quad
    public Material BladeShader;              // Instanced material (GPU-driven)
    public uint BladeCount;                   // Up to 1M instances
    
    // Per-blade data (GPU buffers):
    public ComputeBuffer PositionBuffer;      // position + height scale
    public ComputeBuffer NormalBuffer;        // normal + wind influence
}

// Output: ~10 seconds to generate 1M+ grass blades
// Performance: ~1.74ms frame time rendering
```

### Key Concepts
- **Texture-based state**: Wear, damage, and plant health stored in 2D textures (efficient updates)
- **GPU instancing**: Vegetation rendering via compute buffer batching
- **Deterministic procedural**: Grass placement uses noise + raycast, not "follow random list"

---

## Integration Notes for DOTS

### Adapt Stage 1 (Height Fields)
✅ **Pure computation** → Directly port to Burst job
- Replace `Mathf.PerlinNoise` with table-based or seeded Xorshift hash

### Adapt Stage 2 (WFC)
✅ **Constraint-driven** → Ideal for Jobs (NativeArrays + NativeHashMaps)
- Implement WFC solver as Burst job
- Precompute constraint table before solve
- Use NativeQueue for backtrack stack

### Adapt Stage 3 (Geometry)
⚠️ **Entity spawning** → Hybrid approach recommended
- **Static terrain**: Chunked mesh batches + Entities Graphics instancing
- **Interactive objects**: Create entities with transform components
- Keep pivot affordances (collision points, interaction zones) as entity data

### Adapt Stage 4 (Secondary Features)
✅ **Placement logic** → Burst jobs with spatial queries
- Use NativeHashMap for spatial hashing
- Batch raycasts with Physics.RaycastBurst (if available)
- Create entities in burst job output

### Adapt Stage 5 (Polish)
⚠️ **Texture feedback** → Render texture approach works; consider IJobParallelFor for pixel updates
- Wear texture: Keep as RenderTexture for GPU updates if possible
- Grass instancing: Directly compatible with Entities Graphics

---

## Parameters for 16bitProcGen

**Recommended Starting Values** (adjust for your world size):

```csharp
// Stage 1: Height Field
HeightFieldConfig heightConfig = new()
{
    MapWidth = 256,
    MapHeight = 256,
    Seed = 12345,
    PrimaryScale = 100f,
    PrimaryAmplitude = 0.6f,
    SecondaryScale = 30f,
    SecondaryAmplitude = 0.3f,
    TertiaryScale = 10f,
    TertiaryAmplitude = 0.1f,
    IslandFalloff = 0.4f,
    SeaLevel = 0.4f,
    MountainPeak = 1.0f,
};

// Stage 2: WFC
WfcRunConfig wfcConfig = new()
{
    MapWidthTiles = 256,
    MapHeightTiles = 256,
    Seed = 12345,
    Heuristic = CollapseHeuristic.WeightedEntropy,  // Upgrade from uniform
    Backtracking = BacktrackMode.Deep,
    EnforceHeight = true,
    MaxIterations = 500000,
};

// Stage 3: Geometry
GeometryRealizationConfig geoConfig = new()
{
    TileSize = 10f,
    BakeMeshColliders = true,
    BakeNavMesh = true,
};

// Stage 4: Features
TreePlacementConfig treeConfig = new()
{
    TargetTreeCount = 3000,
    TreeDensityScale = 1.2f,
    TreeCollisionRadius = 2f,
};
```

---

## Performance Targets

| Stage | Time (256×256) | Bottleneck | DOTS Target |
|-------|---|---|---|
| 1: Height | ~100ms | Perlin noise layers | <20ms (Burst) |
| 2: WFC | ~500ms–2s | Constraint solving + backtrack | <500ms (parallel jobs) |
| 3: Geometry | ~1.5s | GameObject instantiation | <300ms (batch entity creation) |
| 4: Features | ~500ms | Raycasts + collision checks | <200ms (spatial hash + burst) |
| 5: Polish | <100ms | Texture updates (runtime) | <100ms (compute shader) |

---

**Reference**: [Tech-Analysis-For-16bitProcGen.md](../Tech-Analysis-For-16bitProcGen.md) Section 4 (Procedural Generation Pipeline Breakdown)

**Generated**: April 9, 2026
