# Plains Terrain — Noise Algorithm and Starting Values
_Status: COMPLETE — all values adopted by TERRAIN_PLAINS_TREES_MVP_CHECKLIST.md_
_Last updated: 2026-04-10_

---

## 1. Purpose

Specify the noise algorithm, seed strategy, and concrete starting values required to implement:

- `SDFMath.SdLayeredGround()` (checklist Step A3)
- `TerrainFieldSettings` plains defaults (checklist Step A2)
- `TreePlacementGenerationSystem` plains placement parameters (checklist Step B2)
- Phase A and Phase B test bounds (checklist Steps A7, B4)

---

## 2. Noise Function Choice

**Use `Unity.Mathematics.noise.snoise(float2)`**

Namespace: `using static Unity.Mathematics.noise;`

### Rationale

| Property | `snoise` (Simplex) | `cnoise` (Classic Perlin) |
|---|---|---|
| Grid artifacts at low frequency | None — isotropic | Visible axis-aligned banding |
| Returns 0 at integer coords | No | Yes — dead zones every world unit |
| Burst-compatible | Yes | Yes |
| Heap allocation | None | None |
| Already validated in project | Yes — `MathematicsTest.cs:87` | Yes — `MathematicsTest.cs:77` |
| Output range | approximately ±0.9 | approximately ±0.7 |

Simplex is preferred for terrain because its isotropy produces rounder, more natural landforms. Classic Perlin's integer-boundary zeros create subtle flat-line artifacts that are visible at gameplay scale.

**Do not use `Mathf.PerlinNoise`** — it is not Burst-safe and repeats at integer boundaries.

---

## 3. Seed Offset Strategy

`snoise` has no seed parameter. Deterministic per-world, per-layer independence is achieved by adding a large world-space offset derived from the seed before sampling.

### Helper function — add to `SDFMath.cs`

```csharp
/// <summary>
/// Produces a stable world-space float2 offset for a given seed and layer index.
/// Used to give each noise layer an independent, non-overlapping sample region.
/// </summary>
private static float2 SeedLayerOffset(uint seed, uint layer)
{
    // Mix seed and layer with distinct primes to produce independent streams.
    var hx = (seed ^ (layer * 2654435761u)) * 0x9e3779b9u;
    var hz = (seed ^ (layer * 1013904223u)) * 0x6c62272eu;
    // Map upper 24 bits to a [0, 500) float range.
    // 500 world units >> any visible terrain region — prevents cross-seed correlation.
    const float scale = 500f / 16777216f;
    return new float2((hx >> 8) * scale, (hz >> 8) * scale);
}
```

### Why 500-unit offset range

The project's streaming radius is on the order of 3–10 chunks at ~15 world units each = 45–150 world units visible at once. An offset range of 500 units guarantees that two different seeds never share a visually overlapping terrain region, and two different layers never correlate even at the lowest frequencies used (0.004 → one feature per 250 units).

---

## 4. `SdLayeredGround` Full Implementation

**File:** `Assets/Scripts/DOTS/Terrain/SDF/SDFMath.cs`

Replace the stub from the checklist with this complete implementation:

```csharp
using static Unity.Mathematics.noise;  // add to file-level usings

/// <summary>
/// Deterministic layered-noise ground function. Replaces SdGround for biome-aware terrain.
/// All sampling is in world space — never restart at chunk origin.
/// </summary>
/// <param name="worldPos">World-space sample position.</param>
/// <param name="settings">Plains (or other biome) terrain field settings.</param>
/// <param name="seed">World seed from TerrainGenerationContext.</param>
public static float SdLayeredGround(float3 worldPos, in TerrainFieldSettings settings, uint seed)
{
    var xz = worldPos.xz;

    // Sample three independent layers using per-layer seed offsets.
    var low  = snoise((xz + SeedLayerOffset(seed, 0u)) * settings.ElevationLowFrequency)
               * settings.ElevationLowAmplitude;
    var mid  = snoise((xz + SeedLayerOffset(seed, 1u)) * settings.ElevationMidFrequency)
               * settings.ElevationMidAmplitude;
    var high = snoise((xz + SeedLayerOffset(seed, 2u)) * settings.ElevationHighFrequency)
               * settings.ElevationHighAmplitude;

    var combined = low + mid + high;

    // Redistribute elevation to shape the biome character:
    //   exponent > 1  → flatten plains, widen valleys (compresses mid-range toward zero)
    //   exponent = 1  → no redistribution
    //   exponent < 1  → sharpen peaks, emphasise mountains (deferred to future biomes)
    var maxPossible = settings.ElevationLowAmplitude
                    + settings.ElevationMidAmplitude
                    + settings.ElevationHighAmplitude;
    maxPossible = math.max(maxPossible, 1e-5f);

    var normalized    = combined / maxPossible;                               // → [-1, 1]
    var redistributed = math.sign(normalized)
                      * math.pow(math.abs(normalized), settings.ElevationExponent);
    var elevation     = redistributed * maxPossible;

    // GlobalHeightOffset from TerrainGenerationContext is baked into settings.BaseHeight
    // by the bootstrap before the field struct is constructed.
    var height = settings.BaseHeight + elevation;
    return worldPos.y - height;
}
```

