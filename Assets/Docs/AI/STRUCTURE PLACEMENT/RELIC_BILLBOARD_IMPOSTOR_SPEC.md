# Relic Billboard Impostor Spec
_Status: DESIGN_
_Last updated: 2026-04-16_
_Owner: Structures / Rendering_
_Supersedes: RELIC_LOD_IMPOSTOR_SPEC.md §10 (Future: Billboard impostor with pre-rendered texture)_

---

## 1. Purpose

Replace the current "scaled-down copy of the full mesh" MVP impostor with a **pre-baked billboard** — a camera-facing quad textured from N pre-rendered views of the relic. This produces the classic distant-landmark look the game is after ("a flat image standing in for the real object"), at a fraction of the vertex cost, while keeping LOD 0 (full mesh) and the existing LOD swap pipeline untouched.

---

## 2. Background

### 2.1 State after `RELIC_LOD_IMPOSTOR_SPEC.md`

The LOD-impostor spec introduced:
- `RelicLodState` on every realized relic.
- A 2-entry `RenderMeshArray` (index 0 = full, index 1 = impostor).
- `RelicLodSelectionSystem` in `PresentationSystemGroup` that swaps `MaterialMeshInfo`, `LocalTransform.Scale`, and `RenderBounds` together based on squared camera distance with hysteresis.
- `ImpostorScale` interpreted as a target world-space half-extent (not a raw transform multiplier) — see `RelicLodSelectionSystem.cs` lines 60–91.

**MVP impostor asset:** `ImpostorMesh` and `ImpostorMaterial` are null by default, so the system reuses the full mesh at a smaller scale. Visually this reads as "same relic, smaller" rather than "distant silhouette painted on the sky" — functional, not final.

### 2.2 What the billboard spec adds

Everything plugs into the existing slots:
- `ImpostorMesh` → a generated camera-facing quad.
- `ImpostorMaterial` → a billboard shader sampling a baked atlas.
- No changes to the LOD swap rule. Billboard state lives on its own tag component.

---

## 3. Scope

