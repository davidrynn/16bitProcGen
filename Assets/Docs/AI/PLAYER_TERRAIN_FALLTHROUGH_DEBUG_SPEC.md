# Player Terrain Fall-Through Debug Spec

**Date:** 2026-02-08
**Updated:** 2026-02-15
**Status:** ROOT CAUSE CONFIRMED + FIXED. Terrain colliders lacked `PhysicsWorldIndex` — invisible to physics broadphase. Runtime validated.
**Bug:** Player intermittently falls through terrain mesh during movement. The issue appears more likely when terrain height increases (uphill movement).

---

## 1. Symptom Description

The player entity moves across SDF terrain and occasionally falls through the surface. Key observations:

- The fall-through is **intermittent**, unknown if it is predictable or not, but does not happen on all terrain.
- It appears more frequent when **terrain height is increasing** (walking uphill).
- The terrain mesh is visually present where the fall-through occurs.

---

## 2. System Architecture Summary

### Terrain Pipeline (tag-driven, multi-frame)

```
TerrainChunkStreamingSystem     [SimulationSystemGroup]
    | adds TerrainChunkNeedsDensityRebuild
    v
TerrainChunkDensitySamplingSystem  [SimulationSystemGroup]
    | adds TerrainChunkNeedsMeshBuild
    v
TerrainChunkMeshBuildSystem     [SimulationSystemGroup]
    | adds TerrainChunkNeedsColliderBuild + TerrainChunkNeedsRenderUpload
    v
TerrainChunkColliderBuildSystem [SimulationSystemGroup, before PhysicsSystemGroup]
    | max 4 colliders/frame
    v
PhysicsSystemGroup
    PlayerTerrainSafetySystem -> teleport player above terrain if below surface
    PlayerGroundingSystem  -> raycast down from entity position
    PlayerMovementSystem   -> set velocity from input + grounded state
    PhysicsSimulationGroup -> integrate velocity, resolve contacts
```

### Player Physics Setup

| Property | Value | Source |
|----------|-------|--------|
| Collider | Capsule: V0=(0,0.5,0) V1=(0,1.5,0) R=0.5 | PlayerEntityBootstrap.cs:125-130 |
| Mass | 70 kg (InverseMass=1/70) | PlayerEntityBootstrap.cs:117 |
| Gravity | Factor=1.0 (standard) | PlayerEntityBootstrap.cs:158 |
| Damping | Linear=0, Angular=0 | PlayerEntityBootstrap.cs:148-149 |
| Layer | BelongsTo=1u, CollidesWith=~0u | PlayerEntityBootstrap.cs:138-139 |
| Spawn Position | (0, 20, 0) | PlayerEntityBootstrap.cs:20,93 |
| Ground Probe | 1.3 units downward from entity position | PlayerEntityBootstrap.cs:68 |

### Terrain Collider Setup

| Property | Value | Source |
|----------|-------|--------|
| Type | MeshCollider from Surface Nets output | TerrainChunkColliderBuildSystem.cs:139 |
| Layer | BelongsTo=2u, CollidesWith=~0u | TerrainChunkColliderBuildSystem.cs:94-98 |
| Build Rate | Max 4 per frame | TerrainChunkColliderBuildSystem.cs:18 |
| Material | Material.Default | TerrainChunkColliderBuildSystem.cs:139 |

### SDF Terrain Shape (defaults from TerrainBootstrapAuthoring)

| Parameter | Default | Effect |
|-----------|---------|--------|
| BaseHeight | 0.0 | Center of terrain undulation |
| Amplitude | 4.0 | Height range: BaseHeight +/- Amplitude = [-4, +4] |
| Frequency | 0.1 | Wavelength ~62.8 units (2*pi/0.1) |
| NoiseValue | 0.0 | Additive noise constant |

Terrain height formula: `h(x,z) = BaseHeight + Amplitude * ((sin(x*Freq) + sin(z*Freq))*0.5 + NoiseValue)`

---

## 3. Hypotheses

### H1: Collider Not Yet Built When Player Arrives — FIX APPLIED

