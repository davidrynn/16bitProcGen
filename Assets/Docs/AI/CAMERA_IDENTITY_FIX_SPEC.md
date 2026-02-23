# Camera Identity Mismatch Fix SPEC

**Purpose**

Fix BUG-003 (reticle not visible) and the aim-not-following-player aspect of BUG-004 by resolving the `Camera.main` identity mismatch. Multiple systems assume `Camera.main` is the player-controlled camera, but it resolves to a static scene camera instead.

**Related Issues:** BUG-003, BUG-004 (aim portion)
**Date:** 2026-02-18

---

## Root Cause (Confirmed)

`PlayerEntityBootstrap` creates a new camera GameObject named `"Main Camera (ECS Player)"` and tags it `MainCamera`. However, the scene already contains a default Unity camera named `"Main Camera"` also tagged `MainCamera`. `Camera.main` returns the **scene camera** (first found), not the ECS-managed one.

**Evidence (Play mode console):**
```
[ReticleBootstrap] DIAG: Bound to camera 'Main Camera' (instanceID=56156) pos=(0.00, 1.00, -10.00)
```  
- Name is `'Main Camera'` (scene default), not `'Main Camera (ECS Player)'`
- Position `(0, 1, -10)` is the Unity default camera position, not the player spawn camera position `(0, 21.6, 0)`

**Impact chain:**

| System | Uses `Camera.main`? | Consequence |
|--------|---------------------|-------------|
| `TerrainEditInputSystem` | Yes (raycast origin/direction) | Edits fire from static position, don't follow player |
| `ReticleBootstrap` | Yes (canvas bind + parent) | Reticle parented to wrong camera, invisible to player |
| `PlayerCameraSystem` | No (uses entity component) | Works correctly, but updates the wrong camera for consumers |

---

## Scope & Constraints

- Fix is limited to camera creation/lifecycle in `PlayerEntityBootstrap` and related bootstrap code.
- Do not change `PlayerCameraSystem` update logic (it already works correctly via entity references).
- Do not change `TerrainEditInputSystem` or `ReticleBootstrap` camera lookup pattern (`Camera.main` is the correct Unity idiom — the problem is which camera it returns).
- Follow SPEC -> TEST -> CODE. Each phase is a reviewable step.

---

## Phase 1 — Eliminate Duplicate MainCamera

**SPEC**

- `PlayerEntityBootstrap.CreateMainCameraAndEntity()` must handle pre-existing `MainCamera`-tagged cameras.
- If a scene camera tagged `MainCamera` already exists:
  - Disable it (set `enabled = false`) and remove its `MainCamera` tag, OR
  - Destroy the GameObject entirely.
- Disabling is preferred over destroying to avoid breaking references in case other scene objects depend on it.
- After creating the ECS camera, verify `Camera.main` returns the newly created camera.

**TEST**

- Add a test that creates a scene with a pre-existing `MainCamera`, runs `PlayerEntityBootstrap`, and asserts:
  - `Camera.main.name` equals `"Main Camera (ECS Player)"`
  - Only one enabled camera is tagged `MainCamera`
  - The old scene camera is disabled or destroyed

**CODE**

- Modify `PlayerEntityBootstrap.CreateMainCameraAndEntity()`:
  - Before creating the new camera, find and disable/untag any existing `MainCamera`.
  - Log the action via `DebugSettings.LogPlayer()`.

---

## Phase 2 — Verify Consumer Systems

**SPEC**

- After Phase 1, `Camera.main` should return the ECS-managed camera.
- `TerrainEditInputSystem` should raycast from the player's current camera position/direction.
- `ReticleBootstrap` should bind its canvas to the ECS camera.
- Both should update correctly as the player moves.

**TEST**

- Manual Play mode verification:
  1. Enter Play mode.
  2. Confirm reticle is visible at screen center (BUG-003 resolved).
  3. Move player with WASD, press Q/E to edit terrain.
  4. Confirm edits happen where the reticle points, not at a fixed world position.
  5. Check console: `[PlayerCamera] DIAG:` should show `Camera.main match=True`.

**CODE**

- No code changes expected in this phase. If tests fail, investigate and fix in a sub-phase.

---

## Phase 3 — Remove Temporary Diagnostics

**SPEC**

- All `forceLog: true` DIAG logs added during the investigation must be removed or converted to standard gated logs once the fix is confirmed.
- The `_hasLoggedCameraDiag` field and associated diagnostic block in `TerrainEditInputSystem` should be removed.
- The camera identity diagnostic block in `PlayerCameraSystem` should be simplified back to a single startup log.
- The multi-camera warning in `ReticleBootstrap` should be removed.

**Files to clean up:**

| File | What to remove/revert |
|------|----------------------|
| `TerrainEditInputSystem.cs` | Remove `_hasLoggedCameraDiag` field, camera enumeration block, per-edit ray origin log |
| `PlayerCameraSystem.cs` | Remove `Camera.main` comparison block, revert to single `LogPlayer` startup message |
| `ReticleBootstrap.cs` | Remove multi-camera warning and instanceID details from bind log |

**TEST**

- Grep all ISystem files for `DIAG:` — should return zero matches.
- Grep all ISystem files for `forceLog: true` — should return zero matches (or only intentional permanent uses).

**CODE**

- Edit the three files listed above.
- Run Play mode once to confirm no regressions from removing the logs.

---

## Phase 4 — Update KNOWN_ISSUES.md

**SPEC**

- Move BUG-003 to resolved section with root cause summary.
- Update BUG-004 to separate the aim-following issue (resolved) from the BlobAssetReference error (still open).
- Remove references to "camera identity theory" investigation status.

**CODE**

- Edit `Assets/Docs/KNOWN_ISSUES.md`.

---

## Hand-off Instructions

- Pick the next incomplete phase, execute SPEC -> TEST -> CODE, request review, then move on.
- BUG-004's BlobAssetReference error is a **separate issue** not addressed by this spec. It should remain open in KNOWN_ISSUES.md with its own investigation track.
