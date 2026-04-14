# Movement Technical Architecture Spec

**Status:** ACTIVE
**Last Updated:** 2026-04-14
**Owner:** Player Systems

---

## 1. Purpose

Map the movement design (see [MOVEMENT_PLANNING.md](MOVEMENT_PLANNING.md)) to concrete ECS components, systems, and update ordering. This document bridges game-design intent to DOTS implementation.

## 2. Scope

- New and modified ECS components for movement state, slingshot, glide, camera effects
- System graph: new systems, update groups, ordering constraints
- Camera effect resolver architecture (data writers + single resolver pattern)
- Input expansion for slingshot mechanics
- VFX integration approach for DOTS

## 3. Non-Goals

- Grapple mechanic implementation (Layer 2, post-MVP)
- Audio system architecture (deferred to Phase D)
- Character animation system (pose changes are placeholder scale/rotation until art pipeline is ready)
- UI/HUD for charge indicators (see Master Plan — separate feature)

## 4. Related Docs

- [MOVEMENT_PLANNING.md](MOVEMENT_PLANNING.md) — Design spec with parameter targets
- [AAA_MOVEMENT_CHECKLIST.md](AAA_MOVEMENT_CHECKLIST.md) — Playtest evaluation rubric
- [/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md](/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) — Bootstrap pattern
- [/CLAUDE.md](/CLAUDE.md) — DOTS coding conventions

---

## 4a. Files to Read Before Starting

Read these files before implementing any phase. They contain the existing code being extended, replaced, or referenced by this spec.

### Core player systems (being modified)

| File | Role | What changes |
|---|---|---|
| `Assets/Scripts/Player/Components/PlayerComponents.cs` | `PlayerMovementMode` enum, `PlayerMovementState`, `PlayerInputComponent` definitions | Enum values renamed, `Velocity` field added to state, slingshot fields added to input |
| `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs` | Ground/air movement logic | New `MovementMode` branches for Ballistic/Gliding air control rates |
| `Assets/Scripts/Player/Systems/PlayerInputSystem.cs` | Reads `Mouse.current` / `Keyboard.current` | Add LMB+RMB hold detection, drag accumulation, release event |
| `Assets/Scripts/Player/Systems/PlayerGroundingSystem.cs` | Ground detection, sets `IsGrounded` and `Mode` | `PlayerMovementMode.Ground` → `Grounded` rename |

### Camera systems (being replaced)

| File | Role | What changes |
|---|---|---|
| `Assets/Scripts/Player/Systems/PlayerCameraSystem.cs` | First-person camera driver | Replaced by `CameraEffectResolverSystem` |
| `Assets/Scripts/Player/Systems/CameraFollowSystem.cs` | Simple third-person test fallback | No changes, remains test-only |
| `Assets/Scripts/Player/Systems/PlayerCinemachineCameraSystem.cs` | Cinemachine bridge (unused) | Optional: drive via resolver instead of direct |

### Bootstrap (being modified)

| File | Role | What changes |
|---|---|---|
| `Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap_WithVisuals.cs` | Camera entity creation | Add `CameraEffectState`, `CameraEffectConfig`, `SlingshotConfig` components |
| `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs` | Player entity creation | `PlayerMovementMode.Ground` → `Grounded` |
| `Assets/Scripts/Player/Bootstrap/PlayerVisualSync.cs` | Syncs GameObject to ECS entity position | Reference pattern for VFX bridge (Section 11) |

### Tests (regression — must still pass after enum migration)

| File | Role |
|---|---|
| `Assets/Scripts/Player/Test/PlayerMovementAirPathPlayModeTests.cs` | Air movement tests — references `Ground` and `Slingshot` enum values |
| `Assets/Scripts/Player/Test/PlayerWallContactCommandPlayModeTests.cs` | Wall contact tests — references `Ground` and `Slingshot` enum values |

### Other files affected by enum rename

| File | Values referenced |
|---|---|
| `Assets/Scripts/Player/Authoring/PlayerAuthoring.cs` | `PlayerMovementMode.Ground` |
| `Assets/Scripts/DOTS/Terrain/Diagnostics/TerrainWallReproBootstrap.cs` | `PlayerMovementMode.Ground` |

### Enum migration summary

Rename only — no logic changes required:

- `PlayerMovementMode.Ground` → `PlayerMovementMode.Grounded` (10 references across 6 files)
- `PlayerMovementMode.Slingshot` → `PlayerMovementMode.SlingshotCharging` (2 references across 2 files)
- Remove `Swim` and `ZeroG` (0 references found — safe to delete)

---

## 5. MovementMode Enum Migration

### Current (to be replaced)

```csharp
public enum PlayerMovementMode : byte
{
    Ground = 0,
    Slingshot = 1,
    Swim = 2,
    ZeroG = 3
}
```

### Target

```csharp
public enum PlayerMovementMode : byte
{
    Grounded = 0,
    SlingshotCharging = 1,
    Ballistic = 2,
    GlideCharging = 3,
    Gliding = 4,
    ThermalBoost = 5,
    Grappling = 6        // Layer 2, post-MVP
}
```

`Swim` and `ZeroG` are removed from the traversal enum. If needed later they become a separate movement-context enum, not part of the traversal state machine.

---

## 6. New Components

### SlingshotChargeState

