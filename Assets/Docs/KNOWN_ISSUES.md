# Known Issues

**Last Updated:** 2026-02-22

Master tracker for active bugs, investigations, and resolved issues. Link to detailed specs rather than duplicating analysis here.

---

## Open Issues

### BUG-003: Reticle not visible in game view

**Status:** FIXED (2026-02-22)
**Severity:** High
**Affected Systems:** `ReticleBootstrap`, `PlayerCameraSystem`, UI rendering path
**Related:** BUG-004 (shared root cause confirmed)
**Spec:** [CAMERA_IDENTITY_FIX_SPEC.md](AI/CAMERA_IDENTITY_FIX_SPEC.md)

**Symptoms:**
- No visible reticle in game view (even after adding `ReticleBootstrap` to a scene GameObject)

**Failed Theories (2026-02-16):**
1. ~~`ReticleBootstrap` not in scene~~ — Added `[RuntimeInitializeOnLoadMethod]` auto-creation and also manually added to a scene GameObject. Reticle still not visible; root cause is elsewhere.

**Current Theory (2026-02-17): Camera identity mismatch**
`ReticleBootstrap` binds to `Camera.main`, but `PlayerCameraSystem` manages a specific camera entity via `SystemAPI.ManagedAPI.GetComponent<Camera>()` — it never goes through `Camera.main`. If a scene camera is also tagged `MainCamera`, or if `Camera.main` resolves to a different camera than the one `PlayerCameraSystem` updates, the reticle gets parented to a static/wrong camera and is invisible from the player's viewpoint.

**Diagnostics added (2026-02-17):**
- `ReticleBootstrap`: logs camera instanceID on bind and warns if multiple cameras exist
- `PlayerCameraSystem`: logs its managed camera instanceID and whether it matches `Camera.main`
- Run Play mode → check Console for `[ReticleBootstrap] DIAG:` and `[PlayerCamera] DIAG:` lines; compare instanceIDs

**Files:**
- `Assets/Scripts/Player/Bootstrap/ReticleBootstrap.cs`
- `Assets/Scripts/Player/Systems/PlayerCameraSystem.cs`

**Additional findings (2026-02-22):**
- Reticle placement is center-anchored and functionally correct for a screen-center indicator.
- Implementation is not yet highly reusable: styling and behavior are hardcoded in `ReticleBootstrap` (dot size/color/outline/canvas config), with no shared settings surface for other features.
- `ReticleBootstrap` previously added a `GraphicRaycaster` even though the reticle is non-interactive; this overhead was removed in the 2026-02-22 refactor.
- Generated reticle sprite/texture lifecycle was previously unmanaged; cleanup was added in the 2026-02-22 refactor.

---

### BUG-005: Terrain add/subtract applies at player feet, not reticle direction

**Status:** FIXED (2026-02-22)
**Severity:** High
**Affected Systems:** `TerrainEditInputSystem`

**Symptoms:**
- Pressing Q/E or mouse buttons did not carve/fill where the reticle pointed
- Modification appeared directly beneath the player cylinder (at or near feet)

**Root Cause (confirmed 2026-02-20):** Collision filter mismatch.
`TerrainEditInputSystem` used `CollisionFilter.Default` for its edit raycast, which matches **all** physics layers. The player capsule is registered in the ECS physics world on layer 1 (`BelongsTo = 1u`). The camera origin sits inside the capsule (camera at player Y+1.6; capsule spans Y=0 to Y=2.0). Unity.Physics can return the capsule surface as a valid ray exit hit, yielding a `hit.Position` at the player's own body — making edits appear "under the player."

**Fix Applied (2026-02-20):**
Changed the raycast `CollisionFilter` in `TerrainEditInputSystem.TryGetBrushCommand` to only collide with the terrain layer (`CollidesWith = 2u`), matching the filter used in `TerrainChunkColliderBuildSystem` (`BelongsTo = 2u`). This prevents the ray from hitting the player capsule (layer 1) entirely.

**Additional findings + remediation (2026-02-22):**
- Aiming previously used `Camera.main.transform.position/forward`, which can diverge from true center-screen under lens shift/custom projection/Cinemachine composition.
- Refactor applied: `TerrainEditInputSystem` now uses shared center-screen ray generation via `Camera.ViewportPointToRay(0.5, 0.5)` (through `CenterAimRayUtility`) so firing semantics align with the reticle center contract.

