# Known Issues

**Last Updated:** 2026-03-01

Master tracker for active bugs, investigations, and resolved issues. Link to detailed specs rather than duplicating analysis here.

---

## Open Issues

### BUG-004: Add/remove action throws BlobAssetReference error during physics raycast

**Status:** PARTIAL — aim fix applied; BlobAsset error still open (2026-02-18)
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

### BUG-008: SDFEdit buffer grows without bound — O(N×V) density rebuild cost

**Status:** OPEN (2026-02-23) — not confirmed as a noticeable issue in current build; do not attempt clear-after-bake fix (density is re-sampled from base field + edits each rebuild, so clearing the buffer would undo prior edits)
**Severity:** Medium (performance)
**Affected Systems:** `TerrainChunkDensitySamplingSystem`, `TerrainEditInputSystem`

**Symptoms:**
- Density rebuild slows progressively the more edits the player makes
- After many edits, visible stutter on chunk rebuild; physics and render lag behind input
- Exacerbates the window in which BUG-006 fallthrough can occur (stale collider)

**Root Cause (confirmed 2026-02-23):**
`TerrainEditInputSystem.OnUpdate` appends each new `SDFEdit` to the singleton `DynamicBuffer<SDFEdit>`. `TerrainChunkDensitySamplingSystem.CopyEditsToTempArray` copies the full buffer on every call but never clears it. Each density rebuild re-applies ALL historical edits from the beginning of the game (O(E) per voxel, where E = total edit count). With `EditCooldown = 0.15 s`, E can exceed 400 after a minute of digging.

**Investigation Needed:**
- Determine the correct architecture: options are (a) clear-after-bake (buffer cleared once all affected chunks finish density sampling) or (b) per-chunk stored accumulated density (edits accumulated directly into the blob, not replayed from a global list)
- Option (a) is simpler but requires coordinating "all dirty chunks for this edit batch have been rebuilt" before clearing; currently every system runs in the same frame so this is feasible
- Option (b) decouples chunks but adds per-chunk memory overhead and complicates undo

**Files:**
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs` (line 57 — `editBuffer.Add`)
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs` (line 162 — `CopyEditsToTempArray`, never clears)

---

### BUG-009: Small visual holes remain in terrain mesh after editing

**Status:** FIXED (2026-04-03) — full 3D gradient fix eliminates reversed normals at CSG seams
**Severity:** Medium
**Affected Systems:** `SurfaceNets`, `SDFMath`, `TerrainChunkDensitySamplingSystem`
**Related:** BUG-007 (primary hole fix), BUG-008 (edit buffer growth)

**Confirmed root cause (2026-03-01):**
The "holes" are backface-culled quads, not missing geometry — confirmed via Scene view (inverted triangles visible from opposite angle, dark patches in Game view from camera side) and by setting material Render Face to "Both" (holes disappear, confirming it is purely a winding issue).

**Root cause progression:**
1. *(2026-03-01)* Single far-corner gradient `(x+1, y+1, z+1)` gave near-zero gradient when the corner was deep inside a carved void → unreliable dot sign → inverted winding. Fixed by averaging the face-axis gradient across all 4 face corners.
2. *(2026-04-03)* Face-axis-only gradient (`(0,0,gz)` for XY faces, etc.) still failed at CSG seams where `OpSubtraction`/`OpUnion` create C0-continuous kinks. At these kinks, the SDF gradient is nearly tangent to the face axis, so the single-axis component drops to near-zero even when averaged across 4 corners. ~2-3% of triangles at crater rims and walls received reversed winding.

**Fix applied (2026-04-03):**
Replaced face-axis-only gradient with the full 3D SDF gradient (`GradientAt` helper computing forward-difference `(gx, gy, gz)` at each corner). Each face generator now calls `AveragedGradient` which sums the full 3D gradient across all 4 face corners and passes the result to `EmitTriangleWithGradient`. The full 3D gradient is robust even when the surface gradient is perpendicular to the face axis, because the cross-axis components still provide a reliable sign for the dot product.

**Verification:**
- Visual: Setting Render Face back to "Back" (default Cull Back) confirms no holes visible from any angle after sphere add/subtract edits
- Automated: `SurfaceNetsJob_Winding_ConsistentAfterSphereSubtraction` and `SurfaceNetsJob_Winding_ConsistentAfterSphereAddition` tests validate winding correctness on `OpSubtraction(ground, sphere)` and `OpUnion(ground, sphere)` density fields; all existing winding tests continue to pass

**Remaining minor factors (low priority):**

1. **`minDensity <= 0f` symmetric case not fixed** — BUG-007's fix relaxed `maxDensity > 0f` to `>= 0f` but did not symmetrically address `minDensity == 0f`. Low frequency but structurally identical hole mechanism.

2. **`hasSurface` fallback vertex (crossingCount == 0)** — If a cell passes `hasSurface` but no edge has a sign change, `crossingCount` stays 0 and vertex falls back to cell center, producing a degenerate or misplaced face.

