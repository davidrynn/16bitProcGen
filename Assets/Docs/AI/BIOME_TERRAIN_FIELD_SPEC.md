# Biome Terrain Field Spec
_Status: DESIGN — Phase 2, deferred until after MVP Vista Moment_
_Last updated: 2026-05-06_

---

## 1. Purpose

Define the architecture for world-field-driven terrain generation: a system where low-frequency climate signals classify each chunk into a terrain region, and that region drives the SDF shaping rules applied during density sampling.

This is the bridge between:
- The archived vision in [`Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md`](../Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md) (temperature/moisture/elevation fields)
- The active SDF pipeline in [`AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md`](TERRAIN_ECS_NEXT_STEPS_SPEC.md) (`SDFTerrainField`, `SdLayeredGround`, `TerrainFieldSettings`)

It is also the prerequisite that [`AI/BIOME_GRASS_STREAMING_MVP_PLAN.md`](BIOME_GRASS_STREAMING_MVP_PLAN.md) explicitly defers to:
> "Core biome-aware terrain shape and chunk behavior come first."

---

## 2. Phase Placement

**Do not start this work until the MVP Vista Moment is deliverable.** The Vista Moment (ground plane impostor, atmospheric fog, mountain skybox, hand mesh validation) is Phase 1 and is not gated on biome classification. See [`AI/MVP_VISTA_MOMENT_SPEC.md`](MVP_VISTA_MOMENT_SPEC.md).

Phase 2 ordering after Vista Moment:
1. **This spec** — world fields + region classifier + per-region shaping
2. Biome-aware surface scatter (trees/rocks read `TerrainChunkBiomeContext`)
3. Biome-aware grass (per `BIOME_GRASS_STREAMING_MVP_PLAN.md`)
4. Rare feature events (fissures, craters — ties into structures pipeline)

---

## 3. Design Principles

**Noise generates raw signals. Constraints decide what those signals mean.**

The current `SdLayeredGround` is good layered noise applied uniformly to the whole world. This spec introduces the constraint layer on top.

**Climate fields are not simulation.** Temperature, moisture, and uplift are control signals that produce believable terrain variety — they are not driven by weather or hydrology. Keep them as simple low-frequency noise functions.

**The 80/15/5 rule** (directly tied to the Vista Moment goal):
```
80%  readable, traversable terrain — the player can move and scan
15%  interesting variation — hills, gullies, rock clusters, denser forest
 5%  spectacular events — fissures, craters, buried relics, vertical cliffs
```
Every dramatic feature that is common becomes noise. Rarity is what makes the hand feel monumental.

**Do not evaluate world fields per voxel.** The density sampling job runs thousands of times per chunk build. World field classification must happen once per chunk at chunk-centre XZ and be stored as a component. The density job reads that component — it never recomputes it.

---

## 4. World Fields — `WorldSample`

A Burst-safe struct computed once per chunk at spawn time.

```csharp
namespace DOTS.Terrain
{
    /// <summary>
    /// Low-frequency climate signals for a terrain chunk.
    /// All values are normalised [0, 1]. Not simulation — game-useful control signals only.
    /// Computed once at chunk spawn from low-frequency noise; never recomputed in the density job.
    /// </summary>
    public struct WorldSample
    {
        public float Temperature;    // 0 = cold, 1 = hot
        public float Moisture;       // 0 = dry,  1 = wet
        public float Uplift;         // 0 = flat/basin, 1 = mountainous
        public float Erosion;        // 0 = rugged/fractured, 1 = smooth/rounded
        public float Weirdness;      // rare anomaly signal — drives fissures, craters, exposed ruins
    }
}
```

### Sampling Strategy

Each field is an independent low-frequency `snoise` call with a per-field seed offset (same pattern as `SDFMath.SeedLayerOffset`). Recommended base frequencies — adjust during tuning:

| Field | Frequency | Notes |
|---|---|---|
| Temperature | 0.0008 | One hot/cold region per ~1250 world units |
| Moisture | 0.0010 | Slightly finer than temperature |
| Uplift | 0.0012 | Mountain ranges ~800 units wide |
| Erosion | 0.0015 | Varies somewhat within uplift zones |
| Weirdness | 0.0005 | Very rare — large anomaly patches |

