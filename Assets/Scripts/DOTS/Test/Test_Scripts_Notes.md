# Test Scripts Editable Settings

The test scripts provide several key editable settings that control terrain generation and testing:

## 1. **AutoTestSetup.cs** - Basic Test Environment
```csharp
[Header("Auto Setup Settings")]
public bool setupOnStart = true;           // Automatically setup on Start()
public bool createTestEntities = true;     // Whether to create test entities
public int numberOfTestEntities = 5;       // How many entities to create
```

## 2. **QuickTerrainEntityCreator.cs** - Entity Creation Control
```csharp
[Header("Creation Settings")]
public bool createOnStart = true;          // Create entities on Start()
public int numberOfEntities = 5;           // Number of terrain entities to create
public int resolution = 32;                // Grid resolution (32x32, 64x64, etc.)
public float worldScale = 10f;             // World scale factor
```

## 3. **HybridTestSetup.cs** - Advanced Test Environment
```csharp
[Header("Setup Settings")]
public bool setupOnStart = true;           // Auto-setup on Start()
public bool logSetupProcess = true;        // Verbose logging
public bool enableDebugLogs = false;       // Debug toggle
```

## 4. **WeatherTestSetup.cs** - Weather Testing
```csharp
[Header("Weather Test Settings")]
public bool runOnStart = true;             // Auto-run on Start()
public bool enableDebugLogs = true;        // Weather debug logs
public bool showDebugGUI = true;           // Show weather GUI
public bool monitorTerrainChanges = true;  // Monitor height changes

[Header("Weather Configuration")]
public WeatherType initialWeather = WeatherType.Clear;
public float weatherChangeInterval = 10f;  // Auto weather change interval
public bool autoChangeWeather = true;      // Auto weather cycling

[Header("Weather Parameters")]
public float temperature = 20f;            // Weather temperature
public float humidity = 0.5f;              // Weather humidity
public float windSpeed = 5f;               // Wind speed
public float weatherIntensity = 0.8f;      // Weather effect intensity
```

# How World Scale and Number of Entities Work

## **World Scale (`worldScale`)**
- **Purpose**: Controls the physical size of each terrain chunk in world units
- **Default**: 10f (10 world units per chunk)
- **Effect**: 
  - `worldScale = 1.0f` → Each chunk is 1x1 world units
  - `worldScale = 10.0f` → Each chunk is 10x10 world units
  - `worldScale = 100.0f` → Each chunk is 100x100 world units
- **Usage**: Passed to `TerrainDataBuilder.CreateTerrainData()` to set the chunk's world scale

## **Number of Entities (`numberOfEntities`)**
- **Purpose**: Controls how many terrain chunks are created for testing
- **Default**: 5 entities
- **Grid Layout**: Entities are arranged in a square grid pattern
  - 5 entities → 3x3 grid (with 2 empty slots)
  - 9 entities → 3x3 grid (full)
  - 16 entities → 4x4 grid
- **Formula**: `gridSize = Mathf.CeilToInt(Mathf.Sqrt(numberOfEntities))`

## **Resolution (`resolution`)**
- **Purpose**: Controls the detail level of each terrain chunk
- **Default**: 32 (32x32 height grid)
- **Options**: 16, 32, 64, 128, 256 (higher = more detail but slower)
- **Memory Impact**: Resolution² × 4 bytes (float) per chunk

# Entity Creation Process

## 1. **Grid Positioning**: Entities are placed in a grid pattern:
```csharp
int gridSize = Mathf.CeilToInt(Mathf.Sqrt(numberOfEntities));
for (int i = 0; i < numberOfEntities; i++)
{
    int x = i % gridSize;  // Column position
    int z = i / gridSize;  // Row position
    var chunkPosition = new int2(x, z);
}
```

## 2. **Entity Creation**: Each entity gets:
- `TerrainData` component with position, resolution, and world scale
- `BiomeComponent` with biome type (default: Plains)
- Marked with `needsGeneration = true` for processing

## 3. **Processing**: The `HybridTerrainGenerationSystem` processes entities marked for generation

# Practical Usage Examples

## **Small Test World**:
```csharp
numberOfEntities = 4;    // 2x2 grid
resolution = 32;         // Medium detail
worldScale = 5f;         // 5x5 world units per chunk
// Result: 10x10 world units total
```

## **Large Test World**:
```csharp
numberOfEntities = 16;   // 4x4 grid
resolution = 64;         // High detail
worldScale = 20f;        // 20x20 world units per chunk
// Result: 80x80 world units total
```

## **Performance Test**:
```csharp
numberOfEntities = 25;   // 5x5 grid
resolution = 128;        // Very high detail
worldScale = 50f;        // 50x50 world units per chunk
// Result: 250x250 world units total (stress test)
```

These settings allow you to easily scale your testing from small performance tests to large world generation tests by adjusting these key parameters.