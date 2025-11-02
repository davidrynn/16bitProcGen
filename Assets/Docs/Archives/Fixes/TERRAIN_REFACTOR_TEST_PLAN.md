# Terrain Refactor Test Plan

## Overview
This document outlines the testing strategy for the terrain system refactoring that removed TerrainTransformSystem and simplified transform handling.

## Changes Made
1. **Removed** `worldPosition`, `rotation`, `scale` fields from `TerrainData`
2. **Deleted** `TerrainTransformSystem.cs` entirely
3. **Updated** `TerrainEntityManager` to calculate transforms directly from `chunkPosition`
4. **Simplified** transform handling to use standard DOTS `LocalTransform` components

## Test Files Created
- `TerrainRefactorTest.cs` - Comprehensive test for the refactored system
- `TerrainRefactorTestSetup.cs` - Setup helper for test environment

## Testing Strategy

### 1. Unit Tests (Automated)
- ✅ **Compilation Test**: Verify all files compile without errors
- ✅ **Component Creation Test**: Verify terrain entities are created with correct components
- ✅ **Transform Calculation Test**: Verify position is calculated correctly from chunkPosition
- ✅ **Matrix Verification Test**: Verify LocalToWorld matrix is correct

### 2. Integration Tests (Manual)
- **TerrainRefactorTest**: Run the comprehensive test script
- **TransformIntegrationTest**: Verify existing test still works
- **Terrain Generation**: Test that terrain generation works without worldPosition field

### 3. Performance Tests (Manual)
- **Memory Usage**: Verify no memory leaks from removed synchronization
- **Frame Rate**: Verify no performance impact from removed TerrainTransformSystem

## How to Run Tests

### Option 1: Using Test Setup Script
1. Create an empty GameObject in your scene
2. Add `TerrainRefactorTestSetup` component
3. The script will automatically set up the test environment and run tests

### Option 2: Manual Setup
1. Ensure you have:
   - `TerrainEntityManager` in the scene
   - `DOTSWorldSetup` in the scene
2. Add `TerrainRefactorTest` component to any GameObject
3. Use the context menu "Run Terrain Refactor Test"

### Option 3: Using Existing Tests
1. Add `TransformIntegrationTest` component to any GameObject
2. Use the context menu "Check Current Transforms"

## Expected Results

### ✅ Success Criteria
- All tests pass without errors
- Terrain entities are created with correct `LocalTransform` components
- Position is calculated as `chunkPosition * worldScale`
- Rotation is `quaternion.identity`
- Scale is `worldScale`
- `LocalToWorld` matrix is correctly calculated
- No references to deleted `TerrainTransformSystem`
- No references to removed transform fields

### ❌ Failure Indicators
- Compilation errors
- Missing components on terrain entities
- Incorrect position calculations
- Missing or incorrect `LocalToWorld` matrix
- Runtime exceptions

## Test Scenarios

### Scenario 1: Basic Terrain Creation
- Create terrain entity at chunk position (0,0)
- Verify all components are present
- Verify transform values are correct

### Scenario 2: Multiple Chunk Positions
- Create terrain entities at different chunk positions
- Verify each has correct world position
- Verify no interference between chunks

### Scenario 3: Different World Scales
- Test with different `worldScale` values
- Verify position calculation scales correctly
- Verify scale component matches worldScale

### Scenario 4: Terrain Generation Integration
- Run terrain generation system
- Verify it works without `worldPosition` field
- Verify mesh generation still works

## Rollback Plan
If tests fail, the changes can be easily rolled back by:
1. Restoring the transform fields to `TerrainData`
2. Recreating `TerrainTransformSystem.cs`
3. Reverting the changes to `TerrainEntityManager` and `TerrainDataBuilder`

## Success Metrics
- ✅ All automated tests pass
- ✅ No compilation errors
- ✅ No runtime exceptions
- ✅ Terrain entities render correctly
- ✅ Performance is maintained or improved
- ✅ Code is simpler and more maintainable