Remap raw `snoise` output `[-1, 1]` to `[0, 1]` before storing: `value = (snoise(...) + 1f) * 0.5f`.

---

## 5. Component — `TerrainChunkBiomeContext`

Stored on each chunk entity. Written at chunk spawn, read by density sampling and surface scatter.

```csharp
namespace DOTS.Terrain
{
    /// <summary>
    /// Pre-computed world fields and region classification for this chunk.
    /// Written once at chunk spawn by TerrainChunkStreamingSystem.
    /// Read by density sampling, tree/rock placement, and grass generation.
    /// </summary>
    public struct TerrainChunkBiomeContext : IComponentData
    {
        public WorldSample Fields;
        public TerrainRegionType Region;
        // Reserved for Phase 2+ soft blending between adjacent regions.
        // For MVP, SecondaryRegion == Region (no blend).
        public TerrainRegionType SecondaryRegion;
        public float BlendWeight;   // 0 = fully primary, 1 = fully secondary
    }
}
```

---

## 6. Region Types

```csharp
namespace DOTS.Terrain
{
    /// <summary>
    /// Terrain region determines which shaping rules apply during density sampling
    /// and which surface scatter profiles are selected.
    /// Uses byte backing for ECS component efficiency.
    /// </summary>
    public enum TerrainRegionType : byte
    {
        Plains        = 0,   // existing SdLayeredGround — baseline, MVP
        Hills         = 1,   // moderate uplift, soft edges — Phase 2
        Mountains     = 2,   // high uplift, ridge noise, sharp exponent — Phase 2
        DesertBasin   = 3,   // hot/dry, dune noise, basin depression — Phase 2
        FissureZone   = 4,   // high weirdness — SDF crack operators, exposed layers — Phase 2
    }
}
```

MVP implementation only needs `Plains` to work; all other values can fall through to `Plains` shaping rules until implemented.

---

## 7. Region Classifier — `WorldFieldSampler`

A Burst-safe static utility. Called once per chunk in `TerrainChunkStreamingSystem`.

```csharp
namespace DOTS.Terrain
{
    [BurstCompile]
    public static class WorldFieldSampler
    {
        /// <summary>
        /// Samples world fields at the chunk centre XZ position.
        /// All fields use independent seed offsets so signals are uncorrelated.
        /// </summary>
        public static WorldSample Sample(float2 chunkCentreXZ, uint worldSeed) { ... }

        /// <summary>
        /// Classifies a WorldSample into a primary TerrainRegionType.
        /// Thresholds are data — expose them in a ScriptableObject for tuning.
        /// </summary>
        public static TerrainRegionType Classify(in WorldSample s) { ... }
    }
}
```

### Classification Logic (MVP Thresholds — tune in play testing)

```
if (s.Uplift > 0.72 && s.Erosion < 0.35)  → Mountains
if (s.Uplift > 0.52)                        → Hills
if (s.Temperature > 0.65 && s.Moisture < 0.25) → DesertBasin
if (s.Weirdness > 0.80)                    → FissureZone
else                                        → Plains
```

Thresholds should be exposed via a `BiomeClassifierSettings` ScriptableObject (or added to the existing `TerrainGenerationSettings` asset) so they can be tuned without recompilation.

---

## 8. Per-Region Shaping

### Extension to `SDFTerrainField`

`SDFTerrainField.Sample` currently uses one `TerrainFieldSettings` singleton. Extend it to accept a region type and select/blend settings at the call site:

```csharp
// Called inside TerrainChunkDensitySamplingSystem — biomeContext comes from the chunk component.
// plainSettings, hillSettings, etc. come from singleton lookups done once before the job.
public float Sample(float3 worldPos, in TerrainChunkBiomeContext biomeContext,
                    in TerrainFieldSettings plainSettings,
                    in TerrainFieldSettings hillSettings,
                    in TerrainFieldSettings mountainSettings,
                    NativeArray<SDFEdit> edits)
```

For MVP, the switch simply selects the matching settings struct. Soft blending between adjacent regions is deferred — add it when seams between biome zones become visually objectionable.

### New `SDFMath` Shaping Functions

Add these to `SDFMath.cs` as the biome regions are implemented:

