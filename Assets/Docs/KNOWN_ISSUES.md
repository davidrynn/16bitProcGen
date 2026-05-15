# Known Issues

**Last Updated:** 2026-05-13

Master tracker for active bugs, investigations, and resolved issues. Link to detailed specs rather than duplicating analysis here.

---

## Open Issues

### BUG-017: Low FPS — sub-20 in BasicTerrainScene, 40–70 in Smoke_BasicPlayable

**Status:** PARTIAL (2026-05-13) — primary hotspot confirmed and fixed; further profiling needed to assess remaining cost
**Severity:** High (gameplay / development experience)
**Affected Systems:** `TerrainChunkMeshUploadSystem`, `TerrainChunkDensitySamplingSystem`, `TerrainChunkMeshBuildSystem`, `TerrainChunkColliderBuildSystem`, `TerrainChunkStreamingSystem`, LOD pipeline, potentially HybridTerrainGenerationSystem
**Related:** BUG-008 (SDFEdit buffer O(N×V)), BUG-012 (RecalculateNormals hotspot)

**Observed Metrics:**
- `Basic Terrain Scene.unity`: **< 20 FPS** (measured in Play mode)
- `Assets/Scenes/Tests/Smoke_BasicPlayable.unity`: **40–70 FPS**

The 2–3× scene gap suggests features unique to BasicTerrainScene account for a large share of the cost. The absolute numbers are surprising given DOTS/ECS + Burst + Job System usage.

**Confirmed Root Causes and Fixes (2026-05-13 profiler):**

1. **(Fixed) `TerrainChunkRenderPrepSystem` iterating all chunks every frame — 27ms/frame.** Query had no filter, iterating every active chunk (400–600 at Medium render distance) recalculating AABB and calling `EntityManager.HasComponent` three times per chunk on the main thread. Fix: added `.WithAll<TerrainChunkNeedsRenderUpload>()` filter and `[UpdateBefore(TerrainChunkMeshUploadSystem)]` ordering. Main thread dropped from 33ms → 17.8ms.

2. **(Fixed) SSAO enabled — 3.3ms `DrawDepthNormalPrepass` every frame.** `ScreenSpaceAmbientOcclusion` renderer feature was active in `PC_Renderer`. Disabled via MCP.

3. **(Fixed) Shadow map: 4 cascades at 2048 resolution with soft shadows — 4.15ms/frame.** Reduced to 2 cascades, 1024 resolution, soft shadows off in `PC_RPAsset`. GPU dropped from 4.1ms → 2.4ms.

**Combined result:** Main thread 33ms → 15.1ms (under 60fps budget). GPU 8ms → 2.4ms. Editor IMGUI overhead (~14ms) accounts for remaining Editor-mode frame time; a standalone build should comfortably hit 60fps.

**Remaining Suspects (ranked by likelihood, not yet profiler-confirmed):**

1. **`RecalculateNormals()` on main thread per upload** — `TerrainChunkMeshUploadSystem.UploadMesh` calls `mesh.RecalculateNormals()` for every chunk that has a new mesh blob. Unity's built-in implementation is single-threaded and O(V+T). With 16 mesh uploads/frame at the default budget, this alone can cost 5–15 ms/frame. This is the same hotspot targeted by the BUG-012 fix (SDF-gradient analytical normals); fixing BUG-012 would eliminate this cost.

2. **Rebuild budgets saturated every frame** — `TerrainLodSettings.Default` sets `MaxDensityRebuildsPerFrame = 16`, `MaxMeshRebuildsPerFrame = 16`, `MaxColliderRebuildsPerFrame = 8`. At Medium render distance (180 world units → streaming radius = 12 chunks), the initial streaming burst generates a large rebuild queue. If the queue never drains to zero (i.e., budgets are hit every frame during normal play), the pipeline runs at maximum throughput permanently, keeping all three rebuild systems active every frame.

3. **SDFEdit buffer O(N×V) cost (BUG-008)** — After the player makes edits, density sampling replays all historical edits on every rebuild. This is a multiplier on suspect #2: each of the 16 density rebuilds/frame scales with edit history length E. At `EditCooldown = 0.15s`, E ≈ 400 after one minute of digging.

