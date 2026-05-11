# Movement System Plan & Spec (MVP)

**Status:** ACTIVE
**Last Updated:** 2026-04-14
**Owner:** Player Systems

### Project: Low-poly Procedural Crafting Game

### Focus: Traversal that feels good within 30 seconds

## Related Docs

- [AAA_MOVEMENT_CHECKLIST.md](AAA_MOVEMENT_CHECKLIST.md) — Playtest evaluation rubric
- [MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md](MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md) — ECS component/system mapping
- [/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md](/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) — Bootstrap pattern

---

# Design Goal

Movement should be intrinsically fun, even without objectives.

Players should:

* experiment with motion voluntarily
* attempt chaining behaviors
* feel expressive control
* quickly discover skill depth

Traversal should support:

* exploration
* procedural terrain readability
* emergent routes
* replayable movement puzzles

---

# Core Movement Pillars

1. Burst mobility (slingshot)
2. Trajectory shaping (glide)
3. Altitude recovery (thermal columns)
4. Directional redirection (grapple – optional layer 2)
5. Momentum chaining across systems

---

# Success Metric

Within 30 seconds of control:

player attempts movement repeatedly for enjoyment.

Indicators:

* player experiments with angle or charge strength
* player attempts longer distance traversal
* player attempts chaining actions
* player seeks vertical surfaces or terrain features
* player voluntarily repeats movement without instruction

---

# Movement Stack Overview

| Layer | Mechanic              | Purpose             |
| ----- | --------------------- | ------------------- |
| 1     | Slingshot             | burst displacement  |
| 1.5   | Chain Slingshot       | escalating re-launch |
| 2     | Glide                 | trajectory control  |
| 3     | Thermal columns       | vertical sustain    |
| 4     | Grapple               | vector redirection  |
| 5     | Momentum preservation | chaining continuity |

---

# Movement State Model

```csharp
enum MovementMode : byte
{
    Grounded,           // on ground, walking/idle
    SlingshotCharging,  // holding input, building charge
    Ballistic,          // launched, in free flight
    GlideCharging,      // holding glide input mid-air, vulnerability window
    Gliding,            // controlled descent, arms spread
    ThermalBoost,       // inside thermal column, gaining altitude
    Grappling           // attached to grapple anchor (Layer 2)
}
```

Note: the current codebase `PlayerMovementMode` enum has `Ground, Slingshot, Swim, ZeroG`. This must be replaced with the full state set above during implementation. See [MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md](MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md) for migration details.

---

# Mechanic Specifications

---

# 1. Slingshot Mechanic

## Description

Player pulls mouse backwards while holding both mouse buttons to charge a directional impulse.

Release launches player at high velocity.

Supports large displacement and expressive control.

---

## Input

```
Hold LMB + RMB
Drag mouse backwards relative to camera forward
Release buttons
```

---

## Core Formula

```cpp
dragNormalized = clamp(dragDistance / maxDragDistance, 0, 1);

charge = pow(dragNormalized, curveExponent);

impulse = aimDirection * maxForce * charge;
```

---

## Parameter Targets

```cpp
maxForce = 55

curveExponent = 1.8

maxDragDistance = 300 px mouse delta

chargeTimeTarget = 150–350 ms

minLaunchThreshold = 0.15

gravity = 22

groundFriction = 0.94

airControlBallistic = 0.25
```

---

## Feel Targets

horizontal distance:
30–80 meters

vertical gain:
15–40 meters

---

## Feedback Requirements

### Charge Buildup (SlingshotCharging state)

The charge phase is the most important feedback moment. The player must *feel* the spring loading.

**Camera:**

- Camera dollies back proportional to charge (0% → base distance, 100% → base + 2–3m)
- FOV narrows slightly during charge (base FOV → base - 3–5 degrees) to create tunnel tension
- Subtle camera shake ramps with charge intensity (amplitude 0.01 → 0.06)

**Character:**

- Character enters crouch/compression pose at charge start
- Crouch deepens with charge level (scale Y: 1.0 → 0.85)
- Lean direction opposes launch direction (visual rubber band)

**World-space effects:**

- Ground decal or ring at feet shows charge radius, expanding with charge
- Particle buildup at feet: subtle dust/energy at 25%, visible swirl at 75%, intense at 100%
- Drag line or elastic band visual from character to drag origin (world-space line renderer)

