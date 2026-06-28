# Player Character Visual Swap Spec
## Synty SM_Chr_Male_01 + Kevin Iglesias Animations

**Status:** PROPOSED
**Last Updated:** 2026-05-15
**Owner:** Player Systems / Art Integration

---

## Related Docs

- [MVP Movement/MOVEMENT_PLANNING.md](../MVP%20Movement/MOVEMENT_PLANNING.md) — Movement state machine and Step 12 (character pose per state)
- [MVP Movement/MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md](../MVP%20Movement/MOVEMENT_TECHNICAL_ARCHITECTURE_SPEC.md) — ECS component/system mapping
- [AI/THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md](THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md) — Sandbox validation checklist
- [/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md](/Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) — Bootstrap pattern guide

---

## Goal

Replace the placeholder capsule visual with the Synty `SM_Chr_Male_01` character and drive it with Kevin Iglesias animation clips matched to each `PlayerMovementMode` state. All ECS physics and game logic remain untouched; the visual layer is a companion GameObject bridge.

This spec fulfills **Step 12** of the Movement Planning prototype order:

> "Character pose changes per state — Placeholder: scale/rotation changes per MovementMode (real animation later)"

---

## Asset Inventory

### Confirmed present

| Asset | Path | Notes |
|-------|------|-------|
| Synty character FBX | `Assets/Synty/PolygonStarter/Models/Characters.fbx` | Contains SM_Chr_Male_01 skeleton |
| Synty character prefab | `Assets/Synty/PolygonStarter/Prefabs/Characters/SM_Chr_Male_01.prefab` | Humanoid rig, no Animator Controller attached |
| Kevin Iglesias folder | `Assets/Kevin Iglesias/` | **Empty — asset import is a prerequisite** |

### Needs importing before implementation

Kevin Iglesias clips required (import from Asset Store into `Assets/Kevin Iglesias/`):

| Pack | Clips needed |
|------|-------------|
| Movement Animset (or equivalent) | Idle, Walk, Run, Sprint |
| Jump, Tumble & Fall (or equivalent) | Jump_Start, Airborne_Fall, Landing_Hard, Landing_Soft |
| Glider / Hang Glider Animset (or equivalent) | GlideCharging (arms spreading), Glide_Loop, ThermalRise |
| Slingshot / Crouch | SlingshotCharge (crouched compression) or repurpose Crouch_Idle |

If the packs above are not the exact names, map whatever clip names exist in the imported pack to the states in §Animation State Map.

---

## Architecture: Hybrid Visual Bridge

### Constraint

Unity 6.2 Entities Graphics does not support `SkinnedMeshRenderer` + `Animator` through the ECS rendering pipeline. Production-quality character skinning requires a hybrid approach.

### Pattern

```
ECS World
  └─ Player Entity (authoritative physics + state)
       ├─ PhysicsCollider (capsule, unchanged)
       ├─ PhysicsVelocity
       ├─ PlayerMovementState  ← bridge reads this
       └─ LocalTransform       ← bridge reads this

GameObject Layer (visual-only, no gameplay logic)
  └─ PlayerCharacterVisual  [GameObject]
       ├─ PlayerVisualSync       (existing — syncs position/rotation from entity)
       ├─ PlayerAnimatorBridge   (new — drives Animator from PlayerMovementState)
       └─ SM_Chr_Male_01  [child GameObject]
            ├─ SkinnedMeshRenderer
            └─ Animator  (Humanoid, Controller = PlayerAnimatorController)
```

**The ECS entity is the source of truth.** The bridge only reads — it never writes back to ECS.

---

## Phase A: Synty Character Wiring (no animations)

**Goal:** Replace the blue capsule visual with the Synty model, positioned and scaled correctly. No Animator yet.

### Step A1 — Validate rig

Open `Assets/Synty/PolygonStarter/Models/Characters.fbx` in the Inspector:

- **Animation Type** must be **Humanoid**
- If it is Generic or None: set to Humanoid, click Configure, verify all required bones (spine, hips, head, arms, legs) are mapped, Apply.

### Step A2 — Verify URP materials

Open `SM_Chr_Male_01.prefab`. Confirm all materials use a URP-compatible shader (URP/Lit or URP/Unlit). The Synty POLYGON pack typically uses their custom `POLYGON` shader — verify it renders correctly in Play Mode with URP. If materials appear pink: reassign to `Assets/Synty/PolygonGeneric/Materials/` equivalents.

### Step A3 — Assemble the visual GameObject

In `PlayerCameraBootstrap_WithVisuals` (or the equivalent scene setup), replace the existing placeholder visual cube/capsule with:

