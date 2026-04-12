# Terrain Surface Scatter Schema
_Status: DESIGN - first ECS/data breakdown for tree-plus-rock surface scatter_
_Last updated: 2026-04-11_
_Owner: Terrain / World Generation DOTS_

---

## 1. Purpose

Define the first concrete ECS data breakdown for `Surface Scatter` by keeping trees as the reference family and adding rocks as the second family.

This schema intentionally favors lean lifecycle reuse over a monolithic all-prop abstraction.

---

## 2. Scope

This schema covers:

- shared lifecycle seams proven by trees and reused for rocks
- first-pass rock family components, buffers, and systems
- minimal bootstrap and LOD integration points needed for parity
- deterministic identity and sparse divergence expectations per family
- test targets for tree-plus-rock coexistence

## 3. Non-Goals

This schema does not cover:

- dense details (grass, flowers, pebbles, clutter fields)
- authored landmarks, structures, ruins, or quest props
- forcing trees and rocks into one shared lifecycle enum
- replacing the current tree runtime in one migration step
- final near-player promotion gameplay for every family

---

## 4. Related Docs

- [TERRAIN_SURFACE_SCATTER_PLAN.md](TERRAIN_SURFACE_SCATTER_PLAN.md) - rollout and sequencing
- [TERRAIN_SURFACE_SCATTER_SPEC.md](TERRAIN_SURFACE_SCATTER_SPEC.md) - runtime contract
- [TERRAIN_TREE_PLACEMENT_SPEC.md](TERRAIN_TREE_PLACEMENT_SPEC.md) - tree behavior and constraints
- [../BIOME_GRASS_STREAMING_MVP_PLAN.md](../BIOME_GRASS_STREAMING_MVP_PLAN.md) - dense details path (separate)
- [../PERSISTENCE_SPEC.md](../PERSISTENCE_SPEC.md) - sparse divergence persistence model

---

## 5. Existing Baseline to Reuse

Current tree runtime already provides the lifecycle pattern Surface Scatter needs:

- deterministic per-chunk placement generation after density sampling
- chunk-scoped placement records with stable local IDs
- sparse delta overlay after deterministic generation
- invalidation on `TerrainChunkNeedsDensityRebuild`
- LOD cull stripping of render-only chunk payload
- managed presentation rendering for far render-only state

Reference implementation files:

- `Assets/Scripts/DOTS/Terrain/Trees/TreePlacementGenerationSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Trees/TreePlacementInvalidationSystem.cs`
- `Assets/Scripts/DOTS/Terrain/Trees/TreePlacementRecord.cs`
- `Assets/Scripts/DOTS/Terrain/Trees/TreeStateDelta.cs`
- `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs`

---

## 6. Schema Strategy (Tree + Rocks)

### 6.1 Reuse Policy

Shared now (lifecycle-level):

- generation timing and chunk query shape
- invalidation trigger (`TerrainChunkNeedsDensityRebuild`)
- stable local identity pattern (candidate-slot derived ID)
- sparse-delta overlay stage after deterministic generation
- LOD cull strip and stream re-entry rebuild behavior

Not shared yet (family-specific):

- placement rule constants and candidate acceptance filters
- state enums and delta semantics
- render asset choices and visual variation policy

### 6.2 Family IDs

Introduce a small family discriminator for indexing and debug only.

```csharp
public enum SurfaceScatterFamilyId : byte
{
    Trees = 0,
    Rocks = 1,
}
```

This enum does not imply one shared runtime buffer for all family states.

---

## 7. ECS Data Breakdown

### 7.1 Keep Existing Tree Data (No Breaking Change)

Keep current tree data model as-is for the first tree-plus-rock phase:

- `TreePlacementRecord : IBufferElementData`
- `ChunkTreePlacementTag : IComponentData`
- `TreeStateDelta : IBufferElementData`
- `TreePersistentIdentity : IComponentData` (promotion hook)

No immediate renames or forced migration to generic record buffers.

### 7.2 Add Rock Family Data (New)

#### `RockPlacementRecord : IBufferElementData`

```csharp
public struct RockPlacementRecord : IBufferElementData
{
    public float3 WorldPosition;
    public float GroundNormalY;
    public float UniformScale;
    public float YawRadians;
    public byte RockTypeId;
    public ushort StableLocalId;
}
```

Notes:

- `StableLocalId` must be derived from deterministic candidate slot identity, not accepted-order index.
- Keep record payload render-focused; gameplay-only state belongs in deltas or promoted entities.

#### `ChunkRockPlacementTag : IComponentData`

```csharp
public struct ChunkRockPlacementTag : IComponentData
{
    public uint GenerationVersion;
}
```

#### `RockStateDelta : IBufferElementData`

```csharp
public struct RockStateDelta : IBufferElementData
{
    public ushort StableLocalId;
    public RockStateStage Stage;
    public uint ModifiedAtTick;
    public uint NextChangeTick;
}

public enum RockStateStage : byte
{
    Intact = 0,
    Cracked = 1,
    Depleted = 2,
}
```

`RockStateDelta` remains family-specific even if tree and rock deltas share storage patterns.

#### `RockPersistentIdentity : IComponentData` (future promotion hook)

```csharp
public struct RockPersistentIdentity : IComponentData
{
    public int3 ChunkCoord;
    public ushort StableLocalId;
    public uint GenerationVersion;
}
```

---

## 8. Rules Data (Minimal First Pass)

Add rock-only rule authoring first, then unify if needed after parity:

