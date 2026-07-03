# AAA Traversal Feel Checklist

**Status:** ACTIVE
**Last Updated:** 2026-04-14
**Owner:** Player Systems
**Role:** Post-implementation validation and tuning artifact. Not an implementation source — use [MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md](MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md) for implementation and [MOVEMENT_PLANNING.md](MOVEMENT_PLANNING.md) for design intent.

Use this checklist when tuning movement parameters or evaluating playtests.

Goal: identify why movement does or does not feel good within 30 seconds.

## Related Docs

- [MOVEMENT_PLANNING.md](MOVEMENT_PLANNING.md) — Full movement spec with parameter targets
- [MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md](MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md) — ECS component/system mapping

---

# 1. Responsiveness

Movement reacts immediately to input.

Targets:

input response visible within 80–120 ms

player can cancel charge early

no unnecessary animation lock

Questions:

does slingshot begin charging immediately?

can player release partial charge?

can glide input be buffered?

Failure symptoms:

movement feels sticky or delayed

player presses inputs repeatedly to confirm responsiveness

---

# 2. Acceleration Curve Quality

Velocity changes feel natural rather than abrupt.

Check:

does initial motion feel powerful?

does top speed feel controllable?

is acceleration non-linear?

Preferred curve:

fast early acceleration
gradual taper approaching max speed

Failure symptoms:

movement feels jerky or robotic

player overshoots frequently

---

# 3. Momentum Preservation

Velocity persists between actions.

Check:

landing does not zero velocity

slingshot preserves incoming momentum

grapple modifies vector rather than replacing velocity

Failure symptoms:

movement feels segmented rather than continuous

players stop between actions instead of chaining

---

# 4. Input Forgiveness

System helps players succeed.

Implement:

coyote time

input buffering

angle forgiveness

Example targets:

coyote time 80–200 ms

buffer window 100–150 ms

Failure symptoms:

player feels controls are unreliable

player misses intended actions frequently

---

# 5. Trajectory Readability

Player can predict motion outcome.

Check:

slingshot direction intuitive

glide path understandable

thermal lift predictable

camera communicates speed

Failure symptoms:

player surprised by motion results

player avoids experimenting

---

# 6. Skill Floor vs Skill Ceiling

System easy to use but supports mastery.

Low skill:

single slingshot launch

Medium skill:

slingshot → glide

High skill:

slingshot → glide → thermal → grapple → relaunch

Failure symptoms:

players stop improving quickly

or feel overwhelmed immediately

---

# 7. Energy Economy

Movement converts between:

speed
height
position
timing

Examples:

slingshot converts input distance → velocity

glide converts speed → distance

thermal converts position → altitude

grapple converts anchor → direction change

Failure symptoms:

movement tools feel redundant

players ignore certain mechanics

---

# 8. Flow Continuity

Player remains engaged continuously.

Check:

no long passive airtime

minimal cooldown waiting

decisions available mid-flight

Failure symptoms:

downtime between actions

player waits for cooldowns

---

# 9. Chaining Potential

Actions can connect fluidly.

Example chains:

slingshot → glide

slingshot → glide → thermal

slingshot → grapple → relaunch

Check:

no hard velocity resets

minimal forced landing pauses

Failure symptoms:

players perform one action at a time only

movement feels fragmented

---

# 10. State Clarity

Each movement state feels distinct.

State sensations:

grounded stable

charging tense

ballistic powerful

gliding smooth

thermal lifted

grappling elastic

Failure symptoms:

movement feels mushy

players unsure what state they are in

---

# 11. Camera Cooperation

Camera enhances motion sensation across all movement phases.

## 11a. Charge Phase Camera

Check:

does camera pull back during slingshot charge?

does FOV narrow slightly to create tension?

does subtle shake communicate charge intensity?

is camera orbit locked to charge direction (no free-look)?

Failure symptoms:

charging feels like standing still

player cannot read charge level from camera alone

## 11b. Launch Phase Camera

Check:

does FOV punch wide on release?

does the camera snap forward (inverse of pull-back)?

is the transition fast enough to feel like a "pop"?

does camera lag increase to sell initial acceleration?

Failure symptoms:

launch feels weak despite high velocity

player doesn't perceive speed difference between partial and full charge

## 11c. Flight Phase Camera

Check:

does FOV scale with speed?

does camera trail behind during high-speed flight?

is damping loose enough to sell momentum?

are speed lines visible above threshold velocity?

Failure symptoms:

ballistic flight feels floaty or slow

player surprised by actual distance covered

## 11d. Glide Phase Camera

Check:

does camera stabilize toward horizon during glide?

does damping tighten for smooth, controlled sensation?

does FOV settle to a calm intermediate value?

Failure symptoms:

glide feels chaotic instead of serene

player gets motion sick during extended glide

## 11e. Landing Camera

Check:

does camera dip on impact proportional to landing speed?

does camera shake fire on hard landings?

does FOV briefly compress on impact?

does camera smoothly return to grounded state after slide?

Failure symptoms:

landings feel weightless

hard landings and soft landings feel identical

---