**Files:**
- `Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs` — `AveragedGradient`, `GradientAt`, `GenerateXYFaces`, `GenerateXZFaces`, `GenerateYZFaces`
- `Assets/Scripts/DOTS/Tests/Automated/SurfaceNetsJobTests.cs` — regression tests added

---

### BUG-010: Grass blades not removed after terrain SDF edits

**Status:** OPEN (2026-02-27)
**Severity:** Medium (visual)
**Affected Systems:** `GrassChunkGenerationSystem`, `TerrainChunkEditUtility`
**Spec:** [GRASS_ECS_SPEC.md](AI/GRASS_ECS_SPEC.md) — Phase 3

**Symptoms:**
- Player carves terrain; mesh updates correctly but grass blades remain floating at the original surface positions
- Blades generated from pre-edit vertex positions persist after the Surface Nets mesh is rebuilt

**Root Cause:**
`TerrainChunkEditUtility.MarkChunksDirty` adds `TerrainChunkNeedsDensityRebuild` to affected chunks but does not add `GrassChunkNeedsRebuild`. `GrassChunkGenerationSystem` only rebuilds blade buffers when `GrassChunkNeedsRebuild` is present, so grass is never updated after an edit.

**Fix (not yet applied — Phase 3 of GRASS_ECS_SPEC.md):**
In `TerrainChunkEditUtility.MarkChunksDirty`, after marking a chunk dirty for density, also check for `TerrainChunkGrassSurface` and add `GrassChunkNeedsRebuild`:
```csharp
if (entityManager.HasComponent<TerrainChunkGrassSurface>(chunk))
    entityManager.AddComponent<GrassChunkNeedsRebuild>(chunk);
```
`GrassChunkGenerationSystem` will then regenerate blade positions from the updated mesh vertices on the next frame.

**Files:**
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkEditUtility.cs` — `MarkChunksDirty`
- `Assets/Scripts/DOTS/Terrain/Rendering/GrassChunkGenerationSystem.cs`

---

## Investigation Backlog

| ID | Title | Priority | Spec |
|----|-------|----------|------|
| INV-001 | Terrain banding visual artifacts | Medium | [TERRAIN_BANDING_DIAGNOSTIC_SPEC.md](AI/TERRAIN_BANDING_DIAGNOSTIC_SPEC.md) |
| INV-002 | Degenerate mesh triangles in Surface Nets output | Medium | [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (H4) |
| INV-003 | Raycast origin vs capsule bottom mismatch on slopes | Low | [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (H3) |
| INV-004 | Physics tunneling at high downward velocity | Low | [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (H5) |
| INV-005 | Parallel/dead terrain code stacks | Low | [TERRAIN_SYSTEMS_CODE_AUDIT.md](/TERRAIN_SYSTEMS_CODE_AUDIT.md) |

---

## Resolved Issues

### BUG-003: Reticle not visible in game view

**Status:** FIXED (2026-02-22)
**Severity:** High
**Affected Systems:** `ReticleBootstrap`, `PlayerCameraSystem`, UI rendering path
**Related:** BUG-004 (shared root cause confirmed)
**Spec:** [CAMERA_IDENTITY_FIX_SPEC.md](AI/CAMERA_IDENTITY_FIX_SPEC.md)

**Root Cause:** `ReticleBootstrap` bound to `Camera.main`, but `PlayerCameraSystem` managed a specific camera entity via `SystemAPI.ManagedAPI.GetComponent<Camera>()` — never going through `Camera.main`. The reticle was parented to a static/wrong camera and invisible from the player's viewpoint.

**Fix Applied (2026-02-22):**
- Reticle placement corrected to center-anchored screen-space canvas
- Removed unnecessary `GraphicRaycaster` (reticle is non-interactive)
- Added generated sprite/texture cleanup

**Files:**
- `Assets/Scripts/Player/Bootstrap/ReticleBootstrap.cs`
- `Assets/Scripts/Player/Systems/PlayerCameraSystem.cs`

---

### BUG-005: Terrain add/subtract applies at player feet, not reticle direction

**Status:** FIXED (2026-02-22)
**Severity:** High
**Affected Systems:** `TerrainEditInputSystem`

**Root Cause (confirmed 2026-02-20):** Collision filter mismatch. `TerrainEditInputSystem` used `CollisionFilter.Default` (matches all layers). The player capsule is on layer 1; the camera origin sits inside the capsule, so the ray could exit through the capsule surface yielding a hit at the player's own body.

**Fix Applied:**
- Changed raycast `CollisionFilter` to only collide with terrain layer (`CollidesWith = 2u`)
- Refactored aiming to use `Camera.ViewportPointToRay(0.5, 0.5)` via `CenterAimRayUtility` for center-screen alignment

**Verification:** Confirmed in runtime testing — edits carve/fill at reticle-aligned aim point.

**Files:**
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs`
- `Assets/Scripts/DOTS/Terrain/SDF/Aim/CenterAimRayUtility.cs`

---

### BUG-006: Player falls through terrain when digging below surface; safety respawn fails

