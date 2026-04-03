# Project Notes & TODO

## Current Work Session
- **Date:** 2025-11-05
- **Focus:** Test organization refactor and camera follow system fixes

---

## Recent Completions

### Test Organization Refactor (2025-11-05)
✅ **Completed comprehensive test organization and automation**

#### What Was Done:
1. **Folder Structure Created**
   - `Assets/Scripts/DOTS/Tests/` - Automated NUnit tests
   - `Assets/Scripts/DOTS/Debug/` - Debug and visualization tools
   - `Assets/Scripts/DOTS/TestHelpers/` - Test setup utilities
   - `Assets/Scripts/DOTS/Test/Archive/` - Archived obsolete tests
   - `Assets/Scripts/DOTS/Test/Archive/Manual/` - Archived manual tests

2. **Test Assembly Created**
   - `DOTS.Terrain.Tests.asmdef` with proper references
   - Configured for PlayMode tests with NUnit framework

3. **Files Organized**
   - Moved 7 debug/visualization tools to `Debug/`
   - Moved 12 test setup helpers to `TestHelpers/`
   - Archived 10 obsolete/duplicate tests
   - Archived 14 manual test scripts

4. **Automated Tests Created** (8 test files, 58+ tests)
   - `ComputeShaderTests.cs` - Shader loading and validation
   - `TerrainGenerationTests.cs` - Entity creation and positioning
   - `WFCSystemTests.cs` - Wave Function Collapse generation
   - `PhysicsSystemTests.cs` - Glob physics interactions
   - `BiomeSystemTests.cs` - Biome system functionality
   - `WeatherSystemTests.cs` - Weather components
   - `TerrainDataTests.cs` - Terrain data management
   - `ModificationSystemTests.cs` - Terrain modification

5. **Documentation Updated**
   - Created `Assets/Scripts/DOTS/Tests/README.md`
   - Updated `Assets/Scripts/Player/Test/HOW_TO_RUN_TESTS.md`
   - Updated this file with completion notes

#### Test Count Summary:
- **Player Tests:** 27 automated tests (unchanged)
- **DOTS Tests:** 58+ automated tests (new)
- **Total:** 85+ automated tests

#### Benefits:
- Automated tests can run in CI/CD
- Better organization and discoverability
- Reduced technical debt
- Easier maintenance and updates

---

## TODO

### High Priority
- [x] **Fix 2 remaining EntityVisualSync tests** (Bootstrap test suite) ✅ FIXED
  - Root cause: GameObject cleanup between tests
  - Solution: Added proper teardown in test fixture
- [ ] Verify all new DOTS tests pass in Unity Test Runner

### Medium Priority
- [ ] Test camera system in actual gameplay scene (not just tests)
- [ ] Verify mouse input integration with camera in PlayerCameraBootstrap
- [ ] Performance test with multiple camera systems active