# 12. Error Recovery

Players can recover from mistakes.

Examples:

partial midair charge

thermal saves

moderate air steering

Failure symptoms:

small mistakes lead to failure cascades

players play cautiously instead of expressively

---

# 13. Terrain Synergy

Traversal interacts with world geometry.

Check:

terrain provides launch surfaces

terrain frequency supports chaining distances

vertical landmarks visible

Failure symptoms:

movement feels disconnected from world

terrain irrelevant to traversal decisions

---

# 14. Surprise Without Randomness

Movement produces emergent outcomes but feels fair.

Check:

player understands why motion occurred

thermal strength predictable

grapple arc readable

Failure symptoms:

movement feels random

players distrust system

---

# 15. Timing Windows

Small timing windows reward mastery.

Examples:

optimal slingshot release timing

glide deploy timing affects distance

thermal entry angle affects lift

Failure symptoms:

movement feels flat or same every time

---

# 16. Mechanical Identity

Each tool serves distinct role.

slingshot burst displacement

glide distance shaping

thermal altitude sustain

grapple direction redirect

Failure symptoms:

one mechanic dominates

others ignored

---

# 17. Friction Budget

Avoid unnecessary constraints.

avoid excessive cooldowns

avoid stamina bars initially

avoid complex button combos

Failure symptoms:

player feels restricted

movement avoided due to effort

---

# 18. Sensory Feedback

Movement reinforced through multi-channel feedback. Each state should be distinguishable through visuals, camera, and audio without UI indicators.

## Per-State Feedback Matrix

### Grounded

| Channel | Expected Feedback |
|---|---|
| Camera | tight follow, base FOV, base distance |
| Character | upright, idle/walk/run cycle |
| Particles | footstep dust (optional) |
| Screen | none |
| Audio | footsteps, ambient |

### SlingshotCharging

| Channel | Expected Feedback |
|---|---|
| Camera | pulling back, FOV narrowing, shake ramping |
| Character | crouching, leaning away from launch direction |
| Particles | ground charge decal, energy buildup at feet |
| Screen | vignette ramp (0→15%) |
| Audio | tension hum, rising pitch proportional to charge |

### Ballistic

| Channel | Expected Feedback |
|---|---|
| Camera | wide FOV, loose trailing, speed-scaled distance |
| Character | tucked/streamlined pose |
| Particles | speed lines, wind particles, launch burst fading |
| Screen | slight motion blur (optional) |
| Audio | whoosh on launch, wind rush scaling with speed |

### Gliding

| Channel | Expected Feedback |
|---|---|
| Camera | horizon-stabilized, moderate FOV, tight damping |
| Character | arms/wings spread, body flattened |
| Particles | gentle wind streaks |
| Screen | clean, no post-processing |
| Audio | smooth sustained wind |

### ThermalBoost

| Channel | Expected Feedback |
|---|---|
| Camera | gentle upward tilt, moderate FOV |
| Character | upright, arms slightly raised |
| Particles | updraft spiral around player |
| Screen | none |
| Audio | rising hum, air column |

### Landing

| Channel | Expected Feedback |
|---|---|
| Camera | dip + shake (proportional to speed) |
| Character | impact crouch (depth scales with speed) |
| Particles | dust burst at feet (radius scales with speed) |
| Screen | brief FOV dip |
| Audio | thud (volume/pitch scale with impact speed) |

## Evaluation Questions

For each state, ask:

can I tell what state I'm in with eyes half-closed? (audio)

can I tell what state I'm in from a screenshot? (visual)

can I tell what state I'm in from camera motion alone? (camera)

If any state fails two or more channels: that state needs more feedback work.

Failure symptoms:

movement feels weak despite high velocity

states feel interchangeable or mushy

player unsure whether they are charging, flying, or gliding

---

# 19. 30 Second Fun Test

Player voluntarily repeats movement.

observe:

player experiments with angle

player seeks height

player attempts chaining

player explores terrain

Failure symptoms:

player waits for objectives

player walks instead of using movement tools

---

# 20. Visual State Readability

The player should always know what movement state they are in from visuals alone.

Test method:

take a screenshot during each movement state.

show screenshots to someone unfamiliar with the game.

ask: which ones look different from each other?

Check:

every state has at least two visually distinct properties (pose + camera + particles)

no two states look identical in a still frame

state transitions produce a visible change within 200ms

Failure symptoms:

testers cannot distinguish ballistic from gliding

charging looks identical to standing still

players report "I didn't know I could glide" after extended play

---

# 21. Camera-Only Fun Test

Mute audio. Hide all UI. Disable particles.

With only the camera and character visible, does the slingshot still feel fun?

Check:

charge pull-back reads clearly

launch punch is viscerally satisfying

flight speed is readable from camera lag and FOV

glide feels calm and controlled

landing has weight

If the movement feels flat with camera-only: camera tuning is the priority, not more effects layers.

---

# How to Use This Checklist

During playtests:

identify which category is failing

adjust parameters affecting that category

retest immediately

iterate rapidly

Traversal feel typically improves through many small adjustments rather than large redesigns.