```csharp
/// <summary>
/// Tracks active slingshot charge. Added when charge begins, removed on release or cancel.
/// </summary>
public struct SlingshotChargeState : IComponentData
{
    public float ChargeNormalized;   // 0..1
    public float2 DragDelta;        // accumulated mouse drag in pixels
    public float3 AimDirection;     // world-space launch direction (opposite of drag)
    public float ChargeStartTime;   // elapsed time at charge start
}
```

### GlideState

```csharp
/// <summary>
/// Tracks active glide. Added when glide deploys, removed on landing.
/// </summary>
public struct GlideState : IComponentData
{
    public float GlideElapsed;          // seconds since glide activated
    public float HorizonBlendProgress;  // 0..1, camera horizon stabilization progress
}
```

### CameraEffectState

```csharp
/// <summary>
/// Single source of truth for all camera effect parameters.
/// Written by multiple movement-feedback systems, consumed by CameraEffectResolverSystem.
/// </summary>
public struct CameraEffectState : IComponentData
{
    public float TargetFOV;
    public float TargetDistance;       // third-person orbit distance
    public float3 PositionOffset;     // dolly / pull-back offset
    public float3 ShakeOffset;        // additive screen shake
    public float ShakeDecayRate;      // how fast shake returns to zero
    public float Damping;             // position smoothing rate
    public float RotationDamping;     // rotation smoothing rate
    public bool HorizonStabilize;     // pitch drifts toward horizon
    public float CameraDip;           // vertical dip (landing impact)
}
```

### CameraEffectConfig

```csharp
/// <summary>
/// Tunable constants for camera effects, set once at bootstrap.
/// </summary>
public struct CameraEffectConfig : IComponentData
{
    public float BaseFOV;                // 60
    public float BaseDistance;           // 4.0
    public float3 BasePivotOffset;      // (0, 1.5, 0)

    // Slingshot charge
    public float ChargeDistanceAdd;     // 2.5
    public float ChargeFOVReduce;       // 5
    public float ChargeShakeMin;        // 0.01
    public float ChargeShakeMax;        // 0.06

    // Ballistic
    public float LaunchFOVPunch;        // 12
    public float LaunchFOVDecayRate;    // 3.0 (per second)
    public float SpeedFOVScale;         // 0.15
    public float SpeedFOVThreshold;     // 15
    public float SpeedFOVMax;           // 12
    public float BallisticDistanceAdd;  // 1.5

    // Glide
    public float GlideFOVAdd;           // 3
    public float GlideDistanceAdd;      // 0.5

    // Thermal
    public float ThermalFOVAdd;         // 4

    // Landing
    public float LandingShakeScale;     // 0.01 per m/s
    public float LandingShakeMax;       // 0.20
    public float LandingFOVDip;         // 3
    public float LandingCameraDipMax;   // 0.8

    // Damping per state
    public float GroundedDamping;       // 12
    public float ChargeDamping;         // 8
    public float BallisticDamping;      // 6
    public float GlideDamping;          // 14
    public float ThermalDamping;        // 10
}
```

### SlingshotConfig

```csharp
/// <summary>
/// Tunable constants for slingshot mechanics, set once at bootstrap.
/// </summary>
public struct SlingshotConfig : IComponentData
{
    public float MaxForce;              // 55
    public float CurveExponent;         // 1.8
    public float MaxDragDistance;        // 300 pixels
    public float MinLaunchThreshold;    // 0.15
    public float CustomGravity;         // 22
    public float GroundFriction;        // 0.94
    public float AirControlBallistic;   // 0.25
}
```

### GlideConfig

```csharp
/// <summary>
/// Tunable constants for glide mechanics, set once at bootstrap.
/// </summary>
public struct GlideConfig : IComponentData
{
    public float GlideChargeTime;            // 0.45 seconds hold to deploy
    public float MinGlideHeight;             // 6 m — below this, glide cannot activate
    public float GlideFallSpeed;             // -5.5 — target vertical velocity during glide
    public float GlideForwardPreservation;   // 0.96 — horizontal speed multiplier per frame
    public float AirControlGlide;            // 0.35 — steering rate during glide
    public float MaxGlideDuration;           // 9 seconds — auto-cancel safety
}
```

### ThermalConfig

```csharp
/// <summary>
/// Tunable constants for thermal column mechanics, set once at bootstrap.
/// </summary>
public struct ThermalConfig : IComponentData
{
    public float VerticalBoostAcceleration;       // 15 — m/s² upward
    public float MaxUpwardVelocity;               // 12 — velocity.y clamp
    public float HorizontalVelocityMultiplier;    // 0.97 — slight horizontal reduction
}
```

### LandingConfig

```csharp
/// <summary>
/// Tunable constants for landing detection and feedback, set once at bootstrap.
/// </summary>
public struct LandingConfig : IComponentData
{
    public float SlideThresholdHorizontalSpeed;   // 8 — above this, landing is a slide
    public float HardLandingVerticalSpeed;         // 12 — above this, hard landing effects trigger
    public float DustBurstMinSpeed;                // 5 — minimum speed for dust particles
    public float DustBurstMaxRadius;               // 3.0 — dust radius at terminal velocity
    public float LandingMomentumPreservation;      // 0.94 — velocity.xz *= this on landing
}
```

### LandingImpactEvent (enableable component)

```csharp
/// <summary>
/// Fires on the frame the player transitions from airborne to grounded.
/// Consumed by camera and VFX systems, then disabled.
/// </summary>
public struct LandingImpactEvent : IComponentData, IEnableableComponent
{
    public float VerticalSpeed;     // abs(velocity.y) at impact
    public float HorizontalSpeed;   // horizontal speed at impact
}
```

