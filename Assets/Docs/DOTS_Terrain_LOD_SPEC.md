# DOTS Terrain LOD System Specification

## Goals

Create a scalable terrain Level of Detail (LOD) system for an infinite procedural world using Unity DOTS that:

* supports infinite streaming terrain
* preserves clean low-poly silhouettes
* avoids visible pop-in during traversal
* supports future destructibility
* scales to large view distances without heavy CPU/GPU cost
* remains architecturally consistent with existing chunk pipeline:

Density -> Surface Nets Mesh -> Upload -> Render

The system prioritizes:

* stable silhouette at distance
* simple implementation first
* incremental extension
* deterministic chunk state transitions
* compatibility with ECS migration plan

---

# High Level Approach

Terrain LOD is implemented using distance-based chunk rings.

Each ring defines simulation and visual fidelity.

Player
 |
 v

Ring 0 (Full gameplay detail)
Ring 1 (Reduced mesh resolution)
Ring 2 (Silhouette mesh only)
Ring 3 (Culled)

LOD state per chunk determines:

* density resolution
* mesh resolution
* collider presence
* grass/detail spawning
* shadow participation
* destruction/update policy

Core principle:

* Streaming owns chunk lifetime.
* LOD owns chunk fidelity.

If streaming already despawns chunks outside radius, Ring 3 may map to "despawned".

---

# Chunk Ring Model

## Initial Recommended Configuration

| Ring | Purpose | Resolution | Simulation | Collider | Detail Objects | Shadow |
|------|--------|------------|------------|----------|---------------|--------|
| LOD0 | Gameplay | high | full | yes | yes | yes |
| LOD1 | Visual mid | medium | minimal | optional/simplified | sparse (optional) | yes |
| LOD2 | Silhouette | low | none | no | no | optional |
| LOD3 | culled | none | none | no | no | no |

## Example Distances

Tune based on chunk size.

Assume:

chunkSize = 32m

Suggested values:

LOD0 radius: 2 chunks
LOD1 radius: 4 chunks
LOD2 radius: 8 chunks
LOD3: beyond 8 chunks

---

# ECS Data Model

Use existing project component names where possible.

## Components

### TerrainChunkLodState

```csharp
public struct TerrainChunkLodState : IComponentData
{
    public int CurrentLod;
    public int TargetLod;
    public uint LastSwitchFrame;
}
```

Level meanings:

0 = full detail
1 = medium detail
2 = silhouette
3 = culled

---

### TerrainLodSettings

Central tuning parameters.

```csharp
public struct TerrainLodSettings : IComponentData
{
    public float Lod0MaxDist;
    public float Lod1MaxDist;
    public float Lod2MaxDist;
    public float HysteresisChunks;

    public int3 Lod0Resolution; public float Lod0VoxelSize;
    public int3 Lod1Resolution; public float Lod1VoxelSize;
    public int3 Lod2Resolution; public float Lod2VoxelSize;

    public int ColliderMaxLod;
    public int GrassMaxLod;
    public int ShadowMaxLod;

    public int MaxDensityRebuildsPerFrame;
    public int MaxMeshRebuildsPerFrame;
    public int MaxColliderRebuildsPerFrame;

    public bool UseStreamingAsCullBoundary;
}
```

Recommended density resolutions:

LOD0 = 32 samples/axis
LOD1 = 16 samples/axis
LOD2 = 8 samples/axis

Resolution rule:

* Lower LOD resolution should be power-of-two divisors of base resolution.

Footprint rule (required):

* Chunk world footprint must remain invariant across LODs.
* For each LOD, `(Resolution.x - 1) * VoxelSize` and `(Resolution.z - 1) * VoxelSize` must equal LOD0 footprint.

---

### TerrainChunkLodDirty

Tag added when chunk LOD actually changes and is applied.

```csharp
public struct TerrainChunkLodDirty : IComponentData {}
```

Used by seam/detail/render policy follow-up systems.

---

# Systems

## TerrainChunkLodSelectionSystem

Determines target ring from player-relative chunk distance.

Runs after:

* player movement position update used for chunk-space mapping
* terrain streaming window update

Selection logic:

```text
if dist <= Lod0MaxDist: lod = 0
else if dist <= Lod1MaxDist: lod = 1
else if dist <= Lod2MaxDist: lod = 2
else: lod = 3
```

Transition rule:

* Promotion (toward lower number) is immediate.
* Demotion requires crossing threshold + hysteresis.

---

## TerrainChunkLodApplySystem

Applies TargetLod to runtime chunk settings.

Responsibilities:

* update `TerrainChunkGridInfo` resolution/voxel size
* write `CurrentLod` and `LastSwitchFrame`
* add `TerrainChunkNeedsDensityRebuild`
* add `TerrainChunkLodDirty`

