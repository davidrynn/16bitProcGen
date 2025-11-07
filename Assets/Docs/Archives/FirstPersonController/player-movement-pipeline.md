# Player Movement Pipeline

## Overview
The player movement stack is split across a handful of DOTS systems that run every frame:

1. **Authoring/Baking** – `PlayerAuthoring` converts inspector values into DOTS components so runtime systems have configuration data.
2. **Input capture** – `PlayerInputSystem` samples Unity's Input System inside `PresentationSystemGroup` and writes user intent into `PlayerInputComponent`.
3. **Ground detection** – `PlayerGroundingSystem` (physics group) raycasts downward to keep `PlayerMovementState.IsGrounded` current.
4. **Movement application** – `PlayerMovementSystem` (physics group) reads intent and state, then pushes the resulting velocity into `PhysicsVelocity` so the physics step performs the actual motion.

This document details each step, the data they share, and how to extend or debug the pipeline.

## Update order at a glance
- `PresentationSystemGroup`
  - `PlayerInputSystem`
- `PhysicsSystemGroup`
  - `PlayerGroundingSystem`
  - `PlayerMovementSystem` (scheduled before `PhysicsSimulationGroup`)
- `PhysicsSimulationGroup`
  - Unity physics steps consume updated velocities

By executing input in the presentation phase and normalizing grounding before movement, physics always receives the freshest velocity decisions for the current frame.

## Core components

| Component | Owner | Purpose |
|-----------|-------|---------|
| `PlayerInputComponent` | Runtime player entities | Mutable buffer that stores the latest move, look, and jump intent for the frame. |
| `PlayerMovementConfig` | Runtime player entities | Read-only tuning values such as ground speed, air control, jump impulse, and probe distance. |
| `PlayerMovementState` | Runtime player entities | Tracks high-level locomotion mode, grounded flag, and cumulative fall time. Updated by grounding/movement systems. |
| `PlayerViewComponent` | Runtime player entities | Holds yaw/pitch angles for view-control systems. Not modified by movement, but included for completeness. |
| `PlayerCameraLink` | Runtime player entities | Links the player to a baked camera entity so view systems can drive camera transforms. |

All of these components are added during baking by `PlayerAuthoring`.

## PlayerAuthoring baker

`PlayerAuthoring` lives on the player prefab. During baking it:

- Adds `PlayerMovementConfig`, copying inspector values and clamping `GroundProbeDistance` to at least 0.1.
- Creates empty `PlayerInputComponent`, `PlayerMovementState`, and `PlayerViewComponent` structs with reasonable defaults (mode starts on ground, grounded flag false until grounding system confirms contact).
- Optionally links the associated camera via `PlayerCameraLink` if one is provided.

No runtime systems modify the configuration data; changes are expected to flow through authoring or other configuration pipelines.

## PlayerInputSystem

Runs each presentation frame and performs:

1. **Device validation** – obtains `Keyboard.current` and `Mouse.current`; aborts the update if either is unavailable to avoid null dereferences on unsupported platforms.
2. **Move vector accumulation** – maps WASD keys onto a planar `float2`. Opposing inputs cancel out, and the result is normalized when its magnitude exceeds 1 so diagonals do not exceed configured speed.
3. **Look delta capture** – reads raw mouse delta through `Mouse.delta.ReadValue()` and stores it directly so downstream view code can apply sensitivity and clamping.
4. **Jump edge detection** – calls `spaceKey.wasPressedThisFrame` to capture the rising edge of the jump input. The boolean is OR'd with the existing `JumpPressed` flag so intent persists until consumed.
5. **Component write-back** – iterates player entities and writes `Move`, `Look`, and `JumpPressed` into their `PlayerInputComponent`. `PlayerMovementConfig` is included in the query purely as a guard to ensure the entity was baked correctly.

Because the system lives in `PresentationSystemGroup`, the work happens before physics systems read the input, ensuring no frame of latency between user action and movement response.

## PlayerGroundingSystem

Executed in the physics group, before movement, to keep grounded state authoritative:

- Pulls the `PhysicsWorldSingleton` and performs a downward `RaycastInput` per player using the entity's world position (`LocalTransform.Position`).
- Uses the larger of `config.GroundProbeDistance` or 0.1 metres to avoid missing ground when the probe distance is too small.
- Assigns `movementState.IsGrounded` based on the raycast hit, resets `FallTime` on hit, and increments `FallTime` when airborne.
- Forces `movementState.Mode` back to `PlayerMovementMode.Ground` when grounded so higher-level systems can rely on consistent mode semantics.

The result feeds directly into `PlayerMovementSystem`, preventing mid-air jumps and keeping landing behaviour consistent.

## PlayerMovementSystem

Scheduled just before `PhysicsSimulationGroup`, it converts intent and grounding data into physics velocities:

1. **Query setup** – reads `PlayerMovementConfig`, `PlayerInputComponent`, `PlayerMovementState`, `LocalTransform`, and `PhysicsVelocity`.
2. **Desired direction** – projects the normalized move vector onto the entity's forward/right axes so movement follows the player's facing direction.
3. **Ground vs air handling** – if grounded, horizontal velocity snaps to the ground speed target for responsive control. If airborne, the system lerps toward that target using `config.AirControl` scaled by delta time, giving limited air steering.
4. **Jump impulse** – when `JumpPressed` is true and the entity is grounded, the system raises the Y component of velocity to at least `config.JumpImpulse`. It then clears the flag, marks the entity as airborne, and leaves vertical motion for physics to integrate.
5. **Velocity write-back** – stores the final linear velocity into `PhysicsVelocity`. Unity physics integrates the value during the subsequent simulation step.

Because `JumpPressed` is consumed inside this system, holding the jump key does not produce repeated impulses; the input system must detect another rising edge before the next jump.

## Data flow summary

```text
PlayerAuthoring (Bake time)
   └─ adds config/state/input components
PlayerInputSystem (Presentation)
   └─ samples devices → PlayerInputComponent
PlayerGroundingSystem (Physics)
   └─ raycasts → PlayerMovementState.IsGrounded
PlayerMovementSystem (Physics)
   └─ combines input + state + config → PhysicsVelocity
PhysicsSimulationGroup
   └─ integrates velocity → actual motion
```

## Extension ideas
- **Additional actions** – add fields to `PlayerInputComponent` (e.g., sprint, crouch, abilities) and extend the input system to populate them. Update `PlayerMovementSystem` or other consumers to react accordingly.
- **Airborne variants** – use `PlayerMovementState.Mode` to branch new behaviour such as slingshot or swimming. Existing systems already load the values from `PlayerMovementConfig` for future modes.
- **Input buffering** – replace the boolean jump flag with timestamps or counters if you need jump queuing or coyote time. Update `PlayerMovementSystem` to interpret the buffer appropriately.
- **Alternative devices** – detect `Gamepad.current` or other devices alongside keyboard/mouse while keeping the same component contract so physics code remains untouched.

## Testing & debugging tips
- Use the ECS compute shader smoke tests and in-editor play mode to verify that input, grounding, and movement remain in sync after changes.
- Watch `PlayerMovementState` and `PhysicsVelocity` via the Entities Hierarchy/Inspector to confirm that grounding flips before jump impulses fire.
- Enable the Input Debugger (`Window > Analysis > Input Debugger`) to ensure device events reach `PlayerInputSystem`.
- Add temporary gizmos or debug lines to visualize raycasts when adjusting `GroundProbeDistance` or collision layers.

With these systems working together, the player enjoys responsive ground movement, deterministic jumping, and a clean separation between raw input capture and physics-driven motion.