---

## 7. Modified Components

### PlayerInputComponent (expanded)

```csharp
public struct PlayerInputComponent : IComponentData
{
    public float2 Move;
    public float2 Look;
    public bool JumpPressed;

    // Slingshot additions
    public bool SlingshotHeld;       // LMB + RMB both held
    public float2 SlingshotDrag;     // mouse delta accumulated during charge
    public bool SlingshotReleased;   // release event this frame
}
```

### PlayerMovementState (expanded)

```csharp
public struct PlayerMovementState : IComponentData
{
    public PlayerMovementMode Mode;
    public bool IsGrounded;
    public float FallTime;
    public float3 PreviousPosition;
    public float3 Velocity;          // cached copy of PhysicsVelocity.Linear
}
```

`Velocity` is written each frame by `MovementStateBookkeepingSystem` (SimulationSystemGroup, runs first). This lets camera feedback systems and `LandingDetectionSystem` read speed without requiring `PhysicsVelocity` access, which would force a PhysicsSystemGroup dependency.

---

## 8. System Graph

### Update Order

```
InitializationSystemGroup
  └─ PlayerInputSystem (expanded: LMB+RMB detection, drag accumulation)

PhysicsSystemGroup
  ├─ PlayerGroundingSystem          [existing, minor modifications]
  ├─ SlingshotChargeSystem          [NEW]
  ├─ SlingshotLaunchSystem          [NEW]
  ├─ PlayerMovementSystem           [existing, extended for ballistic/glide/thermal]
  ├─ GlideSystem                    [NEW]
  └─ ThermalColumnSystem            [NEW]

SimulationSystemGroup
  ├─ MovementStateBookkeepingSystem  [NEW - caches Velocity, resets CameraEffectState]
  ├─ LandingDetectionSystem         [NEW - fires LandingImpactEvent]
  ├─ CameraChargeFeedbackSystem     [NEW - writes CameraEffectState during charge]
  ├─ CameraSpeedFeedbackSystem      [NEW - writes CameraEffectState based on velocity]
  ├─ CameraLandingFeedbackSystem    [NEW - writes CameraEffectState on landing]
  └─ CameraGlideFeedbackSystem      [NEW - writes CameraEffectState during glide]

PresentationSystemGroup
  └─ CameraEffectResolverSystem     [NEW - reads CameraEffectState, applies to camera]
      (OrderLast = true, replaces current PlayerCameraSystem role)
```

### System Descriptions

#### SlingshotChargeSystem

- **Runs in:** PhysicsSystemGroup, after PlayerGroundingSystem
- **Requires:** PlayerMovementState (Grounded), PlayerInputComponent (SlingshotHeld)
- **Behavior:** When player is grounded and SlingshotHeld is true, transitions Mode to SlingshotCharging. Accumulates DragDelta from mouse input. Computes ChargeNormalized using the power curve. Adds SlingshotChargeState if not present.
- **On cancel** (SlingshotHeld becomes false while below MinLaunchThreshold or on explicit cancel): removes SlingshotChargeState, returns Mode to Grounded.

#### SlingshotLaunchSystem

- **Runs in:** PhysicsSystemGroup, after SlingshotChargeSystem
- **Requires:** SlingshotChargeState, PlayerInputComponent (SlingshotReleased)
- **Behavior:** When SlingshotReleased fires and ChargeNormalized >= MinLaunchThreshold: computes impulse from AimDirection * MaxForce * charge, writes to PhysicsVelocity. Removes SlingshotChargeState. Transitions Mode to Ballistic.

#### PlayerMovementSystem (existing — extension guidance)

- **Runs in:** PhysicsSystemGroup (existing location, no change)
- **What changes:** The existing air movement branch (currently `!IsGrounded`) must split on `MovementMode` to apply different air control rates per state. Pseudo-branch:

```csharp
// Existing: single air control path
if (!movementState.IsGrounded)
    // lerp at some rate

// Target: per-mode air control
if (!movementState.IsGrounded)
{
    float airRate = movementState.Mode switch
    {
        PlayerMovementMode.Ballistic => slingshotConfig.AirControlBallistic,       // 0.25
        PlayerMovementMode.Gliding => glideConfig.AirControlGlide,                 // 0.35
        PlayerMovementMode.GlideCharging => slingshotConfig.AirControlBallistic,   // same as ballistic
        PlayerMovementMode.ThermalBoost => slingshotConfig.AirControlBallistic,     // same as ballistic
        _ => 0f  // SlingshotCharging: no air movement (grounded)
    };
    // lerp horizontal velocity at airRate
}
```

- **Landing momentum:** The existing grounding logic must NOT zero horizontal velocity on contact. Instead: `velocity.xz *= LandingConfig.LandingMomentumPreservation` (0.94).
- **No other changes.** Grounded movement, gravity, and friction remain as-is.

#### GlideSystem

- **Runs in:** PhysicsSystemGroup, after PlayerMovementSystem
- **Requires:** PlayerMovementState (Ballistic or GlideCharging), PlayerInputComponent
- **Behavior:** When space is held during Ballistic, starts GlideCharging timer. After glideChargeTime elapses, transitions to Gliding. During Gliding: clamps vertical velocity toward glideFallSpeed, applies horizontal decay, allows air control at glide rate. Adds/manages GlideState component.