```csharp
// Ridge noise: abs(snoise) remapped — produces sharp mountain ridges.
public static float RidgeNoise(float2 xz, float frequency, uint seed) { ... }

// Dune noise: smooth rolling waves with a slight directional lean.
public static float DuneNoise(float2 xz, float frequency, float2 windDir, uint seed) { ... }

// Sharpen: amplifies peaks, compresses valleys. Inverse of the plains flattening exponent.
// Use ElevationExponent < 1 in TerrainFieldSettings instead of a separate function where possible.

// FlattenTowards: lerp height toward a target — used for wetlands, basins, shorelines.
public static float FlattenTowards(float height, float targetHeight, float strength) { ... }

// BasinShape: circular depression for crater/basin regions. Returns a height offset.
public static float BasinShape(float2 xz, float2 centre, float radius, float depth) { ... }
```

---

## 9. Per-Region Terrain Profiles

These are `TerrainFieldSettings` values per region type. Store as a small blob or as individual singletons. Do not hardcode — author in a ScriptableObject.

| Region | BaseHeight | LowAmp | Exponent | Notes |
|---|---|---|---|---|
| Plains | 0 | 5.0 | 1.6 | existing defaults — already tuned |
| Hills | 0 | 8.0 | 1.2 | broader undulation, less flattening |
| Mountains | 0 | 24.0 | 0.7 | high amplitude, sharpening exponent, add RidgeNoise pass |
| DesertBasin | −4 | 3.0 | 1.4 | lower base height, add DuneNoise, BasinShape subtraction |
| FissureZone | 0 | 6.0 | 1.3 | plains-ish base + SDF crack operators on top |

---

## 10. Rare Feature Events

`FissureZone` and crater/burial events are driven by `Weirdness` and implemented as **SDF operators applied after the base height field** — the same pattern as `SDFEdit` (add/subtract spheres) but procedurally generated rather than player-triggered.

They tie into the existing structures pipeline (`Scripts/DOTS/Structures/`):

```
Weirdness > 0.80 at chunk centre
  → TerrainRegionType = FissureZone
  → density sampling adds CrackSDF subtraction along a seeded bearing line
  → structure placement reads FissureZone tag → may anchor a relic or ruin at the fissure terminus
```

### SDF Operators Needed

```csharp
// Long linear crack — carves density along a line segment with a tapered radius.
public static float CrackSDF(float3 worldPos, float3 lineStart, float3 lineEnd,
                              float width, float depth) { ... }

// Circular impact crater — depression + raised rim + optional radial cracks.
public static float CraterSDF(float3 worldPos, float3 centre,
                               float innerRadius, float rimRadius, float depth) { ... }
```

Rare feature placement (which chunks get a fissure vs crater vs nothing) is determined by seeded hash on the chunk coordinate — same approach as the existing structure anchor planning in `StructurePlacementSystem`.

---

## 11. Integration Touch-Points

| System | Change |
|---|---|
| `TerrainChunkStreamingSystem` | Call `WorldFieldSampler.Sample` + `.Classify` when spawning a chunk; write `TerrainChunkBiomeContext` |
| `TerrainChunkDensitySamplingSystem` | Read `TerrainChunkBiomeContext`; select per-region `TerrainFieldSettings` before running the density job |
| `TreePlacementGenerationSystem` | Read `TerrainChunkBiomeContext.Region`; apply per-region density scalar and species rules |
| `RockPlacementGenerationSystem` | Same pattern as trees |
| `GrassChunkGenerationSystem` | Read `TerrainChunkBiomeContext.Region`; map to `BiomeGrassRuleSet` (per `BIOME_GRASS_STREAMING_MVP_PLAN.md`) |
| `TerrainBootstrapAuthoring` | Expose per-region `TerrainFieldSettings` in the inspector; for now default all regions to plains settings |

---

## 12. Performance Contract

- `WorldSample` is computed **once per chunk at spawn**, not per frame, not per voxel.
- Classification is a handful of float comparisons — negligible.
- The density sampling job reads `TerrainChunkBiomeContext` as a read-only component. No structural changes inside the job.
- Adding per-region settings selection adds one `switch` statement per density sample call — Burst-compiled, negligible overhead vs the noise evaluation.
- Soft blending (evaluating two region profiles and lerping) doubles the shaping cost per voxel. Defer until region seams are visually objectionable.