**Status:** FIXED as side-effect of BUG-007 (confirmed 2026-02-23)
**Severity:** High
**Affected Systems:** `TerrainChunkColliderBuildSystem`, `PlayerTerrainSafetySystem`, physics collider lifecycle
**Related:** BUG-R001, BUG-R003, BUG-007

**Resolution:** Same `hasSurface > 0f` strict inequality fixed in BUG-007. Edit-sphere boundaries produced exact `0.0f` density values; Surface Nets dropped those cells, leaving mesh gaps. The collider rebuilds from the mesh blob — no mesh faces → no collider geometry → player fell through. The `>= 0f` fix restores those cells. Confirmed: player can no longer dig through terrain.

**Note:** Small visual holes remain at high edit counts — see BUG-009.

---

### BUG-007: Terrain holes appear at chunk boundaries after editing (cross-chunk density mismatch)

**Status:** FIXED (2026-02-23)
**Severity:** High
**Affected Systems:** `TerrainChunkEditUtility`, `TerrainChunkDensitySamplingSystem`

**Root Cause:** Two interrelated sub-bugs:

**A — AABB undersize in dirty-marking:** `TryGetChunkAabb` computed AABB as `(resolution - 1) * voxelSize` (mesh extent only). Density sampling goes one extra sample (`resolution * voxelSize`). Edits touching a chunk's boundary density row but not its mesh AABB would not mark the adjacent chunk dirty, leaving shared boundary density stale.

**B — Fully-carved chunks leave neighbor meshes stale:** After enough edits, a chunk's density becomes all-positive (fully outside terrain), generating an empty mesh. Adjacent chunks — never rebuilt — still assumed the adjacent chunk was solid, leaving no interior wall facing the dug void.

**Fix Applied:**
1. `SurfaceNets.cs`: Relaxed `hasSurface` check from `maxDensity > 0f` to `maxDensity >= 0f`
2. `TerrainChunkEditUtility.TryGetChunkAabb`: Expanded AABB to `resolution * voxelSize`

**Files:**
- `Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs`
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkEditUtility.cs`
- `Assets/Scripts/DOTS/Tests/Automated/SurfaceNetsJobTests.cs` — regression test added

---

### BUG-R001: Player falls through terrain

**Status:** FIXED (2026-02-15)
**Severity:** Critical
**Spec:** [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md)

**Root Cause:** Terrain chunk entities had `PhysicsCollider` but no `PhysicsWorldIndex`, making them invisible to the physics broadphase. Fixed by adding `PhysicsWorldIndex` in `TerrainChunkColliderBuildSystem.ApplyCollider`.

**Additional Mitigations Applied:**
- Removed bootstrap ground plane (intersected terrain range)
- Raised player spawn height to Y=20
- Added and hardened `PlayerTerrainSafetySystem`

---

### BUG-R002: Terrain seam artifacts

**Status:** INVESTIGATED — no chunk-related issues found (2026-01-20)
**Spec:** [TERRAIN_SEAM_DEBUG_SPEC_v1.md](AI/TERRAIN_SEAM_DEBUG_SPEC_v1.md), [TERRAIN_SEAM_DEBUG_MESH_SPEC.md](AI/TERRAIN_SEAM_DEBUG_MESH_SPEC.md)

**Finding:** Seam validator systems found no mismatched vertices at chunk boundaries. Visual artifacts are from flat-shaded normals; recommendation is smooth normal generation.

---

### BUG-R003: PlayerTerrainSafetySystem causes player bouncing/teleport jitter

**Status:** FIXED (confirmed in Play mode, 2026-02-16)
**Severity:** Medium
**Affected Systems:** `PlayerTerrainSafetySystem`, `DotsSystemBootstrap`
**Spec:** [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (Phase 8)

**Root Cause:** Safety raycasts were sensitive to near-ground contacts and triggered false positives during normal movement/landing.

**Fix Applied:**
- Re-enabled safety activation via config in bootstrap (removed hardcoded `if(false)` gate)
- Added minimum downward velocity requirement, player-layer exclusion, and ignore near-end hits (`hit.Fraction >= 0.9`)

**Files:**
- `Assets/Scripts/Player/Systems/PlayerTerrainSafetySystem.cs`
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`
- `Assets/ScriptableObjects/ProjectFeatureConfig.asset`

---

### BUG-R004: Player visual cylinder embedded halfway in terrain

**Status:** FIXED IN CODE (2026-02-16) — verify in Play mode
**Severity:** Low-Medium
**Affected Systems:** `PlayerEntityBootstrap`, `PlayerVisualSync`, `PlayerGroundingSystem`

**Root Cause:** Visual GameObject was synced directly to ECS entity origin. Physics body origin is feet-based; capsule mesh pivot is center-based.

**Fix Applied:** Added `visualOffset` (default `Y=1.0`) to `PlayerVisualSync`; applied during sync so visual capsule bottom aligns with feet-origin ECS position.

**Files:**
- `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs`
- `Assets/Scripts/Player/Bootstrap/PlayerVisualSync.cs`
