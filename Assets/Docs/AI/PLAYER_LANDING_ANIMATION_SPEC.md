# Player Landing Animation & Clipping Fix Spec

**Status:** ACTIVE  
**Created:** 2026-05-19  
**Revised:** 2026-05-19 (v3 — transition-first phased approach)  
**Related:** `MVP_Movement/MOVEMENT_PLANNING.md`, `AI/PLAYER_CHARACTER_VISUAL_SWAP_SPEC.md`

---

## Problem Statement

After a slingshot launch the player follows a ballistic arc, bounces on terrain contact, and exhibits three compounding visual defects:

1. **Terrain clipping** — On high-speed impact, physics penetration resolution lags one or more frames. The character's visual origin clips partially underground, showing the torso bisected by the terrain surface. *(implemented, pending validation)*
2. **Jarring animation cuts** — Transitions between all airborne states (falling, gliding) and between airborne and grounded snap instantly with no blend. The problem is not that clips are too similar — it is that there is no transition at all.
3. **Tiered landing clips missing** — New triggers (`StandardLandingTrigger`, `HardLandingTrigger`, `SlideLandingTrigger`) were wired into the bridge but the animator controller has no states for them, so they fire into nothing. The original `LandingTrigger` no longer fires for hard/standard impacts, leaving those landings worse than before.

---

## Root Cause Summary

The code changes to `PlayerAnimatorBridge` got ahead of the animator controller. The bridge now dispatches tiered triggers that don't exist as controller parameters yet, while the original single-trigger path that did work has been removed for hard/standard impacts. This must be stabilised before any new clip work begins.

---

## Goals

- Restore stable landing behaviour as a safe baseline (single trigger, original behaviour).
- Fix jarring transitions across all airborne states in the animator controller.
- Progressively enable tiered landing responses behind a feature flag once controller states are ready.
- Eliminate terrain clipping on landing frames.

---

## Non-Goals

- Full ragdoll or procedural IK on landing.
- New `PlayerMovementMode` enum values.
- Changes to terrain physics or collider shape.
- Root-motion slide facing (deferred post-MVP).

---

## Phased Approach

### Phase 1 — Stabilise (do now)

Restore the original single-trigger landing behaviour as a safe fallback, controlled by a flag in `LandingConfig`. When the flag is active, the bridge fires only `LandingTrigger` for all landings — exactly as it did before the tiered work. This gets landings back to a known-good state while the animator controller is built out.

### Phase 2 — Fix Transitions (do now, in Unity Editor)

The primary cause of "jarring" is transition blend settings in the animator controller, not missing clips. All clips may already be distinct enough; the issue is that transitions snap instantly. This phase is pure animator controller work — no code changes.

### Phase 3 — Tiered Landing States (do when clips are available)

Flip the flag to enable tiered dispatch. Wire each new trigger to a dedicated animator state. Source or author the missing clips. The code is already written; only the controller and clips are outstanding.

---

## Architecture Constraints (carried forward from v2)

These are already implemented and remain correct:

| Constraint | Decision | Status |
|---|---|---|
| `IsGrounded`/`Mode` divergence on high-speed landing | Detect landing on `IsGrounded` edge, not `Mode` edge | ✅ implemented |
| `GroundedBool` must use `IsGrounded`, not `Mode == Grounded` | Bridge updated | ✅ implemented |
| `LandingRecoveryTime` must be written in `PhysicsSystemGroup` | Written in `PlayerGroundingSystem` before movement systems run | ✅ implemented |
| Root motion disabled; yaw override for slide facing | Bridge writes yaw only when `LandingIsSlide` is true | ✅ implemented |
| Contact-Y needed for floor clamp | `LandingImpactEvent.GroundContactY` added | ✅ implemented |

---

## Phase 1 — Stabilise: Fallback Flag

### `LandingConfig` — add flag

```csharp
public struct LandingConfig : IComponentData
{
    // existing fields ...
    /// <summary>
    /// When true, all landings fire LandingTrigger regardless of speed tier.
    /// Set false only after the animator controller has dedicated states for
    /// StandardLandingTrigger, HardLandingTrigger, and SlideLandingTrigger.
    /// </summary>
    public bool UseSimpleLandingTrigger;   // default: true

    public static LandingConfig Default => new LandingConfig
    {
        // existing defaults ...
        UseSimpleLandingTrigger = true,
    };
}
```

### `PlayerAnimatorBridge` — honour the flag

In the trigger dispatch block, check `UseSimpleLandingTrigger` first:

```csharp
if (hasLandingEvent)
{
    if (cfg.UseSimpleLandingTrigger)
    {
        // Safe fallback: original behaviour, works with any controller.
        CharacterAnimator.SetTrigger(LandingTriggerHash);
    }
    else
    {
        // Tiered dispatch — requires controller states for all three new triggers.
        bool isHard  = evt.VerticalSpeed >= cfg.HardLandingVerticalSpeed;
        bool isSlide = isHard && evt.HorizontalSpeed >= cfg.SlideThresholdHorizontalSpeed;
        bool isStd   = !isHard && evt.VerticalSpeed >= cfg.StandardLandingVerticalSpeed;

        if (isSlide)        CharacterAnimator.SetTrigger(SlideLandingTriggerHash);
        else if (isHard)    CharacterAnimator.SetTrigger(HardLandingTriggerHash);
        else if (isStd)     CharacterAnimator.SetTrigger(StandardLandingTriggerHash);
        else                CharacterAnimator.SetTrigger(LandingTriggerHash);
    }
}
```