---

## 13. Implementation Order (SPEC → TEST → CODE)

Follow the project's review-gated sequence. Stop after each step and wait for review.

1. **`WorldSample` struct + `TerrainChunkBiomeContext` component** — types only, no systems
2. **`WorldFieldSampler.Sample` tests** — validate field independence, seed stability, [0,1] range
3. **`WorldFieldSampler.Sample` implementation**
4. **`WorldFieldSampler.Classify` tests** — each threshold produces the expected region
5. **`WorldFieldSampler.Classify` implementation**
6. **Wire into `TerrainChunkStreamingSystem`** — write context on chunk spawn; log region in debug mode
7. **Wire into `TerrainChunkDensitySamplingSystem`** — select plains settings for all regions initially (world looks the same but the plumbing is in place)
8. **Author per-region `TerrainFieldSettings`** in inspector; test Hills and Mountains visually
9. **`SDFMath.RidgeNoise` + `SDFMath.DuneNoise`** — unit tests first
10. **Wire Mountains region** to RidgeNoise pass; **DesertBasin region** to DuneNoise pass
11. **Biome-selective surface scatter** — trees and rocks read `TerrainChunkBiomeContext.Region`
12. **Rare features** — `CrackSDF`, `CraterSDF`, `FissureZone` region shaping

---

## 14. Test Plan

### EditMode

- `WorldFieldSampler_Fields_AreInRange_ZeroToOne` — all five fields stay in `[0, 1]`
- `WorldFieldSampler_SameSeed_SamePosition_SameOutput` — deterministic
- `WorldFieldSampler_DifferentFields_AreNotCorrelated` — temperature and moisture differ at the same point
- `WorldFieldSampler_Classify_HighUplift_LowErosion_ReturnsMountains`
- `WorldFieldSampler_Classify_HighWeirdness_ReturnsFissureZone`
- `WorldFieldSampler_Classify_Default_ReturnsPlains`
- `SDFMath_RidgeNoise_IsNonNegative` — ridge noise is always ≥ 0
- `SDFMath_CrackSDF_AtLineStart_IsNegative` — interior of crack is solid removal

### PlayMode / Manual

- Traverse world with debug overlay showing `TerrainRegionType` per chunk — confirm spatial coherence (no salt-and-pepper region switching)
- Mountains region produces visibly sharper, taller terrain than Plains at same seed
- FissureZone chunks contain visible SDF cracks with no mesh artefacts at crack edges
- Re-entering previously visited chunks regenerates identical region classification

---

## 15. Acceptance Criteria

- Each chunk has a `TerrainChunkBiomeContext` written at spawn; downstream systems do not recompute it.
- Plains region produces terrain identical to the current `SdLayeredGround` baseline (no regression).
- At least two additional region types (Hills and Mountains) produce visibly distinct terrain shapes.
- Surface scatter (trees and rocks) applies biome-selective rules when `TerrainChunkBiomeContext` is present.
- World field sampling is deterministic: same `(worldSeed, chunkCoord)` always yields the same `WorldSample`.
- No per-voxel world field evaluation — density job profiler shows no regression vs current baseline.

---

## 16. Related Documents

| Document | Relationship |
|---|---|
| [`AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md`](TERRAIN_ECS_NEXT_STEPS_SPEC.md) | Active SDF pipeline this spec extends |
| [`AI/BIOME_GRASS_STREAMING_MVP_PLAN.md`](BIOME_GRASS_STREAMING_MVP_PLAN.md) | Grass system that consumes `TerrainChunkBiomeContext.Region` |
| [`AI/MVP_VISTA_MOMENT_SPEC.md`](MVP_VISTA_MOMENT_SPEC.md) | Phase 1 prerequisite — must be deliverable before this work starts |
| [`AI/STRUCTURE_PLACEMENT/STRUCTURE_PLACEMENT_SPEC.md`](STRUCTURE_PLACEMENT/STRUCTURE_PLACEMENT_SPEC.md) | Structure pipeline that rare features (fissures, craters) integrate with |
| [`Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md`](../Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md) | Original archived vision — temperature/moisture/elevation concept superseded by this spec |