**Theory:** When the player walks into a newly streamed chunk region, the terrain mesh renders immediately after upload, but the physics collider takes additional frames to build due to the multi-frame pipeline and rate limiting (max 4 colliders/frame). During this window, the player sees terrain but has nothing to stand on.

**Evidence chain:**
- Streaming spawns chunks with `TerrainChunkNeedsDensityRebuild` tag (TerrainChunkStreamingSystem.cs:157)
- Pipeline takes at minimum 3 frames: density -> mesh -> collider
- Collider build is rate-limited to 4/frame (TerrainChunkColliderBuildSystem.cs:18)
- The player's `PlayerGroundingSystem` raycast (PlayerGroundingSystem.cs:55) will return `false` if no collider exists yet
- With `IsGrounded=false`, gravity pulls the player down; the mesh has no physics presence

**Why height-related:** Taller terrain occupies more of the chunk's vertical range. If the player approaches from a lower chunk that has colliders, they step onto a higher neighboring chunk whose collider isn't ready yet. The height transition makes the timing gap more noticeable because there's more vertical distance to "miss."

**Test plan:**
1. Add diagnostic logging to `TerrainChunkColliderBuildSystem` that records how many frames elapse between `TerrainChunkNeedsColliderBuild` being added and the collider actually being built
2. Log the player's chunk coordinate vs. which chunks have `PhysicsCollider` attached
3. Reproduce by walking toward a streaming boundary and checking if the fall occurs on a chunk that lacks `PhysicsCollider`

### H2: Ground Plane Interference at Y=0 — FIX APPLIED

**Theory:** `PlayerEntityBootstrap` created a static 50x50 ground plane at Y=0. The SDF terrain has BaseHeight=0 and Amplitude=4, meaning terrain surface ranges from Y=-4 to Y=+4. The ground plane at Y=0 intersects this range. When the player falls through terrain, they land on the ground plane at Y=0 instead of passing through entirely -- the ground plane traps the player under the terrain surface.

**Suspected mechanism (2026-02-15):** The player spawned at Y=2, fell to the ground plane at Y=0 before terrain colliders were built (H1), and was then trapped between the ground plane below and one-sided terrain mesh colliders above. Mesh colliders only block from the top side, so the player could not push back up through terrain. Jumping high enough to clear the terrain surface allowed the player to land on top and remain correctly grounded. Fix applied: ground plane removed, spawn height raised, safety teleport added. Awaiting runtime validation.

**Evidence chain:**
- Ground plane is at Y=0 with BelongsTo=2u (same layer as terrain)
- Terrain mesh colliders also use BelongsTo=2u
- Ground plane is 50x50 units, terrain chunks extend beyond this
- Both collide with player (CollidesWith=~0u)

**Why height-related:** At terrain heights above Y=0, the ground plane sits below the terrain. At terrain heights near Y=0, the player capsule might simultaneously contact the ground plane and terrain, causing solver instability.

**Test plan:**
1. Disable ground plane creation in bootstrap (comment out `CreateGroundPlane` call)
2. Run game and test if fall-through persists
3. If fall-through stops, ground plane is the cause or a contributing factor

### H8: Missing PhysicsWorldIndex on Terrain Chunks — ROOT CAUSE, FIXED

**Theory:** Terrain chunk entities were built with `PhysicsCollider` but never received `PhysicsWorldIndex`, the shared component that registers an entity in the Unity Physics broadphase. Without it, the physics engine completely ignores the colliders. The player (which has `PhysicsWorldIndex` via `PlayerEntityBootstrap.cs:165`) had nothing to collide against.

**Confirmed mechanism (2026-02-15):** The `TerrainChunkStreamingSystem` spawns chunk entities with `TerrainChunk`, `TerrainChunkGridInfo`, `TerrainChunkBounds`, `LocalTransform`, and pipeline tags — but no `PhysicsWorldIndex`. The `TerrainChunkColliderBuildSystem` then builds and attaches `PhysicsCollider` via ECB, but also never added `PhysicsWorldIndex`. The colliders existed on the entities but were invisible to the broadphase. Every terrain chunk in the scene had a valid mesh collider that the physics solver never saw.

