# Pure ECS Rendering Migration Guide

## Current Setup: Minimal Hybrid Approach

**Active System:** `PlayerEntityBootstrap.cs`

**Architecture:**
- ✅ Pure ECS logic (all gameplay in systems)
- ✅ GameObject visuals (thin "view layer" that syncs to entities)
- ✅ PlayerVisualSync component (syncs GameObject to ECS entity)

**Visuals Created:**
- Blue capsule for player (synced to ECS entity)
- Green ground plane (static visual)
- Unity Camera GameObject (required for rendering)

**Benefits:**
- ✅ Works immediately - no package dependencies
- ✅ Easy to debug (can see GameObjects in Hierarchy)
- ✅ All logic remains in pure ECS systems
- ✅ GameObjects are passive followers (no logic)

**Trade-offs:**
- ⚠️ GameObjects exist at runtime (but no logic)
- ⚠️ Some overhead syncing transforms

## Future: Pure ECS Rendering

**Alternative System:** `PlayerEntityBootstrap_PureECS.cs`

**Architecture:**
- ✅ Pure ECS logic AND rendering
- ✅ No GameObjects at runtime (except camera)
- ✅ Uses Entities.Graphics package for GPU instancing

**Requirements:**
1. Install Entities.Graphics package
2. Enable in Package Manager (Window > Package Manager > Unity Registry)

**To Migrate:**

### Step 1: Install Package
```
Window > Package Manager > Unity Registry
Search: "Entities Graphics"
Install
```

### Step 2: Replace Bootstrap System

**Option A: Disable Current System**
```csharp
// In PlayerEntityBootstrap.cs, add at top:
#if FALSE  // Disabled - using pure ECS rendering
```

**Option B: Remove from Scene**
- Remove any GameObjects with PlayerEntityBootstrap
- Systems are auto-registered, so just remove from scene

### Step 3: Enable Pure ECS System
```csharp
// In PlayerEntityBootstrap_PureECS.cs, ensure it's active
// System is auto-registered if file exists
```

### Step 4: Update Mesh/Material Loading

The pure ECS version currently uses:
```csharp
var mesh = CreateCapsuleMesh(); // Uses GameObject.CreatePrimitive
var material = CreatePlayerMaterial(); // Creates new material
```

**Production Approach:**
```csharp
// Load from Resources
var mesh = Resources.Load<Mesh>("Player/PlayerCapsule");
var material = Resources.Load<Material>("Player/PlayerMaterial");

// OR load from AssetBundle
// OR use procedural mesh generation
```

### Step 5: Clean Up Visual GameObjects

After migration, remove:
- Player Visual GameObject (no longer needed)
- Ground Visual GameObject (no longer needed)
- PlayerVisualSync component (no longer needed)

**Camera GameObject:**
- Keep it! Unity's camera still needs a GameObject
- But it can follow the camera entity instead

## Comparison

| Feature | Minimal Hybrid | Pure ECS |
|---------|---------------|----------|
| **GameObjects** | Visuals only | Camera only |
| **Package Required** | None | Entities.Graphics |
| **Setup Complexity** | Low | Medium |
| **Performance** | Good | Excellent (instancing) |
| **Scalability** | Good | Excellent (thousands) |
| **Debugging** | Easy (GameObjects visible) | Harder (Entity Inspector) |
| **Rendering** | Standard Unity | GPU Instancing |

## When to Use Each

### Use Minimal Hybrid If:
- ✅ Prototyping/early development
- ✅ Need immediate visual feedback
- ✅ Don't want package dependencies
- ✅ Debugging is important
- ✅ Small number of entities (< 100)

### Use Pure ECS If:
- ✅ Production-ready
- ✅ Large number of entities (1000+)
- ✅ Need maximum performance
- ✅ Want true pure ECS architecture
- ✅ OK with Entities.Graphics dependency

## Current Status

**Active:** Minimal Hybrid (`PlayerEntityBootstrap`)  
**Ready:** Pure ECS (`PlayerEntityBootstrap_PureECS`) - install package to use

## Code Structure

```
PlayerEntityBootstrap.cs              ← ACTIVE (minimal hybrid)
PlayerEntityBootstrap_PureECS.cs      ← READY (pure ECS, needs package)
PlayerVisualSync.cs                   ← Used by minimal hybrid
```

## Testing

### Test Minimal Hybrid:
1. Run game
2. Should see blue capsule (player) and green ground
3. WASD should move player
4. Camera should follow

### Test Pure ECS (after migration):
1. Install Entities.Graphics
2. Disable PlayerEntityBootstrap
3. Enable PlayerEntityBootstrap_PureECS
4. Run game
5. Should see same visuals but no GameObjects in Hierarchy
6. Better performance with many entities

## Troubleshooting

### "Unity.Rendering namespace not found"
- Install Entities.Graphics package
- Or use minimal hybrid approach

### "No visuals appearing (Pure ECS)"
- Check Entities.Graphics is installed
- Verify mesh/material are loaded correctly
- Check RenderBounds component exists
- Ensure camera entity has MainCameraTag

### "Visuals not syncing (Hybrid)"
- Check PlayerVisualSync component exists
- Verify targetEntity is set
- Check entity exists in world
- Look for errors in console

## Next Steps

1. ✅ **Now:** Use minimal hybrid (works immediately)
2. 🔄 **Later:** Install Entities.Graphics when ready
3. 🔄 **Migrate:** Switch to pure ECS for production
4. 🔄 **Optimize:** Load meshes from Resources/AssetBundles

