# Relic LOD Impostor Spec
_Status: IMPLEMENTED_
_Last updated: 2026-04-16_
_Owner: Structures / Rendering_
_Supersedes: RELIC_RENDER_REFACTOR_SPEC.md §8 (Future: LOD / Impostor Hook)_

---

## 1. Purpose

Render large-scale relics correctly at every camera distance by swapping between a full mesh (near) and a small impostor mesh (far) when camera distance exceeds a safe threshold. Resolves the residual far-plane per-triangle clipping artifact observed after the batch → per-entity rendering refactor.

---

## 2. Background

### 2.1 Residual artifact after `RELIC_RENDER_REFACTOR_SPEC.md`

The refactor replaced `Graphics.RenderMeshInstanced` with per-entity Entities Graphics rendering, which correctly fixed the *first* cause of relic disappearance: batch-wide frustum culling on a shared `worldBounds`. However a second artifact persisted: at distance, large relics still appeared fragmented, with parts of the mesh visibly missing and a curved "edge" slicing through the geometry as the camera rotated.

Diagnostic outcomes (BUG-016 follow-up):
- `subMeshCount = 1` — not a multi-submesh rendering issue
- `RenderBounds` set to effectively infinite → fragmentation unchanged → not per-entity frustum culling either
- Mesh `localBounds.extents ≈ (0.7, 0.5, 0.5)` at `UniformScale = 500` → world-space bounding radius ≈ 500 units

### 2.2 Root cause: camera far clip plane

Unity's camera far clip is a **plane** perpendicular to camera forward at `farClipPlane` units. When a relic's world-space bounding radius exceeds `farClipPlane − cameraDistanceToCenter`, the GPU rasterizer clips individual triangles against the far plane. The visible result is a sliced relic with missing fragments. As the camera rotates, the plane rotates with it, so different triangles straddle the plane at different angles — creating the appearance of a moving "edge" eating the mesh.

This is fundamental perspective-projection behavior, not a bug. It cannot be fixed by tuning bounds, culling, or shader settings.

### 2.3 Why not just bump the far clip plane

Extending `farClipPlane` to very high values (e.g. 50,000+) reduces depth-buffer precision across the entire scene, causing z-fighting on terrain and close-range geometry. Unusable as a global knob.

### 2.4 Authoring pivot

The in-flight fix is to author large landmark meshes **at their intended final world size** (e.g. a 500-unit-tall relic shipped at that size in the FBX), with `UniformScale = 1` at runtime. This spec assumes that authoring approach. Runtime scaling is supported but discouraged — see §4.

---

## 3. Scope

### In scope
- Per-entity LOD state (`RelicLodState`)
- Distance-based swap between two LODs: full mesh (near) and impostor mesh (far)
- Hysteresis to prevent flicker at the swap boundary
- Fallbacks so a null impostor mesh/material reuses the full mesh/material
- Bootstrap guidance that warns when relic geometry is at risk of far-plane clipping
- Feature flag `EnableRelicLodSelectionSystem`
- Update to `RELIC_RENDER_REFACTOR_SPEC.md` and `DOCUMENT_INDEX.md` on completion

### Out of scope
- Animated/crossfaded LOD transitions — pop is acceptable for MVP
- Billboard impostor with pre-rendered texture — covered by [RELIC_BILLBOARD_IMPOSTOR_SPEC.md](RELIC_BILLBOARD_IMPOSTOR_SPEC.md) (sibling follow-up)
- Multi-level LOD beyond two tiers
- Other family impostors (dungeons, villages) — same pattern can be applied later
- Changing `farClipPlane` dynamically

---

## 4. Authoring Guidance

**Best practice: author the relic FBX at its intended final world size and keep `UniformScale ≈ 1`.**

Reasons to avoid large runtime scale multipliers:
1. Mesh import precision on `mesh.bounds` scales with `UniformScale`. A sub-unit authoring error becomes a hundred-unit world error at `UniformScale = 500`.
2. The LOD swap distance is derived from mesh world extents; confusing when extents are the product of a scaled-down asset.
3. GPU vertex precision artifacts amplify with scale when far from world origin.

If a designer does set a large `UniformScale`, the system still works — but the bootstrap logs a warning (see §5.7).

Impostor reuse: for MVP, the same relic mesh is reused as the impostor at a smaller scale (`ImpostorScale`). This is the simplest workable LOD and takes zero extra assets. A future pass may introduce a billboard quad with a pre-rendered impostor texture.

---

## 5. Design

### 5.1 Components