**Screen effects:**

- Slight vignette ramp (0% → 15% intensity at full charge)
- Optional chromatic aberration ramp at high charge (subtle, 0 → 0.3)

**Charge stages:**

| Charge % | Camera Pullback | Shake | Particles | Vignette |
|-----------|----------------|-------|-----------|----------|
| 0–25%     | minimal        | none  | dust wisps | none     |
| 25–50%    | noticeable     | micro | swirl forming | faint  |
| 50–75%    | strong         | light | visible energy | moderate |
| 75–100%   | maximum        | medium | intense burst-ready | strong |

### Release Burst (SlingshotCharging → Ballistic transition)

- Camera snaps forward aggressively (dolly distance returns to base in ~100ms)
- FOV punches wide (base + 8–15 degrees) then settles over 300–500ms
- Screen shake impulse on release (amplitude 0.15, decay 200ms)
- Radial shockwave particle at feet / launch point
- Speed lines begin immediately
- Audio spike on release

### Cancel

- All charge effects reverse smoothly over 150–200ms
- No burst, no impulse, clean reset

---

## Design Constraints

avoid hard cooldowns

partial charge allowed

cancel allowed

maintain momentum after landing

---

# 1a. Chain Slingshot

## Description

After any slingshot launch, a chain window opens **on landing** and stays briefly open. If the player triggers a slingshot charge within the window the new impulse is **additive** to the existing velocity rather than replacing it. Each successive chain in the same sequence adds a multiplied bonus, making the player go progressively faster.

This rewards rhythm and timing without requiring perfect execution: the window opens the moment the player touches down, giving them time to charge and re-launch before it expires.

---

## Chain Window

```
window opens:    on landing after a slingshot launch (LandingImpactEvent fires with ChainCount > 0)
window closes:   ChainWindowDuration seconds after landing
                 OR if player stays grounded past the window without relaunching
```

Window duration target: `2.0 seconds`

The window is long enough to allow a full charge cycle even after touching down, but short enough that walking around resets it naturally.

---

## Chain Input

During the window, the slingshot input (`LMB + RMB hold`) is accepted while grounded. The player can begin charging:

- On the exact landing frame
- Briefly after landing (within the remaining window)

Normal slingshot input constraints (drag direction, charge threshold) still apply.

---

## Velocity Model

```
// First launch (no chain): normal
velocity = AimDirection * MaxForce * charge

// Chained launch:
velocity = (velocity * ChainVelocityPreservation)
         + (AimDirection * MaxForce * charge * chainBonus)

// where:
chainBonus = 1.0 + min(ChainCount, ChainMaxCount) * ChainImpulseMultiplierStep
```

Existing velocity is partially preserved (not zeroed) and a boosted new impulse is added on top. This means direction can change per chain, but speed compounds.

---

## Parameter Targets

```cpp
ChainWindowDuration          = 2.0     // seconds — wide window for accessibility
ChainVelocityPreservation    = 0.85    // how much existing velocity survives into the chain
ChainImpulseMultiplierStep   = 0.25   // bonus multiplier added per chain level
ChainMaxCount                = 3      // chain 4+ gets no additional bonus (cap at 1.75x)
```

Chain bonus progression:
| Chain # | Bonus multiplier |
|---------|-----------------|
| 1 (first) | 1.0× (normal) |
| 2 | 1.25× |
| 3 | 1.50× |
| 4+ | 1.75× (capped) |

---

## Feel Targets

```
Chain 1 → 2:  player notices "that felt stronger than normal"
Chain 2 → 3:  player consciously hunts the chain window
Chain 3+:     player is playing optimally; speed reaches traversal-breaking territory
```

Maximum chained speed should be exhilarating but not uncontrollable. Cap total launch speed at `MaxForce * ChainVelocityPreservation^n + bonus` naturally limits runaway through the preservation decay.

---

## Feedback Requirements

### Chain Window Active (between launches)

- A subtle visual indicator that the window is still live — not a HUD bar; something diegetic or subtle screen-space (brief edge glow, reticle tint)
- Window indicator fades as time runs out to telegraph urgency without breaking immersion

### Chain Launch (additive impulse fires)