4. **HybridTerrainGenerationSystem enabled alongside SDF pipeline** — `ProjectFeatureConfig.EnableHybridTerrainGenerationSystem = true` by default. The heightmap pipeline runs every frame and queries for `TerrainData` entities (none present in the SDF scene), so it likely no-ops — but the system itself is scheduled and may still impose ECS overhead. Worth disabling in SDF scenes to confirm.

5. **Managed allocations in hot paths** — `TerrainChunkMeshUploadSystem.OnUpdate` creates a `new List<UploadItem>()` every frame on the main thread. With 16 uploads/frame this is a minor GC contributor, but the pattern suggests other similar allocations may exist in the pipeline (check the GC Alloc column in Profiler).

6. **Grass / tree / rock render systems running per frame** — `GrassChunkRenderSystem`, `TreeChunkRenderSystem`, `RockChunkRenderSystem` all issue draw calls or schedule jobs each frame. On large terrain footprints, these can compete with terrain systems for job threads.

7. **Burst compilation not active or partially disabled** — Burst jobs degrade to managed code if Burst compilation is disabled (Jobs > Burst > Enable Compilation). Partial failures (e.g., a job with an unsupported type) silently fall back. This should be ruled out first before blaming the algorithm.

**Investigation Plan:**

*Step 1 — Establish baselines with Unity Profiler (do this first)*
- Open `Window > Analysis > Profiler`. Record 3–5 seconds in both scenes.
- Sort the CPU timeline by "Self ms" on the main thread. Identify top 5 costs.
- Check the GC Alloc column — any per-frame allocations in terrain systems are a quick win.
- Check `Jobs > Burst > Enable Compilation` is on. In the Profiler, Burst jobs show as `(Burst)` label; managed fallbacks show without it.

*Step 2 — Confirm or rule out RecalculateNormals hotspot*
- In the Profiler timeline, look for `Mesh.RecalculateNormals` on the main thread. Note its duration and how many times it fires per frame.
- If it exceeds 2 ms/frame, implementing the BUG-012 fix (SDF-gradient analytical normals) should be next.

*Step 3 — Measure rebuild queue depth*
- Enable `DebugSettings.EnableTerrainColliderPipelineDebug` to log backlog counts each frame.
- If collider backlog is > 0 on most frames (not just the initial streaming burst), the rebuild budget needs tuning or the streaming radius needs reducing.
- Try setting `TerrainLodSettings.MaxDensityRebuildsPerFrame = 4` and `MaxMeshRebuildsPerFrame = 4` to see whether FPS improves. Lower throughput = longer drain time but less per-frame spike.

*Step 4 — Disable HybridTerrainGenerationSystem*
- Set `ProjectFeatureConfig.EnableHybridTerrainGenerationSystem = false` in BasicTerrainScene.
- If FPS improves noticeably, the heightmap pipeline has hidden per-frame cost even when no TerrainData entities exist.

*Step 5 — Reduce streaming radius*
- Switch `ProjectFeatureConfig.TerrainRenderDistancePreset` from `Medium` (12 chunk radius) to `Low` (8 chunk radius).
- Measure FPS delta. If significant, chunk count is the primary driver and LOD coarsening or streaming throttle is the solution.

*Step 6 — Profile grass/tree/rock systems independently*
- Temporarily set `EnableGrassChunkRenderSystem`, `EnableTreeRenderSystem`, `EnableRockRenderSystem` to `false`.
- Measure FPS delta to isolate their contribution.

*Step 7 — Fix allocation hotspot (low-hanging fruit)*
- Pre-allocate `List<UploadItem>` as a persistent field in `TerrainChunkMeshUploadSystem` and `Clear()` it each frame instead of allocating a new one. Similar pattern for any other per-frame managed collections found in Step 1.