**Evidence chain:**
- `PlayerEntityBootstrap.cs:165` adds `PhysicsWorldIndex` to the player entity
- No terrain code path (streaming, collider build) ever added `PhysicsWorldIndex` to chunk entities
- Grep for `PhysicsWorldIndex` across the codebase confirmed: only player, test, and legacy bootstrap code added it
- After adding `PhysicsWorldIndex` in `TerrainChunkColliderBuildSystem.ApplyCollider`, player correctly lands on and walks across terrain

**Fix applied:**
- Added `PhysicsWorldIndex` (default world 0) to terrain chunk entities in `TerrainChunkColliderBuildSystem.ApplyCollider` when first attaching a collider
- Tuned `PlayerTerrainSafetySystem` thresholds: `BelowThreshold` 0.5 -> 2.0 (avoids false triggers from Surface Nets discretization error), `AboveOffset` 2.0 -> 1.0, added 1s cooldown

**Runtime validated:** Player now lands on terrain, walks across all heights, and the safety teleport does not fire during normal movement.

### H3: Raycast Origin vs. Capsule Bottom Mismatch (MEDIUM PRIORITY)

**Theory:** `PlayerGroundingSystem` casts a ray from `transform.Position` (PlayerGroundingSystem.cs:44,49-50), which is the entity's LocalTransform position. The capsule collider has Vertex0 at (0, 0.5, 0), meaning the capsule bottom is at entity_Y + 0.5 - 0.5 = entity_Y. The ray probes 1.3 units below entity position. On steep uphill terrain, the terrain surface may be above the entity position, meaning the player is partially embedded. The raycast goes downward from entity position, potentially missing the actual contact surface above.

**Evidence chain:**
- Ray start: `origin = transform.Position` (entity base)
- Ray end: `origin - up * 1.3`
- Capsule bottom = entity_Y + Vertex0.y - Radius = entity_Y + 0.5 - 0.5 = entity_Y
- On a steep uphill slope, the terrain surface ahead is above the entity's feet
- After stepping up, if physics pushes entity up but the raycast still fires from the old position, there could be a 1-frame grounding gap

**Why height-related:** Steeper uphill terrain creates a larger angle between the terrain normal and the vertical raycast. A single vertical ray is more likely to miss contact on steep slopes.

**Test plan:**
1. Add a `RaycastHit` output to the grounding raycast and log the hit distance and hit normal
2. Test on flat terrain vs. steep uphill terrain
3. Check if `IsGrounded` flickers to `false` when walking uphill on terrain that visually exists

### H4: Degenerate Mesh Triangles Creating Collider Holes (MEDIUM PRIORITY)

**Theory:** The Surface Nets algorithm produces degenerate triangles (near-zero area, extreme aspect ratios) as documented in the banding diagnostic spec. `Unity.Physics.MeshCollider.Create` may discard or mishandle these triangles, creating holes in the collision mesh that the player capsule can pass through.

**Evidence chain:**
- Banding diagnostic found max aspect ratio of 98.53 and min triangle area of 0.0018 (vs avg 0.36)
- These thin triangles occur at height band boundaries -- exactly where "height increasing" terrain transitions happen
- The recent edge-interpolation fix (Phase 8) should reduce banding but validation is pending
- If degenerate triangles still exist post-fix, they create narrow collision gaps

**Why height-related:** Banding/degenerate triangles concentrate at specific Y-levels where the terrain surface is nearly parallel to voxel grid planes. Walking uphill means crossing more of these Y-level transitions per unit of horizontal distance.

**Test plan:**
1. Run the pending SurfaceNetsJobTests (Phase 15 validation) to confirm banding fix effectiveness
2. Add a diagnostic pass in `TerrainChunkColliderBuildSystem` that logs triangle statistics (min area, max aspect ratio) per chunk collider
3. Visualize the collider mesh wireframe vs. the render mesh to identify discrepancies
4. Test with increased voxel resolution (smaller VoxelSize) to see if fall-through frequency changes

