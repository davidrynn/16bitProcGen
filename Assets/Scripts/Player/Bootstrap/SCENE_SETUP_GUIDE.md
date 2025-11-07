# Scene Setup Guide for Pure ECS Player Controller

## Quick Setup (Minimal Scene)

### Step 1: Create Empty Scene
1. **File > New Scene**
2. Choose **Empty Scene** (or Basic scene)
3. Save as `TestPlayerScene`

### Step 2: Ensure DOTS World is Available
Unity 6 automatically creates the DOTS world. The `PlayerEntityBootstrap` system will automatically register and run.

**No additional setup needed** - ECS systems auto-register!

### Step 3: Remove Conflicting Bootstraps (IMPORTANT!)

Check if you have any of these in your scene and **disable or remove them**:

#### ❌ Remove These (if present):
- Any GameObject with `PlayerCameraBootstrap_WithVisuals` component
- Any GameObject with `PlayerCameraBootstrap` component
- Any other player bootstrap scripts

**Why?** These create duplicate player entities and will conflict with `PlayerEntityBootstrap`.

#### ✅ Keep These (if you need them):
- Terrain systems (won't conflict)
- Other game systems

### Step 4: Verify Systems Are Registered

**Option A: Check Console on Play**
- Press Play
- Look for: `[PlayerBootstrap] Player entity created...`
- If you see this, everything is working!

**Option B: Check Entity Inspector**
- **Window > Entities > Hierarchy**
- You should see entities like:
  - "Player (ECS Synced)" entity
  - "Ground Plane (ECS)" entity

### Step 5: Test Movement

1. **Press Play**
2. **Look for:**
   - Blue capsule (player) at spawn position
   - Green ground plane below
   - Camera positioned behind player

3. **Controls:**
   - **WASD** - Move player (relative to camera)
   - **Mouse** - Rotate camera
   - **Space** - Jump (if implemented)

4. **Console should show:**
   ```
   [PlayerBootstrap] Player entity created at float3(0f, 2f, 0f) with all components (Pure ECS)
   [PlayerBootstrap] Ground plane entity created...
   [PlayerBootstrap] Created Main Camera GameObject
   [PlayerVisualSync] First successful sync! Entity X at...
   ```

## Minimal Scene Structure

```
Scene Hierarchy:
├── (Empty - no GameObjects needed!)
│
└── [ECS Systems Auto-Register]
    ├── PlayerEntityBootstrap ← Creates player
    ├── PlayerInputSystem ← Captures input
    ├── PlayerLookSystem ← Camera rotation
    ├── PlayerMovementSystem ← Movement
    └── PlayerCameraSystem ← Camera positioning
```

**Note:** Systems don't appear in Hierarchy - they're in the ECS world!

## Troubleshooting

### "No visuals appearing"

**Check:**
1. Console for errors
2. Entity Inspector (Window > Entities > Hierarchy) - do entities exist?
3. Scene view - is camera positioned correctly?

**Fix:**
- Make sure `PlayerEntityBootstrap` system is running (check console logs)
- Check Entity Inspector for player entity

### "Player doesn't move"

**Check:**
1. Console for `[PlayerInput]` logs
2. Console for `[PlayerMovement]` logs
3. Entity Inspector - does player have `PhysicsVelocity` component?

**Fix:**
- Check input system is capturing WASD
- Verify physics components exist on entity

### "Multiple players spawning"

**Check:**
- Do you have multiple bootstrap scripts in scene?
- Are there multiple `PlayerEntityBootstrap` systems?

**Fix:**
- Remove/disable other player bootstraps
- Only one `PlayerEntityBootstrap` should exist (auto-registered)

### "Camera doesn't follow"

**Check:**
1. Does `Main Camera` exist in scene?
2. Console for `[PlayerCamera]` logs

**Fix:**
- `PlayerEntityBootstrap` creates camera automatically
- If you have existing camera, remove it (bootstrap creates new one)

## Advanced: Scene with Existing Systems

If you have other systems (terrain, WFC, etc.):

### ✅ Safe to Keep:
- TerrainEntityManager
- WFC systems
- Any non-player systems

### ⚠️ Check for Conflicts:
- Other player controllers
- Other camera systems
- Physics settings

## Expected Behavior

### On Play Mode Start:
1. `PlayerEntityBootstrap` runs once
2. Creates player entity with all components
3. Creates ground plane entity
4. Creates camera GameObject
5. Creates visual GameObjects (blue capsule, green ground)
6. Systems start running

### During Play:
1. `PlayerInputSystem` captures WASD/mouse
2. `PlayerLookSystem` updates camera yaw
3. `PlayerMovementSystem` applies movement
4. `PlayerCameraSystem` positions camera
5. `PlayerVisualSync` syncs GameObject visuals

## Verification Checklist

After setup, verify:

- [ ] No conflicting bootstrap scripts in scene
- [ ] Console shows player entity creation logs
- [ ] Blue capsule visible in Scene/Game view
- [ ] Green ground plane visible
- [ ] Camera positioned correctly
- [ ] WASD moves player
- [ ] Mouse rotates camera
- [ ] Movement is camera-relative (W = forward in camera direction)

## Minimal Test Scene

**Absolute minimum setup:**
1. Empty scene
2. No GameObjects
3. Press Play
4. Everything auto-creates!

That's it! The pure ECS approach requires **zero scene setup** - everything is code-driven.

## Next Steps

Once working:
- ✅ Adjust movement speeds in `PlayerEntityBootstrap` config
- ✅ Replace visual primitives with proper models
- ✅ Add terrain/environment
- ✅ Tune physics settings

## Summary

**Pure ECS = Zero Scene Setup!**

- No GameObjects needed
- No manual component placement
- Systems auto-register
- Entities created at runtime
- Everything is code-driven

Just press Play and test! 🚀

