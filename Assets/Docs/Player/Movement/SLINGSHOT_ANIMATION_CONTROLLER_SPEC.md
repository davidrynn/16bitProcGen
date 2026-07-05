# SLINGSHOT_ANIMATION_CONTROLLER_SPEC.md

**Status:** ACTIVE
**Last Updated:** 2026-06-28

## Purpose

Wire the three authored slingshot animation clips into the Unity animation controller/state machine.

This document is intended for an AI coding assistant working in Unity.

The goal is to create animation transitions for the slingshot charge/release mechanic without changing gameplay physics, player movement, launch force, or traversal logic.

---

## Authored Animation Clips

The following clips already exist or are expected to exist after Blender export/import.

### 1. `Player_Slingshot_Charge_Start`

**Meaning:** Player begins the slingshot action.

This clip starts when the player initiates the slingshot input.

Expected visual motion:

```text
reach / grab / brace
→ pull body into tension
→ arrive at charged pose
```

The clip should end in a pose that matches the beginning of `Player_Slingshot_Charge_Hold`.

**Loop:** No

**Typical length:** ~8-12 frames at 30 FPS

**Runtime trigger:** input down / charge begins

---

### 2. `Player_Slingshot_Charge_Hold`

**Meaning:** Player is holding the slingshot at tension.

This clip loops while the player continues holding the slingshot input.

Expected visual motion:

```text
charged pose
→ subtle strain / trembling in pulling arm
→ returns to charged pose
```

The motion should be localized. The pulling arm may tremble slightly. Feet and body should remain generally planted/braced.

**Loop:** Yes

**Typical length:** ~24 frames at 30 FPS

**Runtime trigger:** charge input is still held

---

### 3. `Player_Slingshot_Release`

**Meaning:** Player releases the slingshot and is launched.

This clip starts when the player releases the slingshot input.

Expected visual motion:

```text
max tension
→ release snap
→ body thrown upward/forward
→ airborne transition pose
```

The clip should end in a pose that can blend into an airborne animation such as rising or falling.

**Loop:** No

**Typical length:** ~12 frames at 30 FPS

**Runtime trigger:** input released / launch begins

---

## Intended Runtime Animation Flow

The intended animation sequence is:

```text
Player_Slingshot_Charge_Start
→ Player_Slingshot_Charge_Hold
→ Player_Slingshot_Release
→ Airborne state
```

The airborne state may later become:

```text
Player_Rising_Loop
Player_Falling_Loop
Player_Land_Hard
```

**MVP decision (2026-06-10):** while the character is in the air, every airborne state (rising and falling arcs alike) plays the single existing fall clip (`HumanM@Fall01`). The distinct state labels (`BallisticRise`, `Falling`) are kept so a dedicated ballistic/rise animation can replace the upward-arc clip later without re-plumbing transitions. See ticket A8 in `Assets/Docs/Tickets/vista-moment.md`.

For this task, only wire the three slingshot clips and provide a clean transition point into an existing or placeholder airborne state.

---

## Animator State Requirements

Create or update animation states:

```text
Slingshot_Charge_Start
Slingshot_Charge_Hold
Slingshot_Release
```

Recommended mapping:

| Animator State | Animation Clip |
|---|---|
| `Slingshot_Charge_Start` | `Player_Slingshot_Charge_Start` |
| `Slingshot_Charge_Hold` | `Player_Slingshot_Charge_Hold` |
| `Slingshot_Release` | `Player_Slingshot_Release` |

---

## Animator Parameters

Use existing gameplay parameters if they already exist. Do not create duplicates with different names if the project already has equivalents.

Recommended parameters:

```text
bool IsSlingshotCharging
trigger SlingshotChargeStarted
trigger SlingshotReleased
bool IsGrounded
float VerticalVelocity
float ChargeAmount
```

Minimum required parameters:

```text
bool IsSlingshotCharging
trigger SlingshotReleased
```

Optional but useful:

```text
trigger SlingshotChargeStarted
float ChargeAmount
float VerticalVelocity
```

---

## Transition Logic

### Entering Charge Start

Transition from normal locomotion/idle/grounded states into:

```text
Slingshot_Charge_Start
```

when slingshot input begins.

Condition options:

```text
SlingshotChargeStarted trigger
```

or:

```text
IsSlingshotCharging == true
```

Use whichever matches the project’s existing input/animation architecture.

Expected behavior:

```text
On slingshot input down:
    animation enters Slingshot_Charge_Start
```

Transition settings:

```text
Has Exit Time: false preferred for input responsiveness
Transition Duration: short, around 0.05-0.15 seconds
```

Do not use a long transition here, or the charge start will feel delayed.

---

### Charge Start to Charge Hold

Transition from:

```text
Slingshot_Charge_Start
```

to:

```text
Slingshot_Charge_Hold
```

when the start clip finishes and the player is still holding charge.

Condition:

```text
IsSlingshotCharging == true
```

Transition settings:

```text
Has Exit Time: true
Exit Time: near 0.9-1.0
Transition Duration: very short, around 0.03-0.1 seconds
```

Expected behavior:

```text
Charge_Start plays once.
If the player is still holding input, transition into Charge_Hold.
Charge_Hold loops until release.
```

---

### Charge Hold to Release

Transition from:

```text
Slingshot_Charge_Hold
```

to:

```text
Slingshot_Release
```

when the player releases input.

Condition:

```text
SlingshotReleased trigger
```

or:

```text
IsSlingshotCharging == false
```

Prefer a trigger if available, because release is an event.

Transition settings:

```text
Has Exit Time: false
Transition Duration: very short, around 0.02-0.08 seconds
```

Expected behavior:

