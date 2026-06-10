# Tickets

Lightweight task tracker. Status: `[ ]` pending · `[x]` done · `[-]` blocked

---

## Sprint: Animation + Camera Feel

### Animation

| ID  | Status | Subject | Blocks | Blocked By |
|-----|--------|---------|--------|------------|
| A1  | [ ] | Wire slingshot clips into animator controller | A2, A3 | — |
| A2  | [-] | Fix animator controller transition blend times | A4 | A1 |
| A3  | [-] | Stabilize landing animations | A4 | A1 |
| A4  | [-] | Import Kevin Iglesias pack and wire basic movement animations | A5 | A2, A3 |
| A5  | [-] | Wire glide animation state | — | A4 |

#### A1 — Wire slingshot clips into animator controller
Wire the 3 exported FBX clips into `PlayerAnimatorController` per `SLINGSHOT_ANIMATION_CONTROLLER_SPEC.md`.
- `Player_Slingshot_Charge_Start` — trigger on slingshot input down, no loop, transitions into Hold
- `Player_Slingshot_Charge_Hold` — loops while input held, exits on release or cancel
- `Player_Slingshot_Release` — trigger on launch (transition to Ballistic), no loop

Animator parameters must match what `PlayerAnimatorBridge` already dispatches. No physics/movement changes.

#### A2 — Fix animator controller transition blend times _(blocked by A1)_
All state transitions currently snap at 0s. Fix in Unity Editor — no code changes.
- Grounded → SlingshotCharging: ~0.1s ease-out
- SlingshotCharging → Ballistic: ~0.05s (fast pop)
- Ballistic → Gliding: ~0.2s ease-out
- Any Airborne → Grounded: ~0.1s sharp-in

Set `Has Exit Time = false` and non-zero `Transition Duration` on each. Validate in Play Mode.

#### A3 — Stabilize landing animations _(blocked by A1)_
Per `PLAYER_LANDING_ANIMATION_SPEC.md`:
- **Phase 1:** Add fallback flag in `LandingConfig`. When enabled, bridge fires only original `LandingTrigger` for all landings — restores known-good behaviour. Tiered triggers (Standard/Hard/Slide) currently fire into dead states.
- **Phase 3:** Once controller states exist, flip flag to enable tiered dispatch. Code is already written; controller states + clips are the only missing pieces.

#### A4 — Import Kevin Iglesias pack and wire basic movement animations _(blocked by A2, A3)_
Pack is ready for import into `Assets/Kevin Iglesias/` (currently empty).
1. Import pack
2. Map to `PlayerMovementMode` states per `PLAYER_CHARACTER_VISUAL_SWAP_SPEC.md`:
   - Grounded idle → Idle clip
   - Grounded moving → Walk/Run (speed float parameter)
   - Ballistic falling → Airborne_Fall or equivalent
   - Landing → Landing_Hard / Landing_Soft (feeds A3 Phase 3)
3. Wire into `PlayerAnimatorController`
4. Validate all basic states in Play Mode. Do not replace slingshot states from A1.

#### A5 — Wire glide animation state _(blocked by A4)_
Add Gliding animator state driven by `PlayerMovementMode.Gliding`.
- Clip: Kevin Iglesias glider/arms-spread (from A4 import)
- Transition in from Ballistic on Gliding mode (~0.2s blend), loop, transition out on exit
- Confirm `PlayerAnimatorBridge` dispatches Gliding mode; add parameter if missing
- Validate: hold Space mid-flight → arms-spread pose blends smoothly from tuck

_(Former A6 → folded into backlog **V2**. Former A7 → backlog **R1**. Both were rendering/environment work, not animation.)_

---

### Camera Feel _(independent — can run in parallel with animation)_

| ID  | Status | Subject |
|-----|--------|---------|
| C1  | [ ] | Camera charge pullback and FOV narrow during slingshot charge |
| C2  | [ ] | Camera FOV punch and speed lines on launch |
| C3  | [ ] | Landing camera dip and dust burst |

#### C1 — Camera charge pullback and FOV narrow
Per `MOVEMENT_PLANNING.md` Step 3:
- Dolly back: `TargetDistance = BaseDistance + 2.5 * ChargeNormalized`
- FOV narrow: `TargetFOV = BaseFOV - 5° * ChargeNormalized`
- Camera shake ramps with charge: amplitude 0.01 → 0.06
- Orbit locks to charge direction during SlingshotCharging (no free-look)
- Exponential smoothing (damping = 8). On cancel: reverse over ~150ms

**Test (EditMode):** TargetDistance == BaseDistance + ChargeDistanceAdd * ChargeNormalized

#### C2 — Camera FOV punch and speed lines on launch
Per `MOVEMENT_PLANNING.md` Step 5:
- FOV punch on launch: +8–15°, fast attack ~80ms, decay over 300–500ms
- Speed FOV: +0.15°/m/s above 15 m/s threshold, capped at +12°
- Ballistic camera pulls back (BallisticDistanceAdd = 1.5m), damping loosens (= 6)
- Speed lines: camera-parented particles or screen-space shader, 0%→100% opacity over 15→40 m/s, fade 300ms on drop

