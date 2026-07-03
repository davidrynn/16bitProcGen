# Speed-Based Camera Shake Spec

**Status:** DESIGN — ready to implement  
**Phase Fit:** Phase 1 (movement feel polish)  
**Last Updated:** 2026-04-29  
**Related:** `MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md`, `CameraSpeedFeedbackSystem.cs`

---

## 1. Goal

The faster the player travels, the more the camera shakes — giving a physical sense of speed and air resistance without affecting aim accuracy at low speeds. The effect should feel similar in character to the slingshot charge shake but driven continuously by velocity rather than triggered on a single event.

---

## 2. Behaviour

| Speed (m/s horizontal) | Shake |
|---|---|
| Below threshold (~15 m/s) | None |
| 15 – 40 m/s | Subtle, low-frequency shimmer |
| 40 – 80 m/s | Noticeable, medium-frequency vibration |
| 80+ m/s (terminal / thermal boost) | Strong, rapid shake |

- Shake is **additive** to the camera's position via `CameraEffectState.ShakeOffset` (the field already exists).
- Shake amplitude and frequency both scale with speed — not just amplitude.
- Shake is **frame-independent**: generated fresh each frame, not a decaying impulse, so it tracks speed changes in real time.
- Active only in airborne states: `Ballistic`, `Gliding`, `GlideCharging`, `ThermalBoost`. No shake while `Grounded`.
- During sky-drop freefall (vertical only, low horizontal speed), shake should reflect **total speed** (vertical + horizontal), so the drop itself feels physical.

---

## 3. Implementation Path

### 3.1 Where to add it

Extend `CameraSpeedFeedbackSystem` — it already queries `CameraEffectState`, `CameraEffectConfig`, and `PlayerMovementState` in the exact states needed. Add shake writes to the existing `if (isAirborne)` block.

### 3.2 Config values to add to `CameraEffectConfig`

```csharp
// Speed shake
public float ShakeSpeedThreshold;   // m/s below which shake = 0. Default: 15
public float ShakeAmplitudeScale;   // shake amplitude per m/s above threshold. Default: 0.0012
public float ShakeAmplitudeMax;     // clamp. Default: 0.06
public float ShakeFrequency;        // base oscillation frequency (Hz). Default: 18
public float ShakeFrequencyScale;   // additional Hz per m/s above threshold. Default: 0.15
```

### 3.3 Fragment — shake generation (inside the airborne branch)

```csharp
// Speed shake — uses total speed so sky-drop freefall also shakes
float totalSpeed = math.length(velocity);
float speedAboveShakeThreshold = math.max(0f, totalSpeed - config.ValueRO.ShakeSpeedThreshold);
float shakeAmp = math.min(
    speedAboveShakeThreshold * config.ValueRO.ShakeAmplitudeScale,
    config.ValueRO.ShakeAmplitudeMax);

if (shakeAmp > 0f)
{
    float freq = config.ValueRO.ShakeFrequency
               + speedAboveShakeThreshold * config.ValueRO.ShakeFrequencyScale;
    float t = (float)SystemAPI.Time.ElapsedTime * freq;
    // Two orthogonal sinusoids on a slightly different frequency give organic feel
    effectState.ValueRW.ShakeOffset += new float3(
        math.sin(t * 1.00f) * shakeAmp,
        math.sin(t * 1.37f) * shakeAmp * 0.6f,
        0f);
}
```

### 3.4 Sky-drop specifics

During the sky-drop intro the player falls vertically — horizontal speed is near zero but vertical speed grows to ~90 m/s by the time terrain chunks appear. Using `math.length(velocity)` (total speed) means shake naturally ramps up through the fall, creating a "terminal velocity shudder" without any special-casing.

---

## 4. Relationship to Slingshot Shake

The slingshot charge shake (`CameraChargeFeedbackSystem`) is an **event-driven impulse** — triggered on the charge frame and decayed via `ShakeDecayRate`. Speed shake is a **continuous signal** driven by current velocity. Both write to `ShakeOffset` additively, so they stack during a launch (highest shake at the moment of release when charge + speed peak together).

`MovementStateBookkeepingSystem` resets `ShakeOffset` to zero each frame before these systems write, so there is no accumulation across frames.

---

## 5. Config Defaults (tuning starting point)

```
ShakeSpeedThreshold   = 15 m/s   (walking pace = no shake)
ShakeAmplitudeScale   = 0.0012   (at 80 m/s → 0.078, clamped to 0.06)
ShakeAmplitudeMax     = 0.06     (about half the landing shake)
ShakeFrequency        = 18 Hz    (feels mechanical/aerodynamic)
ShakeFrequencyScale   = 0.15     (at 80 m/s → 27 Hz total)
```

---

## 6. Acceptance Criteria

- [ ] No shake at walking/grounded speed
- [ ] Subtle shimmer visible at slingshot cruising speed (~30 m/s)
- [ ] Clear shake at full-speed Ballistic state (~80+ m/s)
- [ ] Shake ramps noticeably during sky-drop freefall as speed builds
- [ ] Shake stops immediately on landing (grounded state)
- [ ] Slingshot launch: charge shake + speed shake stack naturally at the peak moment
- [ ] No shake during `Grounded` state regardless of slope/slide speed