- Camera FOV punch is **larger** than a normal launch punch, scaled by chain level
- Screen shake is stronger per level
- Audio: pitch-shifted whoosh that rises per chain (Chain 1 = normal, Chain 2 = +20% pitch, Chain 3 = +40%)
- Brief chromatic aberration flash on chain 3+

### Chain Break (window expires without re-launch)

- No punitive feedback — chain just quietly expires
- Window indicator fades out

---

## Skill Gradient Extension

| Skill level | Chain behavior |
|---|---|
| Low | Single launch, ignores chain window |
| Medium | Notices chain opportunity on landing, attempts a second launch |
| High | Charges immediately on landing, near-instant chain re-launch |
| Expert | Mid-air chain — future feature (ground proximity window) |

---

## Design Constraints

- Chain requires landing: window opens on `LandingImpactEvent` after a launch (mid-air chain is a future ground-proximity feature)
- Chain does NOT require completing a full landing — barely-touching terrain counts
- Chain counter resets if the window expires (staying grounded too long) or on cancel
- Charge/cancel rules are identical to normal slingshot (cancel below MinLaunchThreshold still cancels)

---

# 2. Glide Mechanic

## Description

Player converts ballistic motion into controlled descent.

Requires mid-air interaction to activate.

Introduces risk-reward decision during airtime.

---

## Input

```
Hold space while airborne for 300–600 ms
```

---

## Glide Transition

During charge window:

reduced steering

slight downward pull

vulnerable sensation

---

## Glide Motion Model

target vertical velocity:

```cpp
glideFallSpeed = -5.5
```

horizontal decay:

```cpp
velocity.xz *= 0.995 per frame
```

air control:

```cpp
airControlGlide = 0.35
```

---

## Parameter Targets

```cpp
glideChargeTime = 0.45

minGlideHeight = 6 m

glideForwardPreservation = 0.96

maxGlideDuration = 4–9 seconds
```

---

## Design Role

converts velocity into distance

extends traversal routes

creates airtime decision making

---

# 3. Thermal Columns

## Description

Invisible or stylized updraft volumes that provide vertical lift.

Encourage exploration of terrain features.

Enable chaining.

---

## Behavior

when player enters column:

vertical velocity gradually increases.

horizontal velocity mostly preserved.

---

## Thermal Model

```cpp
verticalBoostAcceleration = 12–18

maxUpwardVelocity = 10–14

horizontalVelocityMultiplier = 0.97
```

---

## Size Targets

radius:
4–12 meters

height:
20–80 meters

spacing:
60–200 meters depending on biome density

---

## Visual Design Options

subtle particle column
distortion shader
floating debris motion
heat shimmer
stylized spiral geometry

minimal visual noise preferred

---

## Design Role

allows altitude recovery

creates route planning opportunities

encourages terrain scanning

supports flow state continuation

---

# 4. Grapple Mechanic (Layer 2)

## Description

Allows player to redirect momentum toward anchor surfaces.

Supports chaining and advanced traversal expression.

Optional for MVP.

---

## Anchor Surfaces

rock faces
structures
special surfaces
floating anchor nodes

---

## Behavior

player fires grapple

velocity bends toward anchor

release converts tension into forward momentum

---

## Feel Targets

elastic tension sensation

preserves speed

creates arc-based movement

---

## Parameter Targets

```cpp
grappleRange = 25–60 m

pullAcceleration = 35–60

releaseBoostMultiplier = 1.1–1.25

grappleCooldown = minimal or none
```

---

# Momentum Preservation Rules

avoid resetting velocity between states.

preferred:

actions modify velocity vector.

avoid:

velocity = 0 transitions.

---

## Momentum Guidelines

on landing:

```cpp
velocity *= 0.92–0.97
```

during transitions:

maintain horizontal magnitude.

limit excessive damping.

---

# Air Control

moderate steering allowed during airborne states.

target:

```cpp
airControlBallistic = 0.25

airControlGlide = 0.35
```

avoid excessive steering authority.

---

# Camera Behavior

## Camera Perspective

Third-person camera is required for the slingshot mechanic. The player needs to see:

- the character compressing during charge
- the elastic/spring visual between character and drag origin
- the launch trajectory relative to terrain
- the character pose during flight states

First-person is not suitable for MVP slingshot because the charge buildup has no readable visual anchor without seeing the character.

## Camera Per-State Behavior

### Grounded (idle / walking)

