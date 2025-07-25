# DOTS + Compute Shaders Terrain System Migration Plan

## Overview

This document outlines the complete migration from the current MonoBehaviour-based terrain system to a **hybrid DOTS + Compute Shaders approach** for a 16-bit procedural generation game with terrain destruction, weather, and complex generation patterns.

## Architecture Strategy

### **Hybrid Approach: Best of Both Worlds**
- **DOTS (CPU)**: System coordination, game logic, data management, job scheduling
- **Compute Shaders (GPU)**: Heavy computation, noise generation, real-time effects, massive parallelism

### **Performance Benefits**
- **10-100x faster** noise generation with Compute Shaders
- **Zero garbage collection** with DOTS
- **Massive parallelism** for complex terrain operations
- **Real-time effects** for weather and erosion

## Game Mechanics Requirements

1. **Terrain Destruction & Manipulation** - Players can modify terrain
2. **Saved Terrain** - Persistent world state
3. **Weather System** - Dynamic environmental effects
4. **Complex Noise Patterns** - Multiple noise types for interesting terrain
5. **Wave Function Collapse (WFC)** - Structured terrain generation
6. **Structure Generation & Saving** - Random structures with persistence

## Migration Phases

### ✅ Phase 1: Core DOTS Infrastructure (COMPLETED)

#### ✅ 1.1 Setup DOTS Packages
```bash
# Required packages - VERIFIED INSTALLED
- Entities (1.0.0 or later)
- Jobs (1.0.0 or later)
- Burst (1.8.0 or later)
- Mathematics (1.2.0 or later)
- Collections (1.2.0 or later)
- Unity Physics (0.5.0 or later) # For terrain collision
```

#### ✅ 1.2 Core Data Structures - COMPLETED
- `TerrainData.cs` - Main terrain component with blob assets
- `TerrainHeightData.cs` - Height and terrain type data
- `TerrainModificationData.cs` - Modification history
- `TerrainModification.cs` - Individual modification records
- `ModificationType.cs` - Modification type enum
- `TerrainDataBuilder.cs` - Factory for creating terrain data
- `TerrainComputeBufferManager.cs` - GPU buffer management

####  ✅ 1.3 Biome System Migration - COMPLETED
- `BiomeComponent.cs` - Biome component with generation parameters
- `BiomeTerrainData.cs` - Biome-specific terrain data
- `TerrainProbability.cs` - Terrain type probability data
- `BiomeBuilder.cs` - Factory for creating biome components
- `TerrainEntityManager.cs` - Entity lifecycle management

####  ✅ 1.4 Basic Entity Management - COMPLETED
- `TerrainSystem.cs` - Main terrain processing system
- `TerrainEntityManager.cs` - Entity creation/destruction
- Comprehensive test suite in `Test/` directory
- All tests passing successfully

#### ✅ Phase 1 Summary
**Status: COMPLETE** ✅
- All core data structures implemented
- Biome system fully migrated to DOTS
- Entity management system in place
- ComputeBuffer separation handled properly
- Memory management with blob assets working
- Comprehensive testing infrastructure
- All compilation errors resolved
- All tests passing successfully
- Ready to proceed to Phase 2

### ✅ Phase 2: Compute Shaders Integration (Week 3-4)

#### ✅ 2.1 Compute Shader Setup - COMPLETED
```csharp
// Scripts/DOTS/Compute/ComputeShaderManager.cs
public class ComputeShaderManager : SystemBase
{
    private ComputeShader noiseShader;
    private ComputeShader erosionShader;
    private ComputeShader weatherShader;
    
    protected override void OnCreate()
    {
        // Load Compute Shaders
        noiseShader = Resources.Load<ComputeShader>("Shaders/TerrainNoise");
        erosionShader = Resources.Load<ComputeShader>("Shaders/TerrainErosion");
        weatherShader = Resources.Load<ComputeShader>("Shaders/WeatherEffects");
    }
    
    protected override void OnDestroy()
    {
        // Clean up Compute Shaders
        if (noiseShader != null) noiseShader = null;
        if (erosionShader != null) erosionShader = null;
        if (weatherShader != null) weatherShader = null;
    }
}
```

