# Terrain Edit Player Safety + Deterministic Local Grid Spec

**Date:** 2026-04-08
**Status:** PROPOSED
**Owner:** Terrain / Player DOTS

---

## 1. Problem Statement

Two gameplay defects were reported during runtime terrain modification:

1. **Player overlap on add edits:** additive terrain can appear through/around the player capsule, burying the player and leading to unstable recovery/fall-through outcomes.
2. **Deterministic snapping requirement:** terrain edits must remain snapped to deterministic local coordinates (chunk-local lattice). Any fix must preserve that behavior.

This spec defines a player-safety edit gate while preserving snapped cube, chunk-local deterministic placement.

---

## 2. Decisions

1. **Do not move the player automatically.**
2. **Reject unsafe add edits before enqueue** when the edit volume intersects the player safety volume.
3. **Keep snapped cube workflow** and deterministic local-grid snapping as the primary edit mode.
4. **No auto-aim correction (nudge) in current scope; use block-only overlap handling.**
5. **No fallback to non-deterministic placement** when local snap is required.

---

## 3. Goals

1. Prevent additive terrain from being applied inside/through the player capsule.
2. Preserve deterministic local-grid edit placement (same input state -> same snapped coordinates).
3. Provide immediate player feedback on blocked edits.
4. Keep DOTS-first architecture and Burst-safe runtime data paths.

---

## 4. Non-Goals

1. No change to Surface Nets meshing algorithm.
2. No change to SDF add/subtract math semantics.
3. No conversion from snapped cube to free-sphere editing.
4. No automatic player teleport/push to make edits succeed.

---

## 5. Functional Requirements

### FR-1: Pre-enqueue Player Overlap Guard

In `TerrainEditInputSystem`, evaluate safety **before** calling `editBuffer.Add(edit)`.

- Applies to `SDFEditOperation.Add`.
- If edit intersects player safety volume -> block.

### FR-2: Player Safety Volume

Use runtime player transform + known capsule profile from player bootstrap:

- Capsule segment endpoints: `(0, 0.5, 0)` and `(0, 1.5, 0)` relative to player origin.
- Capsule radius: `0.5`.
- Add configurable clearance margin (`PlayerEditClearance`) to avoid near-contact edge cases.

### FR-3: Deterministic Local Grid Is Preserved

Edits remain snapped to local lattice coordinates.

- Placement mode stays `SnappedCube`.
- Snap space is `ChunkLocal` by default.
- Optional lock prevents runtime snap-space switching away from local.
- No random or frame-order-dependent offsets.

### FR-4: No Auto-Nudge (Current Scope)

If overlap is detected, cancel the add edit.

No deterministic auto-nudge or candidate relocation is in scope for this iteration.

### FR-5: Feedback

Every blocked edit emits feedback:

- Blocked: reason `BlockedByPlayerOverlap`.
- Optional UI reaction: reticle color pulse/flash.
- Always available debug path via `DebugSettings.LogTerrainEditWarning`.

---

## 6. Determinism Rules

To satisfy local-grid determinism:

1. Candidate edit centers must always be generated from lattice math (`SnapToChunkLocalLattice`).
2. Overlap-guard rejection must not mutate the snapped center to another cell.
3. If owning chunk cannot be resolved while local lock is active, cancel edit with feedback (do not fallback to global snap).

---

## 7. Proposed Data Model Updates

## 7.1 TerrainEditSettings

Add fields:

- `bool EnablePlayerOverlapGuard` (default `true`)
- `float PlayerEditClearance` (default `0.15f`, clamp `>= 0`)
- `bool LockChunkLocalSnap` (default `true`)

Existing fields retained:

- `PlacementMode` (default `SnappedCube`)
- `SnapSpace` (set default to `ChunkLocal`)
- `EditCellFraction`
- `GlobalSnapAnchor`
- `CubeDepthCells`

## 7.2 Feedback Event

Add lightweight ECS feedback payload (singleton dynamic buffer or component event), e.g.:

- `TerrainEditFeedbackType` enum:
  - `Applied`
  - `BlockedByPlayerOverlap`
  - `BlockedNoOwningChunk`
- metadata: `HitPosition`, `FinalPosition`, timestamp/frame.

---

## 8. System Changes

### 8.1 TerrainEditInputSystem (Primary)

Add a safety gate pipeline:

1. Build snapped candidate edit as today.
2. Resolve player entity/entities with `PlayerTag` + `LocalTransform`.
3. Perform overlap test for candidate edit shape against player capsule + clearance.
4. If unsafe, cancel and emit feedback.
5. Only safe edits are enqueued and can mark chunks dirty.

### 8.2 ProjectFeatureConfig + Bootstrap

Expose and propagate new terrain-edit safety settings to `TerrainEditSettings` singleton via `DotsSystemBootstrap`.

### 8.3 ReticleBootstrap (Optional UI Phase)

Consume feedback events and provide visual confirmation:

- blocked -> red pulse
- applied -> normal/white

If UI integration is deferred, console/debug feedback still fulfills requirement.

---

## 9. File Impact Plan

Planned file updates:

1. `Assets/Scripts/DOTS/Terrain/SDF/TerrainEditSettings.cs`
2. `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs`
3. `Assets/ScriptableObjects/ProjectFeatureConfig.asset`
4. `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`
5. `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainEditInputSystem.cs`
6. `Assets/Scripts/DOTS/Terrain/SDF/TerrainEditFeedback.cs` (new)
7. `Assets/Scripts/Player/Bootstrap/ReticleBootstrap.cs` (optional feedback phase)

No planned changes required in `PlayerGroundingSystem` for this feature.

---

## 10. Validation Plan

## 10.1 Automated Tests

Add/extend PlayMode tests to verify:

1. Add edit overlapping player is rejected when guard enabled.
2. Safe add edit is accepted and enqueued.
3. With local-lock enabled, missing owning chunk causes cancel (no global fallback).
4. Determinism: repeated identical input state yields identical final edit center.

## 10.2 Manual Runtime Checks

1. Stand still, place add edit at feet/body -> blocked feedback, no terrain spawn through player.
2. Verify edit center debug logs remain chunk-local snapped coordinates.
3. Verify no auto-movement/teleport of player during blocked edits.

---

## 11. Rollout Phases

### Phase 1 (Safety Baseline)

- Implement overlap guard + block behavior.
- Emit debug feedback.

### Phase 2 (UX Polish)

- Reticle feedback integration.
- Minor tuning of clearance.

### Phase 3 (Future, Optional)

- Reevaluate deterministic auto-nudge only if blocking proves too restrictive in playtests.

---

## 12. Acceptance Criteria

1. Player is no longer buried by additive terrain edits at/inside capsule location.
2. Edits remain snapped to deterministic local grid coordinates.
3. No automatic player displacement is used to resolve edit conflicts.
4. Blocked edits provide immediate feedback.
5. Existing snapped-cube workflow remains default and functional.