```csharp
/// <summary>
/// Per-relic LOD state. 0 = full mesh, 1 = impostor. A byte keeps the
/// component small so it stays cheap to add to every realized relic.
/// </summary>
public struct RelicLodState : IComponentData
{
    public byte CurrentLod;
}
```

### 5.2 `RelicRenderConfig` extensions

```csharp
public class RelicRenderConfig : IComponentData
{
    // existing
    public Mesh Mesh;
    public Material Material;
    public float UniformScale;
    public float YOffset;

    // new
    public Mesh ImpostorMesh;           // null → reuse Mesh
    public Material ImpostorMaterial;   // null → reuse Material
    public float ImpostorScale;         // target world-space half-extent (see §5.8); recommended: 30
    public float LodSwapDistance;       // center of swap band; see §5.6
    public float LodHysteresis;         // ± buffer around LodSwapDistance
}
```

### 5.3 `RenderMeshArray` layout

Each relic entity is spawned with a `RenderMeshArray` containing two entries:
- Index 0: `(Material, Mesh)` — full
- Index 1: `(ImpostorMaterial ?? Material, ImpostorMesh ?? Mesh)` — impostor

`MaterialMeshInfo.FromRenderMeshArrayIndices(lodIndex, lodIndex)` swaps between them with no structural change to the entity's archetype. Fast, allocation-free.

### 5.4 Lifecycle

```
RelicRealizationSystem spawns entity
    ↓
    Adds: LocalTransform (scale = UniformScale)
           RenderMeshArray with both LOD entries
           MaterialMeshInfo(0, 0)
           RenderBounds from Mesh.bounds
           RelicLodState { CurrentLod = 0 }
           StructureRealizedTag (lifecycle)
    ↓
RelicLodSelectionSystem per frame
    ↓
    For each RelicLodState entity:
       compute distance to camera
       pick target LOD with hysteresis
       if target != CurrentLod:
           swap MaterialMeshInfo to (target, target)
           set LocalTransform.Scale to UniformScale or ImpostorScale
           set RenderBounds from active mesh bounds
           update CurrentLod
```

### 5.5 `RelicLodSelectionSystem`

**Location:** `Assets/Scripts/DOTS/Structures/RelicLodSelectionSystem.cs`

**Group:** `PresentationSystemGroup` — runs after `LocalToWorldSystem` so transform writes in the simulation group are visible, and before Entities Graphics submits draw calls.

**Class-based `SystemBase`**, not `ISystem` — needs managed access to `Camera.main` and `RelicRenderConfig`.

**Algorithm per update:**
1. Early-out if feature flag disabled or no main camera.
2. Get camera position and cached `RelicRenderConfig` (managed singleton).
3. Pre-compute `nearCutoffSq = (LodSwapDistance − LodHysteresis)²` and `farCutoffSq = (LodSwapDistance + LodHysteresis)²`.
4. `Entities.ForEach` over `(RelicLodState, LocalTransform, MaterialMeshInfo, RenderBounds)`:
   - `dSq = distanceSq(cameraPos, xform.Position)`
   - Target LOD:
     - `dSq < nearCutoffSq` → 0
     - `dSq > farCutoffSq` → 1
     - else keep `CurrentLod`
   - If `target != CurrentLod`:
     - `matMeshInfo = MaterialMeshInfo.FromRenderMeshArrayIndices(target, target)`
     - `xform.Scale = target == 0 ? UniformScale : ImpostorScale`
     - `renderBounds.Value = (target == 0 ? fullBoundsLocal : impostorBoundsLocal)`
     - `state.CurrentLod = target`

**Why `.WithoutBurst().Run()`:** the system uses managed references (`Camera.main`, `Mesh.bounds`). Relic count is small (≤ tens); the main-thread cost is negligible.

### 5.6 Swap distance derivation

The full mesh starts clipping when camera distance drops below:

```
fullMeshWorldRadius = length(mesh.bounds.extents) × UniformScale
safeViewDistance    = farClipPlane − fullMeshWorldRadius − safetyMargin
```

`safetyMargin` covers:
- Rotation slack: an AABB can rotate up to `√3 × max half-extent` further than its axis-aligned radius would suggest
- One-frame player velocity budget (~50 units at fast movement)
- A fudge factor for the impostor scale band below

Recommended defaults (emitted by `RelicVisualBootstrap` if the inspector fields are left at 0):

```
LodSwapDistance ≈ clamp(safeViewDistance × 0.8, 200, farClipPlane × 0.6)
LodHysteresis   ≈ max(LodSwapDistance × 0.05, 20)
ImpostorScale    = 30   (tunable; see §5.8)
```

