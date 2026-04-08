# DOTS Terrain LOD Implementation Checklist

Date: 2026-04-07
Source of truth: Assets/Docs/DOTS_Terrain_LOD_SPEC.md (revised)
Scope: SDF + Surface Nets DOTS terrain path

## Status Legend

- [x] Implemented and aligned
- [~] Implemented but not fully aligned with revised spec
- [ ] Not implemented

---

## 1. Data Model Alignment

- [x] `TerrainChunkLodState` exists with `CurrentLod`, `TargetLod`, `LastSwitchFrame`
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodState.cs`
- [x] `TerrainLodSettings` singleton exists and is created by bootstrap
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
  - File: `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`
- [x] `TerrainChunkLodDirty` exists and is set on applied LOD changes
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodDirty.cs`
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs`
- [x] Add `Lod2MaxDist` to support explicit Ring 3 selection logic
  - Target file: `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
- [x] Add `ShadowMaxLod` policy gate
  - Target file: `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
- [x] Add `UseStreamingAsCullBoundary` option for LOD3 semantics
  - Target file: `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`

---

## 2. LOD Selection and Apply

- [x] LOD selection system exists and uses Chebyshev distance
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
- [x] Hysteresis implemented (promotion immediate, demotion delayed)
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
- [x] LOD apply system updates `TerrainChunkGridInfo` and adds density rebuild tag
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs`
- [x] Selection currently resolves to LOD0/1/2/3 with optional streaming-boundary clamp
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
- [x] Implement explicit LOD3 assignment for `dist > Lod2MaxDist`
  - Target file: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
- [x] Define and enforce LOD3 behavior when chunk remains loaded (if not despawned)
  - Target files:
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs`
    - `Assets/Scripts/DOTS/Terrain/Streaming/TerrainChunkStreamingSystem.cs`

---

## 3. System Ordering (Critical)

Revised spec requires:
PlayerMovement -> Streaming -> LOD Selection -> LOD Apply -> Density -> Mesh Build -> Upload -> Collider -> Detail

- [~] LOD selection/apply order relative to each other is correct
  - Files:
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs`
- [~] LOD systems run after streaming via attribute
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
- [x] Enforce `TerrainChunkLodApplySystem` before `TerrainChunkDensitySamplingSystem`
  - Target file: `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs`
  - Add `[UpdateAfter(typeof(DOTS.Terrain.LOD.TerrainChunkLodApplySystem))]`
- [x] Ensure bootstrap/system registration order does not violate required sequence
  - Target file: `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`
- [x] Add startup player readiness gate to prevent pre-collider fall-through on initial frames
  - Files:
    - `Assets/Scripts/Player/Components/PlayerComponents.cs`
    - `Assets/Scripts/Player/Systems/PlayerStartupReadinessSystem.cs`
    - `Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs`
    - `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`
- [ ] Confirm player position source for LOD is updated after movement each frame
  - Target files:
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
    - `Assets/Scripts/Player/Systems/*`

---

## 4. Chunk Footprint Invariance

Revised spec requires constant world footprint across LODs:
`(Resolution.x - 1) * VoxelSize` and `(Resolution.z - 1) * VoxelSize` must match LOD0.

- [x] Current defaults are footprint-invariant across LOD0/1/2
  - File: `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
- [x] Update LOD defaults to invariant combinations (example: 32/1, 16/~2.0667, 8/~4.4286) or choose integer-friendly equivalent with explicit span lock
  - Target file: `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
- [x] Add runtime validation warning when footprint mismatch is detected
  - Target files:
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`

---

## 5. Collider / Detail / Shadow Policy

- [x] Collider policy partially implemented via `ColliderMaxLod`
  - File: `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs`
- [x] Grass/detail gating by `GrassMaxLod`
  - Target file: `Assets/Scripts/DOTS/Terrain/Rendering/GrassChunkGenerationSystem.cs`
- [ ] Ensure edit-triggered rebuild path also respects/generates detail rebuild in LOD-valid rings
  - Target files:
    - `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkEditUtility.cs`
    - `Assets/Scripts/DOTS/Terrain/Rendering/GrassChunkGenerationSystem.cs`
- [x] Implement shadow policy by `ShadowMaxLod`
  - Target file: `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs`

---

## 6. Fog Blending

- [ ] Wire LOD boundaries to fog start/end guidance (directly or via weather bridge)
  - Candidate files:
    - `Assets/Scripts/DOTS/Weather/*`
    - `Assets/Scripts/DOTS/Core/Authoring/*`

---

## 7. Rebuild Budgets

- [~] Collider budget exists through `TerrainColliderSettings.MaxCollidersPerFrame`
  - File: `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs`
- [x] Use `TerrainLodSettings.MaxDensityRebuildsPerFrame` in density system
  - Target file: `Assets/Scripts/DOTS/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs`
- [x] Use `TerrainLodSettings.MaxMeshRebuildsPerFrame` in mesh build system
  - Target file: `Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs`
- [ ] Optionally unify collider budget source under `TerrainLodSettings` to keep all LOD budgets in one place
  - Target files:
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs`
    - `Assets/Scripts/DOTS/Terrain/Physics/TerrainChunkColliderBuildSystem.cs`

---

## 8. DOTS Best-Practice Improvements

- [~] LOD systems currently allocate arrays per frame (`ToEntityArray` / `ToComponentDataArray`)
  - Files:
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs`
    - `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs`
- [ ] Refactor selection/apply loops to `IJobEntity` or chunk iteration where feasible
- [ ] Route structural changes through end-simulation ECB singleton where practical
- [ ] Add/consume `TerrainChunkLodDirty` in seam/detail/render policy follow-up systems (currently producer-only)

---

## 9. Test Coverage Checklist

- [x] Unit tests for ring selection + hysteresis exist
  - File: `Assets/Scripts/DOTS/Tests/Automated/TerrainLodTests.cs`
- [x] Add integration test: ordering correctness (`LOD apply` before `density rebuild`)
  - File: `Assets/Scripts/DOTS/Tests/Automated/TerrainLodOrderingTests.cs`
- [ ] Add integration test: LOD3 semantics (explicit or streaming-mapped)
- [ ] Add integration test: collider gating by LOD
- [ ] Add integration test: detail/grass gating by LOD
- [ ] Add integration test: footprint invariance and no seam drift across LOD transitions

---

## 10. Execution Order (Recommended)

1. Ordering fixes (Section 3)
2. LOD3 + settings extensions (Sections 1-2)
3. Policy gates (Section 5)
4. Footprint invariance (Section 4)
5. Budgets and perf (Sections 7-8)
6. Integration tests (Section 9)

This order minimizes regression risk by stabilizing correctness first, then applying feature policy and optimization.
