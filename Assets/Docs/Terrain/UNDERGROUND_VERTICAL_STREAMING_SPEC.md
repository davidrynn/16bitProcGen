# Underground & Vertical Streaming Spec

**Status:** DESIGN
**Last Updated:** 2026-07-21
**Relates to:** TERRAIN_ECS_NEXT_STEPS_SPEC.md, KNOWN_ISSUES.md, BIOME_TERRAIN_FIELD_SPEC.md,
[`../Structures/RELIC_TERRAIN_INTEGRATION_SPEC.md`](../Structures/RELIC_TERRAIN_INTEGRATION_SPEC.md)
(destructible relics — the first customer), [`WORLD_STRUCTURE_SPEC.md`](WORLD_STRUCTURE_SPEC.md)
(the `H` `AFar` ramp is blocked by the same slab)

> **2026-07-21 — full cost inventory added as §"3D grid cost inventory" below.** It supersedes the
> "Files that need no changes" table in the *scatter* and *empty-chunk* respects: that table is
> accurate for the systems it lists but **misses two whole problem classes** (scatter topmost-surface
> determination, and a live mesh-budget bug). Read the inventory before estimating.
>
> **Read this first:** with today's SDF field this work buys **nothing**. `SdLayeredGround` is a pure
> heightfield whose amplitudes sum to 6.45 against a 15-unit slab — everything already fits in one
> layer, so sparse 3D allocation would resolve to exactly one occupied layer. **Vertical chunking is
> gated on vertical CONTENT existing** (destructible relics, caves, or `H`'s `AFar` amplitude), not
> the other way round. Do not build it speculatively.

---

## Approach: Full Vertical Columns (Minecraft Model)

The standard pattern in digging games (Minecraft, No Man's Sky, Core Keeper) is to load the entire vertical column and render only exposed surfaces. Nobody spawns chunks selectively along tunnel paths — it creates complex reachability tracking and causes pop-in at branch points.

This pipeline already follows that model naturally. **Surface Nets produces zero triangles for fully-solid chunks** — the iso-surface never crosses, so the mesh job exits with empty output. A fully-solid underground chunk costs density sampling time (spread across frames by the existing rebuild budget) but nothing at the GPU. Tunnels and caves appear as the SDF field carves iso-surface crossings underground.

The implementation is therefore: extend the streaming and LOD systems to spawn vertical columns, then make the SDF field return solid by default underground and carve geometry where needed.

---

## Current Architecture Baseline

All chunks share `ChunkCoord.y = 0`. The streaming window is a 2D XZ square; despawn is 2D Chebyshev. The `int3 ChunkCoord` field already has a Y slot — it is simply always zero.

### Files that gate vertical extent

| File | Current constraint | Change required |
|------|-------------------|-----------------|
| `TerrainChunkStreamingSystem.cs` | `NativeParallelHashMap<int2, Entity>` key; spawn/despawn loops are XZ-only | **Primary change target** |
| `TerrainChunkLodSelectionSystem.cs` | Chebyshev distance uses `ChunkCoord.x` and `ChunkCoord.z` only | Guard underground chunks from LOD demotion |
| `TerrainLodSettings.cs` | No vertical radius fields | Add `VerticalStreamingRadiusAbove`, `VerticalStreamingRadiusBelow` |
| `ProjectFeatureConfig.cs` | Single `TerrainStreamingRadiusInChunks` | Add vertical streaming toggle and radii |

### Files that need no changes

| File | Why |
|------|-----|
| `TerrainChunkDensitySamplingSystem.cs` | Uses `ChunkOrigin` (`float3`); works at any Y |
| `TerrainChunkMeshBuildSystem.cs` | Chunk-local density blob; no Y dependency |
| `SurfaceNetsJob` | Fully 3D; position-agnostic; produces zero triangles for solid input |
| `TerrainChunkMeshUploadSystem.cs` | Uploads whatever geometry the mesh job produced |
| `TerrainChunkRenderPrepSystem.cs` | Reads `TerrainChunkBounds.WorldOrigin`; works at any Y |
| `TerrainChunkColliderBuildSystem.cs` | Chunk-local; no Y dependency |
| `SDFTerrainField` | Already samples `float3` world position |

---

## 3D grid cost inventory _(added 2026-07-21)_

Full read of the codebase against a **sparse 3D vertical grid**. Classified as must-change vs
already-safe vs genuinely hard.

### Blocking bug — fix before enabling any vertical layer

**`TerrainChunkMeshBuildSystem.cs:91`** — a chunk that meshes to **zero vertices** hits
`if (!meshBlob.IsCreated) continue;` and never reaches the `TerrainChunkNeedsMeshBuild` removal at
line 119-120. It is rescheduled **every frame, forever**, eating a slot of
`MaxMeshRebuildsPerFrame` (default 8).

Invisible today because nearly every chunk has surface. In a stacked world most chunks are fully
solid or fully air, so a handful of empty chunks would **permanently starve the mesh budget** and new
chunks would never settle. This directly contradicts the §"Approach" claim above that solid chunks
"cost density sampling time but nothing at the GPU" — today they also cost a budget slot in
perpetuity. Ticket **U1**.

### Costing reality: LOD does not reduce chunk count

`TerrainLodSettings.cs:43-46` keeps chunk world footprint invariant — `(res-1) × voxel = 15` on all
three LODs, on **all three axes** (cubic resolutions, uniform voxel). So chunk count is purely
`(2R+1)² × verticalLayers`. LOD changes only per-chunk sample cost (4096 → 729 → 125, a 33× spread).

**A 4-layer window is therefore a hard 4× on entity count, ECB traffic, physics broadphase entries
and query iteration, and LOD gives none of it back.**

### Must change

| File | Work |
|---|---|
| `Streaming/TerrainChunkStreamingSystem.cs` | `int2`→`int3` map (note `TryAdd` silently drops stacked layers today); derive player Y layer; 3-deep spawn loop; `originY = base − span*0.5 + layer*span`; 3D despawn. `ChunkCoord = new int3(coord.x, **0**, coord.y)` at :170 is the hardcode. **Highest-risk file** — despawn teardown at :209-246 is delicate |
| `Meshing/TerrainChunkMeshBuildSystem.cs:91` | The zero-vertex tag leak above. **Must land first** |
| `LOD/TerrainChunkLodSelectionSystem.cs` | Y-aware distance or explicit off-layer guard; otherwise every off-layer chunk demotes at range 0 |
| `Physics/TerrainChunkColliderBuildSystem.cs:145-197` | 3D distance in the priority sort + a vertical-layer gate; today a chunk 3 layers below sorts as distance 0 and steals the budget-exempt near-player slots |
| `LOD/TerrainLodSettings.cs`, `ProjectFeatureConfig.cs`, `DotsSystemBootstrap.cs` | Vertical radii + feature flag plumbing |
| `Debug/TerrainChunkDebugState.cs:12`, `TerrainDebugConfig.FixedCenterChunk` | `int2` schema — cannot represent a layer |

### Already 3D-safe (verified by reading, not assumed)

`TerrainChunkDensitySamplingSystem` (float3 origin, fully position-agnostic) · `SDFTerrainField` /
`SdLayeredGround` · `TerrainChunkMeshBuilder` · `TerrainChunkRenderPrepSystem` ·
`TerrainChunkMeshUploadSystem` (already stores `int3`) · `TerrainChunkEditUtility` (true 3D AABB, so
vertical dirty-marking already works) · `TerrainEditInputSystem` (**already layer-aware** — iterates
every chunk in a column for `minLayerOriginY`) · `SurfaceScatterRenderBoundsUtility`.

**Vertical seams already work.** The `Resolution + 1` density overlap is isotropic on all three axes,
and `SurfaceNetsJob` emits all three face families (`GenerateXYFaces`, `GenerateXZFaces`,
`GenerateYZFaces`). **No mesher change is required.** Only the seam *validators* need a third
direction — they currently know just `East`/`North` (`TerrainChunkMeshBorderUtility.BorderDirection`).

**No disk persistence for terrain exists**, so there is no save-format migration.

### Genuinely hard — the real cost is scatter, not streaming

1. **`SurfaceScatterPlacementMath.TryFindSurfaceHeight`** scans only its **own** chunk's density blob
   and returns the first air→solid crossing. Every family depends on it (trees, rocks, pebbles). In a
   stacked world a cave-floor chunk reports a valid "surface" and grows **trees inside the cave**.
   There is no column-occupancy index to filter against — it does not exist. **This is design work,
   not a port.** Ticket **U4**.
2. **`SurfaceScatterPlacementMath.CandidateHash:15-24`** takes `int3` but mixes only `.x` and `.z`.
   Stacked chunks produce identical jitter/variant/yaw. One-line fix that **re-rolls every tree, rock
   and pebble in the world** — recoverable via `GenerationVersion`, but a determinism break.
3. **Grass** documents the rule (`TerrainChunkGrassSurface.cs:15-19`: *"Only topmost solid-layer
   chunks…"*) but has **no production tagger** — tagging is an editor menu item that tags everything.
4. **Seam validators fail silently.** Four `NativeParallelHashMap<int2,Entity>` sites where `TryAdd`
   drops stacked layers. Nothing throws; you just lose coverage and get bogus mismatch reports.

### Tests encoding 2D assumptions

`TerrainChunkBorderContinuityTests` · `TerrainMeshBorderContinuityTests` ·
`TerrainBandingDiagnosticTests` (all rebuild their own `int2` maps). `TerrainLodTests` is safe if the
Y-guard goes **around** `ComputeTargetLod` rather than inside it.

### Prerequisite for the relic use case

`SDFTerrainField.Sample` loops **every** edit at **every** sample with no spatial culling, and
`CopyEditsToTempArray` copies the whole singleton buffer per chunk dispatch. A 17-primitive SDF hand
= ~70 k capsule evaluations **per chunk, world-wide**. **Per-chunk edit AABB culling is a
prerequisite, not an optimisation** (ticket **U2**); it also directly relieves BUG-008.

---

## What Already Works (No Code Changes)

Before adding vertical layers, the SDF pipeline already supports caves, overhangs, and arches within the single chunk's Y extent (~15 world units). The density blob is 3D; Surface Nets produces correct concave geometry. Players can dig pits and carve hollows anywhere within the existing layer.

**Limit:** content deeper than ~15 wu below the spawn surface requires a second chunk layer. The floor of the Y=0 chunk is the current hard bottom.

---

## Level 1 — One Underground Layer (Small Lift)

**Effort:** 1–2 days.

Spawn one additional chunk layer directly below the surface (`ChunkCoord.y = -1`). This gives ~30 wu of vertical depth. The layer is always live — same XZ window as the surface, just offset one chunk height down. No dynamic vertical tracking required.

### `TerrainChunkStreamingSystem.cs`

**1. Change the deduplication map key from `int2` to `int3`.**

```csharp
// Before
var map = new NativeParallelHashMap<int2, Entity>(existingEntities.Length, Allocator.Temp);
for (int i = 0; i < existingEntities.Length; i++)
    map.TryAdd(new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z), existingEntities[i]);

// After
var map = new NativeParallelHashMap<int3, Entity>(existingEntities.Length, Allocator.Temp);
for (int i = 0; i < existingEntities.Length; i++)
    map.TryAdd(chunk.ChunkCoord, existingEntities[i]);
```

**2. Add a Y loop over `[-1, 0]` in the spawn block.**

```csharp
for (int dz = -radius; dz <= radius; dz++)
for (int dx = -radius; dx <= radius; dx++)
for (int dy = -1; dy <= 0; dy++)
{
    var coord = new int3(centerCoord.x + dx, dy, centerCoord.y + dz);
    if (map.ContainsKey(coord)) continue;

    // originY: layer 0 uses existing baseHeight calculation; layer -1 is one span below.
    var layerOriginY = baseHeight - (chunkVerticalSpan * 0.5f) + dy * chunkVerticalSpan;
    var origin = new float3(coord.x * chunkStride, layerOriginY, coord.z * chunkStride);

    var entity = ecb.CreateEntity();
    ecb.AddComponent(entity, new TerrainChunk { ChunkCoord = coord });
    ecb.AddComponent(entity, TerrainChunkGridInfo.Create(lod0Resolution, lod0VoxelSize));
    ecb.AddComponent(entity, new TerrainChunkLodState { CurrentLod = 0, TargetLod = 0, LastSwitchFrame = 0 });
    ecb.AddComponent(entity, new TerrainChunkBounds { WorldOrigin = origin });
    ecb.AddComponent<TerrainChunkNeedsDensityRebuild>(entity);
    ecb.AddComponent(entity, LocalTransform.FromPosition(origin));
    // ... debug components unchanged
}
```

**3. Update despawn to use `int3` key.**

The despawn check already uses `chunk.ChunkCoord.x` and `chunk.ChunkCoord.z` against `centerCoord`. For Level 1, simply keep the same XZ bounds check — a chunk at Y=-1 is despawned whenever its XZ peer at Y=0 would be:

```csharp
var xzCoord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);
var xzInRange = math.abs(xzCoord.x - centerCoord.x) <= radius
             && math.abs(xzCoord.y - centerCoord.y) <= radius;
if (xzInRange) continue;
// ... dispose blobs, destroy entity
```

**4. Gate behind a feature flag.**

Add to `ProjectFeatureConfig.cs`:

```csharp
[Header("Terrain Underground")]
[Tooltip("Spawn one underground chunk layer below the surface. Requires underground SDF content (see spec).")]
public bool EnableUndergroundLayer = false;
```

Mirror into `ProjectFeatureConfigSingleton`:

```csharp
public bool EnableUndergroundLayer;
```

In `ProcessStreamingWindow`, read the flag and clamp the Y loop accordingly:

```csharp
var dyMin = config.EnableUndergroundLayer ? -1 : 0;
for (int dy = dyMin; dy <= 0; dy++) { ... }
```

### `TerrainChunkLodSelectionSystem.cs`

Underground chunks should not participate in LOD demotion — a chunk 15 wu below the player is always at close range and should stay at LOD0. Add a guard at the top of the per-chunk loop:

```csharp
// Underground/overhead chunks always stay at LOD0.
if (chunks[i].ChunkCoord.y != 0)
    continue;
```

### SDF field content (prerequisite before enabling)

Without underground SDF content, Y=-1 chunks will sample the same surface field below the terrain floor and produce either fully solid meshes (if the field is positive everywhere underground) or fully empty meshes (if it extrapolates negative). Neither is useful.

Before enabling `EnableUndergroundLayer`, add a baseline underground behaviour to `SDFTerrainField.Sample`:

```csharp
// Below the surface, default to solid rock with occasional cave pockets.
// caveStartDepth, caveFrequency, caveThreshold driven by SDFTerrainFieldSettings.
var depthBelowSurface = baseHeight - worldPos.y;
if (depthBelowSurface > settings.CaveStartDepth)
{
    // Worley/cellular noise inverted: low values = cave void
    var cave = noise.cellular(worldPos * settings.CaveFrequency).x;
    if (cave < settings.CaveThreshold)
        density = math.lerp(density, -1f, math.saturate((settings.CaveThreshold - cave) / settings.CaveThreshold));
}
```

Add `CaveStartDepth`, `CaveFrequency`, and `CaveThreshold` fields to `SDFTerrainFieldSettings`. This is a content parameter — the streaming architecture is not coupled to the specific noise chosen.

**Deliverable:** Player can dig through the ground floor and fall into cave geometry. Adds `(2R+1)²` chunks (625 at radius 12) — same budget as the surface layer. Solid underground chunks produce zero triangles and cost nothing to render.

---

## Level 2 — Asymmetric Vertical Streaming (Medium Lift)

**Effort:** 1–2 weeks.

Dynamic vertical streaming that follows the player's Y layer. The window has separate above/below radii; the system spawns and despawns layers as the player descends or ascends. This is the full Minecraft-column model.

### Why asymmetric radii

Spawning symmetrically above and below wastes budget. When the player is on the surface, there is nothing above sky worth spawning. When descending a deep mine, they need depth below but only a thin ceiling above. Recommended defaults:

```
VerticalStreamingRadiusAbove = 1   (one layer — see ceiling, prevent pop-in from above)
VerticalStreamingRadiusBelow = 3   (three layers — ~45 wu of accessible depth)
```

At radius 12, this adds `(2×12+1)² × 4 = 2,500` chunks vs 625 today. The per-frame rebuild budgets spread settling cost; this is a latency cost on transition, not a per-frame spike.

### `TerrainLodSettings.cs`

```csharp
// Vertical streaming window (in chunk layers). Small values strongly recommended.
// VerticalStreamingRadiusAbove = 1, VerticalStreamingRadiusBelow = 3 is a sensible default.
public int VerticalStreamingRadiusAbove;
public int VerticalStreamingRadiusBelow;

// Gate collider builds to layers the player can physically reach.
// Colliders three layers below the player waste physics broadphase memory.
public int ColliderMaxVerticalLayers;

public static TerrainLodSettings Default => new TerrainLodSettings
{
    // ... existing fields unchanged ...
    VerticalStreamingRadiusAbove = 1,
    VerticalStreamingRadiusBelow = 3,
    ColliderMaxVerticalLayers    = 1,
};
```

### `TerrainChunkStreamingSystem.cs`

Replace the hardcoded `dy = [-1, 0]` with a settings-driven range anchored on the player's current layer:

```csharp
// Derive which vertical layer the player currently occupies.
var playerChunkY = (int)math.floor((playerPos.y - baseHeight) / chunkVerticalSpan);

var vertAbove = 0;
var vertBelow = 0;
if (SystemAPI.TryGetSingleton<TerrainLodSettings>(out var lodSettings))
{
    vertAbove = lodSettings.VerticalStreamingRadiusAbove;
    vertBelow = lodSettings.VerticalStreamingRadiusBelow;
}

// Spawn loop — three nested loops, Y range anchored on player layer.
for (int dz = -radius; dz <= radius; dz++)
for (int dx = -radius; dx <= radius; dx++)
for (int dy = -vertBelow; dy <= vertAbove; dy++)
{
    var coord = new int3(centerCoord.x + dx, playerChunkY + dy, centerCoord.y + dz);
    if (map.ContainsKey(coord)) continue;

    var layerOriginY = baseHeight - (chunkVerticalSpan * 0.5f) + coord.y * chunkVerticalSpan;
    var origin = new float3(coord.x * chunkStride, layerOriginY, coord.z * chunkStride);
    // ... spawn entity as before
}

// Despawn — 3D range check.
var xzInRange = math.abs(chunk.ChunkCoord.x - centerCoord.x) <= radius
             && math.abs(chunk.ChunkCoord.z - centerCoord.y) <= radius;
var yInRange  = chunk.ChunkCoord.y >= playerChunkY - vertBelow
             && chunk.ChunkCoord.y <= playerChunkY + vertAbove;
if (xzInRange && yInRange) continue;
// ... dispose and destroy
```

`ProcessStreamingWindow` must also accept `playerChunkY` as a parameter (or derive it internally from `playerPos` and `baseHeight`). The debug-freeze path passes a fixed `centerCoord` — extend the signature to also accept a fixed `centerChunkY` (default 0 for the freeze case).

### `TerrainChunkLodSelectionSystem.cs`

Underground chunks are always LOD0 — their visual footprint relative to the player doesn't shrink with distance the way a horizontally-distant chunk does. Extend the guard to use the player's current Y layer:

```csharp
var playerChunkY = (int)math.floor(
    (playerTransform.Position.y - baseHeight) / (math.max(0, settings.Lod0Resolution.y - 1) * settings.Lod0VoxelSize));

for (int i = 0; i < entities.Length; i++)
{
    // Chunks not on the player's current layer skip LOD selection — always LOD0.
    if (chunks[i].ChunkCoord.y != playerChunkY)
        continue;

    // ... existing distance calc and ComputeTargetLod unchanged
}
```

### `TerrainChunkColliderBuildSystem.cs`

Wrap the collider build in a vertical range check to avoid wasting physics broadphase memory on layers the player cannot reach:

```csharp
// Read player layer and ColliderMaxVerticalLayers from settings.
// Skip collider build for chunks too far above or below.
if (math.abs(chunk.ChunkCoord.y - playerChunkY) > lodSettings.ColliderMaxVerticalLayers)
{
    ecb.RemoveComponent<TerrainChunkNeedsColliderBuild>(entity);
    continue;
}
```

### `ProjectFeatureConfig.cs`

```csharp
[Header("Terrain Underground")]
public bool EnableVerticalStreaming = false;
[Tooltip("Chunk layers above the player to keep streamed. 1 prevents ceiling pop-in.")]
[Min(0)] public int TerrainVerticalStreamingRadiusAbove = 1;
[Tooltip("Chunk layers below the player to keep streamed. Each layer is ~15 wu of depth.")]
[Min(0)] public int TerrainVerticalStreamingRadiusBelow = 3;
```

Mirror into `ProjectFeatureConfigSingleton` and propagate to `TerrainLodSettings` in the bootstrap (same pattern as `TerrainStreamingRadiusInChunks` today).

**Deliverable:** Player can descend arbitrarily. Streaming window follows Y position. Memory bounded by `(2R+1)² × (above + below + 1)` chunks. Solid rock chunks cost nothing to render; only carved tunnel/cave surfaces draw triangles.

---

## Level 3 — Full Underground World (Large Lift)

**Effort:** 4–8 weeks, depends on content scope.

Requires Level 2 complete plus:

- **Underground biome SDF** — a distinct noise stack below a depth threshold (tunnel widening, ore veins, void pockets, lava lakes). Authored as a separate layer in `SDFTerrainField` or a new `UndergroundTerrainField`, blended at the biome boundary. See `BIOME_TERRAIN_FIELD_SPEC.md`.
- **WFC dungeon integration** — WFC room layouts placed as structure anchors (see `STRUCTURE_PLACEMENT_SPEC.md`) with their bounding volumes submitted as persistent `SDFEdit` carves. Rooms remain stable across streaming respawn cycles because the edit journal survives. See `PERSISTENCE_SPEC.md`.
- **Entrance structures** — cave mouths, mine shafts, dungeon stairs placed by the structure system at the surface. These are `SDFEdit` carves + placed mesh (already supported by the relic/structure placement pipeline).
- **Vertical LOD policy refinement** — if the underground world spans 10+ layers, a vertical LOD policy (lower-res density at deep layers) becomes worth implementing. Not needed for 3–4 layers.

---

## Performance Budget

| Configuration | Added chunks | Total chunks (radius 12) |
|---|---|---|
| Current (surface only) | — | 625 |
| Level 1 (+ 1 underground layer) | 625 | 1,250 |
| Level 2 (above=1, below=3) | 2,500 | 3,125 |
| Level 2 (above=1, below=5) | 3,750 | 4,375 |

**Render cost:** solid underground chunks = 0 draw calls, 0 triangles. Only carved surfaces draw.

**Settling latency:** at 8 density rebuilds/frame, 2,500 new chunks after a deep teleport takes ~312 frames (~5 s at 60 fps) to fully mesh. In practice, chunks nearest the player settle first (already how the budget system works by query ordering). Raise `MaxDensityRebuildsPerFrame` for faster settling on capable hardware.

---

## Seam Integrity at Layer Boundaries

Surface Nets already handles inter-chunk seams horizontally via the +1 density resolution extension (`densityResolution = grid.Resolution + 1`). The same mechanism applies vertically: the bottom row of the Y=0 chunk and the top row of the Y=-1 chunk share density sample points, so the iso-surface stitches correctly at the layer boundary.

**Validate this in Level 1** before proceeding to Level 2. A visual seam at the chunk floor boundary would indicate a world-origin offset error in how Y=-1 chunk origins are computed.

---

## Open Questions

- **Entrance gating:** should underground layers spawn proactively (always, like Minecraft) or reactively (only when the player is within a configured distance of an entrance trigger)? Proactive is simpler; reactive saves memory in worlds with no underground content.
- **Grass and tree scatter:** placement systems filter by `SurfaceY`; underground chunks should produce no vegetation. Confirm `TreePlacementSystem` and `RockPlacementSystem` skip chunks with `ChunkCoord.y < 0` gracefully.
- **Debug visualisation:** `TerrainChunkDebugState` labels use `(coord.x, coord.z)`. Update to include `coord.y` when vertical streaming is active so underground chunks are distinguishable in the debug overlay.
- **Sky islands:** the same vertical streaming infrastructure supports chunks above the ground plane (`VerticalStreamingRadiusAbove`). Sky island content would be authored as a separate SDF layer above `baseHeight + threshold`, analogous to the underground cave layer.