### H5: Physics Tunneling at High Downward Velocity (LOW PRIORITY)

**Theory:** If the player accumulates high downward velocity (e.g., after a brief grounding loss on a slope), the discrete physics step may tunnel through thin terrain mesh colliders. With `PhysicsDamping.Linear=0` and `PhysicsGravityFactor=1`, velocity can accumulate indefinitely during airborne time.

**Evidence chain:**
- No linear damping on player (PlayerEntityBootstrap.cs:149)
- After losing ground contact, `FallTime` increments but nothing caps downward velocity
- Terrain mesh colliders are thin (single-layer Surface Nets mesh)
- Unity Physics uses discrete collision detection by default for mesh colliders
- At 60fps with gravity ~9.81 m/s^2, after 0.5s of freefall: v = 4.9 m/s, displacement per frame = 0.08m. This is unlikely to tunnel through typical colliders, but at lower framerates or after longer falls it becomes possible.

**Why height-related:** Walking uphill and briefly losing contact (H3) leads to a small fall that recontacts at an angle, but if the grounding loss persists for even a few frames, velocity builds.

**Test plan:**
1. Log `PhysicsVelocity.Linear.y` when fall-through occurs
2. Check if continuous collision detection (CCD) is available for the player collider
3. Add a velocity cap or increase terrain collider thickness

### H6: Chunk Vertical Coverage Insufficient (LOW PRIORITY)

**Theory:** The terrain chunks have a single Y-level (ChunkCoord.y=0). With resolution=16, voxelSize=1, the vertical span is 15 units. The origin Y = BaseHeight - 7.5 = -7.5. So the density grid covers Y=-7.5 to Y=+7.5. The terrain surface at Y=+4 (max amplitude) is within range. However, the player starts at Y=2 and can walk to terrain peaks at Y=4. The capsule top reaches entity_Y+2 = Y=6. This is within the chunk range. So this hypothesis is unlikely with default settings, but could trigger with non-default Amplitude or BaseHeight.

**Test plan:**
1. Verify that `Amplitude + BaseHeight + 2 (player height)` < `chunkVerticalSpan / 2 + BaseHeight`
2. If inequality fails, increase chunk resolution or adjust BaseHeight

### H7: ECB Structural Changes Create Frame-Gap in Collider (LOW PRIORITY)

**Theory:** `TerrainChunkColliderBuildSystem` uses an `EntityCommandBuffer` with `Allocator.Temp` and calls `Playback` at the end of `OnUpdate`. The system runs `[UpdateBefore(typeof(PhysicsSystemGroup))]`. If the ECB playback modifies the entity archetype (adding `PhysicsCollider`), the physics broadphase might not see the new collider until the next frame's broadphase build.

**Evidence chain:**
- `ApplyCollider` calls `ecb.AddComponent(entity, colliderComponent)` for new colliders (TerrainChunkColliderBuildSystem.cs:163)
- ECB playback happens at line 112, before the system exits
- But `PhysicsSystemGroup` builds the broadphase from the current world state
- If broadphase build runs before the new collider is "visible" in the world, there's a 1-frame gap

**Test plan:**
1. Check Unity Physics source/docs for whether structural changes from ECB are visible in the same frame's physics step
2. If not, replace ECB with direct `EntityManager` calls for the `PhysicsCollider` component (structural change happens immediately)

---

## 4. Diagnostic Implementation Plan

### Phase 0: Instrumentation (Non-Invasive)

Create `PlayerFallThroughDiagnosticSystem` that runs in `[UpdateInGroup(typeof(SimulationSystemGroup)), OrderLast=true]`:

```
Tracked per frame:
- Player entity position (float3)
- Player velocity.y (float)
- Player IsGrounded state
- Player's current chunk coordinate (derived from position / chunkStride)
- For each chunk in 3x3 grid around player:
  - Has TerrainChunkNeedsColliderBuild tag? (collider pending)
  - Has PhysicsCollider component? (collider ready)
  - Has TerrainChunkMeshData? (mesh ready)
  - Frame count since chunk spawned
```