#### ThermalColumnSystem

- **Runs in:** PhysicsSystemGroup, after GlideSystem
- **Requires:** Player position overlapping thermal volume entities
- **Behavior:** When player enters a thermal column entity's bounds, applies vertical boost acceleration clamped to maxUpwardVelocity. Transitions Mode to ThermalBoost. Preserves horizontal velocity with slight multiplier.

#### MovementStateBookkeepingSystem

- **Runs in:** SimulationSystemGroup (runs first, before all other SimulationSystemGroup movement systems)
- **Reads:** PhysicsVelocity, CameraEffectConfig
- **Writes:** PlayerMovementState.Velocity, CameraEffectState (full reset)
- **Behavior:**
  1. Copies `PhysicsVelocity.Linear` into `PlayerMovementState.Velocity` so downstream feedback systems can read velocity without requiring `PhysicsVelocity` access.
  2. Resets `CameraEffectState` to config defaults (BaseFOV, BaseDistance, zero ShakeOffset, zero CameraDip, GroundedDamping, HorizonStabilize = false). This guarantees a clean slate each frame — subsequent feedback systems overwrite only the fields they own.
- **Why this system exists:** Without an explicit owner, both responsibilities (velocity caching and camera reset) would be duplicated across multiple systems or left to implicit ordering. A single bookkeeping system at the top of SimulationSystemGroup eliminates stale-state bugs and makes the data flow auditable.

#### LandingDetectionSystem