```cpp
baseFOV = 60
baseDistance = 4.0       // third-person orbit distance
basePivotOffset = (0, 1.5, 0)
damping = 12             // position lerp rate
rotationDamping = 16
```

Standard third-person follow. Responsive, tight damping. Player has full mouse-look orbit control.

### SlingshotCharging

```cpp
chargeDistanceAdd = 2.5          // camera pulls back as charge increases
chargeFOVReduce = 5              // FOV narrows to create tension
chargeShakeAmplitude = 0.01–0.06 // ramps with charge
chargeDamping = 8                // slightly looser to show pull-back motion
```

Camera orbits lock to charge direction (player cannot free-look during charge).
Camera position smoothly interpolates toward pull-back target.

### Ballistic (post-launch flight)

```cpp
ballisticFOVAdd = 8–15           // punches wide on launch, then driven by speed
speedFOVScale = 0.15             // additional FOV per m/s above threshold
speedFOVThreshold = 15           // speed below which no FOV bonus
speedFOVMax = 12                 // cap on speed-driven FOV increase
ballisticDistanceAdd = 1.5       // camera pulls further back at high speed
ballisticDamping = 6             // loose damping sells speed (camera trails behind)
```

Speed lines activate above `speedFOVThreshold`.
Camera lag increases with velocity to sell acceleration.

### GlideCharging (vulnerability window)

```cpp
glideChargeFOVReduce = 3         // slight compression during deploy window
glideChargeDamping = 10
```

Brief transition. Screen may darken subtly (vignette pulse) to communicate vulnerability.

### Gliding

```cpp
glideFOV = baseFOV + 3           // slightly wider than ground, narrower than ballistic
glideDistance = baseDistance + 0.5
glideDamping = 14                // tighter damping for smooth, controlled sensation
horizonStabilization = true      // camera pitch drifts toward horizon over 1–2 seconds
```

Camera smoothly stabilizes pitch toward horizon. Communicates control and calm after the chaos of launch.
Wind audio fades in. Speed lines fade to gentle streaks.

### ThermalBoost

```cpp
thermalFOVAdd = 4
thermalDamping = 10
thermalCameraLift = true         // camera tilts slightly upward as player rises
```

Subtle upward camera drift communicates lift. Particle column visible around character.

### Landing Impact

```cpp
landingShakeAmplitude = 0.05–0.20  // scaled by landing speed
landingShakeDecay = 150–300 ms
landingFOVDip = 2–4                // brief FOV compression on impact
landingFOVRecovery = 200 ms
landingCameraDip = 0.3–0.8        // camera drops slightly, recovers over 200ms
```

Impact intensity scales with vertical velocity at landing. Dust particles at feet.
If momentum is preserved (slide landing), camera transitions smoothly to grounded rather than hard-stopping.

## Camera Transition Timing

All camera parameter changes use exponential smoothing with per-state damping rates, not instant snaps.

Exception: the launch FOV punch uses a fast attack (50–100ms) with slow decay (300–500ms) for the "pop" sensation.

| Transition | Duration | Easing |
|---|---|---|
| Grounded → SlingshotCharging | 200ms | ease-out |
| SlingshotCharging → Ballistic | 80–120ms attack, 400ms settle | fast-in, slow-out |
| Ballistic → GlideCharging | 150ms | ease-in |
| GlideCharging → Gliding | 300ms | ease-out |
| Any Airborne → Grounded | 100–200ms (impact), 400ms (settle) | sharp-in, ease-out |

---

# Visual Feedback Per State

Each movement state must be visually distinct. The player should know what state they are in from visuals alone, without any UI indicator.

## State Visual Language

| State | Camera Feel | Character Pose | World Effects | Screen Effects | Audio |
|---|---|---|---|---|---|
| Grounded | tight follow, base FOV | upright, idle/run cycle | footstep dust | none | footsteps |
| SlingshotCharging | pulling back, FOV narrows | crouching, leaning away from launch | ground charge decal, energy particles, elastic line | vignette ramp | tension hum, rising pitch |
| Ballistic | loose follow, wide FOV, trailing | tucked / streamlined | speed lines, launch burst fading | slight motion blur | whoosh, wind rush |
| GlideCharging | slight compression | arms beginning to spread | particles pause briefly | brief vignette pulse | audio dip (vulnerability beat) |
| Gliding | stable, horizon-locked, gentle FOV | arms/wings spread wide | gentle wind streaks, floating particles | clean, no vignette | smooth wind, calm |
| ThermalBoost | tilting upward, moderate FOV | upright, arms slightly raised | updraft particles spiraling around player | none | rising hum, air column |
| Landing (moment) | dip + shake | impact crouch | dust burst at feet, terrain ripple | brief FOV dip | thud scaled by speed |