**Trigger condition:** Log a detailed snapshot when:
- `IsGrounded` transitions from `true` to `false` while `velocity.y <= 0` (unexpected ungrounding)
- Player position.y drops below terrain surface height at that XZ (using SDFTerrainField.Sample)

### Phase 1: Test H1 (Collider Timing)

Add frame counters to track collider pipeline latency:
1. In `TerrainChunkStreamingSystem`: record `SystemAPI.Time.ElapsedTime` when chunk spawns
2. In `TerrainChunkColliderBuildSystem`: compute elapsed time since spawn when collider is built
3. Log: `"Chunk {coord} collider built {N} frames after spawn"`
4. Cross-reference with player position to identify if player entered chunk during the gap

### Phase 2: Test H2 (Ground Plane)

1. Add a `DebugSettings.DisableBootstrapGroundPlane` flag
2. Guard `CreateGroundPlane` call with the flag
3. Run game with ground plane disabled and test for fall-through

### Phase 3: Test H3 (Raycast Accuracy)

1. Modify `PlayerGroundingSystem` to use `physicsWorld.CastRay(rayInput, out RaycastHit closestHit)` instead of the `bool`-only overload
2. Log `closestHit.Position`, `closestHit.SurfaceNormal`, `closestHit.Fraction` when grounding state changes
3. Add a second raycast from capsule center `(entityPos + (0, 1, 0))` to detect if terrain is above entity position
4. Visualize ray with `Debug.DrawLine` in diagnostic mode

### Phase 4: Test H4 (Mesh Quality)

1. After collider build, compute per-chunk mesh statistics:
   - Min/max/avg triangle area
   - Max aspect ratio
   - Number of degenerate triangles (area < epsilon)
2. Store in `TerrainChunkColliderDiagnostics` component
3. When fall-through detected, log the mesh stats for the chunk at player position

### Phase 5: Test H5 (Tunneling)

1. Log max downward velocity before each grounding event
2. If velocity exceeds tunneling threshold (`voxelSize / deltaTime`), flag as tunneling risk
3. Consider adding speculative contacts or velocity clamping

---

## 5. Reproduction Steps

1. Open the project in Unity 6.2
2. Enter Play mode with the terrain scene
3. Move the player (WASD) across terrain, specifically toward uphill slopes
4. Observe: the player occasionally drops through the visible terrain mesh
5. More likely to happen:
   - Near chunk boundaries (streaming region edges)
   - When walking uphill
   - When approaching terrain for the first time (fresh chunk spawns)

---

## 6. Fix Priority Order

Based on likelihood and impact:

| Priority | Hypothesis | Fix Approach | Status |
|----------|-----------|--------------|--------|
| **ROOT** | **H8: Missing PhysicsWorldIndex** | Added `PhysicsWorldIndex` to terrain chunks in `TerrainChunkColliderBuildSystem.ApplyCollider`; tuned safety system thresholds | **FIXED — VALIDATED** |
| 1 | H1: Collider not ready | Raised spawn height to Y=20 so colliders build before player lands; added `PlayerTerrainSafetySystem` as safety net | **FIXED** (mitigating, not root cause) |
| 2 | H2: Ground plane | Removed bootstrap ground plane entirely — terrain mesh colliders are the sole physics surface | **FIXED** (contributing factor, not root cause) |
| 3 | H3: Raycast origin | Adjust raycast to cast from capsule center, use SphereCast instead of RayCast | Open |
| 4 | H4: Degenerate mesh | Complete banding fix validation (Phase 15), add degenerate triangle filtering | Open |
| 5 | H7: ECB frame gap | Use direct EntityManager for PhysicsCollider if ECB delay confirmed | Open |
| 6 | H5: Tunneling | Add velocity cap or CCD | Open |
| 7 | H6: Vertical coverage | Increase chunk resolution if needed | Open |

---

## 7. Key Files