**Files:**
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs` — `RecalculateNormals` + managed List alloc
- `Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs` — `GradientAt` (BUG-012 fix supply)
- `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs` — density rebuild budget
- `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs` — collider rebuild budget
- `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs` — `MaxDensityRebuildsPerFrame`, `MaxMeshRebuildsPerFrame`, `MaxColliderRebuildsPerFrame`
- `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` — `TerrainRenderDistancePreset`, `EnableHybridTerrainGenerationSystem`
- `Assets/Scripts/DOTS/Core/DebugSettings.cs` — `EnableTerrainColliderPipelineDebug`

---

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

### BUG-011: Unstable physics contacts on terrain mesh colliders — walk-through, depenetration launch, grounding flicker

**Status:** FIXED (2026-04-06)
**Severity:** High (gameplay / fall-through + player launch)
**Affected Systems:** `PlayerMovementSystem`, `PlayerTerrainSafetySystem`, Unity Physics solver (contact generation on open-shell `MeshCollider`), `PlayerGroundingSystem`
**Related:** BUG-004 (BlobAssetReference race — observed once during repro), BUG-008 (edit buffer growth → collider backlog, contributing factor), BUG-009 (visual winding — separate, already fixed), BUG-012 (visual faceting — initially misattributed to this bug)

**Symptoms (all confirmed as one bug via console log analysis 2026-04-03):**
- Player walks through vertical terrain faces (crater walls, carved edges) — persistent even after waiting for collider rebuild
- Player "shot" upward (observed Y≈3.9 → Y≈34.5 in a single frame — Δ30 units) on modified terrain — **mitigated by Layer 2**
- Grounding state flickers every frame at the same stationary position (`fraction=0.0000`, `fallTime` cycling `0.000 → 0.017 → 0.033`) — **mitigated by Layer 1**

**Confirmed root cause:**
The player capsule sits exactly on or slightly overlaps terrain `MeshCollider` surfaces (thin open shells from Surface Nets output). Unity Physics solver contact generation on these open-shell meshes is unstable:

1. **Overlap → depenetration impulse** — Solver pushes capsule out along the contact normal. On steep/vertical surfaces the normal has a large Y component → enormous upward launch (observed: 30-unit single-frame displacement).
2. **Depenetration → separation → gravity → re-overlap** — After being pushed out, the capsule is slightly above the surface. Grounding ray misses (`Ungrounded`). Gravity pulls the capsule back down. It overlaps again. Repeat every frame.
3. **Grounding flicker → movement instability** — `PlayerGroundingSystem` alternates `IsGrounded` true/false each frame. `PlayerMovementSystem` switches between ground-speed snap and air-control lerp each frame, producing jerky/unstable movement and compounding the contact problem.

**Evidence from `[DOTS-FallThrough]` console logs:**
- Stationary player at `(5.04, 4.93, 0.97)`: grounded/ungrounded alternating **every frame** for 30+ frames, `fraction=0.0000` (ray hit at start = entity embedded in surface)
- Launch event at `(1.99, ~3.9→34.5, 0.61)`: single-frame Y displacement of ~30 units, followed by 2.4s fall time before re-grounding — pure solver depenetration impulse
- One `BlobAssetReference` invalid error during session (BUG-004 race, contributing factor)

**Changes attempted (2026-04-04; later rolled back locally due no primary fix):**

*Layer 1 — Grounding hysteresis:*
- Added `UngroundedFrameCount` field to `PlayerMovementState` component
- `PlayerGroundingSystem` now requires 2 consecutive ray misses (`UngroundedHysteresisFrames = 2`) before flipping `IsGrounded` to false
- Prevents single-frame solver jitter from destabilising ground/air mode switching
- Initially set to 3 frames; reduced to 2 after testing showed 3 frames kept the player "grounded" while embedded in walls, fighting the solver

*Layer 2 — Ground movement lerp:*
- `PlayerMovementSystem` ground-mode horizontal velocity changed from direct snap (`currentVelocity.xz = target`) to a high-rate lerp (`GroundLerpRate = 20/s`)
- Physics solver's depenetration corrections are no longer fully overwritten each frame; solver can push the capsule out of overlapping geometry over several frames
- Air-control path unchanged (already used lerp)

*Layer 1 + 2 combined effect (pre-Layer3a):*
- Player launch into air: **no longer observed** (solver corrections survive the lerp, preventing impulse accumulation)
- Grounding flicker: **reduced but still reproducible** near vertical modified surfaces
- Walk-through on modified terrain: **still occurs** — prompted Layer 3 follow-up work

*Layer 1b — Grounding probe filter + start offset (2026-04-04 follow-up):*
- `PlayerGroundingSystem` grounding ray filter changed from `CollisionFilter.Default` to terrain-only (`BelongsTo=1u`, `CollidesWith=2u`) to prevent self/capsule hits
- Ground probe start moved upward by `+0.05m` and probe length adjusted to preserve bottom reach, reducing start-on-surface (`fraction=0`) noise
- Runtime result: **no primary fix**; vertical modified-wall pass-through still reproduces (subjectively "maybe a little better" flicker only)

*Rollback decision (2026-04-04):*
- Reverted gameplay behavior files in local branch after repro showed no material improvement to wall pass-through
- Retained diagnostics/docs changes for further investigation (collider pipeline logs, settings throttle, issue notes)

*Layer 3a — Collider material focused test (2026-04-04):*
- Unity Physics 1.4.4 `Material` does **not** expose per-collider `ContactTolerance`; prior Layer 3 wording ("set ContactTolerance on MeshCollider material") is not directly implementable in this version
- Added `TerrainColliderSettings.EnableDetailedStaticMeshCollision` (default `true`) and applied it when creating terrain `MeshCollider` blobs
- This uses Unity Physics' detailed static mesh contact path intended to reduce ghost/unstable contacts on mesh surfaces
- **Validation pending:** needs fresh repro pass specifically on carved vertical walls

**Contributing factors (not root cause):**
- Collider rebuild backlog (21 → 0 over ~6 frames on initial load; 1-2 chunk backlogs during edits) — leaves stale colliders briefly but drains quickly; not the persistent problem
- `TerrainMat.mat` uses `_Cull: 2` (standard backface culling) — confirms visible walls have correct winding, ruling out hidden winding issues in the physics mesh

**Investigated and ruled-out theories:**
1. ~~**Collider rebuild throttle**~~ — Backlog is real (21→0 over 6 frames on load) but drains. Wait test showed persistent collision failure after all colliders built.
2. ~~**Thin / multi-chunk edits**~~ — Not isolated to chunk boundaries; occurs on single-chunk edits too.
3. ~~**Mesh winding / BUG-009 residuals**~~ — Material uses standard backface culling; visible walls have correct winding. Same blob feeds both render and physics, so winding is consistent.
4. ~~**Visual flicker = same bug as physics**~~ — Initially assumed the visible mesh flickering/moiré was caused by the same contact oscillation. Frame Debugger analysis (2026-04-04) confirmed terrain is drawn exactly once per frame (single `Hybrid Batch Group` in opaque pass). The visual pattern is a separate mesh quality issue — see BUG-012.

**Fix applied (2026-04-06):**

*Root cause* — `PlayerMovementSystem` writes horizontal velocity (X/Z) every frame before the physics solver runs. On horizontal ground this is harmless because the solver corrects on Y (which the movement system doesn't write), so the two never fight. On vertical walls, however, the movement system and the solver both act on the same axis. The solver must win every single frame; if it loses even once (due to incomplete contact manifolds on thin open-shell MeshCollider surfaces), the capsule penetrates and the solver's depenetration impulse overshoots — launching the player upward or pushing them through entirely.

*Wall probe (primary fix)* — Added a short raycast in the horizontal movement direction (capsule radius + 0.1 skin = 0.6 units) in `PlayerMovementSystem`. If terrain is detected within probe distance, the into-wall velocity component is projected out via `vel -= dot(vel, normal) * normal` so the player slides along the surface. The movement system no longer fights the solver on any axis.

*Supporting fixes:*
- Ground velocity lerp (`GroundLerpRate = 25/s`) instead of instant snap, preserving solver corrections between frames
- `PlayerTerrainSafetySystem` lateral recovery improved: velocity projection along wall normal + 0.05m push-out on snap-back
- `EnableDetailedStaticMeshCollision = true` on terrain collider material (Layer 3a)

*Validated:*
- `TerrainWall_AutoDrive_DoesNotCrossFarSide` PlayMode test: PASS
- `SurfaceNetsJob_Winding_ThinBoxWall_BUG011` PlayMode test: PASS (100% outward normals, 264 wall-face tris correct)
- Runtime repro with `TerrainWallReproBootstrap`: player stops at X≈6.82 against wall (threshold 9.15), velocity settles to ~0, `crossed=False` throughout entire 8s drive, zero safety snap-backs, no grounding flicker

*Mesh winding ruled out:* All SurfaceNets winding tests pass (simple walls, sphere addition, sphere subtraction, curved surfaces, thin box wall). Live raycast hit normals confirm correct outward direction. Collider index transfer preserves winding order. The issue was purely the movement system fighting the solver, not geometry.

**Future considerations:**
- The wall probe currently only covers horizontal velocity (X/Z). The movement system does not write `velocity.y` — gravity and jump are handled by the physics integrator and a one-shot impulse respectively. If a future movement mode (swim, jetpack, zero-G) writes `velocity.y` directly, a vertical probe must be added to prevent the same solver-fighting issue on horizontal surfaces (floors/ceilings).
- The probe uses a single ray from capsule center. Very wide capsules or extreme angles could require a sphere/capsule cast instead, but the current 0.5-radius capsule works well with a point ray.

**Diagnostic tooling (implemented 2026-04-03, flags default off):**
- `DebugSettings.EnableTerrainColliderPipelineDebug` → `[DOTS-TerrainColliderPipeline]` logs (render upload vs collider applied, backlog counts)
- `DebugSettings.EnableFallThroughDebug` → `[DOTS-FallThrough]` logs (grounded/ungrounded transitions with positions, normals, fractions)
- `TerrainColliderSettings.MaxCollidersPerFrame` — configurable from singleton (default 4)

**Files changed:**
- `Assets/Scripts/Player/Systems/PlayerMovementSystem.cs` — wall probe + ground velocity lerp (BUG-011 fix)
- `Assets/Scripts/Player/Systems/PlayerTerrainSafetySystem.cs` — lateral recovery improvements (wall push-out + velocity projection)
- `Assets/Scripts/DOTS/Tests/Automated/SurfaceNetsJobTests.cs` — `SurfaceNetsJob_Winding_ThinBoxWall_BUG011` regression test
- `Assets/Scripts/Player/Test/PlayerWallContactCommandPlayModeTests.cs` — updated `OverlappedWall_UngroundedGroundMode` test to match corrected air-control behavior
- `Assets/Scripts/Player/Components/PlayerComponents.cs` — `UngroundedFrameCount` trialed, then rolled back locally
- `Assets/Scripts/Player/Systems/PlayerGroundingSystem.cs` — Layer 1 / Layer 1b trialed, then rolled back locally
- `Assets/Scripts/DOTS/Core/DebugSettings.cs` — `EnableTerrainColliderPipelineDebug`, `EnableFallThroughDebug`, `LogTerrainColliderPipeline`
- `Assets/Scripts/DOTS/Terrain/SDF/TerrainColliderSettings.cs` — `MaxCollidersPerFrame`, `EnableDetailedStaticMeshCollision`
- `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs` — configurable throttle + pipeline logs
- `Assets/Scripts/DOTS/Terrain/Physics/TerrainColliderSettingsBootstrapSystem.cs` — initializes/enforces defaults for `MaxCollidersPerFrame` and `EnableDetailedStaticMeshCollision`
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs` — render upload logs + chunk coord tracking
- `Assets/Scripts/DOTS/Tests/Automated/GrassChunkGenerationTests.cs` — fixed triangle winding in test data (unrelated to BUG-011; test triangle was wound incorrectly causing grass scatter failures)