### Notes on the redistribution formula

`sign(x) * pow(abs(x), exp)` applied to a normalized value:

| Normalized input | exp = 1.0 | exp = 1.6 (plains) |
|---|---|---|
| ±1.0 (peak/valley) | ±1.0 | ±1.0 (peaks preserved) |
| ±0.5 (moderate slope) | ±0.5 | ±0.38 (compressed inward) |
| ±0.2 (gentle roll) | ±0.2 | ±0.12 (further compressed) |
| ≈ 0 (flat) | ≈ 0 | ≈ 0 (flat is widened) |

At exp = 1.6, mid-range values are compressed toward zero. This widens flat traversable areas while leaving rare peaks at their full amplitude — the desired plains character.

---

## 5. Plains Terrain Starting Values

These are the defaults for `TerrainFieldSettings` when the biome is Plains.

```csharp
// TerrainBootstrapAuthoring inspector defaults for plains
new TerrainFieldSettings
{
    BaseHeight = 0f,

    // Low layer — broad landforms, ~250 world units per feature
    ElevationLowFrequency  = 0.004f,
    ElevationLowAmplitude  = 5.0f,

    // Mid layer — gentle rolls, ~55 world units per feature
    ElevationMidFrequency  = 0.018f,
    ElevationMidAmplitude  = 1.2f,

    // High layer — subtle surface texture, ~14 world units per feature
    ElevationHighFrequency = 0.07f,
    ElevationHighAmplitude = 0.25f,

    // Redistribution — moderate flattening for plains
    ElevationExponent = 1.6f,

    // Unused in plains MVP — leave zero
    MoistureFrequency  = 0f,
    MoistureAmplitude  = 0f,
    RuggednessFrequency = 0f,
    RuggednessAmplitude = 0f,
}
```

### Expected height profile at these values

| Metric | Expected range |
|---|---|
| Max possible amplitude | 5.0 + 1.2 + 0.25 = 6.45 units |
| Height range after redistribution | approximately ±4.5 units |
| Typical mid-terrain variance (flat area) | ±1.5 units or less |
| Gentle visible rolls | ±2.5–4.0 units |
| Rare elevated shelves | up to ±6.0 units |

This is intentionally conservative for a 16-voxel chunk at 1 unit/voxel (15-unit span). The terrain should feel wide and walkable with occasional gentle variation visible over 3–5 chunk distances.

### Tuning guidance

These values are starting points for iteration. Adjust in this order:

1. `ElevationLowAmplitude` — overall height scale. Increase for bolder terrain, decrease to flatten.
2. `ElevationLowFrequency` — how wide the landforms are. Lower = broader = flatter-feeling plains.
3. `ElevationExponent` — redistribution strength. Range 1.0 (none) to 2.5 (very flat with prominent peaks).
4. `ElevationMidAmplitude` — secondary roll visibility. Keep at 20–25% of low amplitude for plains.
5. `ElevationHighAmplitude` — surface grain. Keep at 4–5% of low amplitude for plains.

Do not adjust `ElevationMidFrequency` or `ElevationHighFrequency` before getting the amplitudes right. Frequency changes alter the spatial scale of features; amplitude changes control their visibility.

---

## 6. Plains Tree Placement Starting Values

These fill the `[REC-2 FILL]` markers in `TreePlacementGenerationSystem` (checklist Step B2).

### Candidate grid

```csharp
const float MinTreeSpacing = 5.0f;   // world units — minimum distance between any two trees
const float CellJitterRadius = 1.5f; // max random offset per cell (30% of cell size)
```