---

## Phase 2 — Fix Transitions (Animator Controller)

**This is the highest-leverage work.** The "too jarring" problem is caused by instant-cut transitions, not by missing clips.

### Principles

| Rule | Reason |
|---|---|
| All parameter-driven transitions: `Has Exit Time = false` | Clips must respond immediately to state changes, not wait to finish playing |
| All blend durations: 0.1–0.15s by default | Gives the body time to move between poses without swimming |
| Fall → Landing: 0.05s or 0 | The impact moment should be sharp; softening it reads as floating |
| Landing → Idle/Walk: 0.15s | Let the landing pose resolve before blending to walk |
| Any → Glide: 0.1s | Enough to see the arms-out transition |
| Glide → Fall: 0.1s | Avoids the instant-drop read |

### Specific Transitions to Fix

| From | To | Condition | Blend | Has Exit Time |
|---|---|---|---|---|
| Any State | Landing | `LandingTrigger` | 0.05s | false |
| Falling | Idle/Walk | `GroundedBool = true` | 0.1s | false |
| Ballistic Rising | Falling | `BallisticRising = false` | 0.1s | false |
| Any | Gliding | `MovementMode = Gliding` | 0.1s | false |
| Gliding | Falling | `MovementMode ≠ Gliding` | 0.1s | false |
| Landing | Idle/Walk | Exit time (clip end) | 0.15s | **true** — let landing clip play out |

### Also Check

- Is the **Falling** clip a looping clip? It should be. If it has exit time on, the loop won't hold.
- Does the **Idle/Walk blend tree** have a threshold set so that low-speed landing doesn't immediately jump to full walk?
- Are any **AnyState → Falling** transitions firing too eagerly while `BallisticRising` is still true?

---

## Phase 3 — Tiered Landing States

Only begin Phase 3 after Phase 2 transitions are confirmed to feel non-jarring.

### Steps

1. Add Animator parameters: `StandardLandingTrigger`, `HardLandingTrigger`, `SlideLandingTrigger` (Trigger), `LandingRecoveryNormalized` (Float).
2. Add three Animator states: `StandardLanding`, `HardLanding`, `SlideLanding`.
3. Wire transitions from `Any State` with the appropriate trigger, blend 0.05s, `Has Exit Time = false`.
4. Each state exits to Idle/Walk blend tree at clip end, blend 0.15s.
5. Source or author clips:
   - **Standard landing** (~0.25s): soft crouch-stand. Check Kevin Iglesias pack first.
   - **Hard landing** (~0.5s): stumble/stagger. Check Kevin Iglesias pack first.
   - **Slide landing** (~0.5s): forward lean + decelerate. May need custom authoring.
6. Flip `UseSimpleLandingTrigger = false` in `LandingConfig.Default` (or in the ScriptableObject).
7. Validate all four tiers in play mode.

---

## Acceptance Criteria

### Phase 1 (stabilise)
- [ ] Hard and standard slingshot landings play the original `Landing` clip — no worse than before the tiered work. **(manual)**
- [ ] No console errors from unknown Animator parameters. **(manual)**

### Phase 2 (transitions)
- [ ] Falling → Landing transition has no visible snap or pop. **(manual)**
- [ ] Falling → Gliding transition is visually readable (not an instant cut). **(manual)**
- [ ] Gliding → Falling transition is visually readable. **(manual)**
- [ ] All transition blend times confirmed set in animator controller. **(manual)**

### Phase 3 (tiered)
- [ ] Slingshot impact > 12 m/s vertical plays `HardLanding` or `SlideLanding`. **(manual)**
- [ ] Impact 6–12 m/s plays `StandardLanding`. **(manual)**
- [ ] Light landings (< 6 m/s) unchanged. **(manual)**
- [ ] High-horizontal-speed landing plays `SlideLanding`; character faces travel direction. **(manual)**

### Clipping (already implemented)
- [ ] After slingshot impact, no part of character mesh appears below terrain surface. **(manual)**
- [ ] Player cannot slingshot or jump while `LandingRecoveryTime > 0`. **(automatable)**
- [ ] `LandingImpactEvent` fires on `IsGrounded` edge on high-speed impact. **(automatable)**
- [ ] `LandingImpactEvent` does NOT fire on the grounded edge right after a slingshot launch (upward velocity > 0.5 m/s gates it — the grounding probe can re-hit the ground before the character rises clear, which previously fired a phantom hard landing at takeoff). **(automatable)**

---

## Open Questions

- Are standard/hard landing clips available in the Kevin Iglesias pack, or do they need to be authored?
- Should the visual floor clamp extend to the full ballistic arc, or remain landing-frame-only?

*(Resolved v2: `LandingRecoveryTime` thresholds live in `LandingConfig`. Detection uses `IsGrounded` edge. Yaw override is slide-only.)*