**Test (EditMode):** TargetFOV includes launch punch on first ballistic frame, decays over subsequent frames

#### C3 — Landing camera dip and dust burst
Per `MOVEMENT_PLANNING.md` Step 7:
- Shake amplitude 0.05–0.20 proportional to vertical impact speed, decay 150–300ms
- FOV dip: 2–4°, 200ms recovery
- Camera drops 0.3–0.8m, recovers over 200ms
- Dust burst at feet: size/density scale with impact speed (min speed = 5 m/s, max radius = 3m)
- Hard landing (vertical > 12 m/s): full shake + dip. Slide landing (horizontal > 8 m/s): smooth transition, no dip

**Test (EditMode):** ShakeOffset proportional to VerticalSpeed, clamped to max. LandingImpactEvent fires exactly one frame then disables.

---

## Backlog

_Tickets for later sprints — not yet scheduled._

| ID  | Subject | Group |
|-----|---------|-------|
| M1  | Glide mechanic (Space hold → GlideCharging → Gliding) | Movement |
| M2  | Chain slingshot (chain window + additive velocity) | Movement |
| M3  | Thermal columns (vertical lift volumes) | Movement |
| V1  | Ground plane impostor | MVP Vista |
| V2  | Atmospheric fog tuning _(canonical fog ticket — folds in former A6)_ | MVP Vista |
| V3  | Mountain skybox panel | MVP Vista |
| P1  | Basic HUD (charge indicator + chain window indicator) | Phase 1 |
| P2  | Magic Hand System (raycast, charge, binary terrain edit) | Phase 1 |
| R1  | Low-poly tree/rock LODs + enable relic LOD | Rendering |

---

### V2 — Atmospheric fog tuning _(canonical fog ticket)_
**Intent:** Fog should read as a thin mist suspended in the air, not as a tint applied to objects (trees, rocks, etc.). From altitude it currently renders as a visible square, which breaks the illusion. Possibly also reconsider what the fog is for.

Goal: distance/height-based atmospheric haze that softens the far horizon without visibly tinting nearby geometry, with **no perceptible plane/quad edge from any camera height**.

- Bias the effect toward distance and/or altitude; reduce density so near geometry is largely unaffected.
- Eliminate the "square from height" artifact. If a finite plane/quad drives the fog, replace it with a camera-relative/global effect or skirt/extend it so no edge shows at expected fly heights.
- Reconcile with the vista stack — check interaction with `GROUND_PLANE_IMPOSTOR_SPEC.md` and `MVP_VISTA_MOMENT_SPEC.md` so fog and the horizon impostor read as one atmosphere.

**Open questions (resolve first):**
1. What actually renders the fog today — URP Volume/global fog (`RenderSettings.fog`), a custom fog shader/material, a skybox blend, or a quad/plane in the scene? The only `Fog` in code is `WeatherSystem.WeatherType.Fog`, which sets weather-state values (temperature/humidity) only — **no visuals** — so the source is elsewhere. (Determines settings-tweak vs. system change.)
2. Is the "square from height" a dedicated fog plane, or the ground-plane impostor edge?

**Acceptance:** From ground level and from max expected fly height, fog shows no plane edge; near objects (within ~1 chunk) show negligible tint; far horizon is softened. Validate in Play Mode at several altitudes.

### R1 — Low-poly tree/rock LODs + enable relic LOD
**Intent:** Reduce environment-object render cost via LOD. **Priority: trees and rocks first** — they cost more FPS than the giant relics. Relics second.

- **Trees / rocks (do first):** authored via `TreeChunkRenderSystem` / `RockChunkRenderSystem`. Add a far LOD so dense scatter holds frame budget. Keep within poly/draw budgets — reference `ArtAndDOTS_Pipeline.md` and `DOTS_Terrain_LOD_SPEC.md`.
- **Spike (decide before building meshes):** for the low-poly far representation, do we author **new low-poly scattered meshes** (Blender) or generate them at runtime via **decimation/impostor** from the existing meshes? Timebox the spike, pick one, note the decision here.
- **Relics (second):** `RelicLodSelectionSystem` already implements distance-based full↔impostor swap (`RelicLodParams` + `RelicRenderConfig.LodSwapDistance`/`LodHysteresis`) but is `[DisableAutoCreation]`, so it only runs if explicitly created. "Large relics not using LOD" is most likely just that it is never enabled — confirm whether the bootstrap creates it; if not, enable and verify each realized relic has valid `RelicLodParams` and a 2-entry `RenderMeshArray`.

**Open questions (resolve in-ticket):**
1. New authored low-poly art vs. runtime decimation/impostor? → resolved by the spike above.
2. Target swap distances / poly budgets — do these exist, or does R1 establish them?
3. Should enabling/verifying `RelicLodSelectionSystem` be a separate quick ticket, or stay folded into R1?

**Acceptance:** Trees/rocks have a far LOD that holds frame budget at a populated viewpoint. Large relics visibly swap to impostor past `LodSwapDistance` (confirm via `DebugSettings.LogRendering` transition log). Add/extend an EditMode test alongside `StructureLodTests` for any new swap logic.
