# Relic Render Refactor Spec
_Status: ARCHIVED (2026-07-02) — IMPLEMENTED; superseded by `RELIC_LOD_IMPOSTOR_SPEC.md` §8. Moved from `Assets/Docs/AI/STRUCTURE PLACEMENT/` to `Archives/StructurePlacement_2026/` (doc cleanup batch D4)._
_Superseded By: [RELIC_LOD_IMPOSTOR_SPEC.md](../../AI/STRUCTURE%20PLACEMENT/RELIC_LOD_IMPOSTOR_SPEC.md)_
_Last updated: 2026-04-16_
_Owner: Structures / Rendering_
_Supersedes: MVP Step 3 rendering approach in STRUCTURE_PLACEMENT_PLAN.md_

---

## 1. Purpose

Replace the batch-instanced `Graphics.RenderMeshInstanced` relic rendering path with per-entity Entities Graphics rendering. The current batch API uses a single `worldBounds` AABB for all instances, which causes incorrect frustum culling on large-scale meshes — the root cause of the "globe eating" visual artifact at distance.

---

## 2. Problem

### 2.1 Observed Artifact

At moderate-to-far camera distance, the relic mesh partially disappears in a spherical clipping pattern. Rotating the camera slightly can restore the full mesh. The artifact is angle-dependent and distance-dependent.

### 2.2 Root Cause

`RelicRenderSystem` uses `Graphics.RenderMeshInstanced` with a single `worldBounds` for the entire batch. URP converts this AABB to a bounding sphere for quick frustum rejection. For a 15x-scaled mesh (~30 unit diameter), the bounding sphere straddles the camera frustum boundary at distance. The SRP culls the entire draw call even though mesh geometry is still visible. Camera rotation shifts the sphere relative to the frustum, causing the mesh to pop back.

### 2.3 Why This Is Architectural, Not a Tunable

Padding the `worldBounds` would mask the symptom for specific scales but breaks again at different distances or with multiple relics at different positions. The batch API is designed for many small instances (grass, rocks, trees) — not a few large landmarks. The correct fix is per-entity rendering with per-entity bounds.

### 2.4 Additional Issues in Current Implementation

1. **Missing `enableInstancing`** — Tree and rock render systems force `material.enableInstancing = true`; the relic system does not, risking undefined behavior.
2. **Single submesh only** — `RenderMeshInstanced(rp, mesh, 0, ...)` renders submesh 0 only. If the FBX has multiple submeshes, geometry on submeshes 1+ is silently dropped.
3. **Runtime `_Cull` hack** — The system forces `_Cull = 0` at runtime to work around single-sided FBX geometry. While functional, this couples the render system to shader internals and is fragile across material/shader changes.
4. **No distance management** — Relics render at full detail from any distance with no LOD, impostor, or distance culling.

---

## 3. Scope

### In Scope

- Replace `RenderMeshInstanced` batch path with per-entity Entities Graphics rendering
- Spawn one ECS entity per accepted relic anchor with correct render components
- Per-entity `RenderBounds` for correct individual frustum culling
- Track spawned entities via `StructureRealizedTag.StableAnchorId` for lifecycle management
- Clean up realized entities when anchors are removed or re-planned
- Preserve existing anchor planning pipeline unchanged

### Out of Scope

- LOD or impostor system (future work; this refactor provides the entity hook)
- Multi-mesh relic variants (single mesh per relic family for MVP)
- Dungeon or village rendering changes
- FBX re-authoring (double-sided geometry is a separate asset task)

---

## 4. Related Docs

- [STRUCTURE_PLACEMENT_PLAN.md](STRUCTURE_PLACEMENT_PLAN.md) — MVP Step 3 (updated to reference this spec)
- [STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md) — section 12.5.2 Relic Realizer
- [../../KNOWN_ISSUES.md](../../KNOWN_ISSUES.md) — BUG-016 relic frustum culling artifact
- [../HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md](../HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) — future far-distance rendering (complementary)

---

## 5. Design

### 5.1 Architecture Change

**Before:** Anchor buffer → `RelicRenderSystem.OnUpdate` collects matrices → `Graphics.RenderMeshInstanced` batch draw per camera

**After:** Anchor buffer → `RelicRealizationSystem.OnUpdate` spawns/syncs ECS entities → Entities Graphics renders each entity with per-entity bounds via SRP Batcher

### 5.2 Entity Structure Per Relic

Each realized relic entity will carry:

| Component | Source | Purpose |
|-----------|--------|---------|
| `LocalTransform` | Anchor position + rotation + scale | World placement |
| `LocalToWorld` | Computed by `LocalToWorldSystem` | Render matrix |
| `RenderMeshArray` | Managed singleton via `RelicRenderConfig` | Mesh + material reference |
| `MaterialMeshInfo` | Set at spawn | Indexes into `RenderMeshArray` |
| `RenderBounds` | Computed from mesh local bounds | Per-entity frustum culling |
| `RenderFilterSettings` | Set at spawn | Layer, shadow, motion vector settings |
| `StructureRealizedTag` | `StableAnchorId` from anchor | Lifecycle tracking / cleanup |

### 5.3 System Responsibilities

**`RelicRealizationSystem` (new, replaces `RelicRenderSystem`)**

- Runs in `SimulationSystemGroup`, after `StructureAnchorPlanningSystem`
- On each update, compares anchor buffer against existing realized entities (by `StableAnchorId`)
- Spawns new entities for unmatched anchors
- Destroys entities whose anchors were removed
- Does NOT run every frame after initial sync — uses a version check or reactive tag

**`RelicRenderConfig` (updated)**

- Continues to hold `Mesh` and `Material` references
- Bootstrap registers the `RenderMeshArray` shared component once at startup
- No longer needs the `_Cull` hack (material should be authored correctly, or set once at bootstrap)