```
PlayerCharacterVisual  [Empty GameObject]
  ├─ PlayerVisualSync (already exists — target entity = player entity, visualOffset = (0, 0, 0))
  └─ SM_Chr_Male_01  [drag prefab in as child]
       └─ (SkinnedMeshRenderer + Animator, no controller yet)
```

Scale and offset the SM_Chr_Male_01 child so the character's feet sit at the entity origin (LocalPosition Y = 0, no Y offset needed if the character root is at hip height — adjust empirically).

**Visual test:** Press Play. The Synty character should appear at the player position and move with the player. It will be in T-pose.

### Step A4 — Collider remains on ECS entity

Do not add a collider to the visual GameObject. The capsule `PhysicsCollider` stays on the ECS player entity unchanged. No physics change in this phase.

---

## Phase B: Kevin Iglesias Import + Animator Controller

**Prerequisites:** Kevin Iglesias packs imported into `Assets/Kevin Iglesias/`.

### Step B1 — Verify animation rig compatibility

Kevin Iglesias clips use the Unity Humanoid standard. Open one imported `.anim` file:

- Inspector → Animation tab → confirm clip type is **Humanoid**
- Test retargeting: drag the clip into the Synty character scene, enter Play Mode, confirm bones move correctly

If the Synty skeleton mapping causes limb distortion: tweak the Avatar in `Characters.fbx` import settings → Configure → manually remap problem bones.

### Step B1.5 — Author the Gliding pose clip

No glide clip exists in Human Basic Motions FREE. Author a one-frame arms-out pose directly on the Synty rig:

1. Select `SM_Chr_Male_01` in the Hierarchy (in Play Mode or a test scene).
2. Open **Window → Animation → Animation**.
3. Click **Create** → save as `Assets/Kevin Iglesias/Glide_Pose.anim`.
4. At frame 0, rotate both upper arm bones outward (~80°) and slightly forward so the character looks like they're hang-gliding flat.
5. Set clip to **loop** and **Root Transform Rotation: Bake into Pose**.
6. This clip is a single static pose looped — no keyframe motion needed.

### Step B2 — Build the Animator Controller

Create `Assets/Scripts/Player/Animation/PlayerAnimatorController.controller`.

**Parameters:**

| Parameter | Type | Purpose |
|-----------|------|---------|
| `Speed` | Float | Ground movement speed (0–1 normalized against RunSpeed) |
| `MovementMode` | Int | Mirrors `PlayerMovementMode` enum value |
| `ChargingNormalized` | Float | Slingshot charge level 0–1 (drives crouch depth) |
| `LandingTrigger` | Trigger | Fires once on landing event |
| `GroundedBool` | Bool | True when Mode == Grounded |
| `BallisticRising` | Bool | True when Ballistic AND vertical velocity ≥ 0 (still rising) |

**Confirmed animation mapping:**

| State | Clip | Loop | Notes |
|-------|------|------|-------|
| `Grounded` speed=0 | Idle | yes | |
| `Grounded` speed>0 | Walk → Run (blend tree) | yes | blend on `Speed` param |
| `SlingshotCharging` | Crouching Idle | yes | additive layer drives crouch depth via `ChargingNormalized` |
| `Ballistic` + `BallisticRising=true` | Falling (reused) | yes | MVP: same fall clip while rising; separate state label kept for a future dedicated rise anim |
| `Ballistic` + `BallisticRising=false` | Falling | yes | vy < 0, gravity winning |
| `GlideCharging` | Falling (cross-fade) | yes | brief transition window |
| `Gliding` | Glide_Pose (custom 1-frame) | yes | authored in Step B1.5 |
| `ThermalBoost` | Falling (reused) | yes | looks like being lifted upward |
| Landing (trigger) | Landing | no | one-shot on `LandingTrigger` |

**States and transitions:**

```
[Any State] → Landing               : LandingTrigger fires
Landing     → Grounded BT           : Has Exit Time (0.35s)

Grounded Blend Tree (BlendTree1D on Speed):
  0.0  → Idle
  0.35 → Walking
  1.0  → Running

SlingshotCharging  : Crouching_Idle loop; additive Layer 1 blends crouch depth via ChargingNormalized

Ballistic (MVP decision 2026-06-10, implemented in ticket A8): the entire
  airborne arc plays the Falling loop — all MovementMode == 2 entries route to
  the Falling state regardless of BallisticRising. The BallisticRise state
  remains in the controller as an unreferenced placeholder, and the bridge
  still dispatches BallisticRising, so a dedicated ballistic/tuck anim can be
  swapped into the upward arc later.

GlideCharging : Falling loop (cross-fade from Ballistic, 0.15s)
Gliding       : Glide_Pose loop (cross-fade from GlideCharging, 0.2s)
ThermalBoost  : Falling loop (same clip as Ballistic falling)

Transitions:
  Grounded BT → SlingshotCharging  : MovementMode == 1, instant
  Any → Ballistic rising           : MovementMode == 2 && BallisticRising == true, instant
  Ballistic rising → Ballistic fall: BallisticRising == false, instant
  Ballistic → GlideCharging        : MovementMode == 3, 0.15s
  GlideCharging → Gliding          : MovementMode == 4
Transitions from Gliding or Ballistic → Grounded BT      : MovementMode == 0 (landing)
Transitions → ThermalBoost                               : MovementMode == 5
```