For an FBX authored at ~500 units tall with `farClipPlane = 2000`, this yields `LodSwapDistance ≈ 1200`, `LodHysteresis ≈ 60`.

### 5.7 Authoring-trap warning

At bootstrap, `RelicVisualBootstrap.Start` computes `fullMeshWorldRadius` (see §5.6) and logs:

- **Warn** if `fullMeshWorldRadius > farClipPlane × 0.5` — the relic is so large that even with LOD the near-view experience may still clip at short distances.
- **Info** always — log the computed safe view distance and the chosen swap distance / hysteresis so issues are diagnosable without a debugger.

### 5.8 Impostor scale tuning

`ImpostorScale` is specified as the target **world-space half-extent** of the impostor (roughly the impostor's visual half-height), not a raw `LocalTransform.Scale` multiplier. `RelicLodSelectionSystem` divides the value by the impostor mesh's largest `bounds.extents` component to compute the actual transform-scale multiplier. This keeps behavior intuitive in the MVP null-fallback case: reusing a 500-unit-tall relic mesh as its own impostor with `ImpostorScale = 30` still yields a ~30-unit impostor, not a 15,000-unit monster.

Tuning rules of thumb (all in world units):
- Small enough to never clip against the far plane: `ImpostorScale ≤ (farClipPlane − LodSwapDistance − safetyMargin) × 0.5`
- Large enough to stay readable at the swap distance.

For the reference case (`farClipPlane = 2000`, `LodSwapDistance = 1200`), any impostor under ~400 units of effective size is safe. `30` is a good visual default — distant enough to feel like scenery, not so small it vanishes. For short-far-clip configurations (e.g. `farClipPlane = 360` in the High preset), values in the 10–30 range are typical.

---

## 6. Feature Flag & Bootstrap

### 6.1 `ProjectFeatureConfig`

```csharp
[Header("Structure Placement")]
public bool EnableStructurePlacementSystem = true;
public bool EnableRelicRealizationSystem = true;
public bool EnableRelicLodSelectionSystem = true;   // new
```

### 6.2 `DotsSystemBootstrap`

After `RelicRealizationSystem` is registered in `SimulationSystemGroup`, register `RelicLodSelectionSystem` in `PresentationSystemGroup`:

```csharp
if (config.EnableRelicLodSelectionSystem)
{
    var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
    var handle = world.CreateSystem<DOTS.Structures.RelicLodSelectionSystem>();
    presentationGroup.AddSystemToUpdateList(handle);
    DebugSettings.Log("Bootstrap: RelicLodSelectionSystem enabled and added to PresentationSystemGroup.");
}
```

Disabling the flag leaves all relics in LOD 0 (full mesh) with no swap — useful for debugging and for scenes where far clipping isn't a concern.

### 6.3 `RelicVisualBootstrap` inspector additions

```csharp
[Header("LOD / Impostor")]
[SerializeField] private Mesh impostorMesh;          // optional
[SerializeField] private Material impostorMaterial;  // optional

[Tooltip("World-space scale of the impostor mesh at distance. 30 is a reasonable default.")]
[SerializeField] private float impostorScale = 30f;

[Tooltip("Camera distance at which the relic swaps between full and impostor. 0 = auto-derive from far clip and mesh extents.")]
[SerializeField] private float lodSwapDistance = 0f;

[Tooltip("Hysteresis band around lodSwapDistance to prevent flicker. 0 = auto-derive (~5% of swap distance).")]
[SerializeField] private float lodHysteresis = 0f;
```

If `lodSwapDistance` or `lodHysteresis` is 0, the bootstrap auto-derives them per §5.6.

---

## 7. Files Changed

| File | Action | Notes |
|------|--------|-------|
| `RelicLodState.cs` | **Create** | New `IComponentData` |
| `RelicRenderConfig.cs` | **Update** | Add impostor fields |
| `RelicVisualBootstrap.cs` | **Update** | Inspector fields + auto-derivation + authoring-trap warning |
| `RelicRealizationSystem.cs` | **Update** | Register RenderMeshArray with both LODs; add `RelicLodState` on spawn |
| `RelicLodSelectionSystem.cs` | **Create** | Distance-based LOD swap in `PresentationSystemGroup` |
| `DotsSystemBootstrap.cs` | **Update** | Register new system |
| `ProjectFeatureConfig.cs` | **Update** | `EnableRelicLodSelectionSystem` flag |
| `RELIC_RENDER_REFACTOR_SPEC.md` | **Update** | Mark §8 as IMPLEMENTED; link to this spec; document residual far-plane root cause |
| `DOCUMENT_INDEX.md` | **Update** | Add this spec |
| `KNOWN_ISSUES.md` | **Update** | Close BUG-016 once verified in Play Mode |

---

## 8. Test Plan

### 8.1 EditMode tests (`StructureLodTests.cs`, new)

- `RelicLodState_DefaultsToZero` — freshly created component reads LOD 0.
- `RelicLodState_CanStoreAndRetrieveLodValue` — round-trip value 0 and 1.
- `RelicRenderConfig_DefaultsProvideWorkableLodFields` — ensure null impostor mesh/material fallbacks are acceptable values.
- `LodThreshold_Hysteresis_DoesNotFlipWithinBand` — direct unit test on a pure-static helper that takes `(dSq, currentLod, near², far²)` and returns next LOD.

### 8.2 PlayMode / manual verification

- **Approach:** player walks from > 2000 units toward the relic. Impostor visible at distance, pops to full mesh somewhere around the swap distance, no far-plane clipping observed at any distance.
- **Retreat:** opposite direction, pops back to impostor. Pop occurs only once (no rapid switching).
- **Boundary flicker:** stand-and-circle at approximately the swap distance while moving in/out by small amounts. No per-frame flicker (hysteresis honoured).
- **Regression:** existing `StructureAnchorPlanningTests` and `StructurePlacementComponentTests` continue to pass.
- **Feature flag off:** `EnableRelicLodSelectionSystem = false` reproduces the original far-plane fragmentation (confirms flag actually gates the fix).

### 8.3 Diagnostics

Leave a single `forceLog = true` info line on the first LOD transition per session (`"Relic <id> LOD 0 → 1 at d=..."`) so playtester issue reports include concrete distances.

---

## 9. Acceptance Criteria

- [ ] `RelicLodState` exists and is added to every realized relic
- [ ] Each relic has a 2-entry `RenderMeshArray` and swaps `MaterialMeshInfo` at the threshold
- [ ] `LocalTransform.Scale` and `RenderBounds` are swapped atomically with `MaterialMeshInfo`
- [ ] Hysteresis band prevents flicker at the boundary (manually verified)
- [ ] No far-plane clipping artifact at any practical view distance
- [ ] `RelicVisualBootstrap` logs computed swap distance and warns on authoring-trap conditions
- [ ] `EnableRelicLodSelectionSystem` flag toggles the system cleanly
- [ ] `RELIC_RENDER_REFACTOR_SPEC.md` §8 marked IMPLEMENTED, linked here
- [ ] `DOCUMENT_INDEX.md` lists this spec
- [ ] `KNOWN_ISSUES.md` BUG-016 marked RESOLVED after Play Mode verification

---

## 10. Future Enhancements

- **Billboard impostor with pre-rendered texture** — specified in [RELIC_BILLBOARD_IMPOSTOR_SPEC.md](RELIC_BILLBOARD_IMPOSTOR_SPEC.md). Plugs into `ImpostorMesh` / `ImpostorMaterial` with zero changes to `RelicLodSelectionSystem`.
- **Crossfade blend zone** — render both LODs with opacity ramp in a narrow band around `LodSwapDistance` for a seamless transition. Requires alpha-blend material path.
- **Multi-level LOD** — full → mid-poly → impostor for extreme-distance landmarks like the seeded far horizon.
- **Shared infrastructure** — factor `*LodState` and `*LodSelectionSystem` into a reusable pattern so dungeons and villages can opt in with one new config.

---

## 11. Related Docs

- [STRUCTURE_PLACEMENT_PLAN.md](STRUCTURE_PLACEMENT_PLAN.md)
- [STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md) — §12.5.2 Relic Realizer
- [RELIC_RENDER_REFACTOR_SPEC.md](../Archives/StructurePlacement_2026/RELIC_RENDER_REFACTOR_SPEC.md) (archived) — §8 Future Hook, superseded by this spec
- [RELIC_BILLBOARD_IMPOSTOR_SPEC.md](RELIC_BILLBOARD_IMPOSTOR_SPEC.md) — follow-up that replaces the MVP scaled-mesh impostor with a pre-baked camera-facing billboard
- [../../KNOWN_ISSUES.md](../KNOWN_ISSUES.md) — BUG-016 relic rendering artifact
- [../HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md](../Rendering/HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) — complementary far-horizon rendering plan
