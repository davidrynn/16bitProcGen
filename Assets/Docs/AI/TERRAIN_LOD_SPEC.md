# Terrain LOD Implementation Spec

Date: 2026-04-06
Status: Ready for implementation
Depends on: DOTS_Terrain_LOD_Plan.md

## Overview

Implement chunk-based LOD for the SDF/Surface Nets terrain pipeline.
Three milestones. Implement one milestone at a time; each milestone must pass
its tests before the next begins.

---

## Milestone 1 — LOD State and Selection

### New files

| File | Purpose |
|------|---------|
| `Assets/Scripts/DOTS/Terrain/LOD/TerrainLodSettings.cs` | Singleton settings component |
| `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodState.cs` | Per-chunk LOD state |
| `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodDirty.cs` | Tag: LOD changed, rebuild needed |
| `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodSelectionSystem.cs` | Computes TargetLod per chunk |
| `Assets/Scripts/DOTS/Terrain/LOD/TerrainChunkLodApplySystem.cs` | Applies TargetLod changes |

### Component: TerrainLodSettings

Singleton. Bootstrap creates one entity with this component.

```csharp
namespace DOTS.Terrain.LOD
{
    public struct TerrainLodSettings : IComponentData
    {
        // Ring thresholds in chunk units (Chebyshev distance).
        // LOD0 when dist <= Lod0MaxDist, LOD1 when <= Lod1MaxDist, else LOD2.
        public float Lod0MaxDist;   // e.g. 2
        public float Lod1MaxDist;   // e.g. 4

        public float HysteresisChunks; // e.g. 0.5 — widen demotion threshold

        // Grid settings per LOD level.
        public int3 Lod0Resolution; public float Lod0VoxelSize;
        public int3 Lod1Resolution; public float Lod1VoxelSize;
        public int3 Lod2Resolution; public float Lod2VoxelSize;

        // Policy gates.
        public int ColliderMaxLod; // chunks at LOD > this skip collider build
        public int GrassMaxLod;    // chunks at LOD > this skip grass rebuild

        // Per-frame rebuild budgets (applied in existing pipeline systems).
        public int MaxDensityRebuildsPerFrame;
        public int MaxMeshRebuildsPerFrame;
        public int MaxColliderRebuildsPerFrame;
    }
}
```

Reasonable defaults: Lod0MaxDist=2, Lod1MaxDist=4, Hysteresis=0.5,
ColliderMaxLod=1, GrassMaxLod=0.

### Component: TerrainChunkLodState

```csharp
namespace DOTS.Terrain.LOD
{
    public struct TerrainChunkLodState : IComponentData
    {
        public int CurrentLod;
        public int TargetLod;
        public uint LastSwitchFrame;
    }
}
```

### Tag: TerrainChunkLodDirty

```csharp
namespace DOTS.Terrain.LOD
{
    public struct TerrainChunkLodDirty : IComponentData { }
}
```

### System: TerrainChunkLodSelectionSystem

```
Namespace:   DOTS.Terrain.LOD
Update group: SimulationSystemGroup
Update after: TerrainChunkStreamingSystem
Attributes:  [BurstCompile], [DisableAutoCreation]
```

**OnCreate**: Require `TerrainLodSettings` and `PlayerTag`.

**OnUpdate**:
1. Read `TerrainLodSettings` singleton.
2. Read player `LocalTransform` to get player world position.
3. Derive chunk stride from any chunk's `TerrainChunkGridInfo`
   (`stride = (resolution.x - 1) * voxelSize`).
4. Compute player center chunk coord (same formula as streaming system).
5. For each chunk with `TerrainChunkLodState`:
   - Compute Chebyshev distance `d = max(|cx - px|, |cz - pz|)`.
   - Compute raw target LOD:
     - `d <= Lod0MaxDist` → LOD 0
     - `d <= Lod1MaxDist` → LOD 1
     - else → LOD 2
   - Apply hysteresis: only allow demotion (increase LOD) when
     `d > threshold + HysteresisChunks`. Promotion (decrease LOD) is immediate.
   - Write `TargetLod` if it changed. Do not touch `CurrentLod`.

Use `IJobEntity` or inline `foreach` over chunks. Keep Burst-compatible.

### System: TerrainChunkLodApplySystem

```
Namespace:   DOTS.Terrain.LOD
Update group: SimulationSystemGroup
Update after: TerrainChunkLodSelectionSystem
Attributes:  [BurstCompile], [DisableAutoCreation]
```

**OnCreate**: Require `TerrainLodSettings`.

**OnUpdate**:
1. Read `TerrainLodSettings` singleton.
2. For each chunk where `TargetLod != CurrentLod`:
   - Look up the target LOD's Resolution and VoxelSize from settings.
   - Overwrite `TerrainChunkGridInfo` with new resolution/voxel size.
   - Set `CurrentLod = TargetLod`.
   - Record `LastSwitchFrame = (uint)UnityEngine.Time.frameCount`.
   - Add `TerrainChunkNeedsDensityRebuild` via ECB (if not already present).
   - Add `TerrainChunkLodDirty` via ECB (if not already present).