---

## TerrainChunkDensitySamplingSystem

Density grid is rebuilt at current LOD resolution.

Budgeting:

* enforce `MaxDensityRebuildsPerFrame`

---

## TerrainChunkMeshBuildSystem

Surface Nets mesh density follows current LOD density grid.

Lower LOD means:

* fewer SDF samples
* fewer vertices
* lower triangle count

Budgeting:

* enforce `MaxMeshRebuildsPerFrame`

Expected order-of-magnitude triangle bands:

LOD0: ~8k-20k
LOD1: ~2k-5k
LOD2: ~200-1000

---

## TerrainChunkColliderBuildSystem

Collider policy:

| LOD | Collider |
|-----|----------|
| LOD0 | full |
| LOD1 | optional/simplified |
| LOD2 | none |
| LOD3 | none |

Rules:

* Collider generation uses local mesh vertex space.
* Respect `ColliderMaxLod` policy.
* Enforce `MaxColliderRebuildsPerFrame` budget.

---

## TerrainChunkDetailSpawnSystem

Grass, props, and detail meshes spawn only when:

* `CurrentLod <= GrassMaxLod`

Default phase-1 policy:

* `GrassMaxLod = 0` (LOD0 only)

Optional extension:

* allow sparse LOD1 detail with separate density multiplier.

---

## TerrainChunkRenderPolicySystem (optional split)

Applies visual policy by LOD:

* shadow participation by `ShadowMaxLod`
* optional material keyword/debug overrides for LOD inspection

---

# Atmospheric Blending

Fog is used to hide LOD transitions.

## URP Fog Configuration

Recommended:

* Linear fog
* Fog start near LOD1 world boundary
* Fog end near LOD2 world boundary

Example:

LOD0 radius = 64m
LOD1 radius = 128m
LOD2 radius = 256m

fog start = 140m
fog end = 260m

If weather controls fog, expose LOD target fog distances through shared settings.

---

# Visual Strategy for Low Poly Style

Preserve silhouette clarity:

* avoid noisy terrain features at far LOD
* preserve large-scale height variation
* reduce micro detail at distance
* maintain consistent color palette
* avoid high-frequency normals far away

Silhouette readability is more important than near-surface micro detail at distance.

---

# System Ordering

Required update order:

PlayerMovementSystem
TerrainChunkStreamingSystem
TerrainChunkLodSelectionSystem
TerrainChunkLodApplySystem
TerrainChunkDensitySamplingSystem
TerrainChunkMeshBuildSystem
TerrainChunkMeshUploadSystem
TerrainChunkColliderBuildSystem
TerrainChunkDetailSpawnSystem

Critical constraint:

* LOD apply must run before density rebuild in the same frame.

---

# Performance Expectations

Typical active chunk counts:

| LOD | chunks active |
|-----|--------------|
| LOD0 | 9-25 |
| LOD1 | 40-80 |
| LOD2 | 100-200 |

Burst meshing and bounded rebuild budgets should keep frame time stable during movement/editing.

---

# Debug Visualization

Add debug color override:

| LOD | color |
|-----|-------|
| LOD0 | green |
| LOD1 | yellow |
| LOD2 | red |

Recommended diagnostics:

* LOD target/apply transition logs
* per-frame pending counts (density/mesh/collider)
* ring occupancy counters

---

# Initial Implementation Scope

Phase 1:

* chunk ring assignment with hysteresis
* density/mesh resolution scaling by LOD
* disable detail objects beyond `GrassMaxLod` (default LOD0)
* fog blending hookup
* enforce LOD->density ordering

Phase 2:

* simplified LOD1 colliders
* mesh simplification and silhouette tuning
* shadow distance / `ShadowMaxLod` tuning

Phase 3:

* biome-aware LOD tuning
* impostor far terrain experiments
* async mesh generation
* movement-direction streaming prioritization

---

# Non Goals

Not required initially:

* geomorph blending between LOD levels
* quad-tree terrain
* clipmaps
* tessellation shaders
* GPU mesh simplification

Keep implementation simple and deterministic first.

---

# Future Extensions

Possible improvements:

* geomorph transitions / smooth vertex interpolation
* impostor horizon terrain
* biome-dependent visibility budgets
* background meshing jobs with priority queues
* explicit LOD3 resident-but-hidden mode (if needed for prediction/caching)

---

# Summary

This revised LOD system:

* fits ECS architecture
* integrates with Surface Nets and streaming
* supports destructible terrain workflows
* keeps deterministic transitions while reducing thrash via hysteresis
* preserves stylized silhouette identity
* scales to infinite terrain with bounded rebuild costs