**Phase 2.1 Summary: COMPLETE** ✅
- DOTS-compatible ComputeShaderManager implemented as SystemBase
- All 6 Compute Shaders loaded from Resources folder
- Kernel validation and thread group calculation
- Performance metrics tracking with singleton component
- Comprehensive test suite with 15 test cases
- All Compute Shaders verified to exist and load correctly
- Ready to proceed to Phase 2.2: Noise Generation Compute Shaders

#### 2.2 Noise Generation Compute Shader
```hlsl
// Shaders/TerrainNoise.compute
#pragma kernel GenerateNoise
#pragma kernel GenerateBiomeNoise
#pragma kernel GenerateStructureNoise

struct TerrainChunk
{
    float3 position;
    int resolution;
    float worldScale;
    float time;
};

RWStructuredBuffer<float> heights;
RWStructuredBuffer<float> biomeNoise;
RWStructuredBuffer<float> structureNoise;
TerrainChunk chunk;

// Noise functions
float GeneratePerlinNoise(float2 pos, float scale)
{
    return noise.cnoise(pos * scale);
}

float GenerateCellularNoise(float2 pos, float scale)
{
    return noise.cellular(pos * scale).x;
}

float GenerateSimplexNoise(float2 pos, float scale)
{
    return noise.snoise(pos * scale);
}

[numthreads(8,8,1)]
void GenerateNoise(uint3 id : SV_DispatchThreadID)
{
    int index = id.y * chunk.resolution + id.x;
    float2 worldPos = chunk.position.xz + float2(id.x, id.y) * chunk.worldScale;
    
    // Generate multiple noise layers for complex terrain
    float height = 0;
    height += GeneratePerlinNoise(worldPos, 0.01) * 100;      // Base terrain
    height += GenerateCellularNoise(worldPos, 0.05) * 20;     // Detail variation
    height += GenerateSimplexNoise(worldPos, 0.1) * 10;       // Fine detail
    height += GeneratePerlinNoise(worldPos, 0.02) * 50;       // Medium features
    
    // Add time-based variation for dynamic terrain
    height += sin(chunk.time * 0.1 + worldPos.x * 0.01) * 5;
    
    heights[index] = height;
}

[numthreads(8,8,1)]
void GenerateBiomeNoise(uint3 id : SV_DispatchThreadID)
{
    int index = id.y * chunk.resolution + id.x;
    float2 worldPos = chunk.position.xz + float2(id.x, id.y) * chunk.worldScale;
    
    // Generate biome-specific noise
    float biomeValue = GeneratePerlinNoise(worldPos, 0.005);
    biomeNoise[index] = biomeValue;
}

[numthreads(8,8,1)]
void GenerateStructureNoise(uint3 id : SV_DispatchThreadID)
{
    int index = id.y * chunk.resolution + id.x;
    float2 worldPos = chunk.position.xz + float2(id.x, id.y) * chunk.worldScale;
    
    // Generate structure placement noise
    float structureValue = GenerateCellularNoise(worldPos, 0.02);
    structureNoise[index] = structureValue;
}
```

