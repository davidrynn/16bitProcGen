# Scatter LOD Speed Bias Spec (Airborne Detail Drop)

**Status:** DESIGN
**Last Updated:** 2026-06-26
**Owner:** Terrain / Rendering
**Extends:** [SURFACE_SCATTER_LOD_SPEC.md](SURFACE_SCATTER_LOD_SPEC.md)

---

## 1. Purpose

Shrink the scatter LOD swap distance as player speed rises, pushing more tree/rock
instances to their far (low-poly) mesh during fast flight. The scene is vertex-bound
with ~92% of frame verts coming from scatter ([../RENDER_PERF_PROFILE_REPORT.md](../../Rendering/RENDER_PERF_PROFILE_REPORT.md)),
so a smaller near-band directly cuts the bottleneck. The fidelity loss is perceptually
cheap: at high airborne speed the player cannot resolve near-mesh detail anyway (motion
blur / rapid parallax), so detail bought there is wasted.

This is a pure threshold modifier on the existing per-frame, per-instance LOD selection
in `TreeChunkRenderSystem` / `RockChunkRenderSystem`. It adds no new iteration, buffers,
or draw calls.

## 2. Why this is nearly free (architecture note)

The base scatter LOD path already:
- Rebuilds all instance matrix buckets every frame (`ResetPendingVariantState` → re-query).
- Selects near vs. far per instance via stateless `SurfaceScatterLodUtility.SelectLodLevel(distanceSq, swapDistance)`.

So biasing `swapDistance` per frame costs one velocity read plus arithmetic; the selection
comparison and bucket rebuild happen regardless. There is **no per-frame switching cost** of
the kind that makes dynamic LOD expensive in GameObject/LODGroup pipelines — nothing is
re-uploaded, and statelessness means no thrash. (See SURFACE_SCATTER_LOD_SPEC §4.1.)

## 3. Scope

- `TreeChunkRenderSystem` / `RockChunkRenderSystem`: compute a speed-biased effective swap
  distance once per frame in `OnUpdate`, feed it into the existing `SelectLodLevel` calls.
- `TreeRenderConfig` / `RockRenderConfig`: add speed-bias tuning fields (below).
- `TreeVisualBootstrap` / `RockVisualBootstrap`: inspector fields for the above.
- Shared pure bias logic in `DOTS.Terrain.SurfaceScatter` (`SurfaceScatterLodUtility`),
  testable without a World.

## 4. Non-Goals

- New LOD levels or impostors — still the existing 2-level near/far swap.
- Per-instance velocity (e.g. swaying scatter); speed is the single global player signal.
- Reacting to camera angular velocity / look-around; speed-only for MVP.
- Changing `CulledScatterLod` chunk-level culling.
- Coupling to actual motion-blur post effects; this is independent of whether blur is on.

## 5. Design

### 5.1 Speed signal

Read `PlayerMovementState.Velocity` (a cached `float3` copy of `PhysicsVelocity.Linear`,
written `OrderFirst` in `SimulationSystemGroup` each frame, so it is fresh before the
presentation-group render systems run). Derive a scalar speed.

- **Speed metric:** horizontal speed `length(velocity.xz)` (precedent: `CameraSpeedFeedbackSystem`).
  Rationale: the bias targets fast traversal/flight; vertical-only motion (e.g. a vertical
  thermal) does not sweep scatter past the camera the same way. *(Open question 1 — revisit
  whether full `length(velocity)` reads better in ballistic arcs.)*
- The render systems are managed `SystemBase`, so they can read this directly in `OnUpdate`.
- Source independence: LOD distance is measured from `Camera.main`, speed from the player
  entity. They are the same actor here but distinct sources — speed is treated strictly as a
  **threshold scale**, never as a stand-in for camera distance.

### 5.2 Bias function

Effective swap distance is the configured swap distance scaled down by a smooth ramp over a
speed window:

```
t           = smoothstep(SpeedBiasMinSpeed, SpeedBiasMaxSpeed, horizontalSpeed)   // 0..1
scale       = lerp(1.0, SpeedBiasMinScale, t)                                      // 1 → min
effectiveSwap = LodSwapDistance * scale
```