All transitions use **Has Exit Time = false**, **Transition Duration = 0.1–0.2s**, **Interruption Source = Current State** unless noted.

Assign this controller to the `Animator` component on `SM_Chr_Male_01`.

---

## Phase C: PlayerAnimatorBridge MonoBehaviour

Create `Assets/Scripts/Player/Bootstrap/PlayerAnimatorBridge.cs`.

### Responsibilities

- Read `PlayerMovementState.Mode` and `PlayerMovementState.Velocity` from the ECS entity every `LateUpdate`.
- Read `SlingshotChargeState.ChargeNormalized` if present.
- Drive Animator parameters.
- Fire `LandingTrigger` exactly once on `LandingImpactEvent`.

### Class skeleton

```csharp
using DOTS.Player.Components;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Hybrid bridge: reads ECS PlayerMovementState and drives the Animator on the
    /// companion Synty character visual. Never writes to ECS — read-only bridge.
    /// </summary>
    [DefaultExecutionOrder(1001)]   // After PlayerVisualSync (1000)
    public class PlayerAnimatorBridge : MonoBehaviour
    {
        [Tooltip("The ECS entity this visual represents. Assigned by bootstrap.")]
        public Entity targetEntity;

        [Tooltip("The Animator on the Synty character child.")]
        public Animator characterAnimator;

        [Tooltip("Max speed used to normalize the Speed parameter (matches PlayerMovementConfig.GroundSpeed).")]
        public float runSpeed = 7f;

        private EntityManager _em;
        private World _world;
        private bool _valid;

        // Animator parameter hashes (pre-hashed for performance)
        private static readonly int SpeedHash            = Animator.StringToHash("Speed");
        private static readonly int MovementModeHash     = Animator.StringToHash("MovementMode");
        private static readonly int ChargingHash         = Animator.StringToHash("ChargingNormalized");
        private static readonly int LandingTriggerHash   = Animator.StringToHash("LandingTrigger");
        private static readonly int GroundedBoolHash     = Animator.StringToHash("GroundedBool");

        private PlayerMovementMode _lastMode;

        private void LateUpdate()
        {
            if (!TryResolveWorld()) return;
            if (targetEntity == Entity.Null || !_em.Exists(targetEntity)) return;
            if (characterAnimator == null) return;

            if (!_em.HasComponent<PlayerMovementState>(targetEntity)) return;
            var state = _em.GetComponentData<PlayerMovementState>(targetEntity);

            // Speed (normalized ground speed for walk/run blend)
            var horizontalSpeed = math.length(new float2(state.Velocity.x, state.Velocity.z));
            characterAnimator.SetFloat(SpeedHash, Mathf.Clamp01(horizontalSpeed / runSpeed));

            // Mode
            characterAnimator.SetInteger(MovementModeHash, (int)state.Mode);
            characterAnimator.SetBool(GroundedBoolHash, state.Mode == PlayerMovementMode.Grounded);

            // Slingshot charge depth
            if (_em.HasComponent<SlingshotChargeState>(targetEntity))
            {
                var charge = _em.GetComponentData<SlingshotChargeState>(targetEntity);
                characterAnimator.SetFloat(ChargingHash, charge.ChargeNormalized);
            }
            else
            {
                characterAnimator.SetFloat(ChargingHash, 0f);
            }

            // Landing trigger: fires once on transition INTO Grounded from airborne
            var wasAirborne = _lastMode is PlayerMovementMode.Ballistic
                or PlayerMovementMode.GlideCharging
                or PlayerMovementMode.Gliding
                or PlayerMovementMode.ThermalBoost;
            if (wasAirborne && state.Mode == PlayerMovementMode.Grounded)
                characterAnimator.SetTrigger(LandingTriggerHash);

            _lastMode = state.Mode;
        }

        private bool TryResolveWorld()
        {
            if (_valid && _world is { IsCreated: true }) return true;
            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated) { _valid = false; return false; }
            _em = _world.EntityManager;
            _valid = true;
            return true;
        }
    }
}
```

