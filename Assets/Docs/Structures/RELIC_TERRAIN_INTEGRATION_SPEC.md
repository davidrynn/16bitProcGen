# Relic ↔ Terrain Integration Spec

**Status:** DESIGN
**Last Updated:** 2026-07-21
**Owner:** David Rynn
**Tickets:** V19 (mound, partially built) · V21 (seam) · V22 (surface parity) · W2 (destructible, blocked)

Canonical answer to **"how does a hero relic meet the ground?"** — the seam between a static relic
mesh and procedural SDF terrain, surface-appearance parity across that seam, and the path to
destructible relics.

Sibling docs: [`RELIC_LOD_IMPOSTOR_SPEC.md`](RELIC_LOD_IMPOSTOR_SPEC.md) (distance swap — a different
problem), [`../Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md`](../Terrain/UNDERGROUND_VERTICAL_STREAMING_SPEC.md)
(the vertical-chunking prerequisite for W2), [`../Terrain/WORLD_STRUCTURE_SPEC.md`](../Terrain/WORLD_STRUCTURE_SPEC.md)
(the `H` authority whose flatten mask V21 reuses).

---

## 1. Problem

The Agony-pose hero relic (hand + mound + rubble, one joined mesh) sits on procedural SDF terrain.
Two defects, owner-reported 2026-07-21 after the first in-engine look:

1. **The mound rim forms a cliff against the terrain.**
2. **The mound does not look like the terrain** it is supposed to be part of.

### 1.1 Why the cliff is structural, not a tuning error

A static mesh has its rim at a **fixed local Y**. The terrain around it varies with the SDF field.
Any mismatch is a step. There is no `yOffset` that fixes this — and because the owner also wants the
mound to *protrude*, **raising `yOffset` makes the cliff taller**. The two requirements are in direct
tension, which is the signal that the approach, not the number, is wrong.

### 1.2 Why the appearance mismatch is the same root cause

The relic renders through `Relic/RelicLit` + `RelicHero.mat`; terrain renders through `TerrainLit`
with `GroundPaletteMix`. Different shaders reading different colour sources cannot match across a
seam by eye-tuning. They have to share the palette function.

---

## 2. Current built state (2026-07-21)

| Piece | Where | Notes |
|---|---|---|
| Mound/rubble/staged-hand generator | `ArtSource/agony_mound_gen.py` | Idempotent, hash-noise (deterministic + diffable), refits to the live `AgonyClaw` pose |
| Staging | `TILT_DEG = 21.2047`, `PALM_ANCHOR = (-1.4217, 0, 2.0652)`, `BURIAL_OFFSET = -1.49`, `PIVOT = (0,50,0)` | Captured from owner's manual placement; anchored on the **Palm bone head**, which survives palm-length edits |
| Export | `Assets/Models/ColossalHand/ColossalHand_AgonyRelic.fbx` | 2475 v / 4806 tris, hand+mound+rubble joined into ONE mesh |
| Scene wiring | `relic_hand_hero`, `scale 15`, `yOffset 10` | 691 m footprint × 260 m tall; hand 221 m above the plain; mound crest 55 m; skirt 39 m buried |
| Procedural surfacing | `Assets/Shaders/RelicSurface.hlsl` + `RelicLit` | Triplanar wrapper over `GroundPatchFBM`, strata, slope weathering. Opt-in via `_SurfaceStrength` (default **0**) |

### 2.1 Export invariants (both mandatory — each has burned us once)

- **`apply_scale_options='FBX_SCALE_UNITS'`** — the API default imports 100× too small.
- **`bake_space_transform=True`** — without it Blender stores the Z-up→Y-up conversion as a −90° X
  rotation on the FBX **transform node**, not in the vertices. The relic path renders the bare
  **Mesh sub-asset** with its own `LocalTransform` and never instantiates that node, so the model
  renders 90° on its side. Observed in play mode 2026-07-21.
- **Verify numerically after every export**: instantiate the FBX, read `MeshRenderer.bounds`. The
  **Y** extent must be the height (17.31 local), not the footprint depth (45.47).
- The mesh sub-asset name (`ColossalHand_AgonyRelic`) is hashed into the Unity fileID and must not
  collide with an existing object in the `.blend`, or Blender suffixes it `.001` and every scene
  reference breaks silently.

---

## 3. Near-term plan (no vertical chunking required)

### 3.1 V21 — Flatten the terrain under the relic footprint

Reuse the **existing** `H`-authority machinery from ticket H3: `WorldStructureMask` capsule-segment
falloff regions and `SampleWithMask`, already mirrored in `WorldStructure.hlsl` and already used to
protect the spawn→hero sightline.

Add a mask region at the relic anchor so terrain under the footprint resolves to a **known, flat**
height. The mesh rim then meets a predictable plane and the cliff disappears by construction.