---

### BUG-012: Terrain mesh faceting / diagonal striping (currently most visible on modified vertical terrain)

**Status:** OPEN (2026-04-04) — Frame Debugger ruled out duplicate draws; normals hypothesis still unimplemented and latest repro scope is narrower
**Severity:** Medium (visual)
**Affected Systems:** `TerrainChunkMeshUploadSystem` (normal generation), `SurfaceNetsJob` (mesh topology)
**Related:** BUG-011 (visual/physics overlap still possible at modified vertical faces), BUG-R002 (seam validator recommended smooth normals — same underlying issue)

**Symptoms:**
- Flicker/striping is most reproducible on **modified vertical terrain surfaces** (carved walls/edges)
- Pattern shifts as camera moves, creating a "flickering" appearance
- Latest playtest did **not** clearly reproduce broad striping on untouched terrain; scope currently considered edit-local until reconfirmed

**Current theory (2026-04-04):**
Frame Debugger confirmed terrain is drawn exactly **once** per frame (single `Hybrid Batch Group` in the `DrawOpaqueObjects` pass; the other `SRP Batch` is the player capsule). No duplicate rendering. The pattern comes from the mesh itself.

`TerrainChunkMeshUploadSystem.UploadMesh` calls `mesh.RecalculateNormals()` after uploading vertex positions. Unity's `RecalculateNormals()` computes per-vertex normals by averaging adjacent face normals. Surface Nets produces a very regular quad grid topology — on gently curved surfaces, this creates per-vertex normals that vary in a regular pattern, causing each quad to shade slightly differently from its neighbors under directional lighting. The result is a visible diagonal grid pattern that shifts with camera/light angle.