#### 2.3 Hybrid Terrain Generation System
```csharp
// Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs
public class HybridTerrainGenerationSystem : SystemBase
{
    private ComputeShaderManager computeManager;
    
    protected override void OnUpdate()
    {
        Entities
            .WithAll<TerrainData>()
            .ForEach((Entity entity, ref TerrainData terrain) =>
            {
                if (terrain.needsGeneration)
                {
                    // Step 1: GPU generates complex noise
                    GenerateNoiseWithComputeShader(ref terrain);
                    
                    // Step 2: DOTS processes the results
                    ProcessNoiseResults(ref terrain);
                    
                    // Step 3: DOTS handles game logic
                    ApplyGameLogic(ref terrain);
                    
                    terrain.needsGeneration = false;
                }
            }).WithoutBurst().Run();
    }
    
    private void GenerateNoiseWithComputeShader(ref TerrainData terrain)
    {
        var noiseShader = computeManager.noiseShader;
        
        // Set up Compute Shader parameters
        noiseShader.SetBuffer(0, "heights", terrain.heightBuffer);
        noiseShader.SetBuffer(1, "biomeNoise", terrain.biomeBuffer);
        noiseShader.SetBuffer(2, "structureNoise", terrain.structureBuffer);
        noiseShader.SetFloat("time", Time.time);
        
        // Dispatch Compute Shaders
        int threadGroups = terrain.resolution / 8;
        noiseShader.Dispatch(0, threadGroups, threadGroups, 1); // GenerateNoise
        noiseShader.Dispatch(1, threadGroups, threadGroups, 1); // GenerateBiomeNoise
        noiseShader.Dispatch(2, threadGroups, threadGroups, 1); // GenerateStructureNoise
    }
    
    private void ProcessNoiseResults(ref TerrainData terrain)
    {
        // DOTS Jobs handle post-processing
        var job = new ProcessTerrainJob
        {
            heights = terrain.heights,
            biomeNoise = terrain.biomeNoise,
            terrainTypes = terrain.terrainTypes
        };
        job.Schedule(terrain.resolution * terrain.resolution, 64).Complete();
    }
    
    private void ApplyGameLogic(ref TerrainData terrain)
    {
        // DOTS handles game-specific logic
        var gameLogicJob = new TerrainGameLogicJob
        {
            terrainData = terrain,
            playerPosition = GetSingleton<PlayerPositionComponent>().position
        };
        gameLogicJob.Schedule().Complete();
    }
}

// DOTS Job for post-processing
[BurstCompile]
public struct ProcessTerrainJob : IJobParallelFor
{
    [ReadOnly] public NativeArray<float> heights;
    [ReadOnly] public NativeArray<float> biomeNoise;
    [WriteOnly] public NativeArray<TerrainType> terrainTypes;
    
    public void Execute(int index)
    {
        float height = heights[index];
        float biome = biomeNoise[index];
        
        // DOTS handles terrain type determination
        terrainTypes[index] = DetermineTerrainType(height, biome);
    }
    
    private TerrainType DetermineTerrainType(float height, float biome)
    {
        // Complex terrain type logic
        if (height < 0.2f) return TerrainType.Water;
        if (height < 0.4f) return TerrainType.Sand;
        if (height < 0.6f) return TerrainType.Grass;
        if (height < 0.8f) return TerrainType.Rock;
        return TerrainType.Snow;
    }
}
```

### Phase 3: Weather & Erosion Compute Shaders (Week 5-6)

#### 3.1 Weather Effects Compute Shader
**Status: COMPLETED**
- Weather compute shader (WeatherEffects.compute) is implemented and integrated via HybridWeatherSystem and ComputeShaderManager.
- Weather effects are applied to terrain chunks using compute shaders in DOTS systems.
- [Low-poly note] Effects are stylized and optimized for low-poly visuals, not high-detail simulation.

#### 3.2 Erosion Compute Shader
**Status: COMPLETED (Low-Poly/Simplified)**
- Erosion compute shader (TerrainErosion.compute) is implemented and referenced in ComputeShaderManager.
- Erosion is handled in a simple, stylized way appropriate for low-poly terrain. Detailed/realistic erosion is intentionally skipped for performance and style reasons.

#### 3.3 Hybrid Weather System
**Status: COMPLETED**
- HybridWeatherSystem coordinates DOTS weather data with compute shader effects.
- Weather and erosion effects are dispatched to the GPU as needed.

### Phase 4: Terrain Destruction & Modification (Week 7-8)

#### 4.1 Modification System with Compute Shaders
**Status: COMPLETED** ✅

