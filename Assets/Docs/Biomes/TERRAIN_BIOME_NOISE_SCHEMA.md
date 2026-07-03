# Terrain Biome Noise Schema
_Status: DESIGN - schema for the active SDF pipeline_
_Last updated: 2026-04-10_

---

## 1. Purpose

Define the concrete data model required to implement the biome-aware terrain behavior described in:

- `TERRAIN_STRATEGY_PLAN.md`
- `TERRAIN_BIOME_NOISE_SPEC.md`

This schema covers ECS runtime components, authoring/config data, deterministic seed inputs, and migration from the current minimal SDF field settings.

---

## 2. Schema Principles

- Serve the active SDF + Surface Nets terrain path only.
- Keep world-generation settings centralized and deterministic.
- Separate global generation controls from per-biome rule data.
- Prefer world-space continuous fields over chunk-local procedural state.
- Allow future extension without forcing immediate complexity into the first implementation pass.

---

## 3. Current State and Migration Target

### 3.1 Current Runtime Settings

The current runtime terrain field singleton is:

```csharp
public struct SDFTerrainFieldSettings : IComponentData
{
    public float BaseHeight;
    public float Amplitude;
    public float Frequency;
    public float NoiseValue;
}
```

This is sufficient for prototype terrain, but not for biome-aware terrain.

### 3.2 Migration Goal

Replace the current one-structure prototype shape with a small set of explicit runtime structures:

- one singleton for global terrain generation settings
- one singleton for biome lookup / blending settings
- one blob-backed rule table for biome-specific terrain shaping
- optional per-chunk cached biome metadata when needed for downstream systems

---

## 4. Proposed Runtime ECS Schema

## 4.1 `TerrainGenerationContext`

Purpose:

- Global deterministic inputs for terrain sampling
- Versioning hook for controlled generation changes

Proposed shape:

```csharp
public struct TerrainGenerationContext : IComponentData
{
    public uint WorldSeed;
    public uint GenerationVersion;
    public float GlobalHeightOffset;
}
```

Notes:

- `WorldSeed` is required for deterministic field evaluation.
- `GenerationVersion` allows intentional algorithm changes without ambiguity.
- `GlobalHeightOffset` replaces the old semantic use of `NoiseValue` as a global bias.

## 4.2 `TerrainFieldSettings`

Purpose:

- Global controls for the continuous fields used by the SDF sampler

Proposed shape:

```csharp
public struct TerrainFieldSettings : IComponentData
{
    public float BaseHeight;

    public NoiseType ElevationNoiseType;
    public float ElevationLowFrequency;
    public float ElevationLowAmplitude;
    public float ElevationMidFrequency;
    public float ElevationMidAmplitude;
    public float ElevationHighFrequency;
    public float ElevationHighAmplitude;

    public NoiseType MoistureNoiseType;
    public float MoistureFrequency;
    public float MoistureAmplitude;

    public NoiseType RuggednessNoiseType;
    public float RuggednessFrequency;
    public float RuggednessAmplitude;

    public float ElevationExponent;
    public float MountainBoost;
    public float BiomeBlendDistance;
}
```

Notes:

- This intentionally separates `Elevation`, `Moisture`, and `Ruggedness`.
- The first implementation pass may ignore some fields while preserving the schema.
- The existing `NoiseType` enum can be reused initially, but only supported runtime values should be honored by the active SDF pipeline.

## 4.3 `TerrainBiomeLookupSettings`

Purpose:

- Global thresholds and blending knobs for biome classification

Proposed shape:

```csharp
public struct TerrainBiomeLookupSettings : IComponentData
{
    public float SeaLevel;
    public float BeachBandHeight;
    public float MountainStart;
    public float MountainPeak;

    public float DryThreshold;
    public float HumidThreshold;
    public float RuggedThreshold;

    public float BlendSharpness;
}
```

Notes:

- This structure describes lookup policy, not the biome rules themselves.
- It should stay compact and purely numeric so Burst jobs can read it directly.

## 4.4 `TerrainBiomeRuleTable`

Purpose:

- Blob-backed per-biome terrain shaping rules used during field evaluation and downstream visualization/gameplay systems

