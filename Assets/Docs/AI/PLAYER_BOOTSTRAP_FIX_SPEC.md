# Player Bootstrap Reliability SPEC

**Purpose**

Restore green status for the failing DOTS player bootstrap tests by tightening the physics bootstrap path and the hybrid visual sync bridge. This SPEC targets only the regressions exercised by `Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrapTests.cs`.

**Scope & Constraints**

- Unity 2022 LTS+/6, ECS/DOTS stack already in project.
- No refactors outside `DOTS.Player.Bootstrap` unless required to fix the enumerated failures.
- Follow SPEC → TEST → CODE. Each phase is a reviewable step: write/update docs/config, add/adjust tests, then implement code.
- Keep MonoBehaviour usage limited to sync/authoring; gameplay stays in ECS.

**Current Failures (Baseline)**

| Test | Symptom | Suspected Area |
| ---- | ------- | -------------- |
| `PlayerEntity_HasCorrectPhysicsProperties` (line ~125) | Initial Y velocity `-0.4905` instead of gravity-step `-0.1635` | Bootstrap applying gravity multiple times / wrong timestep |
| `PlayerVisualSync_SyncsPositionWithEntity` (line ~340) | GameObject lags entity by ~0.01 m | Sync timing / multiple transforms |
| `PlayerVisualSync_SyncsRotationWithEntity` (line ~370) | Rotation never updates, diff 0.76 | Sync not copying rotation or is overridden |

**Execution Order**

1. Phase 0 – Diagnostics Harness
2. Phase 1 – Physics Step Alignment
3. Phase 2 – Visual Position Sync
4. Phase 3 – Visual Rotation Sync
5. Phase 4 – Regression Hooks & Docs

Stop after each phase for review.

---

## Phase 0 — Diagnostics Harness

**SPEC**

- Add lightweight logging toggles (similar to `DebugSettings`) scoped to player bootstrap/tests.
- Provide a helper MonoBehaviour (or test utility) that prints current physics timestep + gravity so mismatches are obvious.

**TEST**

- Extend `PlayerCameraBootstrapTests` with a utility method to capture the physics step used for expectation calculation to ensure tests read the same timestep that the bootstrap applies.

**CODE**

- Implement the helper (likely under `Assets/Scripts/Player/Bootstrap/TestUtilities/`) and gate logs behind a static bool.
- Ensure diagnostics are editor-only or compiled out of builds.

---

## Phase 1 — Physics Step Alignment (`PlayerEntity_HasCorrectPhysicsProperties`)

**SPEC**

- *Phase 1A Baseline Diagnostics*
  - Revert any speculative timestep/rate-manager hacks so we can reproduce the original failure deterministically.
  - Build a minimal harness that records the player's `PhysicsVelocity.Linear.y` before any simulation, after the first fixed-step tick, and after subsequent ticks (without letting other systems mutate it). This should live in the bootstrap test suite so we can capture the exact integration sequence right next to the failing test.
  - Log the world timestep, fixed-step accumulator, and any systems touching `PhysicsVelocity` during initialization so we know precisely where the extra gravity applications originate.
- Only after the harness proves the root cause should we introduce a fix (e.g., adjusting when the player enters the fixed-step loop, deferring gravity, or installing a rate manager).
- Audit where initial `PhysicsVelocity` is set. Ensure bootstrap applies EXACTLY one gravity integration using the same timestep the tests compute (preferably `FixedStepSimulationSystemGroup.Timestep` or `Unity.Physics.PhysicsStep` singleton if present).
- If no explicit integration is needed, set velocity to zero and let the first FixedStep apply gravity.

**TEST**

- Update/augment `PlayerEntity_HasCorrectPhysicsProperties` to read the authoritative timestep from the new helper so we assert against the real value.
- Add a regression test: spawn player, run zero simulation frames, confirm Y velocity equals `gravity * timestep` OR zero according to the decided contract.

**CODE**

- Modify `PlayerEntityBootstrap` (and any other bootstrap variant still used by tests) to either refrain from pre-integrating gravity or to use the same timestep source as the tests.
- Ensure no systems run twice during initialization causing double gravity application.

---

## Phase 2 — Visual Position Sync (`PlayerVisualSync_SyncsPositionWithEntity`)

**SPEC**

- Inspect `PlayerVisualSync.LateUpdate()` / entity transform updates for potential smoothing, scale mutations, or delta-based offsets.
- Guarantee the GameObject copies the ECS `LocalTransform` position verbatim each frame after ECS systems run.

**TEST**

- Enhance the existing test by explicitly waiting for `LateUpdate` via `WaitForEndOfFrame` to remove race conditions.
- Add an epsilon-tight assertion (≤ 0.001 m) to ensure future drift is caught.

**CODE**

- Update `PlayerVisualSync` (and any debug visual syncs like `EntityVisualSync`) to:
  - Use `transform.SetPositionAndRotation` to avoid intermediate corrections.
  - Skip redundant conversions (e.g., `Vector3` double rounding).
  - Early-out only when the entity truly lacks `LocalTransform`.
- Consider caching `World`/`EntityManager` references to avoid lookups that might occur before ECS updates finish.

---

## Phase 3 — Visual Rotation Sync (`PlayerVisualSync_SyncsRotationWithEntity`)

**SPEC**

- Determine why rotation copy is no-op (e.g., overwritten elsewhere, quaternion normalized incorrectly, or object constrained).
- Align the rotation flow between ECS entity and GameObject.

**TEST**

- Expand the rotation test to:
  - Set a non-trivial quaternion (e.g., yaw + pitch) and assert the quaternion difference is < 1e-3.
  - Optionally verify the GameObject’s forward vector matches the entity’s.

**CODE**

- Update `PlayerVisualSync` to set rotation directly and ensure no other MonoBehaviour (e.g., camera follow) overwrites it in the same frame.
- If additional scripts mutate rotation, add an order guarantee (script execution order or explicit sync events) documented in the spec.

---

## Phase 4 — Regression Hooks & Documentation

**SPEC**

- Document the bootstrap expectations in `Assets/Docs/Player/BOOTSTRAP_GUIDE.md` (or nearest doc) so future changes respect the contract.
- Add a CI note to run `PlayerCameraBootstrapTests` whenever bootstrap code changes.

**TEST**

- Ensure all three tests + any new coverage pass in PlayMode test runner locally (document the command/steps).
- Optionally add a smoke test that instantiates the bootstrap in PlayMode scene and waits a few frames to ensure no warnings/errors.

**CODE**

- Add minimal editor tooling (menu item or context action) to spawn the bootstrap test scene for quick manual validation.
- Update documentation changelog referencing this spec.

---

**Hand-off Instructions**

- Use this spec exactly as `AI/TERRAIN…` is used: pick the next incomplete phase, execute SPEC → TEST → CODE, request review, then move on.
- Keep fixes tightly scoped; player movement/controls beyond the failing tests are out of scope.