## Airborne Visual Feedback

Airborne states need continuous visual feedback to prevent "dead airtime" and communicate speed/altitude.

### Speed Lines

- Activate above 15 m/s horizontal velocity
- Intensity scales linearly with speed (15 → 40 m/s maps to 0% → 100% opacity)
- Direction: camera-relative, streaming from forward toward edges
- Implementation: camera-parented particle system or full-screen shader pass
- Fade out over 300ms when speed drops below threshold

### Wind Particles

- Small bright particles streaming past the camera during ballistic and glide
- Density scales with speed
- Direction follows velocity vector relative to camera
- Distinct from speed lines: speed lines are screen-space streaks, wind particles are small discrete dots

### Ground Shadow / Landing Indicator

- Project a circular shadow or indicator below the player during any airborne state
- Helps the player read altitude and predict landing position
- Shadow size decreases as altitude decreases (approaching landing)
- Optional: shadow becomes more opaque near ground to signal imminent landing

### Character Pose

Even with a low-poly character, distinct silhouettes per state are critical for readability:

- **Ballistic:** tucked, compact, arms close to body (speed)
- **Gliding:** arms spread wide, body flattened (control, calm)
- **ThermalBoost:** upright, arms slightly raised (being lifted)
- **Landing anticipation:** legs extend downward in final 2m before ground contact

Pose transitions should blend over 100–200ms, not snap.

## Landing Feedback

Landing must feel impactful to close the movement loop satisfyingly.

### Slide Landing (high horizontal momentum preserved)

- Camera smoothly transitions to grounded damping
- Dust trail behind character during slide
- Character in low crouch, gradually standing
- Speed lines fade over 500ms
- Immediate re-slingshot allowed (no forced pause)

### Hard Landing (high vertical velocity)

- Camera dip + shake proportional to impact speed
- Dust burst at feet (larger with harder landing)
- Brief FOV compression (2–4 degrees, 200ms recovery)
- Character in deep impact crouch
- Screen shake amplitude: `impactSpeed * 0.01` clamped to 0.05–0.20

### Parameter Targets

```cpp
slideThresholdHorizontalSpeed = 8      // above this, landing is a slide
hardLandingVerticalSpeed = 12          // above this, hard landing effects trigger
maxScreenShake = 0.20
dustBurstMinSpeed = 5
dustBurstMaxRadius = 3.0               // at terminal velocity
```

---

# Input Forgiveness

improves perceived responsiveness.

implement:

coyote time:

```cpp
80–200 ms
```

input buffering:

jump or charge queued slightly before landing

angle forgiveness:

small directional tolerance

---

# Traversal Design Principles

---

## Energy Economy

movement converts between:

speed
height
position
timing

example chain:

slingshot → height

glide → distance

thermal → altitude

grapple → direction change

---

## Skill Gradient

low skill:

single slingshot launch

medium skill:

slingshot → glide → chain slingshot on landing

high skill:

chain slingshot on landing (window opens on touch-down, re-launch before it expires)

expert skill:

slingshot → glide → thermal → chain slingshot → grapple → relaunch

---

## Flow Continuity

avoid dead airtime.

provide micro-decisions during airborne states.

encourage terrain scanning.

---

## Terrain Synergy

procedural terrain should produce:

cliffs
slopes
valleys
launch surfaces
landing zones
vertical landmarks

terrain frequency should allow:

medium distance chaining.

---

# 30 Second Fun Test

player should discover:

unexpected distance

expressive angle control

trajectory experimentation

mid-air decisions

momentum chaining potential

---

# MVP Scope

required:

slingshot

glide conversion

momentum preservation

basic air steering

thermal columns

---

optional layer 2:

grapple anchors

boost rings

wind tunnels

trick scoring

---

# Prototype Order

Feedback is interleaved with mechanics so each step produces visible, testable fun.
Do not defer all visual feedback to the end — camera and effects are what make the physics *feel* like a slingshot.

