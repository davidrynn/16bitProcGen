# Work-set: Relic Grounding & Traversal Safety

**Status:** CURRENT FOCUS — opened 2026-07-21
**Last Updated:** 2026-07-21

Board: [`TICKETS.md`](TICKETS.md)

Opened straight after the Vista Moment work-set closed. Two threads, both surfaced by actually
playing the vista that was just shipped:

1. **Relic grounding** — the hero hand now has a mound (V19), but it meets the terrain in a cliff and
   doesn't look like the ground it sits on. Canonical spec:
   [`../Structures/RELIC_TERRAIN_INTEGRATION_SPEC.md`](../Structures/RELIC_TERRAIN_INTEGRATION_SPEC.md).
2. **Traversal safety** — the player falls through terrain after chained slingshots. Long-standing
   backlog item M5, now with a real diagnosis.

Plus one **blocking terrain bug (U1)** found while costing the destructible-relic work, which is live
today and must be fixed regardless of everything else.

> **New track prefix `U`** — Underground/Vertical terrain, mapping 1:1 to
> [`../Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md`](../Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md).
> Introduced 2026-07-21 because terrain-infrastructure tickets had been landing under unrelated
> prefixes (`M6` under Movement, `W2` under World Power). Override if you'd rather fold it elsewhere.

---

## Blocking

### U1 — Zero-vertex chunks permanently starve the mesh budget `[ ]`

**Severity: high, live today, cheap to fix. Do this first.**

`Assets/Scripts/DOTS/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs:91` — a chunk that meshes to zero
vertices hits `if (!meshBlob.IsCreated) continue;` and never reaches line 119-120 where
`TerrainChunkNeedsMeshBuild` is removed. It is therefore **rescheduled every frame, forever**,
consuming a slot of `MaxMeshRebuildsPerFrame` (default 8).

Invisible today because nearly every chunk contains surface. It becomes fatal the moment vertical
layers exist, since most stacked chunks are fully solid or fully air — a handful of empty chunks
would permanently starve the budget and new chunks would never settle.

**Fix:** remove the tag on the zero-vertex path too (or restructure so the tag removal is
unconditional). Add an EditMode test that a uniform-density chunk settles.

Also a plausible contributor to BUG-017 (perf) if any chunk currently meshes empty.

---

## Group A — Relic grounding

Spec: [`../Structures/RELIC_TERRAIN_INTEGRATION_SPEC.md`](../Structures/RELIC_TERRAIN_INTEGRATION_SPEC.md)

### V19 — Hero hand rubble mound base `[~]` _(pulled from backlog; substantially built 2026-07-21)_

Original framing: *"the hand still reads as floating from spawn."* That is fixed — the hand now
emerges from an authored mound.

**Built:**
- `ArtSource/agony_mound_gen.py` — regenerates staged hand + mound + rubble from the live `AgonyClaw`
  pose, then exports. Idempotent; hash-based noise so output is deterministic and diffable.
  Re-running refits the mound to the pose, so a pose tweak doesn't invalidate the rock.
- Staging captured from the owner's manual placement: `TILT_DEG = 21.2047`,
  `PALM_ANCHOR = (-1.4217, 0, 2.0652)`, `BURIAL_OFFSET = -1.49`. Anchored on the **Palm bone head**,
  which survived the owner's palm-shortening edit (bone 10.86 → 9.27, head unmoved) — so placement
  transfers across master-mesh edits for free.
- `BURIAL_OFFSET` is the burial-depth dial: toward `0` sinks the hand, negative lifts it out.
- Export → `Assets/Models/ColossalHand/ColossalHand_AgonyRelic.fbx` (2475 v / 4806 tris, one mesh,
  one material — the relic path renders exactly one of each per entity).