Use `EndSimulationEntityCommandBufferSystem.Singleton` for structural changes.

### Streaming system: attach LOD state on spawn

In `TerrainChunkStreamingSystem.ProcessStreamingWindow`, when creating a new
chunk entity add:

```csharp
ecb.AddComponent(entity, new TerrainChunkLodState
{
    CurrentLod = 0,
    TargetLod  = 0,
    LastSwitchFrame = 0
});
```

This keeps all chunk entities query-eligible for the LOD systems from frame 1.

### Milestone 1 tests

Location: `Assets/Scripts/DOTS/Tests/Automated/TerrainLodSelectionTests.cs`

- **LodSelection_Lod0_WhenWithinLod0Radius**: chunk at distance 1, settings
  Lod0MaxDist=2 → TargetLod == 0 after one system update.
- **LodSelection_Lod2_WhenBeyondLod1Radius**: chunk at distance 6, Lod1MaxDist=4
  → TargetLod == 2.
- **LodHysteresis_NoDeomotionWithinHysteresisBand**: chunk at CurrentLod=0,
  distance = Lod0MaxDist + 0.3 (hysteresis=0.5) → TargetLod stays 0.
- **LodApply_UpdatesGridInfo**: chunk with TargetLod=1 != CurrentLod=0 →
  after apply system, GridInfo matches Lod1 settings and
  TerrainChunkNeedsDensityRebuild is present.

---

## Milestone 2 — Seams and Policy

### Neighbor delta clamp

Add `TerrainChunkLodNeighborClampSystem`:

```
Update after: TerrainChunkLodSelectionSystem
Update before: TerrainChunkLodApplySystem
```

**Logic**: For each chunk with `TerrainChunkLodState`, check the four cardinal
neighbors (XZ plane). If any neighbor's `CurrentLod` differs by more than 1,
clamp this chunk's `TargetLod` so the delta is at most 1.

Build a `NativeParallelHashMap<int2, int>` of coord → CurrentLod before the
clamp pass; read neighbors from the map. Write clamped TargetLod back.

### Seam skirts

Skirts are render-only geometry on the high-LOD (coarser) chunk faces that
border a lower-LOD (finer) chunk.

Add tag: `TerrainChunkNeedsSkirtRebuild : IComponentData`

Add system `TerrainChunkSkirtBuildSystem`:
- Runs after mesh build.
- Detects which faces border a finer neighbor (lower CurrentLod).
- Generates a thin quad skirt strip along those faces, stitched to the coarse
  chunk's border vertices.
- Output is a separate skirt mesh attached to the same chunk entity.

Skirts do not affect density data or colliders.

### Collider gating

In `TerrainChunkColliderBuildSystem.OnUpdate`, before processing a chunk:

```csharp
if (SystemAPI.TryGetSingleton<TerrainLodSettings>(out var lod))
{
    var state = entityManager.GetComponentData<TerrainChunkLodState>(entity);
    if (state.CurrentLod > lod.ColliderMaxLod)
    {
        ecb.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
        continue;
    }
}
```

### Grass gating

In `GrassChunkGenerationSystem.OnUpdate`, skip grass rebuild when
`state.CurrentLod > lod.GrassMaxLod`.

### Milestone 2 tests

- **NeighborDelta_ClampedToOne**: set up two adjacent chunks at LOD 0 and 2 →
  after clamp system, coarse chunk TargetLod is clamped to 1.
- **ColliderGating_SkippedForHighLod**: chunk at CurrentLod=2, ColliderMaxLod=1
  → `TerrainChunkNeedsColliderBuild` removed, no collider built.
- **GrassGating_SkippedForHighLod**: chunk at CurrentLod=1, GrassMaxLod=0 →
  grass rebuild not triggered.

---

## Milestone 3 — Diagnostics and Validation

### LOD debug overlay

Add `TerrainLodDebugSystem` (gated by `DebugSettings.LogSeam` or a new
`DebugSettings.LogLod` flag):
- Draws chunk LOD level as `DrawString` or Gizmo over each chunk center.
- Logs transitions: "Chunk (x,z) LOD 0→1" via `DebugSettings.LogLod(...)`.

### Profiler markers

Wrap `TerrainChunkLodSelectionSystem` and `TerrainChunkLodApplySystem` inner
loops in `ProfilerMarker` for frame-time visibility.

### Integration test

- Spawn streaming window at radius 4, step player from center to edge over
  several frames. Assert:
  - Near chunks stay LOD 0.
  - Far chunks reach LOD 2.
  - No neighbor delta exceeds 1 at any frame.
  - No collider built on LOD 2 chunks.

---

## Integration Notes

- All new systems use `[DisableAutoCreation]` and are registered in the same
  bootstrap that registers terrain systems (`TerrainBootstrapAuthoring` or
  `DotsSystemBootstrap`).
- `TerrainLodSettings` singleton is created by bootstrap with hardcoded
  defaults; expose as a `ScriptableObject` in a later pass if tuning is needed.
- Do not change the density/mesh/collider pipeline systems other than the
  gating additions in Milestone 2.
- Route all logging through `DebugSettings` — no bare `Debug.Log`.