### Low Priority
- [ ] Document camera follow system behavior in README[DOTS] TerrainSystem: Initializing...
UnityEngine.Debug:Log (object)
TerrainSystem:OnCreate () (at Assets/Scripts/DOTS/Core/TerrainSystem.cs:12)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS-Rendering] DungeonVisualizationSystem: OnCreate called
UnityEngine.Debug:Log (object)
DOTS.Terrain.Core.DebugSettings:LogRendering (string,bool) (at Assets/Scripts/DOTS/Core/DebugSettings.cs:76)
DOTS.Terrain.WFC.DungeonVisualizationSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/DungeonVisualizationSystem.cs:29)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS-Rendering] DungeonRenderingSystem: OnCreate called
UnityEngine.Debug:Log (object)
DOTS.Terrain.Core.DebugSettings:LogRendering (string,bool) (at Assets/Scripts/DOTS/Core/DebugSettings.cs:76)
DOTS.Terrain.WFC.DungeonRenderingSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/DungeonRenderingSystem.cs:54)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Initializing Compute Shaders...
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:68)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainNoise shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:82)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainErosion shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:83)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] WeatherEffects shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:84)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainModification shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:85)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainGlobRemoval shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:86)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] WFCGeneration shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:87)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] StructureGeneration shader loaded: True
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:88)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Noise shader kernels: 0, 1, 2
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeKernels () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:112)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:92)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Erosion shader kernel: 0
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeKernels () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:126)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:92)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Weather shader kernel: 0
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeKernels () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:140)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:92)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Modification shader kernel: 0
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeKernels () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:154)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:92)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] WFC shader kernel: 0
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeKernels () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:168)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:92)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Structure shader kernel: 0
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeKernels () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:182)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:92)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] Compute Shader initialization complete
UnityEngine.Debug:Log (object)
ComputeShaderManager:InitializeComputeShaders () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:95)
ComputeShaderManager:get_Instance () (at Assets/Scripts/DOTS/Compute/ComputeShaderManager.cs:51)
DOTS.Terrain.WFC.HybridWFCSystem:OnCreate () (at Assets/Scripts/DOTS/WFC/HybridWFCSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainGenerationSystem: Initializing...
UnityEngine.Debug:Log (object)
TerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs:21)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainGenerationSystem: Initialization complete
UnityEngine.Debug:Log (object)
TerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[PlayerInput] System created
UnityEngine.Debug:Log (object)
DOTS.Player.Systems.PlayerInputSystem:OnCreate (Unity.Entities.SystemState&) (at Assets/Scripts/Player/Systems/PlayerInputSystem.cs:26)
DOTS.Player.Systems.PlayerInputSystem:__codegen__OnCreate (intptr,intptr)
Unity.Entities.SystemBaseRegistry:ForwardToManaged (intptr,Unity.Entities.SystemState*,void*) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/SystemBaseRegistry.cs:364)
Unity.Entities.SystemBaseRegistry:CallForwardingFunction (Unity.Entities.SystemState*,Unity.Entities.UnmanagedSystemFunctionType) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/SystemBaseRegistry.cs:333)
Unity.Entities.SystemBaseRegistry:CallOnCreate (Unity.Entities.SystemState*) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/SystemBaseRegistry.cs:370)
Unity.Entities.WorldUnmanagedImpl:CallSystemOnCreateWithCleanup (Unity.Entities.SystemState*) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/WorldUnmanaged.cs:614)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1302)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[DOTS] TerrainGlobPhysicsSystem: Initializing...
UnityEngine.Debug:Log (object)
DOTS.Terrain.Modification.TerrainGlobPhysicsSystem:OnCreate (Unity.Entities.SystemState&) (at Assets/Scripts/DOTS/Modification/TerrainGlobPhysicsSystem.cs:34)
DOTS.Terrain.Modification.TerrainGlobPhysicsSystem:__codegen__OnCreate (intptr,intptr)
Unity.Entities.SystemBaseRegistry:ForwardToManaged (intptr,Unity.Entities.SystemState*,void*) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/SystemBaseRegistry.cs:364)
Unity.Entities.SystemBaseRegistry:CallForwardingFunction (Unity.Entities.SystemState*,Unity.Entities.UnmanagedSystemFunctionType) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/SystemBaseRegistry.cs:333)
Unity.Entities.SystemBaseRegistry:CallOnCreate (Unity.Entities.SystemState*) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/SystemBaseRegistry.cs:370)
Unity.Entities.WorldUnmanagedImpl:CallSystemOnCreateWithCleanup (Unity.Entities.SystemState*) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/WorldUnmanaged.cs:614)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1302)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

HybridTerrainGenerationSystem: Initializing...
UnityEngine.Debug:Log (object)
DOTS.Terrain.Generation.HybridTerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs:38)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

The referenced script (Unknown) on this Behaviour is missing!
UnityEngine.Resources:Load<DOTS.Terrain.Generation.TerrainGenerationSettings> (string)
DOTS.Terrain.Generation.TerrainGenerationSettings:get_Default () (at Assets/Scripts/DOTS/Generation/TerrainGenerationSettings.cs:66)
DOTS.Terrain.Generation.HybridTerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs:41)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

TerrainGenerationSettings not found in Resources folder. Creating default settings.
UnityEngine.Debug:LogWarning (object)
DOTS.Terrain.Generation.TerrainGenerationSettings:get_Default () (at Assets/Scripts/DOTS/Generation/TerrainGenerationSettings.cs:69)
DOTS.Terrain.Generation.HybridTerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs:41)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

HybridTerrainGenerationSystem: Settings loaded successfully
UnityEngine.Debug:Log (object)
DOTS.Terrain.Generation.HybridTerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs:45)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

