# WFC (Wave Function Collapse) System Map

## Overview
DOTS-based Wave Function Collapse implementation for procedural dungeon generation with macro-tile patterns, constraint propagation, and entity-based rendering.

## Components

### Core WFC Components
- **`WFCComponent`**: Main WFC state (grid size, patterns, generation flags, progress)
- **`WFCCell`**: Individual cell data (position, entropy, collapsed state, pattern mask)
- **`WFCPattern`**: Pattern definition (ID, weight, domain, type, edge sockets)
- **`WFCConstraint`**: Pattern compatibility rules (direction, strength)

### Domain-Specific Components
- **`DungeonPattern`**: Dungeon-specific pattern (Floor, Wall, Door, Corridor, Corner)
- **`DungeonElementComponent`**: Element type for spawned instances
- **`DungeonElementInstance`**: Marker for spawned dungeon elements
- **`DungeonGenerationRequest`**: Triggers dungeon generation
- **`DungeonPrefabRegistry`**: Singleton storing baked prefab entities

### Data Structures
- **`WFCPatternData`**: Blob asset for pattern arrays
- **`WFCConstraintData`**: Blob asset for constraint arrays
- **`WFCGenerationSettings`**: Generation parameters (iterations, thresholds)
- **`WFCPerformanceData`**: Performance monitoring metrics

## Systems

### Core Systems
1. **`HybridWFCSystem`**: Main WFC algorithm execution
   - Processes WFC entities and cells
   - Handles initialization, constraint propagation, collapse
   - Integrates with compute shaders (placeholder)

2. **`DungeonRenderingSystem`**: Entity spawning from WFC results
   - Spawns prefab entities for collapsed cells
   - Applies rotation based on neighbor analysis
   - Maps pattern types to prefab entities

3. **`DungeonVisualizationSystem`**: GameObject creation from entities
   - Creates visible GameObjects from dungeon entities
   - Handles prefab instantiation and positioning
   - Manages visualization lifecycle

### Test Systems (Editor Only)
- **`WFCSystemTest`**: Automated WFC testing with 16x16 grid
- **`WFCTestManager`**: MonoBehaviour coordinator for tests
- **`WFCTestEvents`**: Event system for test communication

### Authoring Systems
- **`DungeonPrefabRegistryAuthoring`**: MonoBehaviour for prefab assignment
- **`DungeonPrefabRegistryBaker`**: Converts GameObjects to entity prefabs
- **`DungeonElementAuthoring`**: Individual element configuration

## Data Flow: Init → Solve → Propagate → Render

### 1. INIT
```
DungeonManager.RequestDungeonGeneration()
  ↓
Creates DungeonGenerationRequest entity
  ↓
HybridWFCSystem.InitializeWFCData()
  ↓
WFCBuilder.CreateDungeonMacroTilePatterns()
  ↓
Creates 20 patterns (5 types × 4 rotations each)
```

### 2. SOLVE
```
HybridWFCSystem.ProcessWFCCells()
  ↓
WFCCell initialization with all patterns possible
  ↓
Entropy calculation based on possible patterns
  ↓
Random collapse when entropy ≤ 3
```

### 3. PROPAGATE
```
HybridWFCSystem.PruneWithCollapsedNeighbors()
  ↓
For each collapsed neighbor:
  - Check edge compatibility (north/east/south/west)
  - Remove incompatible patterns from cell mask
  - Update entropy based on remaining patterns
  ↓
WFCBuilder.PatternsAreCompatible() edge matching
```

### 4. RENDER
```
DungeonRenderingSystem.SpawnDungeonElement()
  ↓
Map pattern type to prefab entity
  ↓
Apply rotation based on pattern edges
  ↓
DungeonVisualizationSystem.CreateVisualization()
  ↓
Instantiate GameObjects from prefabs
```

## Rotation & Sockets

### Socket Definition
- **Edge Types**: `byte north, east, south, west` ('F'=Floor, 'W'=Wall)
- **Pattern Creation**: `WFCBuilder.CreateDungeonMacroTilePatterns()`
- **Rotation Logic**: Each base pattern generates 4 variants (0°, 90°, 180°, 270°)

### Socket Application
- **Constraint Propagation**: `WFCBuilder.PatternsAreCompatible()` matches opposite edges
- **Rendering Rotation**: 
  - Walls: `DetermineWallRotation()` based on neighbor analysis
  - Corridors: `DetermineCorridorRotation()` based on open edges
  - Corners: `DetermineCornerRotation()` maps edge combinations to angles

### Rotation Examples
```csharp
// Floor: All edges open 'F' → No rotation needed
AddRotated(DungeonPatternType.Floor, 'F', 'F', 'F', 'F', 1.0f);

// Wall: One edge closed 'W' → 4 rotations
AddRotated(DungeonPatternType.Wall, 'W', 'F', 'F', 'F', 1.0f);

// Corridor: Two opposite edges open → N/S or E/W alignment
AddRotated(DungeonPatternType.Corridor, 'F', 'W', 'F', 'W', 1.0f);
```

## File Structure
```
Scripts/DOTS/WFC/
├── WFCComponent.cs          # Core components & data structures
├── HybridWFCSystem.cs       # Main WFC algorithm
├── WFCBuilder.cs            # Pattern creation & utilities
├── DungeonTypes.cs          # Domain-specific types
├── DungeonRenderingSystem.cs # Entity spawning
├── DungeonVisualizationSystem.cs # GameObject creation
├── DungeonManager.cs        # MonoBehaviour controller
├── WFCTestManager.cs        # Test coordination
└── WFCTestEvents.cs         # Test event system

Scripts/Authoring/
├── DungeonElementAuthoring.cs      # Element configuration
└── DungeonPrefabRegistryAuthoring.cs # Prefab registry

Scripts/DOTS/Test/
└── WFCSystemTest.cs         # Automated testing

Resources/Shaders/
└── WFCGeneration.compute    # Compute shader (placeholder)
```

## Key Features
- **Macro-tile Patterns**: 5 dungeon types with 4 rotations each (20 total)
- **Constraint Propagation**: Edge-based compatibility checking
- **Entity-based Rendering**: DOTS entities → GameObjects pipeline
- **Test Framework**: Automated 16x16 grid testing with completion tracking
- **Performance Monitoring**: Iteration counting, entropy tracking, timing
- **Debug Controls**: Runtime toggles for test systems and logging