Open question: because flicker is now mainly observed on modified vertical faces, some of the visible instability may still be coupled to BUG-011 contact jitter at those faces (same location, different subsystem symptoms).

**Investigated and ruled-out theories:**
1. ~~**URP shadow cascade boundaries**~~ — Disabled main light shadows entirely; pattern persisted unchanged.
2. ~~**SSAO artifacts**~~ — Disabled ScreenSpaceAmbientOcclusion renderer feature; pattern persisted unchanged.
3. ~~**Duplicate mesh rendering / z-fighting between two meshes**~~ — Frame Debugger confirmed single opaque draw call for terrain. `SRP Batch` in opaque pass is the player capsule, not terrain.
4. ~~**Legacy HybridTerrainGenerationSystem creating overlapping GameObjects**~~ — System is enabled in config but queries for `TerrainData` entities; no `TerrainData` entities exist in the SDF pipeline (only `TerrainChunk`). No `DOTS_TerrainMesh_*` GameObjects in scene.
5. ~~**Chunk boundary overlap**~~ — Surface Nets `BaseCellResolution` / `CellResolution` limits prevent face generation beyond chunk boundaries. Density sampling overlap (+1 padding) is for vertex placement only.
6. ~~**Streaming system creating duplicate chunk entities**~~ — `NativeParallelHashMap` deduplication prevents duplicate coords; bootstrap uses direct entity creation (not ECB) so entities are immediately visible to streaming queries.

