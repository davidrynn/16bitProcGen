# First-Person Camera + Controller (Unity DOTS)

## Overview
A minimal, smooth **first-person camera and character controller** built with **Unity DOTS/Entities**. Features clean separation of data and systems, physics-based movement, and jitter-free camera follow optimized for low-poly procedural worlds.

## Architecture

### Components (Data-Only)
Located in `Scripts/Player/Components/PlayerComponents.cs`:

- **PlayerMovementConfig** - Movement parameters (speeds, jump force, mouse sensitivity, etc.)
- **PlayerInputComponent** - Current frame input (`Move`, `Look`, `JumpPressed`)
- **PlayerMovementState** - Current movement state (mode, grounded, fall time)
- **PlayerViewComponent** - Camera orientation (`YawDegrees`, `PitchDegrees`)
- **PlayerCameraLink** - Link to camera entity

### Systems (Logic)
Located in `Scripts/Player/Systems/`:

1. **PlayerInputSystem** (`InitializationSystemGroup`)
   - Captures WASD + mouse input
   - Manages cursor lock state (Escape to toggle)
   - Auto-locks cursor when entering play mode

2. **PlayerLookSystem** (`SimulationSystemGroup`, OrderFirst)
   - Processes mouse look input
   - Updates yaw/pitch angles with clamping
   - Rotates player entity (yaw only)
   - Runs BEFORE movement so rotation is ready

3. **PlayerGroundingSystem** (`PhysicsSystemGroup`, before PlayerMovementSystem)
   - Raycasts downward to detect ground contact
   - Updates `IsGrounded` state for jump logic

4. **PlayerMovementSystem** (`PhysicsSystemGroup`, before PhysicsSimulationGroup)
   - Translates input into physics velocities
   - Uses player rotation to determine forward/right directions
   - Handles ground movement, air control, and jumping
   - Integrates with Unity Physics

5. **PlayerCameraSystem** (`PresentationSystemGroup`, OrderLast)
   - Positions camera based on player position and view
   - Runs after physics to minimize jitter
   - Combines yaw + pitch for camera rotation
   - Updates both entity transform AND GameObject transform (hybrid approach)

### System Ordering (Clean Separation)
```
InitializationSystemGroup
└── PlayerInputSystem (capture input, cursor lock)

SimulationSystemGroup (OrderFirst)
└── PlayerLookSystem (update yaw/pitch, rotate player)

SimulationSystemGroup
└── PhysicsSystemGroup
    ├── PlayerGroundingSystem (detect ground)
    ├── PlayerMovementSystem (calculate velocities using rotation)
    └── PhysicsSimulationGroup (Unity physics step)

PresentationSystemGroup (OrderLast)
└── PlayerCameraSystem (position camera, no jitter)
    ├── Reads player position + view state
    ├── Updates entity LocalTransform
    └── Syncs to GameObject Transform (hybrid)
```

## Usage

### Basic Setup
1. **Create a player GameObject** with:
   - `PlayerAuthoring` component (set movement parameters)
   - `Rigidbody` (auto-added by authoring, or add manually)
   - `CapsuleCollider` (auto-added by authoring, or add manually)

2. **Create a camera GameObject** with:
   - `Camera` component
   - `CameraAuthoring` component (marks it as DOTS-compatible)

3. **Link the camera**:
   - In PlayerAuthoring inspector, drag the Camera GameObject to the `Player Camera` field

4. **Press Play**:
   - Cursor auto-locks
   - Use WASD to move, Space to jump, Mouse to look
   - Press Escape to unlock cursor

### Manual Setup (Subscene)
For a proper DOTS workflow using subscenes:

1. Create a Subscene named `FirstPersonDemo`
2. Add a **player entity**:
   - GameObject with `PlayerAuthoring`
   - Set initial position (e.g., `0, 2, 0`)
3. Add a **floor entity**:
   - Plane with static collider
   - `PhysicsShape` authoring component
4. Add **camera**:
   - Main Camera with `CameraAuthoring`
   - Link to player via `PlayerAuthoring.playerCamera` field
5. Bake and press Play

## Controls

| Input | Action |
|-------|--------|
| **W** | Move forward |
| **S** | Move backward |
| **A** | Move left |
| **D** | Move right |
| **Mouse** | Look around (yaw/pitch) |
| **Space** | Jump (when grounded) |
| **Escape** | Toggle cursor lock |

## Configuration

### Movement Parameters (PlayerAuthoring)

| Parameter | Default | Description |
|-----------|---------|-------------|
| Ground Speed | 10 | Movement speed on ground (m/s) |
| Jump Impulse | 5 | Upward velocity on jump (m/s) |
| Air Control | 0.2 | Steering responsiveness while airborne (0-1) |
| Mouse Sensitivity | 0.1 | Look rotation speed multiplier |
| Max Pitch Degrees | 85 | Vertical look clamp (prevents over-rotation) |
| Ground Probe Distance | 1.3 | Raycast length for ground detection |

