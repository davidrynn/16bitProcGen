# Unity DOTS System Authoring Checklist

## ✅ Always Mark DOTS Systems as `partial`
- [ ] Every class inheriting from `SystemBase`, `ISystem`, or similar DOTS base types is marked as `partial`.
  ```csharp
  public partial class MySystem : SystemBase
  {
      // ...
  }
  ```

## ✅ One Class Per File
- [ ] Each DOTS system class is defined in its own `.cs` file, with the filename matching the class name.

## ✅ No Duplicate Class Names
- [ ] No other files in the project define a class with the same name and namespace as your system.

## ✅ Clean Up After Refactors
- [ ] After renaming, moving, or deleting system files, clean the `Library`, `Temp`, and `obj` folders and restart Unity to remove stale generated code.

## ✅ Avoid Defining Systems in Markdown or Non-Code Files
- [ ] Do not include full system class definitions in markdown or documentation files, unless they are fully commented out or inside code blocks that cannot be parsed by Unity.

## ✅ Use Namespaces Consistently
- [ ] Use consistent namespaces for your systems to avoid accidental collisions.

## ✅ Check for Source Generator Output
- [ ] If you get a "namespace ... already contains a definition for ..." error, check the `Temp/GeneratedCode/Assembly-CSharp/` folder for generated files with the same class name.

---

## Unity.Transforms Integration

### ✅ Transform Components Added to TerrainData
- [ ] `TerrainData` now includes transform fields: `worldPosition`, `rotation`, `scale`
- [ ] `TerrainEntityManager` automatically adds `LocalTransform` and `LocalToWorld` components
- [ ] `TerrainTransformSystem` synchronizes transforms with terrain data changes

### ✅ Transform Synchronization
- [ ] World position is calculated from chunk position and world scale
- [ ] Y position is updated based on terrain's average height after generation
- [ ] Transforms are automatically updated when terrain data changes

### ✅ Testing Transform Integration
- [ ] Use `TransformIntegrationTest` to verify transform components are working
- [ ] Check console for `[TerrainTransformSystem] Updated transform` messages
- [ ] Use context menu options to inspect and force transform updates

## Terrain Glob System

### ✅ TerrainGlobComponent
- [ ] `TerrainGlobComponent` represents terrain globs removed from terrain
- [ ] Includes physics properties: velocity, angular velocity, mass, bounciness, friction
- [ ] State flags: isGrounded, isCollected, isDestroyed, lifetime
- [ ] Collection properties: collectionRadius, canBeCollected, resourceValue
- [ ] Visual properties: scale, rotation, visualAlpha

### ✅ TerrainGlobPhysicsComponent
- [ ] Optional physics behavior for globs
- [ ] Physics settings: gravityScale, dragCoefficient, maxVelocity, maxAngularVelocity
- [ ] Collision properties: collisionRadius, collideWithTerrain, collideWithOtherGlobs, collideWithPlayer

### ✅ TerrainGlobRenderComponent
- [ ] Optional rendering for globs
- [ ] Visual settings: meshScale, meshVariant, color, useTerrainColor

### ✅ TerrainGlobPhysicsSystem
- [ ] Handles physics behavior for all terrain globs
- [ ] Applies gravity, air resistance, and ground collision
- [ ] Manages glob bouncing, rolling, and velocity clamping
- [ ] Provides glob creation and destruction methods
- [ ] Performance monitoring and cleanup

### ✅ Testing Glob Physics
- [ ] Use `GlobPhysicsTest` to verify glob physics system
- [ ] Test glob creation, physics behavior, and cleanup
- [ ] Check console for physics stats and glob updates
- [ ] Use context menu options to create test globs and force physics updates

### ✅ Transform System Architecture
```csharp
// TerrainData includes transform information
public struct TerrainData : IComponentData
{
    // ... existing fields ...
    public float3 worldPosition;    // Calculated world position
    public quaternion rotation;     // Terrain chunk rotation
    public float3 scale;           // Terrain chunk scale
}

// TerrainTransformSystem keeps transforms synchronized
public partial class TerrainTransformSystem : SystemBase
{
    // Updates LocalTransform and LocalToWorld when TerrainData changes
}
```

---

## Quick Fix for Duplicate System Errors

1. Ensure your system class is marked as `partial`.
2. Clean the `Library`, `Temp`, and `obj` folders.
3. Restart Unity and let it reimport the project.

---

**Tip:**  
Add this checklist to your project's `README.md` or developer documentation to help your team avoid common DOTS system pitfalls! 