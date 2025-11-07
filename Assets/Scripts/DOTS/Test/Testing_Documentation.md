# DOTS Terrain Testing Documentation

This document describes all major test scripts in the DOTS terrain system, what each test covers, why it works, and how to set it up and run it. Use this as a living reference for regression, feature, and integration testing.

---

## Table of Contents

1. [Environment Setup Tests](#environment-setup-tests)
2. [Core System Tests](#core-system-tests)
3. [Visual and Debug Tests](#visual-and-debug-tests)
4. [Weather System Tests](#weather-system-tests)
5. [Performance and Integration Tests](#performance-and-integration-tests)
6. [Specialized Tests](#specialized-tests)
7. [General Setup Tips](#general-setup-tips)
8. [Example Test Scenarios](#example-test-scenarios)

---

## Environment Setup Tests

### 1. **AutoTestSetup.cs**
**What it tests:**
- Basic environment setup for DOTS terrain generation
- Ensures all required managers and a configurable number of test entities are created

**Why it works:**
- Automates the creation of `ComputeShaderManager`, `TerrainEntityManager`, and test entities
- Ensures a valid test environment for all other tests
- Provides quick validation that core systems are functional

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the AutoTestSetup script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Setup On Start" to auto-setup when you press Play
   - Check "Create Test Entities" to automatically create test terrain entities
   - Set "Number Of Test Entities" to 5 (or your desired number)
4. **Press Play** to run the test automatically
5. **Alternative**: Right-click on the AutoTestSetup component header and select "Setup Test Environment" for manual setup
6. **Verify setup**: Right-click on the component header and select "Get Test Status" to check all components are ready

**Expected Results:**
- Console logs showing successful setup of all managers
- Test entities created and ready for processing
- Status report showing all systems are operational

---

### 2. **HybridTestSetup.cs**
**What it tests:**
- Full DOTS terrain test environment, including compute, entity, buffer, and weather systems
- Hybrid (DOTS + MonoBehaviour) workflow validation

**Why it works:**
- Ensures all core systems are present and correctly initialized
- Validates the complete pipeline from DOTS entities to GPU compute shaders
- Provides comprehensive environment verification

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the HybridTestSetup script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Setup On Start" to auto-setup when you press Play
   - Check "Log Setup Process" for detailed console output during setup
   - Check "Enable Debug Logs" to show real-time debug GUI (optional)
4. **Press Play** to run the test automatically
5. **Alternative**: Right-click on the HybridTestSetup component header and select "Setup Hybrid Test Environment" for manual setup
6. **Monitor progress**: If debug logs are enabled, you'll see a status panel in the top-right corner of the Game view

**Expected Results:**
- All managers created and initialized
- DOTS world properly configured
- Weather systems auto-registered
- Debug GUI showing system status

---

## Core System Tests

### 3. **QuickTerrainEntityCreator.cs**
**What it tests:**
- Creation of a grid of terrain entities with configurable resolution and world scale
- Entity management and positioning

**Why it works:**
- Allows rapid scaling of test world size and detail for performance and correctness testing
- Validates entity creation, positioning, and component assignment
- Tests the grid-based chunk system

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the QuickTerrainEntityCreator script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Create On Start" to automatically create entities when you press Play
   - Set "Number Of Entities" to 5 (or your desired number for a grid)
   - Set "Resolution" to 32 (or 64, 128 for higher detail)
   - Set "World Scale" to 10 (controls the size of each terrain chunk)
4. **Press Play** to create entities automatically
5. **Alternative**: Right-click on the QuickTerrainEntityCreator component header and select "Create Terrain Entities" for manual creation
6. **Note**: Entities are arranged in a square grid pattern (e.g., 5 entities = 3x3 grid with 2 empty slots)

**Expected Results:**
- Grid of terrain entities created
- Each entity has proper TerrainData and BiomeComponent
- Entities marked for generation by the terrain system

---

### 4. **BasicComputeShaderTest.cs**
**What it tests:**
- Verifies the compute shader pipeline is functional
- Tests shader loading, dispatch, and data readback

**Why it works:**
- Loads a test compute shader, dispatches it, and checks for expected output values
- Validates the GPU compute pipeline from Unity to shader execution
- Ensures compute buffers are working correctly

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the BasicComputeShaderTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Run Test On Start" to automatically run the test when you press Play
   - Set "Test Resolution" to 64 (or 32, 128 for different test sizes)
   - Set "Test Value" to 10.0 (test parameter for the compute shader)
4. **Press Play** to run the test automatically
5. **Alternative**: Right-click on the BasicComputeShaderTest component header and select "Run Basic Compute Shader Test" for manual execution
6. **Check results**: Open the Console window (Window > General > Console) to see detailed test results

**Expected Results:**
- Compute shader loaded successfully
- Kernel found and dispatched
- Output buffer contains expected marker values
- Test passes with detailed performance metrics

---

### 5. **ComputeShaderTests.cs** (Automated Tests)
**What it tests:**
- Phase 2.1: Compute Shader Setup validation
- Shader loading, kernel validation, and thread group calculations

**Why it works:**
- Comprehensive validation of the compute shader infrastructure using automated NUnit tests
- Tests shader resources, kernel availability, and performance metrics
- Ensures the compute pipeline is ready for terrain generation

**How to run:**
1. **Open Unity Test Runner** (Window > General > Test Runner)
2. **Switch to PlayMode tab**
3. **Find** `DOTS.Terrain.Tests > ComputeShaderTests`
4. **Run all tests** or select individual test methods
5. **Review results** in the Test Runner window

**Note:** This replaces the old MonoBehaviour-based `ComputeShaderSetupTest.cs`. The automated tests provide the same validation but are integrated into the test framework.

**Expected Results:**
- All compute shaders loaded successfully
- Kernels validated and available
- Thread group calculations correct
- Performance metrics within acceptable ranges

---

## Visual and Debug Tests

### 6. **SimpleVisualDebugTest.cs**
**What it tests:**
- End-to-end DOTS terrain pipeline: entity creation, mesh generation, GPU buffer management, and visual output
- Complete data flow from DOTS to GPU to rendered mesh

**Why it works:**
- Demonstrates the full data flow from DOTS to GPU to rendered mesh
- Provides real-time updates and performance metrics
- Shows visual validation of terrain generation

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the SimpleVisualDebugTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Set "Test Chunk Count" to 3 (number of terrain chunks to create)
   - Set "Chunk Resolution" to 32 (vertices per chunk - higher = more detail)
   - Set "Chunk Size" to 10 (world size of each chunk)
   - Set "Height Scale" to 5 (height multiplier for terrain)
   - Check "Show Height Colors" to enable color variation between chunks
   - Check "Show Wireframe" to enable wireframe rendering (optional)
4. **Press Play** to run the test automatically
5. **Alternative**: Right-click on the SimpleVisualDebugTest component header and select "Run Visual Debug Test" for manual execution
6. **For automatic scene setup**: Use VisualTestSceneSetup instead (see below)

**Expected Results:**
- 3D terrain chunks visible in scene
- Wave-like height patterns generated by sine functions
- Color-coded chunks for easy identification
- Performance metrics displayed in console

---

### 7. **VisualTestSceneSetup.cs**
**What it tests:**
- Automatic scene setup for visual testing
- Camera, lighting, and test component configuration

**Why it works:**
- Automates the creation of a complete test environment
- Ensures proper camera positioning and lighting for terrain visualization
- Provides consistent test conditions

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the VisualTestSceneSetup script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Setup On Start" to automatically set up the scene when you press Play
   - Check "Add Camera" to create a camera positioned for terrain viewing
   - Check "Add Lighting" to set up proper lighting for terrain visualization
   - Check "Add Visual Test" to automatically add a SimpleVisualDebugTest component
4. **Press Play** for automatic setup
5. **Alternative**: Right-click on the VisualTestSceneSetup component header and select "Setup Visual Test Scene" for manual setup
6. **Camera controls**: The setup includes a simple camera controller for navigating the terrain

**Expected Results:**
- Camera positioned for optimal terrain viewing
- Proper lighting setup for terrain visualization
- Visual test component configured and ready
- Complete test environment ready for terrain generation

---

### 8. **TerrainHeightVisualizer.cs**
**What it tests:**
- Real-time height data visualization and monitoring
- Weather effects integration and terrain change tracking

**Why it works:**
- Provides real-time GUI visualization of terrain height data
- Tracks height changes over time and weather effects
- Offers comprehensive debugging information

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the TerrainHeightVisualizer script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Enable Visualization" to show the debug GUI
   - Check "Show Height Graph" to display real-time height data
   - Check "Show Weather Overlay" to show weather effects
   - Set "Update Interval" to 0.5 (how often to update the visualization)
4. **Press Play** to start visualization
5. **Controls**: 
   - Press Tab key to toggle cursor lock/unlock
   - Use mouse wheel to scroll through debug information panels
   - The visualization will show real-time terrain height changes and weather effects

**Expected Results:**
- Real-time height graph showing terrain changes
- Weather overlay with visual effects
- Debug panels with system status
- Performance metrics and terrain statistics

---

## Weather System Tests

### 9. **WeatherTestSetup.cs**
**What it tests:**
- Weather system integration with terrain entities
- Weather changes, effects, and debug visualization
- Real-time weather monitoring and control

**Why it works:**
- Adds weather components to all terrain entities
- Provides tools to force/test weather changes and monitor their effects
- Validates weather system integration with terrain generation

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the WeatherTestSetup script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Run On Start" to automatically set up weather testing when you press Play
   - Check "Enable Debug Logs" for detailed weather console output
   - Check "Show Debug GUI" to display weather status and controls
   - Check "Monitor Terrain Changes" to track terrain height changes
   - Set "Initial Weather" to Clear (or your preferred starting weather)
   - Set "Weather Change Interval" to 10 (seconds between auto weather changes)
   - Check "Auto Change Weather" to automatically cycle through weather types
4. **Press Play** to start weather testing
5. **Weather controls**: Use number keys 1-5 during play to force specific weather changes:
   - 1 = Clear weather
   - 2 = Rain weather
   - 3 = Snow weather
   - 4 = Storm weather
   - 5 = Fog weather

**Expected Results:**
- Weather components added to all terrain entities
- Weather effects visible in scene
- Debug GUI showing current weather and effects
- Terrain height changes monitored and logged

---

## Performance and Integration Tests

### 10. **HybridGenerationTest.cs**
**What it tests:**
- Integration of the hybrid terrain generation system
- Performance monitoring and system validation
- Complete pipeline testing from setup to generation

**Why it works:**
- Sets up the environment, creates test entities, verifies system integration
- Monitors performance and provides detailed logging
- Validates the complete hybrid workflow

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the HybridGenerationTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Set "Test Chunk Count" to 3 (number of terrain chunks to test)
   - Check "Run On Start" to automatically run the test when you press Play
   - Check "Log Performance" to record detailed performance metrics
   - Check "Enable Debug Logs" to show additional debug information (optional)
   - Check "Enable Verbose Logs" for very detailed console output (optional)
4. **Press Play** to run the test automatically
5. **Alternative**: Right-click on the HybridGenerationTest component header and select "Run Hybrid Generation Test" for manual execution
6. **Monitor results**: Open the Console window (Window > General > Console) to see detailed test results and performance metrics

**Expected Results:**
- All systems properly integrated
- Test entities created and processed
- Performance metrics logged
- System validation completed successfully

---

### 11. **Phase1CompletionTest.cs**
**What it tests:**
- Comprehensive test for Phase 1 completion
- All core systems working correctly
- Integration validation across all components

**Why it works:**
- Validates that all Phase 1 systems are functional
- Tests core data structures, biome system, entity management, and compute buffer management
- Ensures the foundation is solid for Phase 2 development

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the Phase1CompletionTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Run Tests On Start" to automatically run all Phase 1 tests when you press Play
   - Check "Create Test Entities" to create test terrain entities during testing
   - Set "Test Entity Count" to 3 (number of entities to create for testing)
4. **Press Play** to run all Phase 1 tests automatically
5. **Review results**: Open the Console window (Window > General > Console) to see comprehensive validation results for all core systems

**Expected Results:**
- All core data structures validated
- Biome system working correctly
- Entity management functional
- Compute buffer management operational
- Complete Phase 1 validation passed

---

## Specialized Tests

### 12. **BiomeSystemTest.cs**
**What it tests:**
- Biome system functionality and integration
- Biome component creation and assignment
- Biome-specific terrain generation

**Why it works:**
- Tests the biome assignment system
- Validates biome component creation and data
- Ensures biome-specific terrain generation works correctly

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the BiomeSystemTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Run Tests On Start" to automatically run biome tests when you press Play
   - Set "Test Biome Type" to Forest (or any other biome type you want to test)
4. **Press Play** to run biome system tests automatically
5. **Check results**: Open the Console window (Window > General > Console) to see biome validation results

**Expected Results:**
- BiomeBuilder functionality validated
- Biome component creation successful
- Terrain entities with biome components created
- Biome-specific generation working

---

### 13. **TerrainGenerationTest.cs**
**What it tests:**
- Core terrain generation system
- Entity creation, processing, and validation
- Seamless generation testing

**Why it works:**
- Tests the complete terrain generation pipeline
- Validates entity creation and processing
- Tests seamless generation between chunks

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the TerrainGenerationTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Set "Test Resolution" to 64 (or 32, 128 for different detail levels)
   - Set "Test World Scale" to 10 (size of each terrain chunk in world units)
   - Check "Test Seamless Generation" to validate seamless terrain between chunks
4. **Press Play** to run terrain generation tests automatically
5. **Monitor results**: Open the Console window (Window > General > Console) to see generation results and seam validation

**Expected Results:**
- Test entities created successfully
- Terrain generation completed
- Seamless generation validated (if enabled)
- Performance metrics logged

---

### 14. **SimpleTerrainTest.cs**
**What it tests:**
- Focused test for compute shader terrain generation
- Basic terrain entity creation and processing

**Why it works:**
- Simple, focused test for terrain generation
- Validates basic entity creation and compute shader integration
- Provides quick feedback on core functionality

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the SimpleTerrainTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Set "Test Resolution" to 32 (or 16, 64 for different detail levels)
   - Set "Test World Scale" to 1.0 (size of terrain chunk in world units)
4. **Press Play** to run the test automatically
5. **Alternative**: Right-click on the SimpleTerrainTest component header and select "Run Simple Terrain Test" for manual execution
6. **Check results**: Open the Console window (Window > General > Console) to see test results

**Expected Results:**
- Test entity created successfully
- Terrain generation completed
- Basic validation passed
- Simple performance metrics

---

### 15. **TerrainEntityTest.cs**
**What it tests:**
- Entity creation and management
- DOTS terrain generation system validation

**Why it works:**
- Tests entity creation and management
- Validates the DOTS terrain generation system
- Ensures entities are properly configured

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the TerrainEntityTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Set "Test Resolution" to 64 (or 32, 128 for different detail levels)
   - Set "Test World Scale" to 1.0 (size of each terrain chunk in world units)
   - Set "Number Of Test Chunks" to 3 (number of terrain entities to create)
4. **Press Play** to run entity tests automatically
5. **Check results**: Open the Console window (Window > General > Console) to see entity creation and validation results

**Expected Results:**
- Test terrain entities created
- DOTS system validation passed
- Entity management working correctly

---

### 16. **QuickVisualTest.cs**
**What it tests:**
- Minimal setup for quick visual feedback
- Basic terrain visualization

**Why it works:**
- Provides quick visual validation of terrain generation
- Minimal setup required for immediate feedback
- Good for rapid iteration and testing

**How to set up:**
1. **Create an empty GameObject** in your scene
2. **Add the QuickVisualTest script** to the GameObject
3. **Configure the settings** in the Inspector:
   - Check "Run On Start" to automatically run the test when you press Play
   - Set "Chunk Count" to 2 (number of terrain chunks to create)
   - Set "Height Scale" to 3 (height multiplier for terrain)
4. **Press Play** for quick visual test
5. **Check scene**: Look in the Scene view or Game view to see terrain visualization

**Expected Results:**
- Simple terrain chunks visible
- Basic height generation working
- Quick visual feedback provided

---

### 17. **Additional Unit Tests**
- **CollectionsTest.cs**: Tests Unity Collections package functionality
- **JobsTest.cs**: Tests Unity Jobs system integration
- **PhysicsTest.cs**: Tests physics system integration
- **MathematicsTest.cs**: Tests Unity Mathematics package
- **TerrainDataTest.cs**: Tests terrain data structures
- **TerrainDataManagerSystemTest.cs**: Tests terrain data management system

---

## General Setup Tips

### Prerequisites
- **Always ensure `ComputeShaderManager` and `TerrainEntityManager` are present**
- Most tests will create them if missing
- Check the Unity Console for logs and errors—most tests provide detailed output

### Important: DOTS System Conflicts
**Problem**: You may see duplicate terrain objects (e.g., `TerrainChunk_0_0` AND `DOTS_TerrainMesh_0_0`)

**Cause**: DOTS systems automatically run and process terrain entities created by test scripts, creating their own visual representations.

**Solutions**:
1. **For Test Scripts**: The `HybridTerrainGenerationSystem` is temporarily disabled to prevent conflicts
2. **For DOTS Systems**: Comment out the `return;` statement in `HybridTerrainGenerationSystem.OnUpdate()` and remove test scripts
3. **Separate Scenes**: Use different scenes for testing vs. DOTS system development

**Current Setup**: Test scripts are prioritized - DOTS systems are disabled to prevent conflicts.

### Configuration Management
**TerrainGenerationSettings**: The system now uses a ScriptableObject for configuration instead of hard-coded values.

**To configure settings**:
1. **Create settings asset**: Right-click in Project → Create → DOTS → Terrain → Generation Settings
2. **Place in Resources folder**: Move the asset to `Resources/TerrainGenerationSettings.asset`
3. **Modify in Inspector**: All settings are now configurable without code changes

**Settings include**:
- **Performance**: Max chunks per frame, buffer sizes
- **Noise**: Scale, height multiplier, biome scale, noise offset
- **Visual**: Mesh height scale
- **Debug**: Logging toggles
- **Terrain Types**: Height thresholds for different terrain types

### Configuration Guidelines
- **Use the Inspector to configure test parameters** for different scenarios
- **Start with small test worlds** and scale up for performance testing
- **Use context menu options** (right-click on component header) for manual test control

### Debugging
- **Enable debug logs** for detailed troubleshooting
- **Check system status** using the debug GUIs
- **Monitor performance metrics** for optimization opportunities

### Best Practices
- **Run tests in isolation** to identify specific issues
- **Use consistent test parameters** for reproducible results
- **Document any test failures** with specific error messages and conditions

---

## Example Test Scenarios

### Small Test World
```csharp
numberOfEntities = 4;    // 2x2 grid
resolution = 32;         // Medium detail
worldScale = 5f;         // 5x5 world units per chunk
// Result: 10x10 world units total
```

### Large Test World
```csharp
numberOfEntities = 16;   // 4x4 grid
resolution = 64;         // High detail
worldScale = 20f;        // 20x20 world units per chunk
// Result: 80x80 world units total
```

### Performance Test
```csharp
numberOfEntities = 25;   // 5x5 grid
resolution = 128;        // Very high detail
worldScale = 50f;        // 50x50 world units per chunk
// Result: 250x250 world units total (stress test)
```

### Quick Validation Test
```csharp
numberOfEntities = 1;    // Single chunk
resolution = 16;         // Low detail
worldScale = 1f;         // 1x1 world units
// Result: Quick validation of core systems
```

---

## Troubleshooting Common Issues

### Test Setup Failures
- **Missing DOTS packages**: Ensure all required DOTS packages are installed
- **Compute shader not found**: Check that compute shaders are in the Resources/Shaders folder
- **World not initialized**: Wait a frame for DOTS to initialize before running tests

### Performance Issues
- **High memory usage**: Reduce resolution or number of entities
- **Slow generation**: Check compute shader performance and GPU utilization
- **Frame rate drops**: Monitor chunk count and detail level

### Visual Issues
- **No terrain visible**: Check camera position and lighting
- **Incorrect heights**: Verify noise parameters and world scale
- **Missing colors**: Ensure height colors are enabled in test settings
- **Duplicate terrain objects**: See "DOTS System Conflicts" section above - this is normal when both systems are active

### Memory Management
- **BlobBuilder memory leaks**: Ensure test entities are properly cleaned up
- **Use "Cleanup Test Entities"**: Right-click on HybridGenerationTest component to manually clean up
- **Monitor memory usage**: Check Profiler for BlobAssetReference leaks
- **Automatic cleanup**: Test entities are automatically cleaned up when test is destroyed

---

## How to Expand This Document

### Adding New Tests
For each new test, add a section with:
- **What it tests** - Clear description of test purpose
- **Why it works** - Explanation of test methodology
- **How to set up** - Step-by-step setup instructions
- **Expected Results** - What to expect when the test passes

### Updating Existing Tests
- Update setup instructions if test parameters change
- Add troubleshooting information for common issues
- Include performance benchmarks and optimization tips

### Documentation Standards
- Use consistent formatting and structure
- Include code examples for configuration
- Provide clear expected results and troubleshooting steps
- Link to relevant source files or additional documentation

---

## Version History

- **v1.0** - Initial documentation covering all major test scripts
- **v1.1** - Added troubleshooting section and performance guidelines
- **v1.2** - Expanded with specialized tests and unit test coverage

---

*This document should be updated whenever new tests are added or existing tests are modified. Use it as a living reference for the DOTS terrain testing framework.* 