**Fix (not yet applied):**
Replace `mesh.RecalculateNormals()` with **SDF-gradient-based analytical normals**. The gradient is already computed per-cell in `SurfaceNetsJob` for winding correction (`GradientAt` helper). Store the normalized gradient as the vertex normal during mesh generation, then write it into the mesh vertex buffer alongside positions. This produces smooth normals derived from the actual SDF field shape rather than the discrete mesh topology.

**Files:**
- `Assets/Scripts/DOTS/Terrain/Meshing/SurfaceNets.cs` — `GradientAt` already computes SDF gradient per cell; needs to store per-vertex normals
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs` — `UploadMesh` currently calls `RecalculateNormals()`; needs to write stored normals instead
- `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBlob.cs` — needs `Normals` array added to blob

---

### BUG-016: Relic mesh partially disappears at distance — "globe eating" frustum culling artifact

**Status:** RESOLVED — per-entity rendering + LOD impostor swap implemented (2026-04-24). Billboard impostor mesh not yet authored; full-mesh fallback active until art asset lands.
**Severity:** Medium (visual)
**Affected Systems:** `RelicRealizationSystem`, `RelicLodSelectionSystem`, `DotsSystemBootstrap` (fog)
**Specs:** [RELIC_RENDER_REFACTOR_SPEC.md](AI/STRUCTURE%20PLACEMENT/RELIC_RENDER_REFACTOR_SPEC.md), [RELIC_LOD_IMPOSTOR_SPEC.md](AI/STRUCTURE%20PLACEMENT/RELIC_LOD_IMPOSTOR_SPEC.md)

**Symptoms:**
- At moderate-to-far camera distance, the relic mesh partially disappears in a spherical clipping pattern
- Rotating the camera slightly can restore the full mesh
- The artifact is angle-dependent and distance-dependent
- Visually appears as if the mesh is being "eaten by a globe"

**Root Cause (two layers):**
1. **Original:** `RelicRenderSystem` used `Graphics.RenderMeshInstanced` with a single shared `worldBounds` AABB per batch; URP's bounding-sphere frustum test rejected the whole draw call as the camera moved.
2. **Residual:** After per-entity rendering landed, the GPU rasterizer clips individual triangles against the camera's far clip plane because the relic's world-space bounding radius exceeds `farClipPlane − cameraDistance`. This is fundamental projection behaviour, not a bug.

**Fix (layered):**
- **Per-entity rendering:** Replaced batch `RenderMeshInstanced` with per-entity Entities Graphics in `RelicRealizationSystem`. Each relic has its own `RenderBounds`. Resolves the first artifact (batch-wide frustum culling).
- **Distance fog:** Linear `RenderSettings.fog` applied at bootstrap with start/end distances derived from the camera far clip (`ProjectFeatureConfig.FogStartRatio` / `FogEndRatio`). Provides atmospheric depth but does **not** hide the far-plane clipping — the relic mesh spans such a wide distance range that clipped geometry is still in the un-fogged near field.
- **LOD swap infrastructure:** `RelicLodSelectionSystem` exists and can swap to a billboard impostor. Billboard is the correct fix: a small flat quad never extends past the far clip. See [RELIC_BILLBOARD_IMPOSTOR_SPEC.md](AI/STRUCTURE%20PLACEMENT/RELIC_BILLBOARD_IMPOSTOR_SPEC.md).

**Remaining work:** Implement the billboard impostor (pre-baked atlas quad) so the relic swaps to a small flat representation before far-plane clipping distance. The LOD swap machinery is in place; the billboard slots into the existing LOD 1 mesh/material pair.

**Files:**
- `Assets/Scripts/DOTS/Structures/RelicRealizationSystem.cs`
- `Assets/Scripts/DOTS/Structures/RelicLodSelectionSystem.cs`
- `Assets/Scripts/DOTS/Structures/RelicLodState.cs`
- `Assets/Scripts/DOTS/Structures/RelicRenderConfig.cs`
- `Assets/Scripts/DOTS/Structures/RelicVisualBootstrap.cs`
- `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` — fog config fields + derived distances
- `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` — `ApplyDistanceFog()`

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

### BUG-015: Tree capsule instances not rendering in-game (URP DrawMeshInstanced incompatibility)

**Status:** FIXED (2026-04-10)
**Severity:** High
**Affected Systems:** `TreeChunkRenderSystem`, `TreeVisualBootstrap`, "Unlit green.mat"

**Root Cause:**
`Graphics.DrawMeshInstanced` is a legacy API that does not integrate with Unity 6 / URP's SRP render pipeline. Draw calls were being submitted (confirmed 1434 instances/frame via diagnostic) but never processed by URP's forward renderer. Additionally, the "Unlit green.mat" material had `_BaseColor=(1,1,1,1)` (white) which would have made trees render as white capsules even if the draw calls had worked.

**Fix:**
- Replaced `Graphics.DrawMeshInstanced` with `Graphics.RenderMeshInstanced(RenderParams, ...)` in `TreeChunkRenderSystem.cs` — this API integrates correctly with URP/SRP.
- Updated "Unlit green.mat" `_BaseColor` from white `(1,1,1,1)` to forest green `(0.327, 0.472, 0.347)` to match the intended tree color.
- Removed redundant `material.enableInstancing = true` call (not required by `RenderMeshInstanced`).

**Verified:** 1320 tree instances visible in-game at terrain surface (~Y=4-5), 576 chunks with placement records.

---

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