| File | Role | Lines of Interest |
|------|------|------------------|
| PlayerEntityBootstrap.cs | Player spawn (ground plane removed) | 20 (PlayerStartHeight=20), 93 (spawn position), 127-145 (capsule) |
| PlayerTerrainSafetySystem.cs | Physics-based tunneling safety net | Raycasts previous→current position, snaps back on hit |
| PlayerComponents.cs | Player ECS components | 38 (PreviousPosition field on PlayerMovementState) |
| PlayerGroundingSystem.cs | Ground detection raycast | 44-55 (raycast setup), 57-66 (state update) |
| PlayerMovementSystem.cs | Velocity application | 72-77 (ground movement), 103-105 (velocity write) |
| TerrainChunkColliderBuildSystem.cs | Collider lifecycle + PhysicsWorldIndex | 18 (rate limit), 67-110 (build loop), 139 (MeshCollider.Create), 166-169 (PhysicsWorldIndex fix) |
| TerrainChunkStreamingSystem.cs | Chunk spawn/despawn | 141-166 (spawn), 169-202 (despawn) |
| TerrainChunkDensitySamplingSystem.cs | Density sampling | 68-73 (ghost layer), 83-93 (sampling job) |
| TerrainChunkMeshBuildSystem.cs | Surface Nets meshing | Triggers collider build tag |
| SDFMath.cs | Terrain height formula | 26-32 (SdGround) |
| SDFTerrainFieldSettings.cs | SDF parameters singleton | Used by density sampling |
| TerrainBootstrapAuthoring.cs | Default SDF parameters | 25-28 (defaults), 70-119 (chunk grid) |
| SurfaceNets.cs | Mesh algorithm | Degenerate triangle source |

---

## 8. Implementation Status

**Implemented:** 2026-02-15
**Scope:** Phases 0-3 diagnostic instrumentation. Phase 6 mitigations for H1+H2. Phase 7 root cause fix (H8). Phase 8 physics-based safety system. Phases 4-5 deferred.

### Phase 0: Instrumentation — COMPLETE

- [x] Created `PlayerFallThroughDiagnosticSystem` (`Assets/Scripts/DOTS/Terrain/Debug/PlayerFallThroughDiagnosticSystem.cs`)
  - `[DisableAutoCreation]`, `[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]`
  - Tracks player position, velocity.y, IsGrounded, FallTime per frame
  - Derives player chunk coordinate from position / chunkStride
  - Scans 3x3 chunk grid: checks `PhysicsCollider`, `TerrainChunkNeedsColliderBuild`, `TerrainChunkMeshData`, `TerrainChunkNeedsDensityRebuild` per neighbor
  - Trigger: unexpected ungrounding (`prevIsGrounded && !IsGrounded && velocity.y <= 0`) or player below analytical SDF surface (`SDFMath.SdGround < 0`)
  - Tunneling risk warning when `|velocity.y| * deltaTime > voxelSize`
  - Rate-limited: max 1 snapshot per 30 frames
- [x] Created `FallThroughDiagnosticComponents` (`Assets/Scripts/DOTS/Terrain/Debug/FallThroughDiagnosticComponents.cs`)
  - `TerrainChunkSpawnTimestamp` — records spawn elapsed time and frame count
  - `TerrainChunkColliderDiagnostics` — placeholder for Phase 4 mesh quality stats
- [x] Added `EnableFallThroughDebug` flag to `DebugSettings` (default `false`)
- [x] Added `LogFallThrough()` and `LogFallThroughWarning()` methods with `[DOTS-FallThrough]` prefix
- [x] Added `EnablePlayerFallThroughDiagnosticSystem` to `ProjectFeatureConfig`
- [x] Added bootstrap wiring in `DotsSystemBootstrap`

### Phase 1: Collider Timing (H1) — COMPLETE