**Why this is the right lever:** it needs no new vertical extent, no new subsystem, and it is the
same mechanism already protecting the vista corridor.

**Open question:** the mask currently flattens `H` (macro structure). The near-field SDF base is
Phase C of the World Structure track and is **not yet wired into density sampling**, so a mask on `H`
alone may not flatten what the player actually walks on. Confirm the flatten reaches
`SdLayeredGround` before estimating.

### 3.2 V22 — Surface parity across the seam

Have `RelicSurface.hlsl` call **`GroundPaletteMix(worldXZ, …)`** from `GroundNoise.hlsl` — the same
function `TerrainLit` and the ground-plane impostor use, driven by the same `_AtmoGround` / `_AtmoRock`
globals — and blend the relic's own strata/weathering *on top of* that base rather than over a flat
`_BaseColor`.

**Constraints:**
- **Call, never fork.** `GroundNoiseCore.hlsl` declares a one-definition rule enforced by
  `TerrainChunkMaterialContractTests`.
- **Do not reuse `GroundReliefNormal`** — its header marks it flat-geometry-only; it fights real mesh
  normals and breaks on a vertical palm.
- Preserve the ForwardLit ↔ DepthOnly dissolve parity (`AtmoInterleavedGradientNoise` +
  `AtmoLandmarkEdgeFade`) and the identical `UnityPerMaterial` layout between those two passes.

---

## 4. Long-term: destructible relics (W2)

Owner intent (2026-07-21): *"I had always conceived of these relics as destructible."* Digging into
the relic is a wanted gameplay beat, not a nice-to-have.

### 4.1 The shape of it

Split representation by distance:

- **Near** — the relic **is** SDF: terrain chunks the player can carve. Seamless and
  terrain-coloured by construction; both §1 defects vanish because there is no seam and no second
  shader.
- **Far** — a cheap mesh/impostor.

### 4.2 The elegant part

The Blender hand is **already ~17 primitives** — `Palm`, `F_Index_S1/S2/S3`, `Thumb_Thenar`, … each
an 8-vert box parented to a bone. A smooth-union of ~17 capsules reproduces it well, and the
generator can emit those capsules **from the posed armature's bone head/tail transforms** — so
posing stays in Blender with the `AgonyClaw` action and the SDF follows. The backlog entry for W2
already anticipated this ("V11 master rig doubles as the SDF description").

### 4.3 Blockers (both real, neither optional)

1. **Vertical extent.** The chunk grid is one 15 m slab (world Y ∈ [−7.5, +7.5]). A 220 m hand needs
   ~15 stacked layers. See `UNDERGROUND_VERTICAL_STREAMING_SPEC.md` — Level 2, and note that spec's
   own cost inventory §"3D grid cost" added 2026-07-21.
2. **Edit-cost scaling.** `SDFTerrainField.Sample` loops **every** edit at **every** sample with no
   spatial culling, and `CopyEditsToTempArray` copies the whole singleton buffer per chunk dispatch.
   One hand = 17 edits × 4096 samples = ~70 k capsule evaluations **per chunk, world-wide**,
   including chunks the hand does not touch. **Per-chunk edit AABB culling is a prerequisite, not an
   optimisation** (ticket U2). It also directly relieves BUG-008.

### 4.4 Known consequence to accept

Once the player digs, a far impostor no longer matches the near SDF. For a 220 m landmark a 5 m
excavation is sub-pixel at the swap distance, so **"digging is only visible up close"** is an
acceptable contract. Note the impostor path itself is dormant by owner decision (2026-07-09, V16):
reusing the *full* mesh rescaled saved zero vertices and popped the relic's world size. Re-enabling
needs genuinely decimated art, not the existing mesh.

---

## 5. Non-goals

- Blending the relic mesh into terrain via shader tricks (height-blend, alpha skirts). The mesh rim
  cannot know the terrain height; this hides the symptom and fails on slopes.
- Making terrain conform to the mesh (a mesh→SDF bake). Rejected: it inverts authority, and the SDF
  field is the thing that must stay canonical for edits and persistence.
- Per-relic bespoke terrain art. Whatever lands must work for `relic_head` and `stone_outcrop` too.

---

## 6. Acceptance criteria

- **V21** — no visible step where the mound rim meets terrain, checked on at least one sloped anchor,
  and the relic still reads as *protruding* (owner requirement) rather than sunk.
- **V22** — mound and adjacent terrain are indistinguishable in hue/value at 100 m; relic-specific
  strata and weathering remain legible up close; `relic_head` / `stone_outcrop` are visually unchanged
  (they never opt in — `_SurfaceStrength` defaults to 0).
- **W2** — player can carve the relic near-field; carve persists via the existing `SDFEdit` path; no
  measurable frame-time cost in chunks that do not intersect a relic.