```text
The player may hold Charge_Hold for any duration.
When input is released, immediately enter Release.
```

---

### Charge Start Directly to Release

The player may tap/release before `Charge_Start` finishes.

Support direct transition:

```text
Slingshot_Charge_Start
→ Slingshot_Release
```

Condition:

```text
SlingshotReleased trigger
```

Transition settings:

```text
Has Exit Time: false
Transition Duration: very short, around 0.02-0.08 seconds
```

Expected behavior:

```text
If the player releases quickly, skip Charge_Hold and play Release.
```

This prevents input from feeling ignored.

---

### Release to Airborne

After:

```text
Slingshot_Release
```

transition to airborne animation state.

For now this may be a placeholder state if rising/falling clips are not ready.

Preferred future logic:

```text
if VerticalVelocity > positive threshold:
    transition to Rising_Loop
else:
    transition to Falling_Loop
```

Initial simple logic:

```text
Slingshot_Release
→ Airborne
```

Transition settings:

```text
Has Exit Time: true
Exit Time: near 0.8-1.0
Transition Duration: short, around 0.05-0.15 seconds
```

Do not transition back to idle immediately after release.

---

## Clip Import Settings

### `Player_Slingshot_Charge_Start`

```text
Loop Time: Off
Root Motion: Off
```

### `Player_Slingshot_Charge_Hold`

```text
Loop Time: On
Root Motion: Off
```

### `Player_Slingshot_Release`

```text
Loop Time: Off
Root Motion: Off
```

Important:

```text
Apply Root Motion should remain disabled unless the existing project deliberately uses root motion.
```

The slingshot mechanic should be gameplay/controller-driven, not animation-driven.

---

## Gameplay / Animation Boundary

Do not change gameplay launch physics as part of this task.

Animation should react to gameplay state.

Gameplay owns:

```text
charge amount
charge duration
launch direction
launch force
player velocity
actual world movement
grounded state
```

Animation owns:

```text
charge start pose
charge hold loop
localized trembling/strain
release pose
airborne transition pose
```

Do not bake gameplay movement into the animation controller.

---

## Expected Input Event Mapping

Example conceptual flow:

```text
OnSlingshotInputDown:
    IsSlingshotCharging = true
    SetTrigger(SlingshotChargeStarted)

WhileSlingshotInputHeld:
    update ChargeAmount if used

OnSlingshotInputReleased:
    IsSlingshotCharging = false
    SetTrigger(SlingshotReleased)
    gameplay controller applies launch force
```

The release animation and launch force should happen at approximately the same gameplay moment.

---

## Minimum Animator Graph

Minimum graph:

```text
Locomotion / Idle
    → Slingshot_Charge_Start
        → Slingshot_Charge_Hold
        → Slingshot_Release
    → Slingshot_Release
        → Airborne / Falling placeholder
```

Direct release path:

```text
Slingshot_Charge_Start
    → Slingshot_Release
```

must exist so a quick tap/release does not get stuck waiting for `Charge_Start` to finish.

---

## Acceptance Criteria

The work is complete when:

- `Player_Slingshot_Charge_Start` plays when slingshot input begins.
- `Player_Slingshot_Charge_Start` transitions into `Player_Slingshot_Charge_Hold` if input remains held.
- `Player_Slingshot_Charge_Hold` loops cleanly while input remains held.
- `Player_Slingshot_Release` plays immediately when input is released.
- Quick input release can transition directly from `Charge_Start` to `Release`.
- `Player_Slingshot_Release` transitions into an airborne/falling placeholder or existing airborne state.
- Root motion remains disabled.
- Gameplay launch force remains controlled by existing movement/physics code.
- No movement controller rewrite is performed.
- No physics behavior is changed except wiring existing slingshot state/events into animation parameters if needed.

---

## Testing Checklist

Use a simple in-editor play test.

### Test 1: Normal hold and release

Steps:

```text
Press and hold slingshot input.
Wait at least 1 second.
Release input.
```

Expected:

```text
Charge_Start plays once.
Charge_Hold loops.
Release plays immediately on release.
Character transitions to airborne/falling afterward.
```

### Test 2: Quick tap/release

Steps:

```text
Press slingshot input.
Release almost immediately.
```

Expected:

```text
Charge_Start begins.
Release interrupts or follows immediately.
Character does not get stuck in Charge_Start or Charge_Hold.
```

### Test 3: Long hold

Steps:

```text
Press and hold slingshot input for several seconds.
```

Expected:

```text
Charge_Hold loops indefinitely.
No visible pop at loop point.
No transition to Release until input is released.
```

### Test 4: Root motion check

Steps:

```text
Play all three clips in sequence.
Watch player world position and controller behavior.
```

Expected:

```text
Animation does not drive actual launch movement.
Controller/physics remains responsible for movement.
```

---

## Non-Goals

Do not implement in this task:

```text
new movement physics
new slingshot force math
new camera system
new procedural animation
runtime IK
ragdoll
landing animation
falling loop
rising loop
full animation controller rewrite
multiplayer prediction
```

Only wire the three slingshot animation clips into the existing animation controller/state flow.

---

## Notes for AI Coding Assistant

Keep changes small.

Preferred workflow:

```text
SPEC → TEST → CODE → REVIEW
```

Before coding, inspect the existing project for:

```text
existing Animator Controller
existing animation parameter names
existing player input system
existing movement/slingshot state
existing airborne/falling states
```

Reuse existing architecture and naming where possible.

Do not invent a parallel animation system if one already exists.

If current project architecture has an ECS/DOTS gameplay controller with a hybrid Animator bridge, add only the minimal bridge needed to update Animator parameters from existing slingshot state.