- [x] Modified `TerrainChunkStreamingSystem` to add `TerrainChunkSpawnTimestamp` on chunk spawn when `EnableFallThroughDebug` is true
- [x] Created `TerrainColliderTimingSystem` (`Assets/Scripts/DOTS/Terrain/Debug/TerrainColliderTimingSystem.cs`)
  - `[DisableAutoCreation]`, `[UpdateAfter(typeof(TerrainChunkColliderBuildSystem))]`
  - Queries chunks with `TerrainChunkSpawnTimestamp` + `PhysicsCollider` (collider just built)
  - Logs elapsed time and frame count since spawn
  - Removes timestamp component after logging via ECB
- [x] Added `EnableTerrainColliderTimingSystem` to `ProjectFeatureConfig`
- [x] Added bootstrap wiring in `DotsSystemBootstrap`

### Phase 2: Ground Plane Guard (H2) — SUPERSEDED BY PHASE 6

- [x] ~~Added `DisableBootstrapGroundPlane` flag to `DebugSettings` (default `false`)~~
- [x] ~~Wrapped `CreateGroundPlane(ref state)` in `PlayerEntityBootstrap.OnUpdate` with guard~~
- Ground plane permanently removed in Phase 6. `DisableBootstrapGroundPlane` flag remains in `DebugSettings` but is unused.

### Phase 3: Raycast Diagnostics (H3) — COMPLETE

- [x] Modified `PlayerGroundingSystem.OnUpdate` with conditional debug branch
  - When `EnableFallThroughDebug` is true: uses `CastRay(rayInput, out RaycastHit closestHit)` overload
  - Logs grounding state transitions with hit position, surface normal, fraction, and fallTime
  - When false: uses original `bool`-only overload (zero overhead)
  - Safe: `OnUpdate` is not `[BurstCompile]`, managed `DebugSettings` access is valid

### Phase 4: Mesh Quality (H4) — NOT STARTED

- [ ] Add triangle quality analysis pass in `TerrainChunkColliderBuildSystem`
- [ ] Populate `TerrainChunkColliderDiagnostics` component (struct already created)
- [ ] Log mesh stats for player-occupied chunk on fall-through detection

### Phase 5: Tunneling (H5) — PARTIAL

- [x] Tunneling risk detection integrated into `PlayerFallThroughDiagnosticSystem` (warning when `|velocity.y| * dt > voxelSize`)
- [ ] Velocity cap or CCD implementation
- [ ] Speculative contacts evaluation

### Phase 6: Mitigations for H1+H2 — COMPLETE (helpful but not root cause)

Three mitigations applied before root cause was identified. These remain in place as defense-in-depth:

- [x] **Removed ground plane** — Deleted `CreateGroundPlane` method and call from `PlayerEntityBootstrap`. Terrain mesh colliders are the sole physics surface. The ground plane at Y=0 intersected the terrain range [-4, +4] and trapped the player.
- [x] **Raised spawn height** — Changed `PlayerStartHeight` from `5f` to `20f` and consolidated all spawn position references to use the constant. The player now falls from Y=20, giving terrain colliders ample time to build before the player reaches the surface.
- [x] **Added safety teleport** — Created `PlayerTerrainSafetySystem` (originally analytical formula-based, replaced in Phase 8 with physics-based approach)

### Phase 7: Root Cause Fix (H8: Missing PhysicsWorldIndex) — COMPLETE, VALIDATED

**Root cause:** Terrain chunk entities had `PhysicsCollider` attached by `TerrainChunkColliderBuildSystem` but never received `PhysicsWorldIndex`. Without this shared component, Unity Physics broadphase ignores the entity entirely. Colliders existed but were invisible to the physics solver.

- [x] **Added `PhysicsWorldIndex` to terrain chunks** — Modified `TerrainChunkColliderBuildSystem.ApplyCollider` to add `PhysicsWorldIndex` (default world 0) via ECB when the entity doesn't already have one. This registers the terrain collider in the same physics world as the player.
- [x] **Tuned safety system thresholds** — (superseded by Phase 8 physics-based rewrite)
- [x] **Runtime validated** — Player lands on terrain from Y=20 spawn, walks across all terrain heights without fall-through, safety teleport does not fire during normal gameplay.