**`RelicVisualBootstrap` (updated)**

- Registers `RelicRenderConfig` as before
- Ensures `material.enableInstancing = true` for SRP Batcher compatibility
- Validates `mesh.subMeshCount` and logs warning if > 1

### 5.4 Lifecycle

```
StructureAnchorPlanningSystem writes anchor buffer
    ↓
RelicRealizationSystem detects new/removed anchors
    ↓  (spawn)                    ↓  (destroy)
    Create entity with            Destroy entity,
    render components +           remove StructureRealizedTag
    StructureRealizedTag
    ↓
Entities Graphics renders via SRP Batcher (automatic, per-entity bounds)
```

### 5.5 Cleanup and Re-planning

When `StructureAnchorPlanningSystem` re-plans (generation version change), `RelicRealizationSystem` must:

1. Query all entities with `StructureRealizedTag` where `Family == Relic`
2. Destroy any whose `StableAnchorId` no longer appears in the anchor buffer
3. Spawn new entities for newly accepted anchors
4. Leave existing entities unchanged if their anchor persists

### 5.6 RenderBounds Computation

`RenderBounds` must use the mesh's local-space AABB (from `mesh.bounds`) so Unity's culling system can transform it per-entity via `LocalToWorld`. This is the correct per-entity frustum culling that the batch `worldBounds` approach could not provide.

```csharp
var renderBounds = new RenderBounds
{
    Value = new AABB
    {
        Center = mesh.bounds.center,
        Extents = mesh.bounds.extents,
    }
};
```

Unity transforms this by `LocalToWorld` automatically to get the world-space culling volume.

---

## 6. Files Changed

| File | Action | Notes |
|------|--------|-------|
| `RelicRenderSystem.cs` | **Delete** | Replaced entirely |
| `RelicRealizationSystem.cs` | **Create** | New per-entity spawn/sync system |
| `RelicRenderConfig.cs` | **Update** | Add `RenderMeshArray` caching, remove `_Cull` responsibility |
| `RelicVisualBootstrap.cs` | **Update** | Add instancing validation, submesh warning |
| `DotsSystemBootstrap.cs` | **Update** | Replace `RelicRenderSystem` with `RelicRealizationSystem` |
| `ProjectFeatureConfig.cs` | **Update** | Rename flag if needed (`EnableRelicRenderSystem` → `EnableRelicRealizationSystem` or keep) |

---

## 7. Migration Notes

- The `beginCameraRendering` callback pattern is no longer needed (Entities Graphics handles SRP integration internally)
- The `SurfaceScatterRenderBoundsUtility` is no longer called by relic rendering (still used by trees/rocks)
- The `_Cull = 0` runtime fix is removed; the material should be authored with "Render Face: Both" in the inspector (already the case per user confirmation)
- `StructureRealizedTag` already exists and carries `StableAnchorId` — reuse it directly

---

## 8. Follow-up: LOD / Impostor — IMPLEMENTED

This refactor created the extension point for distance-based rendering; the detailed design and implementation live in [RELIC_LOD_IMPOSTOR_SPEC.md](RELIC_LOD_IMPOSTOR_SPEC.md) (supersedes this section).

**Context.** After this refactor landed, a residual artifact remained at distance: large relics still appeared fragmented, with a curved "edge" slicing through them as the camera rotated. Diagnostics ruled out both submesh rendering (`subMeshCount = 1`) and per-entity frustum culling (effectively-infinite `RenderBounds` did not change the behavior). The actual cause is camera **far-plane clipping**: when a relic's world-space bounding radius exceeds `farClipPlane − cameraDistance`, the GPU rasterizer clips individual triangles against the far plane. This is fundamental projection behavior and cannot be solved upstream of the shader.

The LOD / impostor spec resolves this by swapping to a small impostor mesh whose world extent always fits inside the frustum depth. See that spec for design, swap-distance math, and acceptance criteria.

**Implemented:** `RelicLodSelectionSystem` (PresentationSystemGroup) performs per-frame distance-based swaps against a two-entry `RenderMeshArray`, gated by `ProjectFeatureConfig.EnableRelicLodSelectionSystem`. Swap distance and hysteresis are auto-derived by `RelicVisualBootstrap` from camera far clip and mesh extents when the inspector fields are left at 0.

---

## 9. Test Plan

### 9.1 EditMode Tests

- Verify `RelicRealizationSystem` spawns correct number of entities from anchor buffer
- Verify entities have `RenderBounds`, `LocalTransform`, `StructureRealizedTag`
- Verify cleanup: remove an anchor → entity destroyed
- Verify re-plan: generation version change → stale entities removed, new ones spawned

### 9.2 PlayMode Validation

- Enter Play Mode → relics visible at anchor positions (same as before)
- Orbit camera around relic at distance → no partial disappearance (the bug fix)
- Move far from relic → still visible until beyond camera far plane (no premature culling)
- Multiple relics → each individually culled when off-screen, not as a batch

### 9.3 Regression

- Existing `StructureAnchorPlanningTests` and `StructurePlacementComponentTests` must continue to pass
- Tree/rock rendering unaffected (still uses `RenderMeshInstanced` batch path)

---

## 10. Acceptance Criteria

- [x] Relic entities rendered via Entities Graphics with per-entity `RenderBounds`
- [x] "Globe eating" frustum culling artifact no longer reproducible
- [x] Realized entities tracked by `StructureRealizedTag.StableAnchorId`
- [x] Anchor removal cleans up realized entities
- [x] No `Graphics.RenderMeshInstanced` calls for relics
- [x] Existing anchor planning tests pass
- [x] Material `enableInstancing` validated at bootstrap
