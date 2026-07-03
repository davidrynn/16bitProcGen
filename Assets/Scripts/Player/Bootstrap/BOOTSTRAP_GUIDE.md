# Player Camera Bootstrap - Complete Guide

## Table of Contents
1. [Quick Start](#quick-start)
2. [Which Bootstrap to Use](#which-bootstrap-to-use)
3. [Scene Setup](#scene-setup)
4. [Understanding DOTS Rendering](#understanding-dots-rendering)
5. [Physics Integration](#physics-integration)
6. [Movement](#movement)
7. [Customization](#customization)
8. [Troubleshooting](#troubleshooting)
9. [Next Steps](#next-steps)

---

## Quick Start

### 30-Second Setup

1. **Create or open a scene**
2. **Create Empty GameObject** in Hierarchy
3. **Add Component** → `PlayerCameraBootstrap_WithVisuals`
4. **Verify** `Time.fixedDeltaTime` is set (defaults to 0.0167s / 60Hz)
5. **Press Play** ▶️

**You'll see:**
- 🟦 Blue capsule (player) on green ground
- 📷 Camera following from behind
- **WASD** - Move player
- **Space** - Jump

---

## Which Bootstrap to Use

### Version Comparison

| Feature | `PlayerCameraBootstrap` | `PlayerCameraBootstrap_WithVisuals` |
|---------|------------------------|-------------------------------------|
| Creates Entities | ✅ Yes | ✅ Yes |
| Visible in Entity Debugger | ✅ Yes | ✅ Yes |
| Visible in Game View | ❌ No | ✅ Yes |
| Camera GameObject | ❌ No | ✅ Yes |
| Player Visual | ❌ No | ✅ Yes (blue cube) |
| Physics (Ground + Colliders) | ❌ No | ✅ Yes |
| Systems Work | ✅ Yes | ✅ Yes |
| **Use Case** | Tests, pure data | Demos, development, testing |

### Decision Tree

```
Do you need to SEE the player in Game view?
│
├─ NO ───→ Use PlayerCameraBootstrap
│          (Automated tests, data-only scenarios)
│
└─ YES ──→ Use PlayerCameraBootstrap_WithVisuals
           (Development, debugging, demonstrations)
```

### Common Scenarios

- **"I want to test camera follow visually"** → Use `_WithVisuals`
- **"I'm writing automated tests"** → Use basic version (or spawn entities in test)
- **"I want to show this to my team"** → Use `_WithVisuals`
- **"This is for production"** → Neither - use Entities Graphics with baked prefabs

---

## Scene Setup

### Step-by-Step Checklist

#### 1. Create a New Scene
- `File → New Scene` → Choose "Empty" template
- Save it (e.g., `Assets/Scenes/PlayerTest.unity`)

#### 2. Clean the Scene
Remove unnecessary GameObjects:
- ❌ Delete any SubScenes
- ❌ Delete any authoring GameObjects
- ❌ Delete default Main Camera (if present)
- ✅ Keep Directional Light (optional - for basic lighting)

#### 3. Add the Bootstrap GameObject
- Right-click in Hierarchy → `Create Empty`
- Rename to `"Bootstrap"`
- Add Component → `PlayerCameraBootstrap_WithVisuals` (or basic version)

#### 4. Save and Press Play
- Save the scene (`Ctrl+S` / `Cmd+S`)
- Press Play ▶️
- Check Console for confirmation logs

#### 5. Verify Entities Were Created
- Open `Window → Entities → Hierarchy`
- You should see:
  - Player (Runtime) entity
  - MainCamera (Runtime) entity
  - Ground Plane (Runtime) entity (if using `_WithVisuals`)

---

## Understanding DOTS Rendering

### Why You Don't See Anything (Basic Bootstrap)

The basic `PlayerCameraBootstrap` creates **entity data** but not **visual representation**.

**What it creates (DATA):**
```
Player Entity:
  - PlayerTag
  - LocalTransform (position, rotation, scale)
  - LocalToWorld (computed matrix)

Camera Entity:
  - MainCameraTag
  - LocalTransform
  - LocalToWorld
```

**What's missing for rendering:**
```
1. Camera GameObject (to render the Game view)
2. Rendering components on player (to see the player)
```

### Two Types of "Cameras"

#### 1. Camera GameObject (Traditional Unity)
- **What:** A GameObject with a Camera component
- **Purpose:** Renders to the Game view window
- **Required:** YES - Unity needs at least one Camera GameObject to render

#### 2. Camera Entity (DOTS)
- **What:** An entity with transform data
- **Purpose:** Stores position/rotation data for systems
- **Can Render:** NO - entities don't render on their own

**Key Point:** The camera **entity** holds position/rotation updated by `CameraFollowSystem`. But you need a Camera **GameObject** to actually render the view.

### Solutions

#### Option 1: Use `PlayerCameraBootstrap_WithVisuals` (Easiest)
- Creates player + camera entities
- Creates Camera GameObject that syncs with camera entity
- Creates visual cube for player
- **Result:** You see everything working immediately

#### Option 2: Add Camera GameObject Manually
If you only want to see the scene (no player visuals):
1. Keep basic `PlayerCameraBootstrap`
2. Add a Camera GameObject that syncs to camera entity
3. See `RENDERING_EXPLANATION.md` for code example

#### Option 3: Pure DOTS Rendering (Production)
- Install **Entities Graphics** package
- Create prefabs with rendering components
- Instantiate as entities
- See `Assets/Scripts/DOTS/WFC/DungeonRenderingSystem.cs` for examples

---

## Physics Integration

### What `PlayerCameraBootstrap_WithVisuals` Includes

#### Ground Plane Entity (Static Physics)
- **PhysicsCollider** - Box collider (20x0.1x20 by default)
- **Purpose:** Floor for player to stand on
- **Configurable:** Ground position, size, visuals

#### Player Physics Components (Dynamic)
- **PhysicsCollider** - Capsule (2m tall, 0.5m radius)
- **PhysicsVelocity** - Movement and rotation velocity
- **PhysicsMass** - 70kg by default
- **PhysicsGravityFactor** - 1.0 (normal Earth gravity)
- **Purpose:** Full physics simulation with collision detection

### Physics Simulation Flow

```
1. Start() → Entities Created
   ├─ Ground Plane (static box collider)
   └─ Player (dynamic capsule collider)

2. Unity Physics Systems Run (automatic)
   ├─ Gravity pulls player downward
   ├─ Collision detection (player vs ground)
   ├─ Collision response (player stops at ground)
   └─ PhysicsVelocity updated

3. Transform Systems Run
   └─ LocalToWorld updated from physics

4. LateUpdate() → GameObjects Synced
   ├─ Camera GameObject → Camera Entity transform
   └─ Player Visual → Player Entity transform
```

### Quick Physics Tests

#### Test 1: Gravity
1. Set `Player Start Position.y = 10` in Inspector
2. Press Play
3. **Expected:** Player falls and lands on ground

#### Test 2: Jump
1. Press Play
2. Press **Space** repeatedly
3. **Expected:** Player jumps when on ground

#### Test 3: Walk Off Edge
1. Press Play
2. Hold **W** for 5 seconds
3. **Expected:** Player walks off edge and falls (ground is finite 20x20)

---

## Movement

The player has physics, and `SimplePlayerMovementSystem` provides basic controls:

### Controls
- **W** - Move forward
- **S** - Move backward
- **A** - Move left
- **D** - Move right
- **Space** - Jump (when grounded)

### Adding Custom Movement

Your project has `PlayerInputSystem` and `PlayerMovementSystem` for more advanced movement. To use them:
1. Add required input components to player entity
2. See `Assets/Scripts/Player/Systems/` for implementation details

---

## Customization

### Inspector Settings

| Setting | Default | Purpose |
|---------|---------|---------|
| **Initial Positions** | | |
| Player Start Position | (0, 1, 0) | Where player spawns |
| Camera Start Position | (0, 3, -4) | Where camera spawns |
| **Visual Representation** | | |
| Create Player Visuals | ✓ | Show blue cube for player |
| Player Mesh | (none) | Custom mesh (optional) |
| Player Material | (none) | Custom material (optional) |
| **Ground Plane** | | |
| Ground Position | (0, 0, 0) | Ground center position |
| Ground Size | (20, 20) | Width and depth in meters |
| Create Ground Visuals | ✓ | Show green plane |
| **Physics** | | |
| Player Mass | 70 | Weight in kilograms |
| Player Height | 2 | Capsule collider height |
| Player Radius | 0.5 | Capsule collider radius |

### Common Adjustments

#### Make Player Heavier/Lighter
- Change `Player Mass` (kg)
- Heavier = slower to accelerate
- Lighter = more responsive, floatier

#### Make Ground Bigger
- Increase `Ground Size` (meters)
- Example: 50x50 for larger area

#### Change Camera Distance
Edit `Assets/Scripts/Player/Systems/CameraFollowSystem.cs`:
```csharp
float3 followOffset = new float3(0f, 2f, -4f); // Adjust Y and Z
```

---

## Troubleshooting

### "I don't see anything in Game view"
- **Cause:** Using basic `PlayerCameraBootstrap` (data-only)
- **Fix:** Use `PlayerCameraBootstrap_WithVisuals` instead

### "Player falls through ground"
- **Cause:** Ground physics not set up or player spawned below ground
- **Fix:** Check `Player Start Position.y > 0` and verify ground entity has PhysicsCollider

### "Player doesn't fall (no gravity)"
- **Cause:** PhysicsGravityFactor = 0 or missing
- **Fix:** Check Entity Debugger - `PhysicsGravityFactor` should be 1.0

### "Camera doesn't follow"
- **Check:** Console for spawn logs
- **Verify:** Both entities exist in Entity Debugger
- **Ensure:** `CameraFollowSystem` is running (`Window → Entities → Systems`)

### "I don't see anything in Edit Mode"
- **This is expected!** Entities spawn at runtime, not in Edit Mode
- Press Play to see them created

### "Can't move player"
- **Cause:** Movement system may not be active or input not detected
- **Fix:** Verify `SimplePlayerMovementSystem` is running, try WASD keys
- **Manual test:** Use Entity Debugger to set `PhysicsVelocity.Linear` values

### "Player rotates/tumbles weirdly"
- **Cause:** PhysicsMass doesn't prevent rotation by default
- **Fix:** Add `PhysicsDamping` with high angular damping, or constrain rotation in movement system

### "Console errors about physics"
- **Cause:** Unity Physics package not installed
- **Fix:** Ensure `Unity.Physics` package is in Package Manager

---

## Next Steps

### 1. Add Player Movement
- Create systems that modify player's `LocalTransform` based on input
- See `PlayerInputSystem` for keyboard/mouse example

### 2. Integrate with Terrain
- Connect to WFC dungeon generation
- Spawn entities from procedural systems

### 3. Add More Entities
- Spawn enemies, items, projectiles
- Extend bootstrap with additional spawn methods

### 4. Production Rendering
- Set up Entities Graphics for proper DOTS rendering
- Create baked prefabs with meshes/materials
- See `Assets/Docs/ArtAndDOTS_Pipeline.md`

### 5. Run Tests
- See `Assets/Scripts/Player/Test/CameraFollowSanityTest.cs`
- Tests use the same entity spawning pattern

---

## How It Works

### The Bootstrap Pattern

`PlayerCameraBootstrap.cs` is a minimal MonoBehaviour that:
- Runs once at `Start()`
- Gets the default DOTS World
- Spawns entities with required components
- No baking, no subscenes, pure runtime creation

### Advantages of This Approach

✅ **Pure DOTS** - No GameObject/ECS hybrid confusion  
✅ **Fast Iteration** - No baking delays  
✅ **Testable** - Same pattern as runtime tests  
✅ **Minimal Setup** - One GameObject with script  
✅ **Production-Ready** - Matches runtime entity spawning  

### When to Use Different Approaches

| Approach | Best For |
|----------|----------|
| **Pure Code Bootstrap** | Runtime-spawned entities, testing, procedural generation |
| **Authoring + SubScenes** | Prefabs with complex visuals, static layouts, baked assets |
| **Entities Graphics** | Production rendering, thousands of entities, performance |

---

## Architecture Diagram

```
Bootstrap GameObject
  └─ PlayerCameraBootstrap_WithVisuals
        │
        ├─ Creates Ground Entity
        │    ├─ LocalTransform (0, 0, 0)
        │    ├─ PhysicsCollider (Box 20x0.1x20)
        │    └─ PhysicsWorldIndex
        │
        ├─ Creates Player Entity
        │    ├─ PlayerTag
        │    ├─ LocalTransform (0, 1, 0)
        │    ├─ PhysicsCollider (Capsule)
        │    ├─ PhysicsVelocity
        │    ├─ PhysicsMass (70kg)
        │    ├─ PhysicsGravityFactor (1.0)
        │    └─ PhysicsWorldIndex
        │
        └─ Creates Camera Entity
             ├─ MainCameraTag
             └─ LocalTransform (0, 3, -4)

Systems (Auto-Run):
  ├─ Unity.Physics.PhysicsSystemGroup → Simulates physics
  ├─ CameraFollowSystem → Moves camera to follow player
  └─ SimplePlayerMovementSystem → Reads WASD input
```

---

## Resources

- **Architecture rules:** `/CLAUDE.md` (Core Architecture Principles — Minimal Bootstrap pattern; supersedes the archived AI_Instructions.md)
- **Test Examples:** `Assets/Scripts/Player/Test/CameraFollowSanityTest.cs`
- **Camera Follow System:** `Assets/Scripts/Player/Systems/CameraFollowSystem.cs`
- **Unity Physics Manual:** Unity Documentation → Packages → Unity Physics
- **Entity Debugger:** `Window → Entities → Hierarchy`

---

**You're ready to start! Press Play and experiment! 🎉**