```csharp
public struct RockPlacementRuleData
{
    public float MinSpacing;
    public float CandidateJitterRadius;
    public float MinGroundNormalY;
    public float SpawnProbability;
    public float MinScale;
    public float MaxScale;
    public float MinElevation;
    public float MaxElevation;
}
```

First-pass source can be a single ScriptableObject singleton mirrored into unmanaged runtime data.

Do not move tree constants into shared rule assets until rock behavior is stable and overlap is proven.

---

## 9. System Breakdown (First Rock Family)

### 9.1 Generation

`RockPlacementGenerationSystem : ISystem`

- Group: `SimulationSystemGroup`
- Order: `UpdateAfter(TerrainChunkDensitySamplingSystem)`
- Query: `TerrainChunk`, `TerrainChunkBounds`, `TerrainChunkDensity`
- Rebuild when:
  - no `ChunkRockPlacementTag`, or
  - tag `GenerationVersion` differs from `TerrainGenerationContext.GenerationVersion`
- Steps:
  1. Generate deterministic world-space candidates.
  2. Apply slope/elevation/probability/spacing filters.
  3. Write `RockPlacementRecord` buffer.
  4. Overlay `RockStateDelta` buffer when present.
  5. Write/update `ChunkRockPlacementTag`.

### 9.2 Invalidation

`RockPlacementInvalidationSystem : ISystem`

- Group: `SimulationSystemGroup`
- Order: `UpdateAfter(TerrainChunkLodApplySystem)` and `UpdateBefore(TerrainChunkDensitySamplingSystem)`
- Trigger: chunk has `TerrainChunkNeedsDensityRebuild`
- Behavior: remove `RockPlacementRecord` and `ChunkRockPlacementTag` from invalidated chunks.

### 9.3 Rendering

`RockChunkRenderSystem : SystemBase` (managed presentation path, matching current tree MVP style)

- Group: `PresentationSystemGroup`
- Query: `TerrainChunkLodState` + `DynamicBuffer<RockPlacementRecord>`
- Skip `CurrentLod >= 3`
- Submit instanced draws in camera callback path, mirroring tree renderer behavior.

### 9.4 Optional Future Promotion

Family-local promotion remains optional and should not block first rock parity.

If needed later:

- `RockPromotionSystem`
- `RockDemotionSystem`

Use `RockPersistentIdentity` to map promoted entities back to deterministic records.

---

## 10. Integration Points

### 10.1 Bootstrap

In `Assets/Scripts/DOTS/Core/Authoring/ProjectFeatureConfig.cs`, add optional toggles:

- `EnableRockPlacementSystem`
- `EnableRockRenderSystem`

In `Assets/Scripts/DOTS/Core/Authoring/DotsSystemBootstrap.cs`, mirror tree wiring:

- create + add `RockPlacementInvalidationSystem` to `SimulationSystemGroup`
- create + add `RockPlacementGenerationSystem` to `SimulationSystemGroup`
- optionally create + add `RockChunkRenderSystem` to `PresentationSystemGroup`

### 10.2 LOD Cull Strip

Extend `TerrainChunkLodApplySystem.ApplyCulledLod` to remove rock runtime payload:

- remove `RockPlacementRecord` buffer
- remove `ChunkRockPlacementTag`

This preserves deterministic re-entry parity with trees.

### 10.3 Debug Logging

No `Debug.Log` in systems.

Route logs via `DOTS.Terrain.Core.DebugSettings` with new family-specific toggles only if needed.

---

## 11. File Targets (Proposed)

New runtime folder:

- `Assets/Scripts/DOTS/Terrain/SurfaceScatter/` (shared helpers only)
- `Assets/Scripts/DOTS/Terrain/Rocks/` (rock family-specific data and systems)

Initial files:

- `RockPlacementRecord.cs`
- `ChunkRockPlacementTag.cs`
- `RockStateDelta.cs`
- `RockPersistentIdentity.cs`
- `RockPlacementAlgorithm.cs`
- `RockPlacementGenerationSystem.cs`
- `RockPlacementInvalidationSystem.cs`
- `RockChunkRenderSystem.cs`
- `RockRenderConfig.cs`

Keep `Assets/Scripts/DOTS/Terrain/Trees/` unchanged in first pass except for optional shared helper adoption.

---

## 12. Test Matrix (First Rock Family)

### EditMode

- `RockPlacement_SameSeedSameChunk_SameRecords`
- `RockPlacement_NeighborChunks_NoBorderDuplicates`
- `RockPlacement_SteepSlope_Rejected`
- `RockPlacement_Invalidation_RebuildPreservesStableIds`
- `TreeRock_PlacementRules_NoObviousOverlap`

### PlayMode

- `RockPlacement_ChunkStreamOutIn_RegeneratesIdentically`
- `RockPlacement_TerrainEdit_RebuildsAffectedChunksOnly`
- `RockPlacement_LodCullRestore_NoStaleState`

---

## 13. Implementation Sequence

1. Add rock data components and buffers.
2. Add rock generation algorithm and generation system.
3. Add rock invalidation system.
4. Extend LOD cull strip for rock payload.
5. Add simple rock renderer and bootstrap toggles.
6. Add deterministic + streaming tests.
7. Evaluate shared helpers after tree and rock parity is stable.

---

## 14. Acceptance Criteria

- rocks run through the same chunk lifecycle seams currently proven by trees
- tree-specific state model remains unchanged
- rock state model is family-specific and not forced into tree lifecycle semantics
- terrain edit and LOD invalidation correctly clear and deterministically rebuild rock records
- dense details remain on the separate grass/details path
- no monolithic all terrain props runtime is introduced in this phase