- Scene: `relic_hand_hero` at **scale 15, yOffset 10** (owner: *"the hand should be above the ground,
  the mound is meant to protrude"*). 691 m footprint, 260 m tall.
- Procedural stone surfacing — `Assets/Shaders/RelicSurface.hlsl` + new `RelicLit` properties, opt-in
  via `_SurfaceStrength` (default 0, so `relic_head` / `stone_outcrop` are untouched).

**Remaining:** V21 + V22 below. Close V19 when those land.

**yOffset ceiling ≈ 35** at scale 15. The mound skirt tucks *inward* as it drops (deliberate, so the
rim reads as a rolled lip rather than a cut plate edge); lifting past 39 exposes it as an overhang.

### V21 — Mound↔terrain seam via corridor flatten mask `[ ]`

The rim currently forms a cliff. Reuse the H3 `WorldStructureMask` capsule-segment falloff (already
mirrored in `WorldStructure.hlsl`, already protecting the spawn→hero sightline) to flatten terrain
under the relic footprint, so the mesh rim meets a known plane.

**Confirm first:** the mask flattens `H`, but near-field `H` in the SDF base is World-Structure
Phase C and is **not yet wired into density sampling**. Verify the flatten actually reaches
`SdLayeredGround` before estimating — if it doesn't, this ticket depends on Phase C.

### V22 — Mound surface parity with terrain `[ ]`

`RelicSurface.hlsl` should call `GroundPaletteMix` from `GroundNoise.hlsl` — the same function
`TerrainLit` uses, from the same `_AtmoGround`/`_AtmoRock` globals — and layer relic strata/weathering
on top, instead of shading over a flat `_BaseColor`.

**Call, never fork** (`GroundNoiseCore.hlsl` one-definition rule, enforced by
`TerrainChunkMaterialContractTests`). **Do not reuse `GroundReliefNormal`** — flat-geometry-only,
fights real mesh normals. Preserve ForwardLit↔DepthOnly dissolve parity and identical
`UnityPerMaterial` layout.

---

## Group B — Traversal safety

### M5 — Harden against high-speed tunneling `[~]` _(pulled from backlog; diagnosed 2026-07-21)_

Player falls through terrain "after a number of slingshots". **Diagnosis complete; fix not chosen.**

**Root cause is compound, and the collider-lag hypothesis was only half right:**

1. **Chain-slingshot velocity is unclamped and escalates geometrically.** `SlingshotLaunchSystem`
   chains as `velocity * 0.85 + aim * impulse * chainBonus` with **no clamp anywhere in the project**:
   55 → 115 → 181 → 250 → 309 m/s over five launches, asymptote **642 m/s**. `ChainCount` resets only
   when the 2 s window expires. This matches "after a number of slingshots" exactly.
2. **Amplifier:** while charging, `Mode == SlingshotCharging`, so `PlayerMovementSystem` takes the
   **air-control** branch (`AirControl = 0.2`, lerp ≈ 0.0033) even standing on the ground. Horizontal
   speed is essentially undamped between launches, so each launch compounds the last.
3. **No CCD.** Unity Physics 1.4 has none; `AngularExpansionFactor = 0`. At 250 m/s the player moves
   **4.2 m per 1/60 s step** through a **1 m** voxel thin open shell.
4. **Pipeline starvation at speed.** `TerrainChunkDensitySamplingSystem` and
   `TerrainChunkMeshBuildSystem` iterate in **arbitrary archetype order** and stop at 8/frame —
   **no player-distance prioritisation** (only the collider system sorts by distance). The chunk
   ahead of the player queues behind hundreds of irrelevant ones, and the collider system's
   near-player exemption can't help because it requires `TerrainChunkMeshData` to exist.
5. Colliders only exist within ~200 m (`ColliderMaxLod = 1`); at 250 m/s that's 0.8 s of coverage.
   The only safety net (`PlayerTerrainSafetySystem`) needs a collider to exist **and** has a 0.5 s
   cooldown, so it rescues at most twice a second.

**Measured, not inferred:** even the **sky-drop alone** is already in tunneling territory —
1.0 → 1.4 m per fixed step at 60–85 m/s terminal descent, before any slingshot. Margin at 55 m/s
(one un-chained launch) is already zero.

**Done this sprint:** three defects fixed in `PlayerFallThroughDiagnosticSystem` (see below), and
both diagnostics enabled in `ProjectFeatureConfig.asset`.

### ✅ VERDICT — instrumented traverse, 2026-07-21: **pipeline starvation, not tunneling**

Owner reproduced a fall-through **on landing**. First snapshot at the moment of breach:

```
Player pos=(-221.88, -3.78, 562.96)  speed=42.3 (h=10.0, y=-41.1)  FallTime=8.400
Player chunk=(-15,37), sdf=-4.62
  ALL NINE CHUNKS: Collider=False, NeedsCollider=False, MeshData=True, NeedsDensity=True
```

**`Collider=False` *and* `NeedsCollider=False`** — no collider existed and none was queued, while
`MeshData=True` and `NeedsDensity=True`. That combination means the chunks were **LOD-demoted while
the player was far away** (`RemoveCollidersOutsideLodPolicy` strips `PhysicsCollider` above
`ColliderMaxLod = 1`) and were **mid-promotion** on arrival: the mesh present was the stale LOD2 one,
the higher-detail density still queued.

**Landing speed was 42.3 m/s** — nowhere near the 250–309 m/s the chain-velocity model predicted.
**Velocity was not the cause of this event.** The player simply outran LOD promotion.

Corroborating latency, from `TerrainColliderTimingSystem` (2,645 builds in the session):

```
Collider built: chunk=(-30,28), latency=14.631s, frames=274
Collider built: chunk=(-21,26), latency=26.838s, frames=617
```

**14–27 s / 274–617 frames** spawn→collider. Not the 1-fixed-step floor — a deeply saturated
backlog, exactly the unprioritised-queue problem (`TerrainChunkDensitySamplingSystem` and
`TerrainChunkMeshBuildSystem` iterate in arbitrary archetype order at 8/frame with **no
player-distance sort**; only the collider stage sorts).

**Revised fix ranking** — the per-step `ColliderCast` I had ranked first **would not have helped**;
there was no collider to cast against.

| Rank | Fix | Cost |
|---|---|---|
| 1 | Player-distance sort on the density/mesh rebuild queues | ~free — directly targets the 14–27 s latency |
| 2 | Bias streaming/LOD centre along velocity (promote before arrival) | ~free |
| 3 | Retain colliders through LOD promotion instead of drop-and-rebuild | needs investigation |
| 4 | Per-fixed-step `ColliderCast` on the player | ~free — different failure mode, still worth it |
| 5 | M7 clamp | wouldn't have prevented this event |

**Also fixed 2026-07-21:** the snapshot's `vs voxel=` came from `gridInfos[0]` — an arbitrary chunk,
so the tunneling threshold was compared against a random chunk's LOD. Now reads the player's own
chunk. (`chunkStride` from `gridInfos[0]` is *safe* and stays — footprint is LOD-invariant, so the
`Player chunk=` coord and the 3×3 grid were always correct. Only `voxelSize` varies by LOD.)

**Candidate fixes, ranked by cost/ceiling:**

| Fix | Cost | Speed ceiling after |
|---|---|---|
| Per-fixed-step `ColliderCast` on the player (predictive, replacing the reactive 0.5 s-cooldown snap-back) | ~free, O(1)/step | **none** |
| Player-distance sort on the density/mesh queues | ~free | ~250 → much higher |
| Bias streaming centre along velocity | ~free | higher |
| Widen near-player collider exemption (1→3 chunks) | low, bursty main thread | high |
| Raise `ColliderMaxLod` 1→2 | **~950 extra `MeshCollider`s** — avoid | — |
| Substep physics 60→120/240 Hz | linear physics cost, **worsens collider staleness** | — |

### M8 — Below-world recovery (kill plane) `[ ]` _(opened 2026-07-21 — highest value/cost ratio in the whole investigation)_

**The fall is unbounded.** In the instrumented run the player passed the surface and kept
accelerating with no floor, no recovery, no respawn:

```
FallTime=90.088   pos.y = -36,094   speed = 842.4 m/s   still accelerating
```

Once through, the run is over. This is independent of *why* they fell, and it converts a
run-ending bug into a hiccup.

**Cost is negligible** — one float compare per frame on a single entity, plus a stored
last-grounded position (one `float3` write per frame). The recovery raycast only runs on the rare
event itself.

**Design notes:**
- Re-seat **and log loudly**. A silent catch would have hidden this entire bug class; the whole
  reason it went undiagnosed is that nothing complained.
- Prefer re-seating to the last grounded position over re-running the sky-drop gate — the drop
  sequence is a narrative beat, not an error handler.
- **Do not hardcode the floor.** Today the terrain slab bottoms out at Y = −7.5 so anything around
  −100 is safe, but ticket **U3** (vertical chunking) moves the world floor. Derive it from the
  streaming window's lowest layer so it stays correct.
- Consider reusing it as the generic "player is somewhere impossible" net rather than a
  fall-specific patch.

### M7 — Chain-slingshot velocity clamp (stopgap) `[ ]`

Owner intent (2026-07-21): *"Ideally speed is progressive based on user builds or ability leveling
but still fast. So a clamp may be ok for now but later get much faster."*

So the clamp is **explicitly a stopgap, not a design decision** — and its value later becomes an
ability-scaling knob rather than a safety limit. Land it to buy stability while the durable fixes
(per-step cast + queue prioritisation) are built, then raise or remove it.

Do not "fix" this by removing chaining — speed-building is wanted.

### M5/M7 supporting work — diagnostic fixes `[x]` _(done 2026-07-21)_

`PlayerFallThroughDiagnosticSystem` had **three** defects that made it lie:

1. **Wrong surface.** Called `SDFMath.SdGround` (legacy sine field) while the density sampler had
   moved to `SdLayeredGround` — `BELOW_SURFACE` was tested against a surface that is never meshed.
   Now builds an `SDFTerrainField` exactly the way `TerrainChunkDensitySamplingSystem` does and calls
   `Sample()`, so the two cannot drift again; also passes the `SDFEdit` buffer, so dug terrain no
   longer reads as solid.
2. **Vertical-only tunneling check.** `abs(velocityY) * dt > voxelSize` stays silent on a slingshot
   near apex (`v.y ≈ 0`, hundreds of m/s horizontal) — precisely the case that tunnels. Now uses full
   velocity magnitude against the **fixed** step read from `FixedStepSimulationSystemGroup.RateManager`,
   not the variable frame delta.
3. **Log spam.** The tunneling warning was gated on `_framesSinceLastSnapshot` but never reset it, so
   once the cooldown elapsed it fired **every frame** — unusable during exactly the sustained fast
   traverse it exists to catch. Now has its own counter.

Plus a tolerance on `BELOW_SURFACE`: the player's transform origin **is** the capsule base
(`Vertex0 = (0,0.5,0)`, radius 0.5), so standing samples ≈ 0 and Surface Nets' approximation of the
analytical zero-crossing reads slightly negative. At `< 0` it fired constantly while standing still
and — because it shares the snapshot cooldown — would have masked real ungrounding events. Now
requires one voxel of real penetration and logs the actual `sdf=` depth.

`ProjectFeatureConfig.asset`: `EnablePlayerFallThroughDiagnosticSystem` and
`EnableTerrainColliderTimingSystem` flipped 0 → 1. **Flip them back when this work-set closes.**

---

## Not in this work-set (recorded so the trail survives)

- **U2** — per-chunk `SDFEdit` AABB culling. Prerequisite for destructible relics, and directly
  relieves BUG-008.
- **U3** — 3D sparse vertical chunking (Level 2). ~1–2 weeks against an existing spec.
- **U4** — scatter topmost-surface determination. The genuine unknown; design work, not a port.
- **W2** — destructible hero relics. Blocked on U2 + U3.

All four are in the backlog with detail; **U3/U4 costing lives in
[`../Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md`](../Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md) §"3D grid cost inventory"**.

**Sequencing note:** vertical chunking and SDF relics are *mutually dependent* — the chunking enables
the relic, and the relic (or caves) is the only content that justifies the chunking. With today's
pure-heightfield field, sparse 3D allocation would resolve to exactly **one** occupied layer, i.e.
current behaviour at added complexity cost. **Do not build U3 speculatively; commit to the content
at the same time.**
