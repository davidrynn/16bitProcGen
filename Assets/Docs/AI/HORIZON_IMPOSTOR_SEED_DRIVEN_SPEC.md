# Seed-Driven Far Horizon Impostor Spec

**Status:** DESIGN (Deferred, Low Priority)  
**Phase Fit:** Phase 2 (world streaming + LOD)  
**Last Updated:** 2026-04-09

---

## 1. Purpose

Define a low-cost far-distance horizon system that renders mountains, hills, and sea using a deterministic 2D impostor texture generated from world sampling, not full distant terrain meshes.

This is a visual optimization feature. It is not required for MVP completion.

---

## 2. Why This Exists

Current world goals include larger perceived scale with controlled performance cost.

Rendering real terrain at extreme distance is expensive and unnecessary for gameplay. A horizon impostor gives:
- Strong silhouette readability.
- Stable retro-style composition.
- Large reduction in distant mesh, collider, and draw overhead.

---

## 3. Scope

### In Scope
- Deterministic 360-degree horizon profile generation from world seed and player region.
- Mountain and hill silhouette approximation from low-resolution terrain sampling.
- Sea classification in the far view.
- Infrequent horizon texture rebuilds based on movement and state triggers.
- Crossfade between previous and new horizon texture to avoid visible pops.

### Out of Scope
- Exact visual parity with every distant terrain chunk.
- Real-time per-frame horizon regeneration.
- High-altitude flight-perfect horizon correctness.
- Full weather cloud simulation and volumetric sky replacement.

---

## 4. Design Constraints

- DOTS-first runtime ownership. MonoBehaviour usage limited to bootstrap/authoring references.
- Must remain decoupled from legacy heightmap chunk mesh generation logic.
- Sampling must be deterministic for fixed world seed and sampling cell.
- No Debug.Log in systems. Use DebugSettings log channels.
- Rebuild frequency must be bounded to avoid frame spikes.

---

## 5. Visual Model

Use a three-zone representation:
- Near zone: real terrain chunks (full gameplay).
- Mid zone: existing LOD terrain path (or simplified geometry where available).
- Far zone: skydome/cylinder material using a generated horizon texture.

The far shell follows player XZ position only, preserving an infinite-distance illusion.

---

## 6. Data Model (Proposed)

### 6.1 HorizonImpostorSettings (ScriptableObject)
Location: Assets/Resources/HorizonImpostorSettings.asset

Core fields:
- WorldSampleStartDistance
- WorldSampleEndDistance
- AzimuthSampleCount
- RadialSampleCount
- RebuildCellSizeXZ
- AltitudeRebuildThreshold
- MinRebuildIntervalSeconds
- SeaLevel
- CrossfadeDurationSeconds
- HorizonTextureWidth
- HorizonTextureHeight

### 6.2 HorizonImpostorState (IComponentData)
- uint WorldSeed
- int2 CurrentCenterCellXZ
- float LastRebuildPlayerY
- double LastRebuildTime
- byte RebuildPending

### 6.3 HorizonAzimuthProfileElement (IBufferElementData)
Per azimuth bin:
- float MaxElevationAngle
- float WaterWeight
- byte DominantBiomeId

### 6.4 HorizonTextureResource (managed component)
- Texture2D ActiveTexture
- Texture2D PreviousTexture
- float BlendT

---

## 7. Sampling Strategy (Mountains, Hills, Sea)

For each azimuth angle $\phi_i$:
- Sample radial distances $d_j$ from start to end range.
- Query low-resolution world height estimate $h(d_j, \phi_i)$ from deterministic world function.
- Compute elevation angle relative to player eye height $y_p$:

$$
\alpha_{i,j} = \arctan\left(\frac{h(d_j, \phi_i) - y_p}{d_j}\right)
$$

- Use silhouette envelope:

$$
\alpha_i = \max_j \alpha_{i,j}
$$

- Classify sea contribution using sampled points where $h(d_j, \phi_i) \leq SeaLevel$.
- Choose dominant biome id by weighted sample majority per azimuth bin.

Result: a compact per-direction profile that encodes horizon shape and color context without generating far meshes.

---

## 8. Update Policy (When Image Rebuilds)

No per-frame rebuild.