**Verification:**
- Confirmed in runtime testing: edits now carve/fill at reticle-aligned aim point, not at player feet.
- Center-screen aiming uses shared viewport center ray generation (`CenterAimRayUtility`).

**Files:**
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs` (line ~104)
- `Assets/Scripts/DOTS/Terrain/SDF/Aim/CenterAimRayUtility.cs`

---

### BUG-004: Add/remove action throws BlobAssetReference error during physics raycast

**Status:** ROOT CAUSE CONFIRMED for aim; BlobAsset error still open (2026-02-18)
**Severity:** High
**Affected Systems:** `TerrainEditInputSystem`, `PlayerGroundingSystem`, `PlayerCameraSystem`, terrain collider lifecycle
**Related:** BUG-003 (shared root cause confirmed for aim-not-following-player)
**Spec:** [CAMERA_IDENTITY_FIX_SPEC.md](AI/CAMERA_IDENTITY_FIX_SPEC.md) (aim fix); BlobAsset error is separate

**Symptoms:**
- Add/remove interaction triggers `InvalidOperationException` related to invalid `BlobAssetReference` during `physicsWorld.CastRay()`
- **New observation (2026-02-17):** Terrain edits work but do NOT follow the player when moving — edits fire from a fixed world position instead of the current camera aim

**Observed Error (abbreviated):**
```text
InvalidOperationException: The BlobAssetReference is not valid. Likely it has already been unloaded or released.
Unity.Entities.BlobAssetReferenceData.ValidateNonBurst()
Unity.Entities.BlobAssetReference`1[T].get_Value()
Unity.Physics.RigidBody+RigidBodyUtil.CastRay(...)
Unity.Physics.PhysicsWorld.CastRay(...)
DOTS.Player.Systems.PlayerGroundingSystem.OnUpdate(...) (Assets/Scripts/Player/Systems/PlayerGroundingSystem.cs:82)
```

**Failed Theories (2026-02-16):**
1. ~~ECB disposal timing~~ — Deferred blob disposal until after ECB playback in `TerrainChunkColliderBuildSystem`. Did not fix the error.
2. ~~Missing collider cleanup in streaming~~ — Added `TerrainChunkColliderData` disposal and `PhysicsCollider` removal in `TerrainChunkStreamingSystem` despawn path. Did not fix the error. (Kept as a valid leak fix but not the root cause.)

**Current Theory (2026-02-17): Camera identity mismatch (aim not following player)**
`TerrainEditInputSystem` raycasts from `Camera.main.transform.position/forward`. `PlayerCameraSystem` updates a specific camera entity's managed `Camera` component — NOT via `Camera.main`. If `Camera.main` resolves to a different (static) camera, the edit raycast always fires from the same world position regardless of player movement. This is the same root cause suspected for BUG-003.

The BlobAssetReference error is a separate issue: it occurs in `PlayerGroundingSystem` when raycasting against a terrain chunk whose collider blob has been invalidated (likely by chunk despawn/rebuild in the streaming system).

**Diagnostics added (2026-02-17):**
- `TerrainEditInputSystem`: logs `Camera.main` identity (name, instanceID, position) on first edit; enumerates all cameras in scene; logs ray origin/direction on every edit
- `PlayerCameraSystem`: logs its managed camera instanceID and whether it matches `Camera.main`
- Run Play mode → move player → press Q/E → check Console for `[TerrainEditInput] DIAG:` and `[PlayerCamera] DIAG:` lines; compare instanceIDs and positions

**Notes:**
- The BlobAssetReference error may originate from a collider that was never valid to begin with, not from premature disposal.
- Further investigation needed into what specific `RigidBody` has the invalid collider reference and why.