- **Runs in:** SimulationSystemGroup, after MovementStateBookkeepingSystem
- **Requires:** PlayerMovementState
- **Behavior:** Detects transition from any airborne Mode to Grounded (using previous frame's Mode vs current). On transition: enables LandingImpactEvent with speed data captured from the frame before landing. One-frame event: disabled next frame.

#### CameraChargeFeedbackSystem

- **Runs in:** SimulationSystemGroup
- **Reads:** SlingshotChargeState, CameraEffectConfig
- **Writes:** CameraEffectState (TargetDistance, TargetFOV, ShakeOffset, Damping)
- **Behavior:** When SlingshotChargeState exists, computes pull-back distance and FOV reduction proportional to ChargeNormalized. Writes shake offset using Perlin noise scaled by charge. Sets Damping to chargeDamping.

#### CameraSpeedFeedbackSystem

- **Runs in:** SimulationSystemGroup, after MovementStateBookkeepingSystem
- **Reads:** PlayerMovementState (Mode, Velocity — cached by bookkeeping system), CameraEffectConfig
- **Writes:** CameraEffectState (TargetFOV, TargetDistance, Damping)
- **Behavior:** During Ballistic/Gliding/ThermalBoost: computes speed-based FOV addition and distance addition. Handles the launch FOV punch as a decaying impulse (fast attack, slow decay). Sets state-appropriate damping.

#### CameraLandingFeedbackSystem

- **Runs in:** SimulationSystemGroup
- **Reads:** LandingImpactEvent, CameraEffectConfig
- **Writes:** CameraEffectState (ShakeOffset, CameraDip, TargetFOV)
- **Behavior:** On LandingImpactEvent: fires shake impulse proportional to VerticalSpeed. Applies FOV dip. Sets CameraDip for the resolver to apply as a transient vertical offset.

#### CameraGlideFeedbackSystem

- **Runs in:** SimulationSystemGroup
- **Reads:** GlideState, CameraEffectConfig
- **Writes:** CameraEffectState (TargetFOV, Damping, HorizonStabilize)
- **Behavior:** During Gliding: sets calm FOV, tight damping, enables HorizonStabilize. The resolver uses HorizonStabilize to gradually blend camera pitch toward horizon over 1–2 seconds.

#### CameraEffectResolverSystem

- **Runs in:** PresentationSystemGroup (OrderLast = true)
- **Reads:** CameraEffectState, CameraEffectConfig, PlayerViewComponent, LocalTransform (player)
- **Writes:** Camera entity LocalTransform, managed Camera.fieldOfView, managed Camera.transform
- **Behavior:**
  1. Reads CameraEffectState (all fields represent the desired state for this frame)
  2. Smoothly interpolates current camera FOV toward TargetFOV using exponential smoothing
  3. Computes third-person orbit position from player position + pivot + TargetDistance + PositionOffset
  4. Adds ShakeOffset as additive noise
  5. Applies CameraDip as transient vertical offset (decays over time)
  6. If HorizonStabilize: blends pitch toward 0 at a slow rate
  7. Writes final position/rotation to camera entity LocalTransform
  8. Writes to managed Camera component (same pattern as current PlayerCameraSystem)

**This is the only system that writes to the camera.** All other feedback systems write to CameraEffectState only.

---

## 9. Writer Priority and Blending

When multiple feedback systems write to CameraEffectState in the same frame, the **last writer wins** for exclusive fields (FOV, Distance, Damping). This is acceptable because only one movement state is active at a time, and the corresponding feedback system is the only one writing non-default values.

For additive fields (ShakeOffset, CameraDip), values accumulate within a frame and the resolver applies them all.

Reset protocol: `MovementStateBookkeepingSystem` resets CameraEffectState to config defaults at the top of SimulationSystemGroup each frame. Each active feedback system then overwrites the fields it owns.

### State → Feedback System Ownership

| MovementMode | Active Feedback System |
|---|---|
| Grounded | none (defaults from config) |
| SlingshotCharging | CameraChargeFeedbackSystem |
| Ballistic | CameraSpeedFeedbackSystem |
| GlideCharging | CameraSpeedFeedbackSystem (reduced) |
| Gliding | CameraGlideFeedbackSystem |
| ThermalBoost | CameraSpeedFeedbackSystem (thermal variant) |
| Landing frame | CameraLandingFeedbackSystem (additive, one-frame) |

---

## 10. Input System Expansion

### Current PlayerInputSystem captures

- WASD → Move
- Mouse delta → Look
- Space → JumpPressed

### Required additions

- LMB + RMB simultaneous hold → SlingshotHeld
- Mouse delta during SlingshotHeld → SlingshotDrag (accumulated, not per-frame)
- LMB or RMB release while SlingshotHeld was true → SlingshotReleased (one-frame event)
- Space hold duration tracking for glide charge — intentionally NOT added to `PlayerInputComponent`. `GlideSystem` tracks hold duration internally using `JumpPressed` state, keeping input-layer responsibility narrow

Implementation note: `PlayerInputSystem` cannot use `[BurstCompile]` on `OnUpdate` because it accesses `Mouse.current` and `Keyboard.current` (managed). This is already the case.

---

## 11. VFX Integration Approach

The project is DOTS-first, but visual effects use managed Unity systems. The bridge pattern:

### World-Space Particles (speed lines, dust, charge particles)

- Use VFX Graph or legacy ParticleSystem attached to a synced GameObject
- `PlayerVisualSync` (existing `EntityVisualSync` pattern) keeps the GameObject at player position
- A MonoBehaviour on the visual GameObject reads `CameraEffectState` or `PlayerMovementState` from the ECS world each frame to drive particle emission rate, color, and lifetime
- Alternatively: a managed system in PresentationSystemGroup reads movement state and calls into VFX Graph `SetFloat`/`SetVector` APIs

### Screen-Space Effects (vignette, chromatic aberration)

- Use URP Volume overrides on the camera or a global Volume
- `CameraEffectResolverSystem` (or a companion managed system) reads CameraEffectState and sets Volume override properties
- Requires a reference to the Volume component, obtained through the bootstrap

### Speed Lines

Two viable approaches:

1. **Camera-parented ParticleSystem** — particles emit from a ring behind the camera, stream forward. Emission rate driven by speed. Simple, performant.
2. **Full-screen shader** (URP Renderer Feature) — screen-space radial blur or line overlay driven by a speed uniform. Higher quality, more GPU cost.

Recommendation for MVP: camera-parented ParticleSystem. Lower complexity, sufficient visual quality for 16-bit aesthetic.

---

## 12. Third-Person Camera Migration

### Current State

- `PlayerCameraSystem` is first-person (offset from player head, combined yaw+pitch rotation)
- `CameraFollowSystem` is a simple third-person test-only fallback
- `PlayerCinemachineCameraSystem` exists but is not connected to movement state

### Migration Path

1. `CameraEffectResolverSystem` replaces `PlayerCameraSystem` as the primary camera driver
2. It implements third-person orbit: position = playerPos + pivotOffset + sphericalToCartesian(yaw, pitch, distance)
3. `CameraFollowSystem` remains as test-only fallback (no changes needed)
4. `PlayerCinemachineCameraSystem` can optionally be used if Cinemachine is preferred for the orbit — in that case, the resolver drives Cinemachine parameters instead of direct transform writes

### Bootstrap Changes

- `PlayerCameraBootstrap_WithVisuals` updated to add `CameraEffectState`, `CameraEffectConfig`, `SlingshotConfig`, `GlideConfig`, `ThermalConfig`, and `LandingConfig` to the player entity
- Camera created at third-person distance behind player instead of first-person head position
- `PlayerCameraSettings.IsThirdPerson` set to true

---

## 13. Test Plan

Testing follows the project's SPEC → TEST → CODE workflow. Every prototype step gets at least one automated test (EditMode or PlayMode) for deterministic ECS logic, and at least one manual visual test card for feel/feedback verification.

Test files follow existing project conventions:
- EditMode tests: `Assets/Scripts/DOTS/Tests/EditMode/` or `Assets/Scripts/Player/Tests/EditMode/`
- PlayMode tests: `Assets/Scripts/Player/Test/` (existing pattern) or `Assets/Scripts/DOTS/Tests/PlayMode/`
- Test harness pattern: create isolated test world, manually add `[DisableAutoCreation]` systems, tick system groups, assert on component state (see `PlayerMovementAirPathPlayModeTests` for reference)

### 13.1 Per-System Automated Tests

#### MovementStateBookkeepingSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Velocity cached from PhysicsVelocity | PhysicsVelocity.Linear = (10, -5, 3) | PlayerMovementState.Velocity == (10, -5, 3) |
| CameraEffectState reset to defaults | CameraEffectState has non-default values from previous frame | After tick, TargetFOV == BaseFOV, TargetDistance == BaseDistance, ShakeOffset == 0, CameraDip == 0 |
| Runs before feedback systems | CameraChargeFeedbackSystem writes after bookkeeping | Charge values survive frame (not overwritten by reset) |

#### SlingshotChargeSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Charge accumulates on hold | Entity with Grounded mode, SlingshotHeld=true, DragDelta=(0,200) | ChargeNormalized equals `pow(200/MaxDragDistance, CurveExponent)` |
| Charge clamps to 1.0 | DragDelta exceeds MaxDragDistance | ChargeNormalized == 1.0 |
| Mode transitions to SlingshotCharging | Grounded + SlingshotHeld=true, tick once | Mode == SlingshotCharging |
| Cancel below threshold | SlingshotHeld becomes false while ChargeNormalized < MinLaunchThreshold | Mode returns to Grounded, SlingshotChargeState removed |
| Cancel above threshold without release | SlingshotHeld becomes false without SlingshotReleased | Mode returns to Grounded (cancel, not launch) |
| Aim direction opposes drag | DragDelta = (0, -100) (backward drag) | AimDirection points forward (camera forward) |
| Charge does not activate while airborne | Mode == Ballistic, SlingshotHeld=true | Mode unchanged, no SlingshotChargeState added |

#### SlingshotLaunchSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Launch applies impulse | ChargeNormalized=0.8, AimDirection=forward, SlingshotReleased=true | PhysicsVelocity.Linear matches `AimDirection * MaxForce * pow(0.8, CurveExponent)` |
| Launch removes charge state | Valid release | SlingshotChargeState removed from entity |
| Mode transitions to Ballistic | Valid release | Mode == Ballistic |
| Below-threshold release cancels | ChargeNormalized=0.1 (< MinLaunchThreshold), SlingshotReleased=true | No velocity change, Mode returns to Grounded |
| Launch preserves existing vertical velocity | Entity has positive velocity.y, valid release | velocity.y not zeroed (impulse is additive or max'd) |

#### PlayerMovementSystem Tests (EditMode — extend existing)

| Test | Setup | Assertion |
|---|---|---|
| Ballistic air control uses correct rate | Mode=Ballistic, IsGrounded=false, AirControlBallistic=0.25 | Horizontal velocity lerps at 0.25 rate, not GroundLerpRate |
| Grounded movement unchanged | Existing tests pass with new MovementMode enum | Regression: existing `GroundedWithGroundMode_UsesGroundLerpRate` still passes |

#### GlideSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Glide charge begins on space hold | Mode=Ballistic, JumpPressed held, above minGlideHeight | Mode transitions to GlideCharging |
| Glide deploys after charge time | GlideCharging for glideChargeTime seconds | Mode transitions to Gliding, GlideState added |
| Glide clamps vertical velocity | Mode=Gliding, velocity.y = -20 | After tick, velocity.y approaches glideFallSpeed (-5.5), not instant snap |
| Glide preserves horizontal | Mode=Gliding, velocity.xz = (30, 0) | After tick, velocity.xz magnitude >= 30 * glideForwardPreservation |
| Glide air control at glide rate | Mode=Gliding, move input applied | Horizontal lerps at airControlGlide (0.35), not airControlBallistic (0.25) |
| Glide requires minimum height | Mode=Ballistic, player below minGlideHeight | Space hold does not trigger GlideCharging |
| Space release during GlideCharging cancels | Release space before glideChargeTime elapses | Mode returns to Ballistic, no GlideState |

#### ThermalColumnSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Thermal applies vertical boost | Player entity position inside thermal volume bounds | velocity.y increases by verticalBoostAcceleration * deltaTime |
| Thermal clamps max upward velocity | velocity.y already at maxUpwardVelocity | velocity.y does not exceed max |
| Thermal preserves horizontal | velocity.xz = (20, 0), player inside thermal | velocity.xz magnitude >= 20 * horizontalVelocityMultiplier |
| Mode transitions to ThermalBoost | Player enters thermal volume while airborne | Mode == ThermalBoost |
| Mode exits on leaving thermal | Player position outside thermal bounds | Mode returns to Ballistic or Gliding (whichever was active before) |

#### LandingDetectionSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Landing event fires on transition | Previous frame: Mode=Ballistic, IsGrounded=false. Current frame: IsGrounded=true | LandingImpactEvent enabled with correct VerticalSpeed and HorizontalSpeed |
| Landing event captures pre-landing speed | velocity = (15, -10, 0) before grounding | LandingImpactEvent.VerticalSpeed == 10, HorizontalSpeed == 15 |
| No event on grounded-to-grounded | Was grounded, still grounded | LandingImpactEvent remains disabled |
| Event disabled after one frame | LandingImpactEvent was enabled last frame | After next tick, LandingImpactEvent disabled |

#### CameraChargeFeedbackSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Distance increases with charge | SlingshotChargeState with ChargeNormalized=0.5 | CameraEffectState.TargetDistance == BaseDistance + ChargeDistanceAdd * 0.5 |
| FOV decreases with charge | ChargeNormalized=1.0 | CameraEffectState.TargetFOV == BaseFOV - ChargeFOVReduce |
| Shake scales with charge | ChargeNormalized=0.75 | ShakeOffset magnitude between ChargeShakeMin and ChargeShakeMax |
| No charge: defaults | No SlingshotChargeState on entity | CameraEffectState matches config defaults |

#### CameraSpeedFeedbackSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| FOV scales with speed | Mode=Ballistic, horizontal speed=25 (above threshold 15) | TargetFOV == BaseFOV + (25-15) * SpeedFOVScale, clamped to SpeedFOVMax |
| FOV below threshold: no bonus | horizontal speed=10 (below threshold) | TargetFOV == BaseFOV (no speed addition) |
| Launch FOV punch decays | First frame after launch vs 10 frames later | FOV decreases toward speed-only value over time |
| Distance increases with speed | Mode=Ballistic, high speed | TargetDistance > BaseDistance |

#### CameraLandingFeedbackSystem Tests (EditMode)

| Test | Setup | Assertion |
|---|---|---|
| Shake proportional to impact | LandingImpactEvent.VerticalSpeed=15 | ShakeOffset magnitude == min(15 * LandingShakeScale, LandingShakeMax) |
| FOV dip on landing | LandingImpactEvent enabled | TargetFOV == BaseFOV - LandingFOVDip |
| Camera dip on hard landing | VerticalSpeed > hardLandingVerticalSpeed | CameraDip > 0 |
| Soft landing: minimal shake | VerticalSpeed=3 | ShakeOffset magnitude small |

#### CameraEffectResolverSystem Tests (PlayMode)

| Test | Setup | Assertion |
|---|---|---|
| Resolver is sole camera writer | Create camera entity + player entity, tick full pipeline | Camera entity LocalTransform matches CameraEffectState-derived position |
| FOV smoothing not instant | Set TargetFOV=80, current=60, tick once | Camera FOV moves toward 80 but doesn't reach it in one frame |
| Orbit distance correct | TargetDistance=5, player at origin | Camera position magnitude from player ~= 5 (within pivot offset) |
| Horizon stabilization | HorizonStabilize=true, camera pitch=30 degrees | After several ticks, pitch is closer to 0 |

### 13.2 Per-Step Visual Test Cards

Each prototype step has a manual visual test card. Run these in Play Mode with the actual game scene.

#### Phase A: Slingshot Core + Camera

**Step 1: Third-person camera**

- [ ] Enter Play Mode — camera is behind and above the player at ~4m distance
- [ ] Mouse orbit works — move mouse to rotate camera around player
- [ ] WASD movement is camera-relative (W moves toward where camera looks)
- [ ] Camera follows smoothly, no jitter on flat terrain

**Step 2: Slingshot charge input**

- [ ] Hold LMB+RMB — entity debugger shows Mode = SlingshotCharging
- [ ] Drag mouse backward — entity debugger shows ChargeNormalized increasing
- [ ] Release at low drag — entity debugger shows cancel (Mode returns to Grounded)
- [ ] Character cannot walk during charge (WASD disabled or ignored while charging)

**Step 3: Camera pull-back during charge**

- [ ] Hold LMB+RMB and drag — camera visibly moves backward
- [ ] Drag further — camera pulls back more (proportional, not binary)
- [ ] FOV narrows slightly (check Game view stats or feel the tunnel effect)
- [ ] At high charge, subtle camera shake is visible
- [ ] Release without threshold — camera smoothly returns to base position over ~200ms
- [ ] Full charge hold — camera at maximum pull-back, shake noticeable

**Step 4: Slingshot launch impulse**

- [ ] Full charge + release — player launches at high velocity in the aimed direction
- [ ] Partial charge launches with proportionally less speed
- [ ] Drag direction determines launch direction (drag back → launch forward)
- [ ] Launch angle is intuitive — drag down-back → launch up-forward

**Step 5: Camera FOV punch + speed lines on launch**

- [ ] On release: FOV visibly widens in a quick "pop" then slowly settles
- [ ] Speed lines appear during high-velocity flight
- [ ] Speed lines fade as player decelerates
- [ ] Camera trails behind player during fast movement (lag is visible)

**Step 6: Landing momentum preservation**

- [ ] Launch at shallow angle — on landing, player slides forward with residual speed
- [ ] Velocity is NOT zeroed on contact (player doesn't stop dead)
- [ ] Sliding speed is ~92–97% of pre-landing horizontal speed

**Step 7: Landing camera dip + dust**

- [ ] Land from height — camera dips down briefly, then recovers
- [ ] Harder landings produce bigger camera dip and more shake
- [ ] Dust burst appears at feet on landing (larger with harder impact)
- [ ] Soft landings (low speed) produce minimal or no camera effects

**Phase A Gate Test (30-second fun):**

- [ ] Without any instruction, do you voluntarily charge and launch again?
- [ ] Do you experiment with different charge angles?
- [ ] Do you try to launch further or higher than last time?
- [ ] If NO to any of the above: stop and tune before proceeding to Phase B

#### Phase B: Air Control + Glide

**Step 8: Ballistic air steering**

- [ ] During flight, WASD gently steers the trajectory
- [ ] Steering is noticeably weaker than ground movement (air control factor)
- [ ] Player can adjust landing position mid-flight

**Step 9: Camera speed-based offset during flight**

- [ ] At high speed: camera is further back and FOV is wider
- [ ] As speed decreases: camera gradually returns closer, FOV narrows
- [ ] Transition is smooth, not steppy

**Step 10: Glide conversion**

- [ ] During ballistic flight, hold Space for ~0.45s — player enters glide
- [ ] Glide visibly changes descent rate (slower fall)
- [ ] Horizontal speed is mostly preserved during glide
- [ ] Glide has a distinct feel from ballistic (calmer, more controlled)
- [ ] Space hold below minGlideHeight does nothing

**Step 11: Camera stabilization during glide**

- [ ] During glide, camera pitch gradually levels toward horizon
- [ ] Camera damping tightens (less floaty than ballistic)
- [ ] FOV settles to a calm intermediate value
- [ ] Transition from ballistic → glide camera feels like "settling in"

**Step 12: Character pose changes**

- [ ] Grounded: upright stance
- [ ] Charging: visible crouch / compression
- [ ] Ballistic: tucked pose
- [ ] Gliding: arms/body spread
- [ ] Pose transitions blend smoothly (100–200ms), not instant snap

**Step 13: Wind particles and speed lines**

- [ ] Speed lines active during ballistic above 15 m/s
- [ ] Wind particles (small dots) stream past during flight
- [ ] Particle density scales with speed
- [ ] Particles fade smoothly when speed drops

#### Phase C: Thermals + Chaining

**Step 14: Thermal columns**

- [ ] Flying into a thermal column lifts the player upward
- [ ] Lift feels gradual, not instant teleport
- [ ] Horizontal speed is mostly preserved (slight reduction)
- [ ] Exiting the column stops the lift

**Step 15: Thermal visual feedback**

- [ ] Thermal columns have visible particle updraft (spiral or column)
- [ ] Camera tilts slightly upward while in thermal
- [ ] Player can spot thermals from a distance during flight

**Step 16: Chaining continuity**

- [ ] Slingshot → glide: velocity carries through without reset
- [ ] Glide → thermal: entering thermal during glide adds lift without breaking glide feel
- [ ] Thermal → glide: exiting thermal at altitude allows immediate glide
- [ ] Full chain (slingshot → glide → thermal → glide → land) feels continuous

**Step 17: Ground shadow**

- [ ] During any airborne state, a shadow/indicator is visible below the player on terrain
- [ ] Shadow size decreases as player approaches ground
- [ ] Shadow helps predict landing position

**Step 18: Camera transition polish**

- [ ] Watch all state transitions in sequence — no jarring camera jumps
- [ ] Grounded → Charging: smooth pull-back (~200ms)
- [ ] Charging → Ballistic: fast punch (~100ms) with slow settle (~400ms)
- [ ] Ballistic → Gliding: gentle transition (~300ms)
- [ ] Any → Grounded: impact dip + smooth recovery

### 13.3 Test File Organization

```
Assets/Scripts/Player/Tests/
├── EditMode/
│   ├── MovementStateBookkeepingSystemTests.cs
│   ├── SlingshotChargeSystemTests.cs
│   ├── SlingshotLaunchSystemTests.cs
│   ├── GlideSystemTests.cs
│   ├── ThermalColumnSystemTests.cs
│   ├── LandingDetectionSystemTests.cs
│   ├── CameraChargeFeedbackSystemTests.cs
│   ├── CameraSpeedFeedbackSystemTests.cs
│   └── CameraLandingFeedbackSystemTests.cs
├── PlayMode/
│   ├── CameraEffectResolverSystemTests.cs
│   ├── SlingshotFullChainPlayModeTests.cs
│   └── MovementStateTransitionPlayModeTests.cs
└── (existing)
    ├── PlayerMovementAirPathPlayModeTests.cs
    └── PlayerWallContactCommandPlayModeTests.cs
```

### 13.4 Test Harness Pattern

All new EditMode tests follow the established pattern from `PlayerMovementAirPathPlayModeTests`:

```csharp
[SetUp]
public void SetUp()
{
    // Save and replace default world
    // Create test world with DisableAutoCreation
    // Manually create and register systems under test
    // Sort system groups
}

[TearDown]
public void TearDown()
{
    // Dispose test world
    // Restore previous default world
}

// Helper: create entity with required components for the system under test
private Entity CreateSlingshotTestEntity(...) { ... }

// Helper: tick the relevant system group once with fixed deltaTime
private void TickOnce() { ... }
```

PlayMode tests that need managed components (Camera, VFX) use `[UnityTest]` with `yield return null` to allow frame processing.

---

## 14. Acceptance Criteria

Phase A (Slingshot Core + Camera) is complete when:

- [ ] All MovementStateBookkeepingSystem EditMode tests pass (Section 13.1)
- [ ] All SlingshotChargeSystem EditMode tests pass (Section 13.1)
- [ ] All SlingshotLaunchSystem EditMode tests pass (Section 13.1)
- [ ] All CameraChargeFeedbackSystem EditMode tests pass (Section 13.1)
- [ ] All CameraLandingFeedbackSystem EditMode tests pass (Section 13.1)
- [ ] CameraEffectResolverSystem PlayMode tests pass (Section 13.1)
- [ ] Visual test cards for Steps 1–7 all checked (Section 13.2)
- [ ] Phase A Gate Test passed: player voluntarily experiments with slingshot (Section 13.2)
- [ ] Existing PlayerMovementAirPathPlayModeTests still pass (regression)

Phase B (Air Control + Glide) is complete when:

- [ ] All GlideSystem EditMode tests pass (Section 13.1)
- [ ] All CameraSpeedFeedbackSystem EditMode tests pass (Section 13.1)
- [ ] Visual test cards for Steps 8–13 all checked (Section 13.2)
- [ ] Each movement state is visually distinguishable (per AAA Checklist #20)

Phase C (Thermals + Chaining) is complete when:

- [ ] All ThermalColumnSystem EditMode tests pass (Section 13.1)
- [ ] All LandingDetectionSystem EditMode tests pass (Section 13.1)
- [ ] SlingshotFullChainPlayModeTests pass (slingshot → glide → thermal without velocity resets)
- [ ] MovementStateTransitionPlayModeTests pass (valid transitions only)
- [ ] Visual test cards for Steps 14–18 all checked (Section 13.2)
- [ ] Camera transitions between all states are smooth (per timing table in MOVEMENT_PLANNING.md)
