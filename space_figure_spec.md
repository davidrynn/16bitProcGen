# Humanoid Block Figure — Spec v3 (Minimalist)

## Overview

A humanoid figure built from **tapered rectangular cuboids**, mapped to the **Unity Humanoid Avatar** bone hierarchy. Based on the actual built model — a simplified ~1.75m tall adult male silhouette. One Blender unit = one metre.

The figure is in **T-pose** (arms horizontal). The design is intentionally minimal:

- **No separate mesh blocks** for spine, neck, shoulders, hands, knees, or feet.
- Arms and legs **taper toward their tips** to imply hands and feet without dedicated meshes.
- The armature supplies bone-only entries for all segments required by Unity Humanoid.

---

## Mesh Objects — 12 total

### Axial (centre line, X = 0)

Dimensions: **W (X) × D (Y) × H (Z)**. Positions measured from actual world bounding boxes.

| Unity Bone | Object | W × D × H | Z range | Notes |
|---|---|---|---|---|
| `Hips` | `hips` | 0.34 × 0.20 × 0.12 | 0.880 – 1.000 | Pelvis block |
| `Chest` | `chest` | 0.34 × 0.20 × 0.17 | 1.080 – 1.250 | Mid-torso; no spine block below |
| `UpperChest` | `upper_chest` | 0.32 × 0.20 × 0.14 | 1.290 – 1.430 | Shoulder-level; no neck block above |
| `Head` | `skull` | 0.17 × 0.20 × 0.22 | 1.530 – 1.750 | Chin z=1.530, crown z=1.750 |

> ~0.08m gap between `hips` and `chest` — spine bone spans this.
> ~0.04m gap between `chest` and `upper_chest` — bone chain connects through here.
> ~0.10m gap between `upper_chest` and `skull` — neck bone spans this.

---

### Arms (mirrored ±X) — arm centreline Z ≈ 1.380

Arms taper from shoulder toward wrist tip; no hand mesh. Dimensions at widest cross-section.

| Unity Bone | Object | Length (X) | Section (widest) | Inner X | Outer X |
|---|---|---|---|---|---|
| `LeftUpperArm` / `RightUpperArm` | `upper_arm_L/R` | 0.260 | 0.095 × 0.095 | ±0.200 | ±0.460 |
| `LeftLowerArm` / `RightLowerArm` | `lower_arm_L/R` | 0.210 | ≈0.064 × 0.064 | ±0.500 | ±0.710 |

> ~0.04m elbow gap between upper and lower arm blocks.
> Arms hang ~0.05m below the top of `upper_chest` (arm Z ≈ 1.380 vs shoulder Z = 1.430).

---

### Legs (mirrored ±X = ±0.110)

Legs taper from hip toward ankle tip; no foot mesh.

| Unity Bone | Object | W × D × H | Z range | Notes |
|---|---|---|---|---|
| `LeftUpperLeg` / `RightUpperLeg` | `upper_leg_L/R` | 0.130 × 0.130 × 0.336 | 0.500 – 0.836 | |
| `LeftLowerLeg` / `RightLowerLeg` | `lower_leg_L/R` | 0.100 × 0.105 × 0.370 | 0.090 – 0.460 | |

> ~0.04m knee gap between upper and lower leg blocks; no kneecap mesh.

---

## Joint Landmarks (from actual mesh)

| Joint | Z |
|---|---|
| Floor / toe reference | 0.000 |
| Ankle | 0.090 |
| Knee | 0.460 |
| Upper leg top (hip attach) | 0.836 |
| Arm centreline | 1.380 |
| Shoulder / top of upper_chest | 1.430 |
| Chin / skull base | 1.530 |
| Crown | 1.750 |

---

## Armature — Bone-Only Entries

These bones are required by Unity Humanoid but have no mesh block. They occupy the gaps in the figure.

| Bone | Head position | Tail position | Reason |
|---|---|---|---|
| `Spine` | (0, 0, 1.000) | (0, 0, 1.080) | Bridges Hips → Chest |
| `Neck` | (0, 0, 1.430) | (0, 0, 1.530) | Bridges UpperChest → Head |
| `LeftShoulder` | (0, 0, 1.430) | (+0.200, 0, 1.380) | Branches from UpperChest |
| `RightShoulder` | (0, 0, 1.430) | (−0.200, 0, 1.380) | Branches from UpperChest |
| `LeftHand` | (+0.710, 0, 1.380) | (+0.800, 0, 1.380) | **Required** Unity bone; tip of lower arm |
| `RightHand` | (−0.710, 0, 1.380) | (−0.800, 0, 1.380) | **Required** Unity bone; tip of lower arm |
| `LeftFoot` | (+0.110, 0, 0.090) | (+0.110, +0.115, 0.000) | **Required** Unity bone; at ankle, points forward |
| `RightFoot` | (−0.110, 0, 0.090) | (−0.110, +0.115, 0.000) | **Required** Unity bone; at ankle, points forward |