- Below `SpeedBiasMinSpeed`: `scale == 1`, identical to base behavior (zero regression at rest/walk).
- At/above `SpeedBiasMaxSpeed`: `scale == SpeedBiasMinScale` (near band fully shrunk).
- **Use the smooth ramp, not a hard speed threshold.** A binary "snap everything to far above
  speed X" produces a visible whole-scene pop at the threshold and oscillates when decelerating near
  it. The ramp is the same cost and avoids both. The base LOD path is hysteresis-free precisely
  because buckets rebuild every frame; a continuous bias preserves that property, a step does not.

This is a pure function: `SurfaceScatterLodUtility.ComputeSpeedBiasedSwapDistance(baseSwap, speed, cfg)`.

### 5.3 Config contract

Added to `TreeRenderConfig` / `RockRenderConfig` (and matching bootstrap inspector fields):

| Field | Type | Default | Semantics |
|---|---|---|---|
| `EnableSpeedLodBias` | `bool` | `false` | Master switch. `false` → behavior identical to SURFACE_SCATTER_LOD_SPEC. |
| `SpeedBiasMinSpeed` | `float` | `15` | Speed (m/s) at which biasing begins. Below this, no bias. |
| `SpeedBiasMaxSpeed` | `float` | `40` | Speed (m/s) at which bias saturates. Must be `> SpeedBiasMinSpeed`. |
| `SpeedBiasMinScale` | `float` | `0.4` | Swap-distance multiplier at/above max speed (0..1). Lower = more aggressive far-swap. |

Defaults chosen to overlap the existing camera speed-feel windows (speed-line / FOV thresholds
start ~15 m/s) so detail drop coincides with when the camera already signals "fast." Tune in
Play mode.

### 5.4 Fallback / zero-regression rules

The base LOD fallbacks (SURFACE_SCATTER_LOD_SPEC §4.4) still apply. Additionally:

1. `EnableSpeedLodBias == false` → effective swap = `LodSwapDistance`, byte-for-byte base behavior.
2. `LodSwapDistance <= 0` (LOD disabled) → bias is moot; all instances near regardless of speed.
3. No player / no `PlayerMovementState` found → treat speed as 0 → `scale == 1` (no bias).
4. `SpeedBiasMaxSpeed <= SpeedBiasMinSpeed` (misconfig) → clamp `t` to 0 (no bias) rather than divide-by-zero.

### 5.5 Grounding offset interaction

Unchanged from base spec §4.5: grounding is computed from the mesh actually drawn. Because the
bias only changes *which* instances are far (not the meshes themselves), the existing
"keep far-mesh bounds ≈ near-mesh bounds" authoring rule fully covers any vertical pop. At high
speed a small residual pop is further masked by motion.

## 6. Related Docs

- [SURFACE_SCATTER_LOD_SPEC.md](SURFACE_SCATTER_LOD_SPEC.md) — base distance LOD this extends
- [../RENDER_PERF_PROFILE_REPORT.md](../../Rendering/RENDER_PERF_PROFILE_REPORT.md) — vertex-bound profiling evidence
- `MOVEMENT_PLANNING.md` C2 (speed lines / speed FOV) — the speed-feel windows these defaults align to

## 7. Open Questions (resolve in-ticket)

1. Speed metric: horizontal `xz` vs. full velocity magnitude? Horizontal is the MVP pick (§5.1) — confirm it reads well on steep ballistic arcs.
2. One shared bias config vs. independent tree/rock tuning? Spec allows per-config fields; decide whether to expose both or drive from one source.
3. Should `SpeedBiasMinScale` have a floor tied to the relic/structure LOD so distant landmarks don't pop differently? Likely out of scope, but note interaction.

## 8. Acceptance Criteria

1. EditMode tests pass for the pure bias function: `scale == 1` below min speed, `== SpeedBiasMinScale` at/above max speed, monotonic and continuous between (no step), and misconfig (`max <= min`) yields no bias rather than NaN/throw.
2. With `EnableSpeedLodBias == false`, rendering output and all pre-existing scatter LOD tests are identical to before the change.
3. With bias enabled and far meshes assigned, a Play-mode profiler capture during sustained high-speed airborne movement shows a further scatter vertex drop beyond the static-distance LOD, with no measurable frame-time cost from the bias computation itself.
4. No whole-scene LOD pop or oscillation when crossing or hovering near `SpeedBiasMinSpeed` (validates the smooth ramp).
