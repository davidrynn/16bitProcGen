# First-Person Controller Fix: System Ordering

## Problem
The player controller had two critical issues:
1. **Erratic movement** - Movement direction changed randomly with sustained keypresses
2. **Camera not following** - Camera remained static despite mouse input being captured

## Root Cause
The original `PlayerCameraSystem` was handling **both** mouse look AND camera positioning, and it ran in `PresentationSystemGroup` (AFTER physics/movement). This created a 1-frame delay:

```
Frame N:
1. PlayerInputSystem captures input
2. PlayerMovementSystem calculates movement using OLD rotation
3. Physics updates player position
4. PlayerCameraSystem updates rotation (too late!)

Frame N+1:
1. PlayerInputSystem captures input
2. PlayerMovementSystem uses rotation from Frame N (1-frame delay)
3. Player moves in unexpected direction
```

## Solution
Split the system into two separate concerns:

### 1. PlayerLookSystem (NEW)
- **Group**: `SimulationSystemGroup, OrderFirst`
- **Responsibility**: Process mouse look and rotate player
- **Timing**: Runs BEFORE movement systems
- **Benefits**: Rotation is ready when movement needs it

```csharp
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial struct PlayerLookSystem : ISystem
{
    // Updates yaw/pitch
    // Rotates player entity (yaw only)
    // Clears look input
}
```

### 2. PlayerCameraSystem (MODIFIED)
- **Group**: `PresentationSystemGroup, OrderLast`
- **Responsibility**: Position camera based on player state
- **Timing**: Runs AFTER all physics/movement
- **Benefits**: No jitter, reads final player position

```csharp
[UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
public partial struct PlayerCameraSystem : ISystem
{
    // Reads player position + view state
    // Calculates camera position/rotation
    // Updates GameObject transform (hybrid)
}
```

## Correct System Ordering

```
InitializationSystemGroup
└── PlayerInputSystem
    └── Captures WASD + mouse input

SimulationSystemGroup (OrderFirst)
└── PlayerLookSystem ★ NEW
    └── Updates player rotation from mouse input

SimulationSystemGroup/PhysicsSystemGroup
├── PlayerGroundingSystem
├── PlayerMovementSystem
│   └── Uses CURRENT rotation (no delay!)
└── PhysicsSimulationGroup

PresentationSystemGroup (OrderLast)
└── PlayerCameraSystem (modified)
    └── Positions camera based on final player state
```

## Key Changes

### Created
- `Scripts/Player/Systems/PlayerLookSystem.cs`
  - Burst-compiled for performance
  - Runs first in simulation group
  - Updates player rotation before movement

### Modified
- `Scripts/Player/Systems/PlayerCameraSystem.cs`
  - Removed look input handling
  - Removed player rotation updates
  - Only positions camera now
  - Added debug logging

- `Scripts/Player/Systems/PlayerInputSystem.cs`
  - Added debug logging
  - Log confirms input capture

## Debug Logging
Added logging to diagnose issues:
- `[PlayerInput] System created` - Confirms system initialization
- `[PlayerInput] Input captured: ...` - Shows first input capture
- `[PlayerCamera] Camera updated: ...` - Shows camera positioning
- `[PlayerCamera] Camera entity is null!` - Warns if link missing

## Testing
After this fix:
1. ✅ Movement direction matches view direction
2. ✅ Sustained keypresses maintain direction
3. ✅ Camera follows player smoothly
4. ✅ Mouse look updates camera view
5. ✅ No 1-frame delay or jitter

## Lessons Learned
1. **System ordering matters** - Movement needs rotation BEFORE it runs
2. **Separation of concerns** - Look vs Follow should be separate systems
3. **Group selection is critical** - Use SimulationSystemGroup for gameplay logic, PresentationSystemGroup for rendering
4. **Debug logging is essential** - Helps identify when systems aren't running

---

**Date**: October 23, 2025  
**Status**: Fixed ✅