**Note on math namespace:** Add `using Unity.Mathematics;` at the top. `math.length(float2)` is Burst-safe but we are in a MonoBehaviour here, so `Vector2.magnitude` is also acceptable.

### Wiring in bootstrap

In `PlayerCameraBootstrap_WithVisuals.Start()`, after instantiating the visual GameObject, find the bridge component and set:

```csharp
var bridge = visualGo.GetComponent<PlayerAnimatorBridge>();
bridge.targetEntity = playerEntity;
bridge.characterAnimator = visualGo.GetComponentInChildren<Animator>();
```

---

## Phase D: Per-State Animation Targets and Tuning

Reference: MOVEMENT_PLANNING.md §State Visual Language table.

| MovementMode | Animation / Blend | Transition in | Key feel |
|---|---|---|---|
| `Grounded` (idle) | Idle clip, Speed ≈ 0 | 0.15s blend | relaxed upright |
| `Grounded` (moving) | Walk→Run blend on Speed param | 0.1s blend | responsive, snappy |
| `SlingshotCharging` | Crouch anim; secondary layer blends depth via ChargingNormalized | 0.1s | spring compression |
| `Ballistic` | Tuck / airborne fall loop | 0.1s | compact, streamlined |
| `GlideCharging` | Arms beginning to spread (0.35s non-looping into...) | 0.0s exit time | vulnerability beat |
| `Gliding` | Arms spread wide, body flat, loop | 0.2s | calm, controlled |
| `ThermalBoost` | Upright, arms slightly raised, loop | 0.15s | being lifted |
| Landing | LandingTrigger → impact crouch one-shot | instant trigger | satisfying thud |

### Landing anticipation (Phase D extension, lower priority)

When vertical velocity is below `-8 m/s` and altitude above ground < 2m, blend legs-extended pose as a sub-state layer. Requires a ground-proximity query — implement after the base landing anim is solid.

### Crouch depth blend (SlingshotCharging)

The `ChargingNormalized` float (0→1) should drive an **Additive** animation layer:

- Layer weight = `ChargingNormalized`
- Layer clip = a standalone deep-crouch pose clip (not a cycle)
- The base layer keeps the idle/breathing loop running underneath

This produces a smooth crouch that deepens as charge builds without needing a dedicated blend tree.

---

## ECS Changes

This feature requires **no new ECS components** and **no changes to existing systems**. The bridge is read-only from the ECS side.

The only code addition is:
- `PlayerAnimatorBridge.cs` (MonoBehaviour, purely read-only bridge)
- `PlayerAnimatorController.controller` (Animator Controller asset)
- Updated bootstrap wiring in `PlayerCameraBootstrap_WithVisuals.cs`

`PlayerVisualSync.cs` is unchanged.

---

## Test Plan

### Phase A

- [ ] Press Play → Synty character visible at player spawn position
- [ ] Walk/jump → character follows player correctly, no position lag vs. capsule collider
- [ ] Character is in T-pose (expected before animations)
- [ ] No pink/missing material errors

### Phase B

- [ ] Kevin Iglesias clips retarget cleanly onto Synty Humanoid avatar (no limb distortion)
- [ ] Animator Controller transitions preview correctly in Animator window

### Phase C + D (visual confirmation only — no automated tests for animation)

- [ ] Stand still → Idle loop plays
- [ ] WASD movement → Walk and Run blend by speed
- [ ] Slingshot charge (hold LMB+RMB, drag) → character crouches, deepens with charge
- [ ] Release → character launches, Ballistic tuck pose starts within 1 frame
- [ ] Hold Space mid-air → arms begin spreading (GlideCharging), then full Glide_Loop
- [ ] Fly into thermal → ThermalRise loop
- [ ] Land → LandingTrigger fires, impact crouch plays once, returns to idle
- [ ] Regression: physics, slingshot impulse, and chain window still function correctly

---

## Risks and Open Questions

| Risk | Mitigation |
|------|-----------|
| Kevin Iglesias packs not yet imported | Import is step zero; all subsequent phases are blocked until this is done |
| Synty skeleton incompatible with Kevin Iglesias retargeting | Validate in sandbox project per THIRD_PARTY_ASSET_EVALUATION_PLAYBOOK.md Phase B before main project import |
| Glide pose clip may not exist in available packs | Substitute a T-pose or arms-wide custom blend; or author a static pose asset in Blender |
| `PlayerAnimatorBridge` LateUpdate timing relative to physics | Bridge runs at `DefaultExecutionOrder(1001)`, after `PlayerVisualSync` (1000), after physics. This is correct. |
| Additive crouch layer clip authoring | Requires a standalone Unity "pose clip" (single-frame animation); author in Unity Animation window from the Synty rig |