---

## Vertical Stack (actual positions)

```
z=1.750  ╔════════╗
         ║  SKULL ║  0.17×0.20×0.22       HEAD
z=1.530  ╚════════╝
         ╌ neck bone only ╌
z=1.430  ┄ shoulder joint / top of UPPER_CHEST ┄
         ╔══════════╗
         ║ UPCHEST  ║  0.32×0.20×0.14     UPPER_CHEST
z=1.290  ╚══════════╝
           (4cm gap)
z=1.250  ╔══════════╗
         ║  CHEST   ║  0.34×0.20×0.17     CHEST
z=1.080  ╚══════════╝
         ╌ spine bone only ╌
z=1.000  ╔══════════╗
         ║   HIPS   ║  0.34×0.20×0.12     HIPS
z=0.880  ╚══════════╝  ← leg branch
           (4cm gap)
z=0.836  ╔══╗  ╔══╗
         ║UL║  ║UL║  0.13×0.13×0.34      UPPER_LEG ×2
z=0.500  ╚══╝  ╚══╝
           (4cm gap — knee clearance)
z=0.460  ╔═╗   ╔═╗
         ║L║   ║L║   0.10×0.11×0.37      LOWER_LEG ×2
z=0.090  ╚═╝   ╚═╝   ← ankle / foot bone only below
z=0.000  ┄ floor ┄
```

---

## Arm Diagram (top view, T-pose)

```
centre                                               wrist tip
X=0    ±0.20      ±0.46   ±0.50      ±0.71
  ║  ╔══╗╔═════════════╗ ╔══════════════╗
  ║  ║SH║║  UPPER ARM  ║ ║  LOWER ARM  ║►
  ║  ╚══╝╚═════════════╝ ╚══════════════╝
CHEST    |←── 0.260 ───→| |←── 0.210 ──→|
         shoulder        elbow gap      wrist
```

---

## Unity Humanoid Bone Hierarchy

```
Hips
 ├── Spine                    [bone-only]
 │    └── Chest
 │         └── UpperChest
 │              ├── Neck      [bone-only]
 │              │    └── Head
 │              ├── LeftShoulder   [bone-only]
 │              │    └── LeftUpperArm
 │              │         └── LeftLowerArm
 │              │              └── LeftHand    [bone-only, required]
 │              └── RightShoulder  [bone-only]
 │                   └── RightUpperArm
 │                        └── RightLowerArm
 │                             └── RightHand   [bone-only, required]
 ├── LeftUpperLeg
 │    └── LeftLowerLeg
 │         └── LeftFoot       [bone-only, required]
 └── RightUpperLeg
      └── RightLowerLeg
           └── RightFoot      [bone-only, required]
```

Unity required bones covered: **15 / 15** ✓
Optional bones included: Chest, UpperChest, Neck, Shoulder ×2

---

## Build Rules

1. Each object origin at its **geometric centre**.
2. Objects kept **separate** — do not join before rigging.
3. **Apply all transforms** (`Ctrl+A → All Transforms`) before adding the armature.
   Several blocks currently have non-unit scale that will corrupt weights if not baked.
4. Naming exactly as listed above — the rigging script uses these names.
5. After rigging and weighting, export as FBX with **Y Forward / Z Up**.
6. Unity import: enable **Humanoid** rig; remap any bones Avatar Configuration flags.

---

## First-Person Usage

This figure is used **primarily in first-person view**. No separate view model is needed — the minimalist aesthetic (no hand/foot meshes, tapering limbs) works at any camera distance and avoids the proportion mismatch that makes block figures look odd with detailed view model arms.

### Camera Setup

Parent the main camera to the **Head bone** in your bootstrap:

```csharp
Transform headBone = animator.GetBoneTransform(HumanBodyBones.Head);
mainCamera.transform.SetParent(headBone);
mainCamera.transform.localPosition = new Vector3(0, 0.08f, 0.05f); // fine-tune per feel
mainCamera.transform.localRotation = Quaternion.identity;
```

- **Hide the `skull` mesh** in first-person — the camera is inside it.
- All arm animations apply directly; arms are fully visible in front of the camera.
- Body bob (walk/run rhythm, landing impact) passes through the Head bone to the camera automatically — no separate camera shake system needed for locomotion feedback.

### Visibility by View Mode

| Element | First-Person | Third-Person | Notes |
|---|---|---|---|
| Arms (upper + lower) | ✅ Visible | ✅ Visible | Primary FP feedback |
| Skull / head | ❌ Hide | ✅ Visible | Camera sits inside it in FP |
| Torso / chest blocks | ❌ Behind camera | ✅ Visible | |
| Legs | ❌ Below frustum | ✅ Visible | |
| Slingshot charge pose | ✅ **Critical** | ✅ Visible | Player watches full charge cycle |
| Landing camera bob | ✅ Via Head bone | ✅ Via full body | No extra system needed |