Trigger a rebuild only when one of the following is true:
- Player enters a new horizon cell: floor(playerXZ / RebuildCellSizeXZ) changed.
- Player altitude changed more than AltitudeRebuildThreshold.
- World seed changed (new world/session).
- Major visual context changed (biome regime shift or weather preset change), if enabled.

Rate limits:
- Respect MinRebuildIntervalSeconds.
- Allow one pending request while sampling is in progress.

Presentation behavior:
- Build new texture off the critical path.
- Crossfade ActiveTexture from previous to new over CrossfadeDurationSeconds.

---

## 9. ECS System Plan (Proposed)

1. HorizonImpostorRequestSystem
- Detect trigger conditions.
- Set RebuildPending when allowed by interval gating.

2. HorizonImpostorSamplingSystem
- Consume RebuildPending.
- Fill HorizonAzimuthProfileElement buffer deterministically.

3. HorizonImpostorTextureBuildSystem
- Convert azimuth profile into horizon texture bands (silhouette, sea, biome tint).
- Update managed texture resource.

4. HorizonImpostorPresentationSystem
- Apply textures and blend parameter to far-shell material.
- Keep far shell centered on player XZ.

---

## 10. Determinism Contract

Given identical:
- WorldSeed
- CurrentCenterCellXZ
- Player sampling height policy
- HorizonImpostorSettings

The generated azimuth profile and resulting texture content must be identical across runs.

Non-deterministic noise/time inputs are excluded from profile generation.

---

## 11. Performance Targets

- Rebuild cadence: typically every large movement cell transition, not continuous.
- Sampling CPU budget target: under 3 ms average on desktop target hardware.
- Texture build budget target: under 2 ms average for configured texture size.
- Steady-state per-frame cost after build: near-zero beyond one material draw.

If budgets are exceeded:
- Reduce AzimuthSampleCount.
- Reduce RadialSampleCount.
- Increase RebuildCellSizeXZ.
- Increase MinRebuildIntervalSeconds.

---

## 12. Seam and Visual Blending Rules

- Use atmospheric fog/haze to mask handoff between mid and far zones.
- Keep far shell unlit or minimally lit to avoid shadow mismatch with gameplay terrain.
- Match palette/tint rules to active biome and time-of-day state.
- Never attach far shell to player Y translation.

---

## 13. Terrain Modification Interaction

MVP behavior for this feature:
- Ignore local terrain edits for immediate horizon updates.
- Horizon stays region-based and may lag local edits until next rebuild trigger.

Future extension:
- Optional local edit influence map that perturbs azimuth bins near camera-facing directions.

---

## 14. Test Plan (When Implemented)

### EditMode
- HorizonDeterminism_SameSeedSameCell_SameProfile
- HorizonDeterminism_DifferentCell_ProfileChanges
- HorizonSeaClassification_SeaLevelBoundaryStable

### PlayMode
- HorizonUpdate_PlayerCrossesCell_RebuildOnce
- HorizonUpdate_Stationary_NoRebuild
- HorizonCrossfade_NoHardPopOnRebuild
- HorizonShell_RecenterXZOnly

### Manual
- Confirm directional silhouette roughly matches visible distant mountains/hills.
- Confirm ocean-facing directions remain water-dominant in far ring.
- Confirm no obvious seam under default fog settings.

---

## 15. Risks and Mitigations

- Risk: silhouette mismatch noticeable at high altitude.
  - Mitigation: add altitude-sensitive rebuild and stronger haze at extreme view heights.

- Risk: visible pop when horizon updates.
  - Mitigation: texture double-buffer plus timed crossfade.

- Risk: sampling stalls on low-end hardware.
  - Mitigation: coarse sample presets and strict rebuild interval gating.

- Risk: coupling to non-authoritative terrain path.
  - Mitigation: sample deterministic world height source directly, independent from mesh chunk state.

---

## 16. Acceptance Criteria

- System produces a deterministic far-horizon texture from seed and player region.
- Mountains/hills/sea directional cues are visually plausible from gameplay camera height.
- Horizon does not rebuild every frame.
- Transition to new horizon texture is smooth (crossfade).
- Feature can remain disabled without affecting core gameplay systems.

---

## 17. Priority and Scheduling Note

This feature is explicitly low priority.

Implement only after current Phase 1 player-critical work is complete and Phase 2 optimization/streaming tasks are actively scheduled.