Proposed shape:

```csharp
public struct TerrainBiomeRuleTable : IComponentData
{
    public BlobAssetReference<TerrainBiomeRuleBlob> Rules;
}

public struct TerrainBiomeRuleBlob
{
    public BlobArray<TerrainBiomeRule> Values;
}

public struct TerrainBiomeRule
{
    public byte BiomeId;
    public NoiseType PrimaryNoiseType;
    public float LowFrequencyWeight;
    public float MidFrequencyWeight;
    public float HighFrequencyWeight;
    public float ElevationMultiplier;
    public float ElevationExponent;
    public float RuggednessMultiplier;
    public float RidgeStrength;
    public float DetailSuppression;
    public float MoistureBias;
    public float TemperatureBias;
    public float BasinBias;
    public float DirectionalWarpStrength;
    public float TerraceStrength;
}
```

Notes:

- A blob table keeps per-biome rules compact and shareable.
- `BiomeId` should be stable and versioned through content, not inferred from enum order unless that order is formally locked.
- `PrimaryNoiseType` is optional but useful when certain biomes need specialized treatment such as cellular desert detail or simplex mountain shaping.
- `LowFrequencyWeight`, `MidFrequencyWeight`, and `HighFrequencyWeight` expose the relative recipe mix described in the behavior spec.
- `TemperatureBias` is included so snow/alpine logic does not need to be represented as mountains plus a material swap.
- `BasinBias`, `DirectionalWarpStrength`, and `TerraceStrength` are optional hooks for swamp, desert, and corrupted-biome shaping.

## 4.5 `TerrainChunkBiomeState`

Purpose:

- Optional per-chunk cached metadata for downstream systems such as grass, props, VFX, diagnostics, or future persistence

Proposed shape:

```csharp
public struct TerrainChunkBiomeState : IComponentData
{
    public byte DominantBiomeId;
    public float AverageMoisture;
    public float AverageRuggedness;
    public float AverageElevation;
}
```

Notes:

- This is not required for the first field-evaluation pass.
- It becomes useful once biome-aware downstream systems need chunk-level summaries.

## 4.6 `TerrainChunkGenerationStamp`

Purpose:

- Explicit identity of what inputs generated a chunk

Proposed shape:

```csharp
public struct TerrainChunkGenerationStamp : IComponentData
{
    public uint WorldSeed;
    public uint GenerationVersion;
    public int3 ChunkCoord;
}
```

Notes:

- This supports deterministic tests, debugging, and future persistence hooks.
- It should be attached when a chunk is spawned or first sampled.

---

## 5. Authoring / Config Schema

## 5.1 `TerrainBiomeNoiseProfile` ScriptableObject

Purpose:

- Authoring surface for global terrain generation configuration
- Source of truth loaded into runtime ECS singleton data

Proposed contents:

- `WorldSeed`
- `GenerationVersion`
- `BaseHeight`
- Global elevation layer settings
- Moisture settings
- Ruggedness settings
- Lookup settings
- Reference to biome rule asset

Recommended role:

- Replace bootstrap-only tuning as the long-term source of generation settings.
- Allow bootstrap authoring to override only for test scenes or debugging.

## 5.2 `TerrainBiomeRuleSet` ScriptableObject

Purpose:

- Authoring asset for biome rule definitions

Proposed contents per biome:

- biome id / name
- biome archetype label (Plains, Forest, Desert, SnowAlpine, etc.)
- terrain shaping multipliers
- low/mid/high frequency recipe weights
- moisture bias
- optional temperature bias
- ridge strength
- optional basin bias, directional warp, terrace strength
- material/color integration hooks for downstream systems

Runtime conversion:

- Converted at startup into `TerrainBiomeRuleTable` blob data.

## 5.3 `TerrainBootstrapAuthoring` Transitional Role

Transitional recommendation:

- Keep `TerrainBootstrapAuthoring` for chunk spawning and scene bootstrap.
- Reduce its long-term terrain-parameter responsibility.
- Eventually replace inline scalar settings with either:
  - a reference to `TerrainBiomeNoiseProfile`, or
  - a debug override block used only in test scenes

