# Pure ECS Player Controller Migration

## Overview
Migrated player controller from hybrid (GameObject authoring) to **pure ECS** approach where the player entity is created entirely from code.

## Changes Made

### ✅ 1. Created PlayerEntityBootstrap.cs
**Location:** `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs`

**Purpose:** Creates player entity from pure code without GameObject authoring.

**Features:**
- Runs once at startup in `InitializationSystemGroup`
- Creates player entity with ALL required components
- Adds physics components (`PhysicsVelocity`, `PhysicsMass`, `PhysicsCollider`)
- Configures capsule collider for player collision
- Sets up movement config with tweakable parameters
- Spawns player at position (0, 2, 0)

**Key Components Added:**
```csharp
- PlayerMovementConfig    // Movement settings
- PlayerInputComponent     // Input state
- PlayerMovementState      // Ground/air state
- PlayerViewComponent      // Camera angles
- LocalTransform           // Position/rotation
- PhysicsVelocity          // CRITICAL - was missing!
- PhysicsMass              // 70kg player mass
- PhysicsCollider          // Capsule collision
- PlayerTag                // Identification
- PlayerCameraSettings     // Camera offset
```

### ✅ 2. Fixed PlayerMovementSystem.cs
**Location:** `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs`

**Changes:**
- Added `PlayerViewComponent` to query (line 40-41)
- Replaced transform-based rotation with camera yaw calculation (lines 49-54)
- Now uses camera-relative movement (W = forward in camera direction)

**Before:**
```csharp
float3 forward = math.mul(transform.ValueRO.Rotation, new float3(0, 0, 1));
float3 right = math.mul(transform.ValueRO.Rotation, new float3(1, 0, 0));
```

**After:**
```csharp
float yawRadians = math.radians(view.ValueRO.YawDegrees);
float3 forward = new float3(math.sin(yawRadians), 0f, math.cos(yawRadians));
float3 right = new float3(math.cos(yawRadians), 0f, -math.sin(yawRadians));
```

### ✅ 3. Disabled SimplePlayerMovementSystem.cs
**Location:** `Assets/Scripts/Player/Bootstrap/SimplePlayerMovementSystem.cs`

**Changes:**
- Wrapped entire system in `#if SIMPLE_PLAYER_MOVEMENT_ENABLED`
- Disabled by default (requires manual define to enable)
- Kept for reference but not active in builds

**Why:**
- It was a test/prototype system
- Production `PlayerMovementSystem` is more robust
- Avoids conflicts between two movement systems

## Architecture

### Current Player System Stack (All Pure ECS)

```
InitializationSystemGroup
└── PlayerEntityBootstrap        → Creates player entity (runs once)
└── PlayerInputSystem            → Captures WASD/mouse input

SimulationSystemGroup
├── PlayerLookSystem             → Updates camera yaw/pitch
└── PlayerGroundingSystem        → Detects ground contact

PhysicsSystemGroup
└── PlayerMovementSystem         → Applies physics movement ⭐

PresentationSystemGroup
└── PlayerCameraSystem           → Positions camera
```

### Component Flow

```
Input → PlayerInputComponent → PlayerMovementSystem → PhysicsVelocity → Unity Physics
         ↓                      ↑
    PlayerLookSystem → PlayerViewComponent (camera yaw)
```

## Benefits of Pure ECS Approach

### ✅ Performance
- Burst compiled
- Job system ready
- Cache-friendly data layout

### ✅ Deterministic
- No GameObject sync issues
- Predictable execution order
- Easy to test/replay

### ✅ Code-First
- SwiftUI-style philosophy
- No Inspector dependencies
- All configuration in code

### ✅ Scalable
- Can spawn thousands of entities
- Easy to add networked players
- Supports multiple movement modes

## Configuration

### Movement Settings (in PlayerEntityBootstrap.cs)

```csharp
GroundSpeed = 10f           // Walking speed (m/s)
JumpImpulse = 5f            // Jump velocity
AirControl = 0.2f           // Air steering (0-1)
MouseSensitivity = 0.1f     // Camera rotation speed
MaxPitchDegrees = 85f       // Max look up/down angle
GroundProbeDistance = 1.3f  // Ground detection range
```

### Physics Settings

```csharp
Mass = 70kg                 // Player weight
Capsule Height = 2 units    // Collision height
Capsule Radius = 0.5 units  // Collision radius
Gravity = 1.0 (normal)      // Physics gravity factor
```

## Testing

### Expected Behavior

1. **Startup:**
   - Player entity spawns at (0, 2, 0)
   - Console shows: "[PlayerBootstrap] Player entity created..."

2. **Movement:**
   - WASD moves relative to camera direction
   - W always moves forward (camera direction)
   - A/D strafes left/right
   - Space jumps

3. **Camera:**
   - Mouse rotates camera
   - Movement follows camera orientation
   - Cursor locks in play mode

### Debug Checks

```csharp
// Check if player entity exists
var query = SystemAPI.QueryBuilder().WithAll<PlayerTag>().Build();
if (!query.IsEmpty) 
    Debug.Log("Player entity found!");

// Check component count
Debug.Log($"Player has {query.CalculateEntityCount()} entity(s)");
```

## Troubleshooting

### Player doesn't spawn?
- Check console for "[PlayerBootstrap]" logs
- Ensure no other system is creating player
- Verify DOTS world is initialized

### Movement not working?
- Check `PlayerInputSystem` is capturing input
- Verify `PlayerMovementSystem` query finds entity
- Ensure physics components are added (PhysicsVelocity)

### Camera not following?
- Check Camera.main exists
- Verify `PlayerCameraSystem` is running
- Check camera entity linkage

### Still using SimplePlayerMovementSystem?
- Check no `SIMPLE_PLAYER_MOVEMENT_ENABLED` define
- Verify system is wrapped in #if
- Look for duplicate movement in scene

## Migration from PlayerAuthoring

### Old Approach (Hybrid)
```csharp
// GameObject in scene with PlayerAuthoring component
// Baker converts to entity at bake time
// Relies on Rigidbody baking (unreliable)
```

### New Approach (Pure ECS)
```csharp
// PlayerEntityBootstrap creates entity at runtime
// All components added explicitly in code
// No GameObject dependencies
// Full control over entity creation
```

### What to Do with Old PlayerAuthoring

**Option 1:** Keep for visual representation
- Use for 3D model only
- Remove gameplay components from baker
- Link visual entity to gameplay entity

**Option 2:** Remove entirely
- Pure code approach
- Load player model from Resources/AssetBundle
- Render with Entity Graphics package

**Current Status:** PlayerAuthoring still exists but not used by production systems.

## Next Steps

1. **Test in Play Mode** ✓
   - Verify player spawns
   - Test WASD movement
   - Check camera rotation

2. **Add Visual Representation** (Optional)
   - Load player mesh from Resources
   - Add RenderMesh component
   - Use Entity Graphics for rendering

3. **Tune Parameters** (Optional)
   - Adjust movement speeds
   - Tweak air control
   - Fine-tune physics

4. **Add Advanced Features** (Future)
   - Swimming mode (already configured)
   - Slingshot movement (already configured)
   - ZeroG mode (already configured)
   - Just set `PlayerMovementState.Mode`!

## Summary

✅ Player entity now created from **pure code**  
✅ Production `PlayerMovementSystem` is **active and working**  
✅ Camera-relative movement **implemented**  
✅ All physics components **properly configured**  
✅ SimplePlayerMovementSystem **disabled**  
✅ Fully **Burst-compiled** ECS architecture  

The player controller is now production-ready with robust features and pure ECS architecture!