### In scope
- Editor-side **bake tool** that captures N azimuth views of a relic FBX into a single atlas texture and writes a sibling `Material` + a tiny upright-quad `Mesh` asset.
- Runtime **billboard rotation** (Y-axis only) for entities currently at LOD 1, so the quad always faces the camera.
- Runtime **view-index selection** that picks which atlas tile the shader should sample based on camera azimuth around the relic.
- Billboard shader (URP unlit) — alpha-tested, palette-friendly, honours a single `_AtlasTileCount` + `_CurrentTile` property pair.
- Authoring workflow in `RelicVisualBootstrap` — a "Bake Impostor" button plus inspector fields that reference the generated asset.
- New feature flag `EnableRelicBillboardImpostor` (gates both the billboard rotation system and the bake tool's runtime assumption).
- Documentation updates (`RELIC_LOD_IMPOSTOR_SPEC.md` §10, `DOCUMENT_INDEX.md`, `KNOWN_ISSUES.md` if a new bug surfaces).

### Out of scope
- Octahedral / hemispherical impostors (many-view atlas with shader-side 2-way blending). Horizontal billboards cover the flat/distant-horizon look the MVP is targeting.
- Animated impostors (wind sway, emissive flicker). Static only.
- Per-instance tint / variation — all relics sharing a `RelicRenderConfig` share one baked atlas.
- Sea / terrain silhouette horizon — that's `HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md`, a different feature.
- Replacing LOD 0 with a higher-poly hero mesh — that's a future multi-tier LOD spec.
- Runtime (Play-mode) re-bakes. Atlas generation is an Editor-time tool only.

---

## 4. Design Principles

1. **Zero changes to the LOD swap path.** `RelicLodSelectionSystem` already does exactly the right thing — it swaps between LOD 0 and LOD 1 at distance with hysteresis. Billboards slot into LOD 1's mesh/material/scale; the system shouldn't know or care.
2. **Billboard rotation is an add-on system**, not a new selection path. A dedicated `RelicBillboardFacingSystem` only touches entities currently at LOD 1 and flagged with `RelicBillboardImpostorTag`.
3. **Bake offline, ship flat data.** Everything the runtime sees is a plain `Mesh` + `Material` + `Texture2D` asset. No runtime `RenderTexture`, no runtime camera gymnastics.
4. **One atlas per relic kind**, not per entity. The existing `RelicRenderConfig` is already a managed singleton shared by all realized relics — billboard data lives on the same config.
5. **Y-axis billboarding only.** Distant landmarks read better when they stay "standing up" than when they pitch with the camera. Matches 16-bit/retro composition.

---

## 5. Data Model

### 5.1 New components

```csharp
/// <summary>
/// Marker placed on relic entities that should use billboard impostor
/// behaviour at LOD 1. Added by RelicRealizationSystem when the active
/// RelicRenderConfig has a valid ImpostorBillboardAtlas. Absence of this
/// tag means the entity falls back to the MVP "scaled-down mesh" impostor
/// from RELIC_LOD_IMPOSTOR_SPEC.
/// </summary>
public struct RelicBillboardImpostorTag : IComponentData { }
```

No per-entity billboard data is needed beyond the tag. `LocalTransform.Rotation` already holds the current facing; the system overwrites it every frame while the entity is at LOD 1.

### 5.2 `RelicRenderConfig` additions

```csharp
public class RelicRenderConfig : IComponentData
{
    // existing (LOD_IMPOSTOR spec)
    public Mesh   Mesh;
    public Material Material;
    public float  UniformScale;
    public float  YOffset;

    public Mesh    ImpostorMesh;         // null → reuse Mesh (MVP fallback)
    public Material ImpostorMaterial;    // null → reuse Material
    public float  ImpostorScale;         // target world-space half-extent
    public float  LodSwapDistance;
    public float  LodHysteresis;

    // new (this spec)
    public Texture2D ImpostorBillboardAtlas;   // null → no billboard; use MVP impostor
    public byte      ImpostorBillboardTileCount; // 8 typical; 1 = single-view
    public float     ImpostorBillboardWidth;    // world units, derived by bake tool
    public float     ImpostorBillboardHeight;   // world units, derived by bake tool
}
```

Invariants enforced by `RelicVisualBootstrap`:
- If `ImpostorBillboardAtlas != null`, then `ImpostorMesh` must be a billboard quad and `ImpostorMaterial` must be the billboard shader (otherwise the atlas is ignored and a warning is logged).
- `ImpostorBillboardTileCount` must be a power of two in `{1, 2, 4, 8, 16}` (atlas layout is a single row).
- `ImpostorBillboardWidth` and `ImpostorBillboardHeight` are written by the bake tool and drive the quad mesh's UV and vertex extents; they also feed `RenderBounds` when LOD 1 is active.

### 5.3 Billboard quad mesh

Baked by the tool into `Assets/GeneratedMeshes/Relics/<RelicName>_ImpostorQuad.asset`.

- 4 vertices, 2 triangles.
- Centered on its pivot (same origin as the full mesh).
- Vertical extent = baked height; horizontal extent = baked width.
- UVs span `[0,1]²`. Shader remaps horizontal UV into the current tile: `u' = (u + currentTile) / tileCount`.
- No normals needed (unlit shader), but a forward-pointing normal is written for robustness.

### 5.4 Billboard material

Location: `Assets/GeneratedMaterials/Relics/<RelicName>_Impostor.mat`.

Uses a new shader `Assets/Resources/Shaders/RelicBillboardImpostor.shader` (URP-compatible unlit) with properties:

- `_MainTex (2D)` — atlas.
- `_AtlasTileCount (Float)` — 1/2/4/8/16.
- `_CurrentTile (Float)` — integer-valued, written per-entity via `MaterialPropertyBlock` from the C# side *or* via a global shader property if per-kind granularity is sufficient for MVP.
- `_AlphaCutoff (Float)` — default 0.5; enables `AlphaTest` queue for clean silhouettes against sky.
- `_TintColor (Color)` — default white; future hook for biome/time-of-day tinting.

**MVP simplification:** use a single global `_CurrentTile` set from `RelicBillboardFacingSystem` per frame if all realized relics share the same azimuth bin relative to the camera. Since relics are typically far apart, this is **not** acceptable — each entity needs its own tile. See §6.3 for per-entity delivery.

---

## 6. Runtime Systems

### 6.1 `RelicBillboardFacingSystem` (new)

**Location:** `Assets/Scripts/DOTS/Structures/RelicBillboardFacingSystem.cs`

**Group:** `PresentationSystemGroup`, `[UpdateAfter(typeof(RelicLodSelectionSystem))]` so the LOD swap has already picked the correct entity layout this frame.

**Type:** `partial class RelicBillboardFacingSystem : SystemBase` — matches `RelicLodSelectionSystem`'s style; needs managed `Camera.main` access.

**Algorithm per update:**
1. Early-out if feature flag disabled or no main camera.
2. `Camera.main.transform.position` → `cameraPos`.
3. Read `RelicRenderConfig` from singleton; skip if `ImpostorBillboardAtlas == null`.
4. Iterate `(LocalTransform, RelicLodState, RelicBillboardImpostorTag)`:
   - Skip if `CurrentLod != 1`. Billboards only rotate when actually visible as impostors — avoids rotating hidden-behind-LOD0 entities.
   - Compute yaw-to-camera on XZ plane:
     ```
     float3 to = cameraPos - xform.Position;
     float yaw = math.atan2(to.x, to.z);   // right-handed, Y up
     xform.Rotation = quaternion.RotateY(yaw);
     ```
   - Determine atlas tile:
     ```
     float normalized = (yaw / (2 * math.PI)) + 0.5f;       // [0,1)
     byte currentTile = (byte)math.floor(normalized * config.ImpostorBillboardTileCount) % tileCount;
     ```
   - Write `currentTile` to the entity's `MaterialPropertyBlock` via a per-entity `MaterialPropertyOverride_float` component (see §6.3).

**Why a system, not a shader-only billboard:**
- A vertex-shader billboard would remove the `LocalTransform.Rotation` write, but then Entities Graphics still AABB-culls against the unrotated `RenderBounds`, which can cull silhouette-wide billboards incorrectly when the camera approaches from the side.
- Relic counts are tiny (≤ tens). A main-thread rotation write per frame is free.

### 6.2 Entity archetype changes in `RelicRealizationSystem`

On spawn, when `config.ImpostorBillboardAtlas != null`:
- `RelicRenderConfig` → same as today.
- `RenderMeshArray` → index 0 = (Material, Mesh), index 1 = (`ImpostorMaterial`, `ImpostorMesh`) — already the shape from LOD spec; billboard fields flow through naturally.
- **Add** `RelicBillboardImpostorTag`.
- **Add** the `_CurrentTile` override component (see §6.3).

No change to `MaterialMeshInfo`, `RenderBounds`, or `LocalTransform` initialization beyond what the LOD spec already does.

### 6.3 Per-entity atlas tile delivery

Entities Graphics supports per-instance material overrides via `[MaterialProperty("_CurrentTile")] public struct TileOverride : IComponentData { public float Value; }`. `RelicBillboardFacingSystem` writes this value each frame while the entity is at LOD 1; the SRP-batched draw then samples the right tile from the shared atlas.

Declared as:
```csharp
[MaterialProperty("_CurrentTile")]
public struct RelicBillboardTileOverride : IComponentData
{
    public float Value;
}
```

This keeps the billboard shader property per-instance without any `MaterialPropertyBlock` goo on the managed side and stays compatible with SRP Batcher.

---

## 7. Bake Tool

### 7.1 Purpose

Generate — entirely at Editor time — the three assets referenced by `RelicRenderConfig`:

1. Atlas texture: `<name>_ImpostorAtlas.png`
2. Billboard quad mesh: `<name>_ImpostorQuad.asset`
3. Billboard material: `<name>_Impostor.mat`

### 7.2 Invocation surface

Two equivalent entry points:

- **`RelicVisualBootstrap` inspector button** — `[CustomEditor]` adds a "Bake Billboard Impostor" button that pipes the currently-assigned FBX/material into the tool and writes the three generated assets into sibling folders. Writes the results back into the bootstrap's inspector fields so the designer sees immediate wiring.
- **Menu item** — `Assets > Relics > Bake Billboard Impostor` for ad-hoc baking of a selected prefab / FBX.

### 7.3 Capture pipeline

1. Instantiate the source mesh + material into a throwaway scene GameObject at origin, `UniformScale = 1`, default rotation.
2. Compute `worldBounds` from the instantiated renderer; record `width = 2 × max(extents.x, extents.z)` and `height = 2 × extents.y`. These become `ImpostorBillboardWidth` / `Height`.
3. Create an orthographic `Camera` sized to `(width × 1.05, height × 1.05)` (5% margin) positioned at `distance = height × 4` on the +Z axis looking at `-Z`.
4. For each of `N = tileCount` azimuth steps:
   - Rotate source object by `-(360 / N) × i` around Y.
   - Render to a `RenderTexture` of size `(tileWidth, tileHeight)` where `tileWidth * tileCount = atlasWidth`. MVP defaults: `tileWidth = tileHeight = 256`, `tileCount = 8` → 2048 × 256 atlas.
   - Blit the RT into the atlas `Texture2D` at column `i`.
5. `atlas.Apply()`, write PNG to disk, and import with `TextureImporter.alphaIsTransparency = true`, `filterMode = Point` (keeps the 16-bit aesthetic), `npotScale = None`, `generateMipmaps = false` (single-use texture at known distance), `wrapMode = Clamp`.

### 7.4 Generated mesh

Upright camera-facing quad with vertices:
```
(-w/2, 0, 0) uv (0, 0)
( w/2, 0, 0) uv (1, 0)
(-w/2, h, 0) uv (0, 1)
( w/2, h, 0) uv (1, 1)
```
where `w = ImpostorBillboardWidth`, `h = ImpostorBillboardHeight`. Pivot at bottom-center matches how FBX relics are typically authored (terrain-relative `YOffset` from `RelicRenderConfig` already targets the base).

Saved via `AssetDatabase.CreateAsset`.

### 7.5 Generated material

`new Material(Shader.Find("Custom/RelicBillboardImpostor"))`, populated with the atlas, `_AtlasTileCount`, default `_AlphaCutoff = 0.5`. Saved to disk.

### 7.6 Determinism

Bake output must be reproducible from the same (mesh, material, tileCount, tile dims) triple — no time-of-day lighting, no shadow-receiving, unlit capture. This keeps artist re-bakes diff-friendly.

---

## 8. Feature Flags & Bootstrap

### 8.1 `ProjectFeatureConfig`

```csharp
[Header("Structure Placement")]
public bool EnableStructurePlacementSystem = true;
public bool EnableRelicRealizationSystem = true;
public bool EnableRelicLodSelectionSystem = true;
public bool EnableRelicBillboardImpostor = true;   // new
```

Interactions:
- `EnableRelicLodSelectionSystem = false` → no swap happens; billboards never activate regardless of this flag. Documented in the field's XML summary.
- `EnableRelicBillboardImpostor = false` → `RelicRealizationSystem` ignores `ImpostorBillboardAtlas` and does not add `RelicBillboardImpostorTag`; `RelicBillboardFacingSystem` is not registered. MVP scaled-down-mesh impostor is used instead.

### 8.2 `DotsSystemBootstrap`

After `RelicLodSelectionSystem`'s existing registration:

```csharp
if (config.EnableRelicLodSelectionSystem && config.EnableRelicBillboardImpostor)
{
    var presentationGroup = world.GetExistingSystemManaged<PresentationSystemGroup>();
    var handle = world.CreateSystem<DOTS.Structures.RelicBillboardFacingSystem>();
    presentationGroup.AddSystemToUpdateList(handle);
    DebugSettings.Log("Bootstrap: RelicBillboardFacingSystem enabled and added to PresentationSystemGroup.");
}
```

### 8.3 `RelicVisualBootstrap` inspector additions

```csharp
[Header("Billboard Impostor (LOD 1)")]
[Tooltip("Pre-baked atlas containing N azimuth views. Leave null to fall back to the scaled-mesh impostor from RELIC_LOD_IMPOSTOR_SPEC.")]
[SerializeField] private Texture2D impostorBillboardAtlas;

[Tooltip("Number of azimuth views encoded horizontally in the atlas. 1/2/4/8/16. 8 is the recommended default.")]
[SerializeField, Range(1, 16)] private int impostorBillboardTileCount = 8;

[Tooltip("World-space width of the billboard quad (derived from the bake, editable for tweaks).")]
[SerializeField] private float impostorBillboardWidth = 0f;

[Tooltip("World-space height of the billboard quad (derived from the bake, editable for tweaks).")]
[SerializeField] private float impostorBillboardHeight = 0f;
```

When `impostorBillboardAtlas != null`, bootstrap refuses to start unless `impostorMesh` and `impostorMaterial` are also set, and warns if they don't match the expected billboard shader. Prevents silently shipping a misconfigured billboard pair.

---

## 9. Files Changed

| File | Action | Notes |
|------|--------|-------|
| `RelicBillboardImpostorTag.cs` | **Create** | Empty marker component |
| `RelicBillboardTileOverride.cs` | **Create** | `[MaterialProperty("_CurrentTile")]` single-float override |
| `RelicRenderConfig.cs` | **Update** | Atlas, tile count, quad dims |
| `RelicVisualBootstrap.cs` | **Update** | Inspector block + bake-button editor + start-time validation |
| `RelicVisualBootstrapEditor.cs` | **Create** | `[CustomEditor]` adding the bake button |
| `RelicRealizationSystem.cs` | **Update** | Add billboard tag + tile override when atlas assigned |
| `RelicBillboardFacingSystem.cs` | **Create** | Yaw + tile write per frame for LOD 1 entities |
| `DotsSystemBootstrap.cs` | **Update** | Register billboard system |
| `ProjectFeatureConfig.cs` | **Update** | `EnableRelicBillboardImpostor` flag |
| `Assets/Editor/Relics/RelicBillboardBaker.cs` | **Create** | Menu item + bake pipeline |
| `Assets/Resources/Shaders/RelicBillboardImpostor.shader` | **Create** | URP unlit alpha-tested atlas sampler |
| `RELIC_LOD_IMPOSTOR_SPEC.md` | **Update** | §3 out-of-scope → delivered; §10 link this spec |
| `DOCUMENT_INDEX.md` | **Update** | Add this spec |
| `KNOWN_ISSUES.md` | **Update** | Only if regressions surface |

---

## 10. Test Plan

### 10.1 EditMode (`StructureBillboardTests.cs`, new)

- `BillboardYawFromCameraDelta_MatchesExpectedTile` — table-driven: for a grid of camera offsets, confirm the tile index computed from `yaw / (2π) × tileCount` matches a hand-derived expectation. Static helper, no World.
- `RelicRenderConfig_WithAtlas_RequiresImpostorMeshAndMaterial` — the start-time validator refuses mismatched setups.
- `BillboardTileCount_NonPowerOfTwo_RejectedByValidator` — authoring invariant.
- `BillboardBoundsDerivedFromQuadDimensions` — when swapping to LOD 1, `RenderBounds` uses quad width/height, not the original mesh extents.

### 10.2 PlayMode / manual verification

- **Approach pass:** identical test as `RELIC_LOD_IMPOSTOR_SPEC` §8.2; at far distance the billboard should face the camera and read as a flat picture. Verify no per-frame rotation stutter.
- **Orbit pass:** strafe around a relic at LOD 1 distance. Billboard rotates smoothly; tile index steps through all `N` views; visible "seams" at tile boundaries are within artist tolerance (not a visual jump).
- **LOD transition:** cross the swap distance. LOD 0 ↔ LOD 1 pop is indistinguishable from the non-billboard case (billboard is opaque and roughly the right silhouette at the swap band).
- **Multiple relics:** realize ≥ 3 relics at different headings. Each billboard faces the player independently (confirms per-entity tile override).
- **Feature flag off:** `EnableRelicBillboardImpostor = false` falls back to the scaled-mesh impostor; no billboard system ticks.

### 10.3 Bake-tool validation

- Bake a known relic; confirm atlas PNG exists, has `tileCount * tileWidth` pixel width, has premultiplied/clamped alpha.
- Re-bake without changing inputs → binary-identical PNG (determinism).
- Bake with `tileCount = 1` (single-view degenerate case) → works; runtime uses the single tile for all headings.

### 10.4 Diagnostics

- First billboard tile transition per session logs via `DebugSettings.LogRendering(..., forceLog: true)` — concrete entity id + tile index, so playtest reports include actionable data.

---

## 11. Acceptance Criteria

- [ ] Bake tool writes deterministic atlas + quad mesh + material from a selected relic FBX.
- [ ] At distance, realized relics render as camera-facing billboards that correctly pick atlas tiles from 8 azimuth bins.
- [ ] Billboard rotation runs only for entities currently at LOD 1 — no rotation writes on LOD 0 entities.
- [ ] `RelicLodSelectionSystem` is unchanged in behaviour.
- [ ] Per-entity `_CurrentTile` override uses the Entities Graphics `[MaterialProperty]` pattern (SRP-batched).
- [ ] Feature flag `EnableRelicBillboardImpostor = false` falls back cleanly to the scaled-mesh impostor from `RELIC_LOD_IMPOSTOR_SPEC`.
- [ ] No per-frame allocations in `RelicBillboardFacingSystem`.
- [ ] EditMode tests for yaw-to-tile mapping, config validation, and bounds derivation all pass.
- [ ] `RELIC_LOD_IMPOSTOR_SPEC.md` §3 Out-of-scope and §10 Future Enhancements updated to link this spec.
- [ ] `DOCUMENT_INDEX.md` lists this spec.

---

## 12. Risks and Mitigations

- **Risk:** Billboard yaw quantization (`N = 8`) is noticeable when orbiting fast.
  - Mitigation: raise `tileCount` to 16 (doubles atlas size to 4096 × 256 — still fine for a single unlit texture). Shader could also linearly blend adjacent tiles; out of MVP scope but trivial to add later.

- **Risk:** Alpha-tested silhouette shimmers against the sky.
  - Mitigation: bake atlas at `filterMode = Point` and disable mipmaps to kill the shimmering "dust" MIP artifacts. If needed, enable 2× MSAA or pre-dilate alpha edges in the bake step.

- **Risk:** Billboard quad's upright orientation ignores world terrain slope.
  - Mitigation: acceptable — distant relics read as vertical landmarks regardless of their precise ground normal. If a relic is authored tilted, re-bake from a neutral upright pose.

- **Risk:** Per-entity `MaterialProperty` override breaks SRP-batching compatibility under some URP versions.
  - Mitigation: confirm with Unity 6.2 URP 17.2.0 on the exact shader; fall back to a `MaterialPropertyBlock` path if needed (still runs, loses batching for billboards only).

- **Risk:** Atlas grows across many distinct relics.
  - Mitigation: each relic kind gets its own small atlas. One-landmark-one-atlas keeps memory predictable and asset diffs local.

---

## 13. Future Enhancements (Not In This Spec)

- **Octahedral impostors** — many-view atlas (e.g. 4 × 4) with shader-side two-way blending for smooth viewpoint changes when the player flies overhead.
- **Crossfade blend zone** — shared enhancement with `RELIC_LOD_IMPOSTOR_SPEC.md` §10; alpha-ramp in a narrow band around `LodSwapDistance`.
- **Time-of-day tint** — feed global `_TintColor` from the lighting/weather state.
- **Shared infrastructure** — promote `BillboardImpostor*` into a generic `Rendering/BillboardImpostor` module so villages, mountains peaks, and special landmarks can reuse it without relic-specific code.

---

## 14. Related Docs

- [RELIC_LOD_IMPOSTOR_SPEC.md](RELIC_LOD_IMPOSTOR_SPEC.md) — the LOD swap machinery this spec plugs into
- [RELIC_RENDER_REFACTOR_SPEC.md](../../Archives/StructurePlacement_2026/RELIC_RENDER_REFACTOR_SPEC.md) (archived) — per-entity render path prerequisite
- [STRUCTURE_PLACEMENT_SPEC.md](STRUCTURE_PLACEMENT_SPEC.md) — §12.5.2 Relic Realizer
- [../HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md](../HORIZON_IMPOSTOR_SEED_DRIVEN_SPEC.md) — sibling far-horizon system (separate scope: terrain silhouette, not per-landmark billboards)
- [../../KNOWN_ISSUES.md](../../KNOWN_ISSUES.md) — BUG-016 relic rendering artifact (resolved by LOD spec; billboard is visual polish on top)
- [../../ArtAndDOTS_Pipeline.md](../../ArtAndDOTS_Pipeline.md) — 16-bit art conventions (Point filter, palette)