---

## Animation

### Authoring Approach

| Clip category | Source strategy |
|---|---|
| Locomotion (Idle, Walk, Run) | Kevin Iglesias pack (`Assets/Kevin Iglesias/`) — check first; retargets automatically via Humanoid |
| Airborne (Jump, Fall) | Kevin Iglesias / Mixamo — retargets automatically |
| Landing (soft, standard, hard, slide) | Kevin Iglesias pack first; full tiered spec in `Assets/Docs/Player/PLAYER_LANDING_ANIMATION_SPEC.md` |
| Slingshot charge / release | **Custom — author in Blender** (no existing pack source fits this mechanic) |

**Retro style:** Apply **stepped / constant interpolation** in Blender's Graph Editor (`T → Constant`) to all custom clips. This converts smooth spline curves to frame-hold snaps — gives the figure a retro puppet feel consistent with the block aesthetic.

**Retargeting:** All Kevin Iglesias and Mixamo clips retarget through Unity Humanoid automatically. Source rig bone names are irrelevant — Unity maps by Avatar configuration.

---

### Clip List

| # | Clip | Trigger / parameter | Source | FP priority | Notes |
|---|---|---|---|---|---|
| 1 | `Idle` | `GroundedBool=true`, `Speed≈0` | Kevin Iglesias | Low | Subtle weight shift; body bob suppressed |
| 2 | `Walk` | `Speed` blend tree | Kevin Iglesias | Low | Head bob reaches camera |
| 3 | `Run` | `Speed` blend tree | Kevin Iglesias | Low | Head bob reaches camera |
| 4 | `JumpAscend` | `BallisticRising=true` | Kevin Iglesias | Medium | Arms-out silhouette |
| 5 | `Fall` | `GroundedBool=false`, looping | Kevin Iglesias | Medium | Loops until landing trigger fires |
| 6 | `LandingSoft` | `LandingTrigger` (< 6 m/s) | Kevin Iglesias | High | Current fallback / Phase 1 path |
| 7 | `LandingStandard` | `StandardLandingTrigger` (6–12 m/s) | Kevin Iglesias | High | Crouch-recover, ~0.25 s |
| 8 | `LandingHard` | `HardLandingTrigger` (> 12 m/s) | Kevin Iglesias / custom | **Critical** | Stagger/stumble, ~0.5 s; strong head bob |
| 9 | `LandingSlide` | `SlideLandingTrigger` (high horiz speed) | Custom | **Critical** | Forward lean + decelerate |
| 10 | `SlingshotCharge` | `SlingshotCharging=true` | **Custom** | **Critical** | Arms pull back; loopable hold pose |
| 11 | `SlingshotRelease` | `SlingshotReleaseTrigger` | **Custom** | **Critical** | Arms fling forward; ~0.15 s, sharp |

> Landing tiered dispatch is controlled by `LandingConfig.UseSimpleLandingTrigger`. Phase 1–3 implementation plan lives in `Assets/Docs/Player/PLAYER_LANDING_ANIMATION_SPEC.md`.

---

### Slingshot Clip Detail (Custom — Author in Blender)

The slingshot clips are the **highest-priority first-person animations** — the player watches the arms through the entire charge and release cycle.

**Charge** (~0.4 s, loopable at peak):
- Both block arms pull back symmetrically; elbows wide, forearms angled back.
- With stepped interpolation: 3 key poses — neutral → mid-draw → full draw (held).
- Hold pose loops until release input.

**Release** (~0.15 s, non-looping):
- Arms thrust forward simultaneously from full-draw pose.
- With stepped interpolation: full-draw → full-extension (1 frame) → return to idle.
- Sharpness of the one-frame extension is the retro "snap" that sells the throw.

---

## Status

### Model & Rig
- [x] Spec approved (v3 — minimalist)
- [x] Built in Blender (12 mesh objects)
- [x] Transforms applied (`Ctrl+A` on all objects before rigging)
- [x] Armature added via script (20 bones, anatomical landmark positions)
- [x] Vertex weights applied (rigid 100% single-bone per block; no cross-block bleeding)
- [x] FBX exported to Unity (`Assets/Models/BlockFigure.fbx`)
- [x] Unity Humanoid Avatar configured (LeftHand/RightHand/LeftFoot/RightFoot manually assigned)
- [ ] Spacesuit skin layer added

### Animation
- [ ] Locomotion clips sourced from Kevin Iglesias pack (Idle, Walk, Run)
- [ ] Airborne clips sourced (Jump, Fall)
- [ ] Landing clips sourced / tiered controller built (see `PLAYER_LANDING_ANIMATION_SPEC.md`)
- [ ] Slingshot charge clip authored in Blender
- [ ] Slingshot release clip authored in Blender
- [ ] Stepped interpolation applied to all custom clips