**Files:**
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs`
- `Assets/Scripts/Player/Systems/PlayerCameraSystem.cs`
- `Assets/Scripts/Player/Systems/PlayerGroundingSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs`

---

### BUG-R003: PlayerTerrainSafetySystem causes player bouncing/teleport jitter

**Status:** FIXED (confirmed in Play mode, 2026-02-16)
**Severity:** Medium
**Affected Systems:** `PlayerTerrainSafetySystem`, `DotsSystemBootstrap`
**Spec:** [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (Phase 8)

**Symptom (previous):** Player capsule visibly snap-backed/bounced in place when safety was active.

**Root Cause (confirmed):** Safety raycasts were still sensitive to near-ground contacts and could trigger false positives in normal movement/landing conditions.

**Fix Applied (2026-02-16):**
- Re-enabled safety activation via config in bootstrap (removed hardcoded `if(false)` gate)
- Added stronger safety guards in `PlayerTerrainSafetySystem`:
  - Minimum downward velocity requirement
  - Player-layer exclusion in collision filter
  - Ignore near-end hits (`hit.Fraction >= 0.9`) and self hits

**Verification:** User-confirmed resolved; issue no longer reproduces.

**Files:**
- `Assets/Scripts/Player/Systems/PlayerTerrainSafetySystem.cs`
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`
- `Assets/ScriptableObjects/ProjectFeatureConfig.asset` (toggle)

---

### BUG-R004: Player visual cylinder embedded halfway in terrain

**Status:** FIXED IN CODE (2026-02-16) — verify in Play mode
**Severity:** Low-Medium
**Affected Systems:** `PlayerEntityBootstrap`, `PlayerVisualSync`, `PlayerGroundingSystem`

**Symptom:** The player's visual capsule (`Player Visual (ECS Synced)`) appears to sit halfway inside the terrain surface rather than resting on top of it.

**Cause:** The visual GameObject was synced directly to the ECS entity origin. Physics body origin is feet-based; capsule mesh pivot is center-based.

**Fix Applied (2026-02-16):** Added `visualOffset` to `PlayerVisualSync` (default `Y=1.0`) and apply rotated offset during sync so visual capsule bottom aligns with feet-origin ECS position.

**Verification Needed:** Confirm visual alignment across first-person, third-person, and non-upright rotations.

**Files:**
- `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs` — `CreatePlayerVisual()` (line ~216)
- `Assets/Scripts/Player/Bootstrap/PlayerVisualSync.cs` — sync logic

---

### BUG-R001: Player falls through terrain

**Status:** FIXED (2026-02-15)
**Severity:** Critical
**Spec:** [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md)

**Root Cause:** Terrain chunk entities had `PhysicsCollider` but no `PhysicsWorldIndex`, making them invisible to the physics broadphase. Fixed by adding `PhysicsWorldIndex` in `TerrainChunkColliderBuildSystem.ApplyCollider`.

**Additional Mitigations Applied:**
- Removed bootstrap ground plane (intersected terrain range)
- Raised player spawn height to Y=20
- Added and hardened `PlayerTerrainSafetySystem` (re-enabled via config, with anti-false-positive guards)

---

### BUG-R002: Terrain seam artifacts

**Status:** INVESTIGATED — no chunk-related issues found (2026-01-20)
**Spec:** [TERRAIN_SEAM_DEBUG_SPEC_v1.md](AI/TERRAIN_SEAM_DEBUG_SPEC_v1.md), [TERRAIN_SEAM_DEBUG_MESH_SPEC.md](AI/TERRAIN_SEAM_DEBUG_MESH_SPEC.md)

**Finding:** Seam validator systems found no mismatched vertices at chunk boundaries. Visual artifacts are from flat-shaded normals; recommendation is smooth normal generation.

---

## Investigation Backlog

| ID | Title | Priority | Spec |
|----|-------|----------|------|
| INV-001 | Terrain banding visual artifacts | Medium | [TERRAIN_BANDING_DIAGNOSTIC_SPEC.md](AI/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md) |
| INV-002 | Degenerate mesh triangles in Surface Nets output | Medium | [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (H4) |
| INV-003 | Raycast origin vs capsule bottom mismatch on slopes | Low | [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (H3) |
| INV-004 | Physics tunneling at high downward velocity | Low | [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (H5) |
| INV-005 | Parallel/dead terrain code stacks | Low | [TERRAIN_SYSTEMS_CODE_AUDIT.md](/TERRAIN_SYSTEMS_CODE_AUDIT.md) |