### Future Modes (Planned)
- **Slingshot** - Launch player through destructible terrain
- **Swim** - Navigate liquid biomes
- **Zero-G** - Floating caverns with damped movement

## Editor Tools

Access via Unity menu: **Tools > FirstPerson**

- **Lock Cursor** - Manually lock cursor (useful when debugging)
- **Unlock Cursor** - Manually unlock cursor
- **Toggle Lock** - Quick toggle (or use Escape in play mode)

## Testing

### Runtime Tests
See `Scripts/Player/Test/PlayerMovementTestPlan.md` for comprehensive test cases.

**Quick validation**:
1. Open `Scenes/Test.unity` (or any scene with player setup)
2. Press Play
3. Verify:
   - WASD moves the player relative to view direction
   - Mouse rotates view (pitch clamped at ~85°)
   - Space jumps when grounded, not in air
   - Camera follows player with no jitter or lag
   - Escape toggles cursor visibility

### Debug Tools
- **PlayerMovementDebugger** - On-screen GUI showing entity state
- **Console Logs** - System initialization and entity detection
- **Systems Window** - Verify all four systems are running

### Test Prefab
Use `PlayerTestSetup.cs` for quick environment creation:
1. Add script to empty GameObject
2. Right-click component → "Setup Test Environment"
3. Auto-creates player, floor, camera, and debugger

## Known Issues & Pitfalls

### ✅ Resolved by Design
- **Camera jitter** - Solved by running PlayerCameraSystem in PresentationSystemGroup after TransformSystemGroup
- **Gimbal lock** - Avoided by quaternion composition (yaw + pitch)
- **Input delay** - Input captured in InitializationSystemGroup (earliest)
- **Cursor stuck** - Escape key toggles lock state

### ⚠️ Limitations
- **No collision pushback** - Player uses standard physics; may clip if moving too fast
- **Simple grounding** - Single raycast; may fail on steep slopes or edges
- **No crouching/prone** - Ground mode only (for now)
- **Cursor auto-lock** - Always locks on play start (by design for FPS feel)
- **Hybrid camera** - Camera must remain a GameObject (not pure ECS) for Unity's rendering pipeline

### 🔧 Troubleshooting

**No movement?**
- Check Console for errors
- Verify all four systems are running (Windows > Entities > Systems)
- Ensure Rigidbody and Collider exist on player
- Confirm ground exists below player (grounding system needs collision)

**No mouse look?**
- Verify cursor is locked (check Cursor.lockState in debugger)
- Ensure Input System package is installed
- Check camera is linked in PlayerAuthoring inspector
- Press Escape once to force lock state refresh

**Player falls through floor?**
- Add PhysicsShape authoring to floor GameObject
- Ensure floor has a collider in Play mode
- Check Physics Layer collision matrix (Project Settings)

**Camera too high/low?**
- Adjust `cameraOffset` in PlayerCameraSystem.cs (line 36)
- Default is `(0, 1.6, 0)` for standing eye height

**Camera not moving at all?**
- Verify the camera GameObject is linked in PlayerAuthoring inspector
- Check that CameraAuthoring is on the camera GameObject
- PlayerCameraSystem updates the GameObject Transform directly (hybrid approach required for Unity's rendering)

## Integration with Existing Project

This controller is designed for the 16-bit procedural generation project and integrates with:

- **Terrain System** - Uses Unity Physics for collision with generated terrain
- **WFC Dungeons** - Navigate through macro-tile layouts
- **DOTS Pipeline** - Fully entity-based, no MonoBehaviour singletons

### Performance
- **Burst-compiled** - All systems use [BurstCompile] for maximum performance
- **Zero GC allocations** - Pure ECS, no managed heap pressure
- **Scalable** - Handles multiple players (future multiplayer support)

## References & Inspiration

The implementation follows patterns from:
- [Unity CharacterController Samples](https://github.com/Unity-Technologies/CharacterControllerSamples) - System ordering and physics integration
- [Rival Documentation](https://github.com/Unity-Technologies/rival-documentation) - Ground detection and movement modes
- [Vertex Fragment DOTS Controller](https://github.com/ssell/UnityDotsCharacterController) - Input bridging and camera follow

## Version History

**v1.0** (Current)
- First-person camera with smooth yaw/pitch
- WASD movement relative to view direction
- Space to jump (grounded only)
- Cursor lock with Escape toggle
- Physics-based ground detection
- Jitter-free camera follow

**Planned**
- Additional movement modes (slingshot, swim, zero-G)
- Slope handling and ledge detection
- Player animations (if/when visual model added)
- Footstep audio integration
- Sprint/crouch mechanics

## Credits
Developed for the 16-bit Procedural Generation Unity DOTS project.
System architecture follows Unity DOTS best practices for Entities 1.2+.

---

**Questions or issues?** Check `Scripts/Player/Test/PlayerMovementTestPlan.md` for detailed diagnostics.