HybridTerrainGenerationSystem: Initialization complete
UnityEngine.Debug:Log (object)
DOTS.Terrain.Generation.HybridTerrainGenerationSystem:OnCreate () (at Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs:57)
Unity.Entities.ComponentSystemBase:CreateInstance (Unity.Entities.World) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:209)
Unity.Entities.World:AddSystem_OnCreate_Internal (Unity.Entities.ComponentSystemBase) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:480)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,int,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1309)
Unity.Entities.World:GetOrCreateSystemsAndLogException (Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Collections.AllocatorManager/AllocatorHandle) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1341)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal<Unity.Entities.DefaultWorldInitialization/DefaultRootGroups> (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>,Unity.Entities.ComponentSystemGroup,Unity.Entities.DefaultWorldInitialization/DefaultRootGroups) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:257)
Unity.Entities.DefaultWorldInitialization:AddSystemToRootLevelSystemGroupsInternal (Unity.Entities.World,Unity.Collections.NativeList`1<Unity.Entities.SystemTypeIndex>) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:296)
Unity.Entities.DefaultWorldInitialization:Initialize (string,bool) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:152)
Unity.Entities.AutomaticWorldBootstrap:Initialize () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities.Hybrid/Injection/AutomaticWorldBootstrap.cs:16)

[PlayerCameraBootstrap] Ground plane created at (0.00, 0.00, 0.00) with size (20.00, 20.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:CreateGroundPlane (Unity.Entities.EntityManager) (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:237)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:49)

[PlayerCameraBootstrap] Created ground visual GameObject
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:CreateGroundVisualGameObject (Unity.Transforms.LocalTransform) (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:296)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:CreateGroundPlane (Unity.Entities.EntityManager) (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:242)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:49)

[PlayerCameraBootstrap] Physics components added to player (mass: 70kg)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:AddPlayerPhysics (Unity.Entities.EntityManager,Unity.Entities.Entity) (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:278)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:100)

[PlayerCameraBootstrap] Created player visual GameObject
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:CreatePlayerVisualGameObject (Unity.Entities.Entity,Unity.Transforms.LocalTransform) (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:164)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:105)

[PlayerCameraBootstrap] Player entity spawned at (0.00, 1.00, 0.00) with physics
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:108)

[PlayerCameraBootstrap] Camera entity spawned at (0.00, 3.00, -4.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:130)

[PlayerCameraBootstrap] Created Camera GameObject for rendering
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:CreateCameraGameObject () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:188)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:135)

[PlayerCameraBootstrap] CameraFollowSystem will automatically start following player
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.PlayerCameraBootstrap_WithVisuals:Start () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:137)

[EntityVisualSync] First successful sync! Entity 102 at float3(0f, 1f, 0f)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:363)

[EntityVisualSync] Update #2: Entity 102:1 reading position float3(0f, 1f, 0f), setting GameObject to (0.00, 1.00, 0.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:370)

[TEST] PlayerEntity queried: Index=102, Version=1
UnityEngine.Debug:Log (object)
DOTS.Player.Tests.Bootstrap.PlayerEntityBootstrapTests/<PlayerVisualSync_SyncsPositionWithEntity>d__24:MoveNext () (at Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrapTests.cs:351)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[TEST] EntityVisualSync.entity: Index=102, Version=1
UnityEngine.Debug:Log (object)
DOTS.Player.Tests.Bootstrap.PlayerCameraBootstrap_WithVisualsTests/<EntityVisualSync_SyncsPositionWithEntity>d__22:MoveNext () (at Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrap_WithVisualsTests.cs:319)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[TEST] Entities match exactly: True
UnityEngine.Debug:Log (object)
DOTS.Player.Tests.Bootstrap.PlayerCameraBootstrap_WithVisualsTests/<EntityVisualSync_SyncsPositionWithEntity>d__22:MoveNext () (at Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrap_WithVisualsTests.cs:320)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[TEST] Entities match by Index: True
UnityEngine.Debug:Log (object)
DOTS.Player.Tests.Bootstrap.PlayerCameraBootstrap_WithVisualsTests/<EntityVisualSync_SyncsPositionWithEntity>d__22:MoveNext () (at Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrap_WithVisualsTests.cs:321)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[TEST] After SetComponentData, entity position is: float3(5f, 10f, 15f)
UnityEngine.Debug:Log (object)
DOTS.Player.Tests.Bootstrap.PlayerCameraBootstrap_WithVisualsTests/<EntityVisualSync_SyncsPositionWithEntity>d__22:MoveNext () (at Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrap_WithVisualsTests.cs:335)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

[EntityVisualSync] Update #3: Entity 102:1 reading position float3(5f, 10f, 15f), setting GameObject to (5.00, 10.00, 15.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:370)

[EntityVisualSync] Update #4: Entity 102:1 reading position float3(5f, 10f, 15f), setting GameObject to (5.00, 10.00, 15.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:370)

[EntityVisualSync] Update #5: Entity 102:1 reading position float3(5f, 10f, 15f), setting GameObject to (5.00, 10.00, 15.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:370)

[TEST] After yield frames, GameObject position is: (5.00, 10.00, 15.00)
UnityEngine.Debug:Log (object)
DOTS.Player.Tests.Bootstrap.PlayerCameraBootstrap_WithVisualsTests/<EntityVisualSync_SyncsPositionWithEntity>d__22:MoveNext () (at Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrap_WithVisualsTests.cs:343)
UnityEngine.SetupCoroutine:InvokeMoveNext (System.Collections.IEnumerator,intptr)

Saving results to: C:/Users/david/AppData/LocalLow/DefaultCompany/16bitProcGen\TestResults.xml

[EntityVisualSync] Update #6: World.DefaultGameObjectInjectionWorld is null!
UnityEngine.Debug:LogWarning (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:335)

[EntityVisualSync] Update #7: World.DefaultGameObjectInjectionWorld is null!
UnityEngine.Debug:LogWarning (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:335)

[EntityVisualSync] Update #8: World.DefaultGameObjectInjectionWorld is null!
UnityEngine.Debug:LogWarning (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:335)

[EntityVisualSync] Update #9: World.DefaultGameObjectInjectionWorld is null!
UnityEngine.Debug:LogWarning (object)
DOTS.Player.Bootstrap.EntityVisualSync:LateUpdate () (at Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs:335)

[DOTS] TerrainGenerationSystem: Destroyed
UnityEngine.Debug:Log (object)
TerrainGenerationSystem:OnDestroy () (at Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs:342)
Unity.Entities.ComponentSystemBase:OnDestroy_Internal () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/ComponentSystemBase.cs:305)
Unity.Entities.World:DestroyAllSystemsAndLogException (bool&) (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:1158)
Unity.Entities.World:Dispose () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:313)
Unity.Entities.World:DisposeAllWorlds () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/World.cs:339)
Unity.Entities.DefaultWorldInitialization:DomainUnloadOrPlayModeChangeShutdown () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitialization.cs:99)
Unity.Entities.DefaultWorldInitializationProxy:OnDisable () (at ./Library/PackageCache/com.unity.entities@f6e02210e263/Unity.Entities/DefaultWorldInitializationProxy.cs:28)

Executing IPostBuildCleanup for: Unity.PerformanceTesting.Editor.TestRunBuilder.
UnityEditor.EditorApplication:Internal_CallUpdateFunctions ()

- [ ] Add camera smoothing configuration options
- [ ] Reduce debug output in production builds (consider conditional compilation)

### Human section
-need to figure out how to keep interest for finding treasure - too much, not enough, too random, not exciting
-need to fix issue with movement where WASD does not change relative to mouse position - A always goes -x, no matter where camera is facing.
-need to pare down code and get a deeper connection to movement system.
---

## ✅ Recently Completed

### 2025-11-05: Camera Follow System Fixes
- ✅ Fixed all compilation errors (assembly references, namespace issues)
- ✅ Fixed CameraFollowSystem not running in tests (manual SimulationSystemGroup.Update)
- ✅ Fixed camera not moving due to DeltaTime=0 in test mode (instant snap fallback)
- ✅ **CameraFollowsPlayerMovement test - PASSING** 
- ✅ **CameraFollowsPlayerRotation test - PASSING**
- ✅ **CameraMaintainsFollowDistance test - PASSING**
- ✅ Reduced verbose debug output from CameraFollowSystem
- ✅ Added EntityVisualSync diagnostic logging

---

## Notes & Discoveries

### Camera System Behavior
- **Test Mode Issue:** `SystemAPI.Time.DeltaTime` returns 0 in Unity test environment
  - Solution: Added instant snap (no smoothing) when `dt < 0.001f`
  - Runtime gameplay uses normal exponential smoothing
- **System Update:** Tests must manually call `simulationGroup.Update()` 
  - `yield return null` doesn't auto-update SimulationSystemGroup in test mode
- **Camera Offset:** Using `(0, 1.6, -3)` - behind and above player at head height
- **Rotation:** Camera applies yaw to position, yaw+pitch to viewing angle

### EntityVisualSync Investigation
- Component syncs GameObject transforms with ECS entity transforms in LateUpdate()
- Uses `World.DefaultGameObjectInjectionWorld` to access entity data
- Added comprehensive error checking with first-time logging
- Waiting for test run results to see why sync isn't working

### System Architecture
- `CameraFollowSystem` - Simple test system (no PlayerCameraLink)
  - Only runs when exactly 1 camera and 1 player exist
  - Auto-disables if PlayerCameraLink components present
  - Updates both LocalTransform and LocalToWorld for test compatibility
- `PlayerCameraSystem` - Production system (uses PlayerCameraLink)
- Both systems coexist, production system takes priority

---

## Questions/Issues

### Open Questions
- Why doesn't LateUpdate() sync GameObjects in EntityVisualSync tests?
  - Hypothesis: Test environment might not run MonoBehaviour lifecycle methods normally
  - Added debug logs to confirm if LateUpdate() is being called
- Should we keep manual SimulationSystemGroup.Update() calls in tests?
  - Current approach works but feels fragile

### Known Limitations
- Camera smoothing disabled in test mode (DeltaTime=0)
- Tests require explicit system group updates
- EntityVisualSync depends on Unity's MonoBehaviour lifecycle

---

## Future Work

### Camera System Enhancements
- [ ] Add configurable camera offsets and follow distances
- [ ] Implement camera collision avoidance
- [ ] Add camera shake/trauma system for impacts
- [ ] Multiple camera modes (first-person, third-person, orbit)

### Testing Infrastructure
- [ ] Investigate proper DeltaTime simulation in tests
- [ ] Create test helper for automatic system updates
- [ ] Add performance benchmarks for camera systems
- [ ] Test with physics-based player movement

### Integration Work
- [ ] Connect camera to actual player controller
- [ ] Test with terrain interaction
- [ ] Verify behavior in multiplayer scenarios
- [ ] Mobile input support for camera rotation

---

## Code Locations

### Camera Systems
- `Assets/Scripts/Player/Systems/CameraFollowSystem.cs` - Test/bootstrap camera
- `Assets/Scripts/Player/Systems/PlayerCameraSystem.cs` - Production camera
- `Assets/Scripts/Player/Components/CameraComponents.cs` - Camera component definitions

### Tests
- `Assets/Scripts/Player/Test/CameraFollowSanityTest.cs` - Camera follow tests (3/3 passing)
- `Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrap_WithVisualsTests.cs` - Bootstrap tests (22/24 passing)

### Bootstrap
- `Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs` - Visual test bootstrap
- Includes `EntityVisualSync` MonoBehaviour for GameObject-Entity syncing

---

## Debug Commands

### Run Camera Tests
```powershell
# PowerShell script to run tests
.\RunCameraFollowTests.ps1
```

### Console Log Location
```
Assets/Docs/DebugTraces/ConsoleLogs.txt
```

### Test Results
```
C:/Users/david/AppData/LocalLow/DefaultCompany/16bitProcGen/TestResults.xml
```

---

## Project Status Summary

**Overall:** Making excellent progress on player/camera systems.

**Working:**
- ✅ DOTS camera follow system
- ✅ Player entity spawning
- ✅ Camera entity spawning
- ✅ Camera position/rotation following player
- ✅ Test infrastructure for camera behavior

**In Progress:**
- 🔄 EntityVisualSync GameObject-to-Entity syncing
- 🔄 Full integration testing

**Next Steps:**
1. Fix EntityVisualSync tests
2. Run full integration test in gameplay scene
3. Connect to actual player input system
4. Performance validation

---

**Last Updated:** 2025-11-05 (after camera follow test fixes)