### Phase 8: Physics-Based Safety System — COMPLETE

Replaced the analytical-formula safety system with a geometry-agnostic physics raycast approach.
The original Phase 6/7 safety system compared the player's Y against the analytical `SdGround` height
formula, which only works for the top terrain surface. It would incorrectly teleport the player out of
dungeons, caves, or any SDF carve-out below the surface.

- [x] **Rewrote `PlayerTerrainSafetySystem`** — Now raycasts from the player's previous position to
  current position each frame using `PhysicsWorldSingleton`. If the ray hits a collider between
  the two points, the player tunneled through a surface and is snapped back to the previous
  known-good position.
  - Works for any collidable geometry (terrain, dungeons, caves, SDF carve-outs)
  - No dependency on `SDFTerrainFieldSettings` or analytical formulas
  - 0.5s cooldown + minimum displacement threshold (`0.01` sq units) to avoid false triggers
  - Logs snap-backs via `DebugSettings.LogFallThrough()` with hit fraction detail
- [x] **Added `PreviousPosition` field to `PlayerMovementState`** (`PlayerComponents.cs`)
  - Initialized to spawn position in `PlayerEntityBootstrap`
  - Updated every frame by the safety system
- [x] **Updated `PlayerEntityBootstrap`** — Moved `spawnPos` declaration earlier so
  `PreviousPosition` is initialized correctly at spawn time

### Files Changed

| Action | File | Phase |
|--------|------|-------|
| Modified | `Assets/Scripts/DOTS/Core/DebugSettings.cs` | 0, 2 |
| Modified | `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs` | 2, 6 |
| Created | `Assets/Scripts/DOTS/Terrain/Debug/FallThroughDiagnosticComponents.cs` | 0, 1 |
| Created | `Assets/Scripts/DOTS/Terrain/Debug/PlayerFallThroughDiagnosticSystem.cs` | 0 |
| Modified | `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs` | 0, 1, 6 |
| Modified | `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs` | 0, 1, 6 |
| Modified | `Assets/Scripts/Player/Systems/PlayerGroundingSystem.cs` | 3 |
| Modified | `Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs` | 1 |
| Created | `Assets/Scripts/DOTS/Terrain/Debug/TerrainColliderTimingSystem.cs` | 1 |
| Created | `Assets/Scripts/Player/Systems/PlayerTerrainSafetySystem.cs` | 6, 7, 8 |
| Modified | `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs` | 7 |
| Modified | `Assets/Scripts/Player/Components/PlayerComponents.cs` | 8 |

### Activation Steps

**Diagnostics (Phases 0-3):**
1. In `ProjectFeatureConfig` asset, enable `EnablePlayerFallThroughDiagnosticSystem` and/or `EnableTerrainColliderTimingSystem`
2. Set `DebugSettings.EnableFallThroughDebug = true` (via code or editor script)
3. Enter Play mode, move player across terrain (especially uphill near chunk boundaries)
4. Check Console for `[DOTS-FallThrough]` log messages
5. Collider timing logs show frame counts between chunk spawn and collider completion

**Fixes (Phases 6-8) — active by default:**
1. Ground plane is removed; no action needed
2. Player spawns at Y=20 and falls onto terrain after colliders build
3. `PlayerTerrainSafetySystem` is enabled by default (`EnablePlayerTerrainSafetySystem = true` in config)
4. Safety system raycasts previous→current position; snaps back on tunneling detection
5. Works for all geometry (terrain, dungeons, caves) — no analytical formula dependency
6. Snap-back logs are visible when `DebugSettings.EnableFallThroughDebug = true`

---

## 9. Success Criteria

- [x] Root cause identified: missing `PhysicsWorldIndex` on terrain chunk entities (H8)
- [x] Fall-through no longer reproducible after fix — **runtime validated 2026-02-15**
- [x] Player can walk across all terrain heights without falling through — **runtime validated 2026-02-15**
- [ ] No regression in terrain streaming performance
- [ ] Automated test added that validates collider coverage for player-occupied chunks