For a 15-unit chunk (16 voxels at voxelSize=1), this produces a 3×3 candidate grid = 9 raw candidates per chunk before slope and probability filtering.

### Jitter seed function

```csharp
// Add to TreePlacementGenerationSystem or a shared utility
private static float2 CandidateJitter(uint worldSeed, int3 chunkCoord, int cellX, int cellZ)
{
    var h = worldSeed;
    h ^= (uint)chunkCoord.x * 2654435761u;
    h ^= (uint)chunkCoord.z * 1013904223u;
    h ^= (uint)cellX * 374761393u;
    h ^= (uint)cellZ * 668265263u;
    h *= 0x9e3779b9u;

    var jx = ((h >> 8) & 0xFFFFu) * (1f / 65535f) * 2f - 1f; // → [-1, 1]
    var jz = ((h >> 20) & 0xFFFFu) * (1f / 65535f) * 2f - 1f;
    return new float2(jx, jz) * CellJitterRadius;
}
```

### Acceptance thresholds

```csharp
const float PlainsSlopeMinNormalY = 0.85f;  // reject if dot(normal, up) < this (~32° max slope)
const float PlainsProbability     = 0.35f;  // 35% of slope-valid candidates are accepted
```

### Probability noise evaluation

```csharp
// In the candidate accept/reject loop:
var probNoise = snoise((candidate.xz + SeedLayerOffset(worldSeed, 3u)) * 0.06f);
var normalizedProb = probNoise * 0.5f + 0.5f; // → [0, 1]
if (normalizedProb > PlainsProbability) continue; // reject
```

Layer index 3 is reserved for tree probability so it never correlates with terrain layers 0–2.

### Expected placement density

| Metric | Expected value |
|---|---|
| Raw candidates per chunk | 9 (3×3 grid) |
| After slope filter (flat plains) | ~7–9 |
| After probability filter (35%) | ~2–4 trees per chunk |
| Min spacing enforcement loss | ~0–1 (low density, rarely triggers) |
| Final accepted trees per chunk | 2–4 (sparse — correct for plains) |

---

## 7. Test Bounds

These fill the test acceptance criteria marked `[REC-2 FILL]` in the checklist.

### Phase A — `TerrainLayeredNoiseTests.cs`

**Plains flatness test:**
```csharp
// Sample 100 evenly-spaced points across a 3×3 chunk area
// Assert standard deviation of height values < 3.5f
// (Flatter than hills target of ~6.0f, tighter than mountains target of ~10.0f)
const float MaxPlainsHeightStdDev = 3.5f;
```

**Seam continuity tolerance:**
```csharp
// Samples at the same world position from different chunk origins must agree within float epsilon
const float SeamTolerance = 1e-4f;
```

### Phase B — `TreePlacementTests.cs`

**Sparsity bounds for plains:**
```csharp
// Per 15×15-unit chunk with 9 candidates
const int MinPlainsTreesPerChunk = 0;  // occasional empty chunk is valid (low probability + all filtered)
const int MaxPlainsTreesPerChunk = 6;  // more than 6 reads as too dense for plains
```

**Minimum spacing assertion:**
```csharp
const float MinSpacingAssertTolerance = MinTreeSpacing - 0.01f; // 4.99f
// Assert no two accepted candidates are closer than this
```

---

## 8. Summary of All REC-2 Fills

| Checklist marker | Value or reference |
|---|---|
| Noise algorithm | `Unity.Mathematics.noise.snoise(float2)` |
| Seed offset strategy | `SeedLayerOffset(seed, layer)` — see §3 above |
| `SdLayeredGround` implementation | §4 above — complete, copy directly |
| `ElevationLowFrequency` default | `0.004f` |
| `ElevationLowAmplitude` default | `5.0f` |
| `ElevationMidFrequency` default | `0.018f` |
| `ElevationMidAmplitude` default | `1.2f` |
| `ElevationHighFrequency` default | `0.07f` |
| `ElevationHighAmplitude` default | `0.25f` |
| `ElevationExponent` default | `1.6f` |
| Plains min tree spacing | `5.0f` world units |
| Plains slope threshold | `normalY >= 0.85f` |
| Plains probability threshold | `0.35f` |
| Phase A flatness variance bound | std dev `< 3.5f` over 100 samples |
| Phase B sparsity range | `0–6` trees per chunk |
| Phase B spacing assertion | `>= 4.99f` between any two accepted candidates |