---

## 6. Field Semantics

### 6.1 Elevation Fields

- `ElevationLowFrequency`: continent-scale or broad landform control
- `ElevationMidFrequency`: rolling hills and regional undulation
- `ElevationHighFrequency`: local detail and roughness support

### 6.2 Moisture Field

- Must use a separate seed stream or offset from elevation
- Must not be derived by reusing the same exact sampled field as elevation

### 6.3 Ruggedness Field

- Controls terrain harshness independent of absolute elevation
- Supports making mountains sharper without simply multiplying height everywhere

### 6.4 Temperature Bias

- Optional control used mainly by snow/alpine or cold-region biome logic
- Should bias biome lookup rather than replacing elevation logic

### 6.5 Basin Bias

- Optional shaping input for lowland, swamp, or basin-heavy biomes
- Helps create wet depressions and interrupted flats without full erosion simulation

### 6.6 Directional Warp Strength

- Optional shaping input mainly for desert or stylized magical biomes
- Supports dune-like or flow-like directional terrain deformation

### 6.7 Terrace Strength

- Optional shaping input for badlands, magical, or corrupted terrain
- Should remain disabled by default for natural biomes unless intentionally authored

### 6.8 Global Height Offset

- Replaces the old `NoiseValue` role as a constant vertical bias
- Must not be named as noise in new schema because it is not sampled noise

---

## 7. Recommended Naming Decisions

- Prefer `TerrainFieldSettings` over `SDFTerrainFieldSettings` once migration is complete, because the structure describes terrain generation rather than SDF mechanics.
- Prefer `GlobalHeightOffset` over `NoiseValue`.
- Prefer `BiomeRuleTable` or `TerrainBiomeRuleTable` for runtime blobs.
- Prefer stable numeric biome ids over string comparisons in Burst paths.

---

## 8. Backward Compatibility / Migration Plan

### Stage 1

- Introduce new schema alongside existing `SDFTerrainFieldSettings`.
- Populate new structures from bootstrap or profile asset.
- Keep old fields available while the sampler is migrated.

### Stage 2

- Update `SDFTerrainField` / `SDFMath` to consume `TerrainGenerationContext`, `TerrainFieldSettings`, and biome rule data.
- Mark `SDFTerrainFieldSettings` as legacy compatibility state.

### Stage 3

- Remove `Amplitude`, `Frequency`, and `NoiseValue` from primary authoring flow.
- Keep only compatibility adapters if tests or migration scenes still rely on them.

---

## 9. Testing Hooks Implied by Schema

The schema must support tests for:

- same seed + same settings -> same terrain field values
- different `GenerationVersion` -> intentionally different outputs where expected
- different biome rules -> distinguishable terrain shapes
- same world-space border samples across neighboring chunks -> identical field values

`TerrainChunkGenerationStamp` and blob-backed biome rules are included partly to make these tests explicit and easy to reason about.

---

## 10. Minimal First Implementation Subset

The first implementation pass does not need the full schema active.

Minimum required subset:

- `TerrainGenerationContext`
- `TerrainFieldSettings`
- `TerrainBiomeLookupSettings`
- `TerrainBiomeRuleTable`

Deferred until needed:

- `TerrainChunkBiomeState`
- more advanced temperature/erosion fields
- persistence-specific metadata beyond `TerrainChunkGenerationStamp`

Recommended biome presets to author early even if some fields are initially ignored:

- Plains
- Hills / Grassland
- Forest
- Mountains
- Desert
- Snow / Alpine

---

## 11. Schema Summary

Recommended runtime set:

- `TerrainGenerationContext`
- `TerrainFieldSettings`
- `TerrainBiomeLookupSettings`
- `TerrainBiomeRuleTable`
- `TerrainChunkGenerationStamp`
- optional `TerrainChunkBiomeState`

Recommended authoring set:

- `TerrainBiomeNoiseProfile`
- `TerrainBiomeRuleSet`
- transitional `TerrainBootstrapAuthoring` overrides

This schema is intentionally small enough to implement incrementally, but broad enough to support the terrain behavior described by the spec without another structural redesign.