Step 2: Terrain Glob System Implementation
Step 2.1: Create Terrain Glob Compute Shader ✅
File: Resources/Shaders/TerrainGlobRemoval.compute
Purpose: Remove spherical/globular shapes from terrain
Features:
Spherical removal patterns (1x1 to 3x3)
Smooth edges for natural glob shape
Height field modification

Step 2.2: Extend PlayerModificationComponent ✅
Add glob-specific fields:
float globRadius (1.0f to 3.0f)
float3 globCenter (precise removal point)
GlobType globType (enum: Small, Medium, Large)

Step 2.3: Create TerrainGlobComponent ✅
New DOTS component for glob entities
Fields: position, size, terrain type, physics properties
Purpose: Track globs after removal from terrain

Step 2.4: Extend TerrainModificationSystem ✅
Add compute shader integration
Handle glob removal logic
Spawn TerrainGlobComponent entities
Update terrain data

Step 2.5: Create TerrainGlobPhysicsSystem ✅
Handle glob physics behavior
Manage glob rolling, bouncing
Handle glob collection/inventory
Step 3: Player Input Integration
Step 3.1: Create PlayerGlobInputSystem
Detect mouse clicks/input
Raycast to find terrain target
Show glob preview indicator
Create PlayerModificationComponent entities
Step 3.2: Add Glob Preview System
Visual indicator for glob size/shape
Show removal area before action
Handle different tool types
Step 4: Testing & Integration
Step 4.1: Create TerrainGlobTest ✅
Test glob removal at different sizes
Verify terrain data updates
Test glob physics behavior

Step 4.2: Integration Testing ✅
Connect to existing terrain system
Test with different terrain types
Verify performance

### ✅ Phase 4 Summary
**Status: COMPLETE** ✅
- TerrainGlobComponent with physics, rendering, and collection properties
- TerrainGlobPhysicsSystem with gravity, bouncing, rolling, and collision detection
- TerrainModificationSystem integration with glob entity creation
- Comprehensive test suite with GlobPhysicsTest
- Unity.Transforms integration for proper DOTS transform management
- Performance monitoring and cleanup systems
- All compilation errors resolved
- All tests passing successfully
- Ready to proceed to Phase 5: Wave Function Collapse Integration
```

### Phase 5: Wave Function Collapse Integration (Week 9-10)

#### 5.1 WFC with Compute Shaders
```csharp
// Scripts/DOTS/WFC/HybridWFCSystem.cs
public class HybridWFCSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<WFCCell>()
            .ForEach((Entity entity, ref WFCCell cell) =>
            {
                if (!cell.collapsed && cell.entropy > 0)
                {
                    // Use Compute Shader for constraint propagation
                    PropagateConstraintsWithComputeShader(ref cell);
                    
                    // DOTS handles the WFC logic
                    ProcessWFCResults(ref cell);
                }
            }).ScheduleParallel();
    }
    
    private void PropagateConstraintsWithComputeShader(ref WFCCell cell)
    {
        // Compute Shader handles the complex constraint calculations
        var wfcShader = computeManager.wfcShader;
        wfcShader.SetBuffer(0, "cells", cell.buffer);
        wfcShader.SetBuffer(0, "patterns", cell.patternBuffer);
        
        int threadGroups = cell.gridSize / 8;
        wfcShader.Dispatch(0, threadGroups, threadGroups, 1);
    }
}
```

### Phase 6: Structure Generation & Persistence (Week 11-12)

#### 6.1 Structure Generation with Compute Shaders
```csharp
// Scripts/DOTS/Structures/HybridStructureGenerationSystem.cs
public class HybridStructureGenerationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .WithAll<TerrainData>()
            .ForEach((in TerrainData terrain) =>
            {
                if (ShouldGenerateStructure(terrain))
                {
                    // Use Compute Shader for structure placement
                    GenerateStructureWithComputeShader(terrain);
                    
                    // DOTS handles structure logic
                    ProcessStructureResults(terrain);
                }
            }).WithoutBurst().Run();
    }
    
    private void GenerateStructureWithComputeShader(TerrainData terrain)
    {
        // Compute Shader handles structure generation
        var structureShader = computeManager.structureShader;
        structureShader.SetBuffer(0, "terrain", terrain.heightBuffer);
        structureShader.SetBuffer(0, "structures", terrain.structureBuffer);
        
        int threadGroups = terrain.resolution / 8;
        structureShader.Dispatch(0, threadGroups, threadGroups, 1);
    }
}
```

### Phase 7: Integration & Optimization (Week 13-14)

#### 7.1 Performance Monitoring
```csharp
// Scripts/DOTS/Debug/HybridPerformanceMonitor.cs
public class HybridPerformanceMonitor : SystemBase
{
    protected override void OnUpdate()
    {
        // Monitor both DOTS and Compute Shader performance
        MonitorDOTSPerformance();
        MonitorComputeShaderPerformance();
        OptimizeHybridSystem();
    }
    
