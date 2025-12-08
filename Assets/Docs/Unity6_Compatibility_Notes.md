# Unity 6 DOTS Compatibility Fixes

## Issues Fixed

### 1. PhysicsSystemGroup Error
**Error:** `CS0246: The type or namespace name 'PhysicsSystemGroup' could not be found`

**Fix:** In Unity 6 with the latest Unity Physics package:
- Changed `PhysicsSystemGroup` to `PhysicsSimulationGroup`
- Added `using Unity.Physics.Systems;` directive

**File:** `SimplePlayerMovementSystem.cs`

```csharp
// Before
[UpdateBefore(typeof(PhysicsSystemGroup))]

// After
using Unity.Physics.Systems;
[UpdateBefore(typeof(PhysicsSimulationGroup))]
```

### 2. Material Ambiguity Error
**Error:** `CS0104: 'Material' is an ambiguous reference between 'Unity.Physics.Material' and 'UnityEngine.Material'`

**Fix:** Explicitly specified `UnityEngine.Material` for the playerMaterial field

**File:** `PlayerCameraBootstrap_WithVisuals.cs`

```csharp
// Before
[SerializeField] private Material playerMaterial;

// After
[SerializeField] private UnityEngine.Material playerMaterial;
```

### 3. UnityTest Attribute Not Found
**Error:** `CS0246: The type or namespace name 'UnityTestAttribute' could not be found`

**Fix:** Created proper assembly definition files with Unity Test Framework references

**Files Created:**
- `DOTS.Player.Components.asmdef` - For component files
- `DOTS.Player.Bootstrap.asmdef` - For bootstrap scripts
- `DOTS.Player.Bootstrap.Tests.asmdef` - For test scripts

## Assembly Definition Structure

```
Assets/Scripts/Player/
??? Components/
?   ??? PlayerTag.cs
???? MainCameraTag.cs
?   ??? DOTS.Player.Components.asmdef
??? Bootstrap/
?   ??? SimplePlayerMovementSystem.cs
?   ??? PlayerCameraBootstrap_WithVisuals.cs
?   ??? DOTS.Player.Bootstrap.asmdef
?   ??? Tests/
?       ??? PlayerCameraBootstrapTests.cs
?       ??? DOTS.Player.Bootstrap.Tests.asmdef
```

## Components Created

### PlayerTag.cs
```csharp
using Unity.Entities;

namespace DOTS.Player.Components
{
    public struct PlayerTag : IComponentData
    {
}
}
```

### MainCameraTag.cs
```csharp
using Unity.Entities;

namespace DOTS.Player.Components
{
    public struct MainCameraTag : IComponentData
    {
    }
}
```

## Assembly Definition Details

### DOTS.Player.Components.asmdef
- References: Unity.Entities
- Purpose: Shared component definitions

### DOTS.Player.Bootstrap.asmdef
- References: Unity.Entities, Unity.Transforms, Unity.Physics, Unity.Mathematics, Unity.Collections, Unity.Burst, DOTS.Player.Components
- Purpose: Runtime player systems and bootstrapping
- AllowUnsafeCode: true (required for some DOTS operations)

### DOTS.Player.Bootstrap.Tests.asmdef
- References: UnityEngine.TestRunner, UnityEditor.TestRunner, Unity.Entities, Unity.Transforms, Unity.Physics, Unity.Mathematics, Unity.Collections, DOTS.Player.Bootstrap, DOTS.Player.Components
- Precompiled References: nunit.framework.dll
- DefineConstraints: UNITY_INCLUDE_TESTS
- Purpose: Unit tests for bootstrap code

## Unity 6 Compatibility Notes

### Physics API Changes
- `PhysicsSystemGroup` ? `PhysicsSimulationGroup`
- Import from `Unity.Physics.Systems` namespace

### Material Handling
- When using both Unity.Physics and UnityEngine, always qualify Material with the namespace
- Unity.Physics.Material - for physics materials
- UnityEngine.Material - for rendering materials

### Test Framework
- Unity Test Framework requires proper assembly definitions
- Must set `overrideReferences: true` and include `nunit.framework.dll`
- Must set `UNITY_INCLUDE_TESTS` define constraint

## Next Steps

1. **Restart Unity Editor** - Assembly definition changes require Unity to recompile
2. **Check Unity Package Manager** - Ensure you have these packages installed:
   - Unity Physics (latest version compatible with Unity 6)
   - Entities (latest version)
   - Burst (latest version)
   - Test Framework (comes with Unity)

3. **Verify Test Runner** - Open Unity Test Runner (Window ? General ? Test Runner) to see your tests

## Troubleshooting

If you still see errors after these fixes:

1. **Close and reopen Visual Studio** - Sometimes IntelliSense needs a refresh
2. **Delete Library folder** in Unity project and let Unity reimport
3. **Check Package Manager** - Ensure all DOTS packages are up to date
4. **Verify API compatibility** - Unity 6 uses DOTS 1.0+ which has breaking changes from earlier versions

All code should now be compatible with Unity 6 and the latest DOTS packages!