Each step includes automated tests (run via Test Runner) and a visual test (run manually in Play Mode).
Full test specifications: [MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md § 13](MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md).

## Phase A: Slingshot Core + Camera (the "30 second fun" gate)

**1. Switch to third-person camera with orbit control**
- Replace first-person camera setup with CameraEffectResolverSystem
- *Test (EditMode):* Resolver positions camera at BaseDistance behind player
- *Visual:* Enter Play Mode — camera orbits player at ~4m, mouse look works

**2. Implement slingshot charge input**
- LMB+RMB hold detection, mouse drag accumulation, ChargeNormalized power curve
- *Test (EditMode):* SlingshotChargeSystem computes correct ChargeNormalized for given DragDelta
- *Test (EditMode):* Cancel below MinLaunchThreshold returns to Grounded
- *Test (EditMode):* Charge does not activate while airborne
- *Visual:* Hold LMB+RMB, entity debugger shows Mode = SlingshotCharging and rising charge

**3. Camera pull-back during charge**
- CameraChargeFeedbackSystem writes TargetDistance and TargetFOV proportional to charge
- *Test (EditMode):* TargetDistance == BaseDistance + ChargeDistanceAdd * ChargeNormalized
- *Test (EditMode):* TargetFOV == BaseFOV - ChargeFOVReduce * ChargeNormalized
- *Visual:* Hold LMB+RMB, drag mouse — camera visibly pulls back, FOV narrows, subtle shake at high charge

**4. Implement slingshot launch impulse**
- SlingshotLaunchSystem applies velocity on release, transitions to Ballistic
- *Test (EditMode):* PhysicsVelocity matches AimDirection * MaxForce * pow(charge, exponent)
- *Test (EditMode):* Below-threshold release cancels with no velocity change
- *Test (EditMode):* SlingshotChargeState removed after launch
- *Visual:* Full charge + release — player launches at high speed in aimed direction

**5. Camera FOV punch + speed lines on launch**
- CameraSpeedFeedbackSystem applies launch FOV punch with fast attack / slow decay
- *Test (EditMode):* TargetFOV includes launch punch on first frame, decays over subsequent frames
- *Test (EditMode):* FOV scales with speed above SpeedFOVThreshold
- *Visual:* On release, FOV pops wide then settles; speed lines appear during flight

**6. Landing momentum preservation**
- PlayerMovementSystem / grounding does not zero horizontal velocity on contact
- *Test (EditMode):* After grounding, velocity.xz magnitude >= 92% of pre-landing value
- *Test (EditMode):* Existing PlayerMovementAirPathPlayModeTests still pass (regression)
- *Visual:* Shallow-angle launch — player slides on landing with residual speed

**7. Landing camera dip + dust particles**
- CameraLandingFeedbackSystem fires on LandingImpactEvent
- *Test (EditMode):* ShakeOffset proportional to VerticalSpeed, clamped to max
- *Test (EditMode):* LandingImpactEvent fires exactly one frame then disables
- *Visual:* Land from height — camera dips, shakes, dust burst at feet; harder = bigger effect

**Phase A Gate (stop and tune if this fails):**
- [ ] Do you voluntarily charge and launch again without being told to?
- [ ] Do you experiment with different angles and charge amounts?
- [ ] Do you try to beat your previous distance?
- If NO: tune parameters before proceeding. Camera and impulse feel are the priority.

## Phase B: Air Control + Glide

**8. Add ballistic air steering**
- PlayerMovementSystem uses AirControlBallistic rate when Mode == Ballistic
- *Test (EditMode):* Horizontal velocity lerps at AirControlBallistic, not GroundLerpRate
- *Visual:* During flight, WASD gently steers trajectory (weaker than ground movement)

**9. Camera speed-based offset during flight**
- CameraSpeedFeedbackSystem adjusts distance and damping based on velocity
- *Test (EditMode):* TargetDistance increases with speed; Damping == BallisticDamping
- *Visual:* High speed = camera further back and trailing; slowing down = camera returns closer

