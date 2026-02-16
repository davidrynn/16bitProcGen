# Known Issues

**Last Updated:** 2026-02-15

Master tracker for active bugs, investigations, and resolved issues. Link to detailed specs rather than duplicating analysis here.

---

## Open Issues

### BUG-001: PlayerTerrainSafetySystem causes player bouncing

**Status:** OPEN — system disabled as workaround
**Severity:** Medium
**Affected Systems:** `PlayerTerrainSafetySystem`
**Spec:** [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md) (Phase 8)

**Symptom:** Player capsule visibly bounces/jitters in place. Disabling `EnablePlayerTerrainSafetySystem` in `ProjectFeatureConfig` eliminates the bounce.

**Root Cause (suspected):** The safety system raycasts from `previousPosition` to `currentPosition` to detect tunneling. Even with the ray offset to capsule center height (+1.0 Y) and guards for grounded state / fall time / downward displacement, the ray still triggers false-positive hits against the terrain the player is near. The exact mechanism of the false positive needs further investigation with diagnostic logging.

**Workaround:** Set `EnablePlayerTerrainSafetySystem = false` in `ProjectFeatureConfig` asset. This disables tunneling protection entirely.

**Next Steps:**
- Run with `DebugSettings.EnableFallThroughDebug = true` and inspect `[DOTS-FallThrough]` console logs to confirm whether snap-backs are firing
- If snap-backs aren't firing, the bounce has a different cause (physics solver oscillation, grounding flicker, etc.)
- Consider replacing prev-to-current raycast with a downward probe from current position to detect "below terrain" state instead

**Files:**
- `Assets/Scripts/Player/Systems/PlayerTerrainSafetySystem.cs`
- `Assets/ScriptableObjects/ProjectFeatureConfig.asset` (toggle)

---

### BUG-002: Player visual cylinder embedded halfway in terrain

**Status:** OPEN — needs investigation
**Severity:** Low-Medium
**Affected Systems:** `PlayerEntityBootstrap`, `PlayerVisualSync`, `PlayerGroundingSystem`

**Symptom:** The player's visual capsule (`Player Visual (ECS Synced)`) appears to sit halfway inside the terrain surface rather than resting on top of it.

**Likely Cause:** The visual GameObject is synced to the ECS entity's `LocalTransform.Position`, which is the entity origin (bottom of the physics capsule). The visual capsule (`PrimitiveType.Capsule`, scale `(1, 2, 1)`) has its pivot at center, so it renders centered on the entity origin — placing its lower half below the terrain surface. The physics capsule (Vertex0=`(0,0.5,0)`, Vertex1=`(0,1.5,0)`, R=0.5) correctly sits on terrain, but the visual doesn't account for this offset.

**Possible Fixes:**
- Offset the visual sync position upward by ~1.0 (half the visual capsule height) in `PlayerVisualSync`
- Or adjust the visual capsule's local position so its bottom aligns with entity origin
- May also relate to BUG-001 if the entity position itself is slightly below the terrain surface

**Files:**
- `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs` — `CreatePlayerVisual()` (line ~216)
- `Assets/Scripts/Player/Bootstrap/PlayerVisualSync.cs` — sync logic

---

## Resolved Issues

### BUG-R001: Player falls through terrain

**Status:** FIXED (2026-02-15)
**Severity:** Critical
**Spec:** [PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md](AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md)

**Root Cause:** Terrain chunk entities had `PhysicsCollider` but no `PhysicsWorldIndex`, making them invisible to the physics broadphase. Fixed by adding `PhysicsWorldIndex` in `TerrainChunkColliderBuildSystem.ApplyCollider`.

**Additional Mitigations Applied:**
- Removed bootstrap ground plane (intersected terrain range)
- Raised player spawn height to Y=20
- Added `PlayerTerrainSafetySystem` (currently disabled due to BUG-001)

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
