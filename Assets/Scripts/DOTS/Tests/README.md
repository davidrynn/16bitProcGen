# DOTS Terrain Tests

This directory contains automated tests for the DOTS terrain generation system.

## Test Organization

### Automated Tests (`Automated/`)

NUnit automated tests that can be run in Unity Test Runner:

- **ComputeShaderTests.cs** - Tests for compute shader manager functionality
- **TerrainGenerationTests.cs** - Tests for terrain entity creation and data management
- **WFCSystemTests.cs** - Tests for Wave Function Collapse dungeon generation
- **PhysicsSystemTests.cs** - Tests for glob physics and terrain interaction
- **BiomeSystemTests.cs** - Tests for biome component and builder functionality
- **WeatherSystemTests.cs** - Tests for weather system components
- **TerrainDataTests.cs** - Tests for TerrainData component creation and management
- **ModificationSystemTests.cs** - Tests for terrain modification system

**Total: 8 automated test files with 60+ individual tests**

## Running Tests

### In Unity Editor

1. Open **Window > General > Test Runner** (or press `Ctrl+Alt+T`)
2. Click the **PlayMode** tab
3. Expand **DOTS.Terrain.Tests**
4. Click **Run All** to run all DOTS tests
5. Or right-click individual test classes to run specific tests

### Command Line

```powershell
# Run all DOTS terrain tests
& "C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" `
    -batchmode `
    -projectPath "<project-path>" `
    -runTests `
    -testPlatform PlayMode `
    -testFilter "DOTS.Terrain.Tests" `
    -logFile -
```

## Test Coverage

### Core Systems (4 test files)
- ✓ Compute shader loading and validation
- ✓ Terrain entity creation and positioning
- ✓ WFC pattern generation
- ✓ Physics and glob interaction

### Extended Features (4 test files)
- ✓ Biome system functionality
- ✓ Weather component management
- ✓ Terrain data validation
- ✓ Modification system operations

## Related Directories

- **../Debug/** - Visual debug tools and inspectors
- **../TestHelpers/** - Setup utilities for manual testing
- **../Test/Archive/** - Archived obsolete tests
- **../Test/Archive/Manual/** - Archived manual test scripts

## Test Guidelines

### Writing New Tests

1. Add tests to appropriate file in `Automated/` directory
2. Use `[TestFixture]` for test classes
3. Use `[Test]` for simple tests
4. Use `[UnityTest]` for tests requiring frame updates
5. Follow naming pattern: `MethodUnderTest_Scenario_ExpectedBehavior`

### Example Test

```csharp
[Test]
public void TerrainData_ValidResolution()
{
    var terrainData = TerrainDataBuilder.CreateTerrainData(
        new int2(0, 0),
        64,
        10f,
        BiomeType.Plains
    );
    
    Assert.AreEqual(64, terrainData.resolution,
        "Resolution should be stored correctly");
}
```

## Dependencies

Tests require these Unity packages:
- Unity.Entities
- Unity.Mathematics
- Unity.Collections
- Unity.Physics
- Unity.Transforms
- NUnit Framework (included with Test Runner)

## Continuous Integration

These tests are designed to run in CI/CD pipelines:
- All tests should complete within 30 seconds
- No external dependencies required
- Tests create and dispose their own DOTS worlds

## Troubleshooting

### Tests Don't Appear
- Check for compilation errors in Console
- Verify `DOTS.Terrain.Tests.asmdef` is properly configured
- Ensure test files are in the `Automated/` directory

### Tests Fail
- Check that all required components exist in your project
- Verify DOTS packages are up to date
- Review Console for specific error messages

### Performance Issues
- Reduce number of entities created in tests
- Use smaller resolutions for terrain tests
- Run tests individually instead of all at once

## Documentation

- See `../Test/Testing_Documentation.md` for comprehensive testing guide
- See `../../Player/Test/HOW_TO_RUN_TESTS.md` for general test running info
- See `../../../Docs/PROJECT_NOTES.md` for project-wide testing notes