**10. Implement glide conversion**
- GlideSystem: space hold during Ballistic above minGlideHeight → GlideCharging → Gliding
- *Test (EditMode):* Space hold for glideChargeTime transitions Mode to Gliding
- *Test (EditMode):* Vertical velocity clamps toward glideFallSpeed during Gliding
- *Test (EditMode):* Horizontal speed preserved at glideForwardPreservation rate
- *Test (EditMode):* Below minGlideHeight, space hold does not trigger glide
- *Test (EditMode):* Space release during GlideCharging cancels back to Ballistic
- *Visual:* Hold Space mid-flight — descent slows, feel changes to controlled glide

**11. Camera stabilization during glide**
- CameraGlideFeedbackSystem sets calm FOV, tight damping, horizon stabilization
- *Test (EditMode):* During Gliding, HorizonStabilize == true, Damping == GlideDamping
- *Visual:* Camera pitch drifts toward horizon, movement feels calm and stable

**12. Character pose changes per state**
- Placeholder: scale/rotation changes per MovementMode (real animation later)
- *Visual:* Grounded=upright, Charging=crouch, Ballistic=tuck, Gliding=spread
- *Visual:* Transitions blend over 100–200ms, no snapping

**13. Wind particles and speed line refinement**
- Particle systems driven by velocity magnitude
- *Visual:* Speed lines above 15 m/s, wind particles during flight, both scale with speed and fade on deceleration

## Phase C: Thermals + Chaining

**13.5. Implement chain slingshot**
- ChainWindowSystem: opens window on landing when ChainCount > 0, ticks down, resets ChainCount on expiry
- SlingshotLaunchSystem: additive velocity formula + chain bonus multiplier when ChainCount > 0
- *Test (EditMode):* ChainWindowSystem opens window on LandingImpactEvent when ChainCount > 0, counts down, resets ChainCount at expiry
- *Test (EditMode):* ChainWindowSystem does NOT open window on landing if ChainCount == 0
- *Test (EditMode):* Launch velocity is additive (chain 2 speed > chain 1 speed)
- *Test (EditMode):* Bonus multiplier scales correctly with ChainCount (capped at ChainMaxCount)
- *Visual:* Launch → touch ground → charge → relaunch — speed noticeably higher on chain

**14. Implement thermal columns**
- ThermalColumnSystem: volumes that apply vertical boost to player
- *Test (EditMode):* velocity.y increases by verticalBoostAcceleration * dt inside thermal
- *Test (EditMode):* velocity.y clamped to maxUpwardVelocity
- *Test (EditMode):* Horizontal preserved at horizontalVelocityMultiplier rate
- *Visual:* Fly into thermal — player lifts upward, feels gradual not instant

**15. Thermal visual feedback**
- Particle column, camera tilt
- *Visual:* Thermal columns visible from distance, camera tilts up while inside

**16. Tune chaining continuity**
- No velocity resets between state transitions
- *Test (PlayMode):* SlingshotFullChainPlayModeTests — slingshot → glide → thermal sequence preserves velocity magnitude within expected decay
- *Test (PlayMode):* MovementStateTransitionPlayModeTests — only valid transitions occur (no Grounded → Gliding)
- *Visual:* Execute full chain — feels continuous, no dead stops or jarring resets

**17. Ground shadow / landing indicator**
- Projected shadow below player during airborne states
- *Visual:* Shadow visible during flight, shrinks approaching ground, helps predict landing

**18. Full camera transition polish**
- Review all state boundary camera transitions against timing table
- *Visual:* Watch full movement chain — no jarring camera jumps between states
- Grounded→Charging: ~200ms ease-out
- Charging→Ballistic: ~100ms attack, ~400ms settle
- Ballistic→Gliding: ~300ms ease-out
- Airborne→Grounded: impact dip + 400ms recovery

## Phase D: Polish + Layer 2 (post-MVP)

19. Grapple mechanic
20. Boost rings / wind tunnels
21. Screen effects polish (vignette, chromatic aberration)
22. Audio integration
23. Trick scoring / combo system

---

# Tuning Checklist

movement feels good when:

player intentionally seeks height

player experiments with angles

player attempts longer routes

player chains actions without prompting

player invents traversal challenges

---

# Future Extensions

boost rings

midair pickups

thermal biome variation

wind tunnels

movement combo scoring

biome-specific traversal rules

procedural traversal landmarks

---

# Summary

The traversal stack supports:

expressive control

high skill ceiling

procedural exploration synergy

continuous player engagement

strong mechanical identity

movement-first gameplay foundation

---