    private void MonitorDOTSPerformance()
    {
        // Monitor DOTS system performance
    }
    
    private void MonitorComputeShaderPerformance()
    {
        // Monitor Compute Shader performance
    }
    
    private void OptimizeHybridSystem()
    {
        // Optimize DOTS-GPU communication
    }
}
```

## Implementation Timeline

### Week 1-2: Core DOTS Infrastructure
- [ ] Setup DOTS packages
- [ ] Create core data structures
- [ ] Migrate biome system
- [ ] Basic entity management

### Week 3-4: Compute Shaders Integration
- [x] Setup Compute Shader infrastructure
- [ ] Implement noise generation Compute Shaders
- [ ] Create hybrid terrain generation system
- [ ] Performance testing

### Week 5-6: Weather & Erosion
- [ ] Weather effects Compute Shaders
- [ ] Erosion simulation Compute Shaders
- [ ] Hybrid weather system
- [ ] Real-time effects testing

### Week 7-8: Terrain Modification
- [ ] Modification Compute Shaders
- [ ] Hybrid modification system
- [ ] Player interaction
- [ ] Modification persistence

### Week 9-10: Wave Function Collapse
- [ ] WFC Compute Shaders
- [ ] Hybrid WFC system
- [ ] Pattern generation
- [ ] Constraint propagation

### Week 11-12: Structures & Persistence
- [ ] Structure generation Compute Shaders
- [ ] Hybrid structure system
- [ ] Structure persistence
- [ ] World saving/loading

### Week 13-14: Integration & Polish
- [ ] System integration
- [ ] Performance optimization
- [ ] DOTS-GPU communication optimization
- [ ] Final testing

## Performance Targets

- **Noise Generation**: < 0.5ms per chunk (Compute Shaders)
- **Terrain Generation**: < 1ms per chunk (Hybrid)
- **Weather Effects**: < 0.2ms per frame (Compute Shaders)
- **Erosion Simulation**: < 0.3ms per frame (Compute Shaders)
- **WFC Generation**: < 2ms per structure (Hybrid)
- **Memory Usage**: < 50MB for 1000 chunks
- **Garbage Collection**: 0 allocations per frame

## Migration Strategy

### Risk Mitigation
1. **Parallel Development**: Keep old system running while building hybrid system
2. **Feature Parity**: Ensure all features work before switching
3. **Performance Testing**: Continuous performance monitoring
4. **Rollback Plan**: Ability to revert to old system if needed

### Testing Strategy
1. **Unit Tests**: Test individual DOTS systems and Compute Shaders
2. **Integration Tests**: Test DOTS-GPU communication
3. **Performance Tests**: Monitor both CPU and GPU performance
4. **Playability Tests**: Ensure game feel remains the same

## Conclusion

This hybrid DOTS + Compute Shaders approach provides the best of both worlds:
- **DOTS** for system coordination, game logic, and data management
- **Compute Shaders** for heavy computation, noise generation, and real-time effects

The final system will be capable of handling massive terrain areas with complex generation patterns, real-time modifications, dynamic weather, and persistent world state - all while maintaining excellent performance on both CPU and GPU.
