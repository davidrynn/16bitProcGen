## 16‑bit Art Direction and DOTS Integration Guide

This guide covers how to author assets for a stylized “16‑bit” low‑poly look (akin to Dredge / Caravan SandWitch / Hyper Light Drifter aesthetics) and integrate them into Unity DOTS (Entities Graphics) efficiently.

### Goals
- Low‑poly silhouettes, flat shading, limited palettes
- Small textures, minimal materials, excellent batching
- Author in DCC (Blender), bake to Entity prefabs, instantiate via DOTS

### Visual Style
- Modeling (Blender)
  - Keep meshes low‑poly; emphasize silhouettes over micro detail
  - Prefer flat shading (turn off Auto Smooth or set per‑face normals)
  - Modular tiles/props for strong repetition with variety
- Textures
  - Small sizes (64–256 px); limit palette; avoid normal/height maps
  - Use atlases/palette sheets to minimize material count and draw calls
- Materials/Shaders
  - Unlit or simple Lit materials for flat look
  - Optional retro pass: pixelation + palette quantization/dithering (URP Render Feature)
- Lighting/Post
  - Minimal lights; a single key light + ambient or baked GI
  - Subtle dithered fog for depth if desired
- Animation/VFX
  - Snappy, stepped animation; sprite‑sheet particles or simple meshes
  - Keep per‑object material variants to a minimum; use per‑instance properties

### Recommended Project Structure (under `Assets/`)

```
Art/
  Blender/
  Textures/
    Palettes/
Prefabs/
SubScenes/
Scripts/
  Authoring/
  DOTS/
Docs/
```

### Production Pipeline
1) Author in Blender
   - Export FBX/GLTF with clean scale (1,1,1) and rotations
   - Keep polycounts small; separate modular kits into distinct files or collections

2) Create GameObject Prefabs in Unity
   - Add `MeshFilter`/`MeshRenderer` using your stylized materials
   - Prefer one material family (atlas) across many meshes

3) Add Authoring + Baker
   - Attach small Authoring MonoBehaviours to capture gameplay tags/params
   - Write Bakers to add ECS components during bake; Entities Graphics will convert the renderers

4) SubScenes for Conversion
   - Place authoring prefabs into a `SubScene` that contains your prefab library
   - Baking creates Entity prefabs; reference them at runtime via systems

5) Runtime Instantiation
   - Systems instantiate baked Entity prefabs (not Meshes/Materials built in code)
   - Use material property components for per‑instance tints or minor variation

### Minimal Authoring + Baker Example

```csharp
// Authoring MonoBehaviour on your prefab
using UnityEngine;
using DOTS.Terrain.WFC;

public class DungeonElementAuthoring : MonoBehaviour
{
  public DungeonElementType elementType;
}

// Baker adds the ECS data during bake
using Unity.Entities;

public class DungeonElementBaker : Baker<DungeonElementAuthoring>
{
  public override void Bake(DungeonElementAuthoring authoring)
  {
    var entity = GetEntity(TransformUsageFlags.Renderable);
    AddComponent(entity, new DOTS.Terrain.WFC.DungeonElementComponent {
      elementType = authoring.elementType
    });
    // If this prefab is meant to be instantiated at runtime as an entity prefab:
    AddComponent<Prefab>(entity);
  }
}
```

Usage:
- Put the GameObject prefab (with `DungeonElementAuthoring`) in a `SubScene`.
- Baking produces an Entity prefab with Entities Graphics render data and your tag component.
- Instantiate that Entity prefab from DOTS systems.

### Batching and Variants
- Keep materials count low; reuse one `RenderMeshArray`/atlas where possible
- Use per‑instance properties (e.g., color/tint) via MaterialProperty components
- Stick to uniform scale for better batching

### URP Retro Pass (optional)
- Create a URP Render Feature that applies:
  - Pixelation (downscale + upscale)
  - Color quantization and/or ordered dithering using a small palette

### Tools
- Blender (modeling), Aseprite/Photoshop (palettes, small textures)
- Unity URP + Entities (Entities Graphics for rendering)

### Do/Don’t
- Do: author in DCC, bake via Bakers/SubScenes, instantiate Entity prefabs
- Don’t: ship with runtime primitive meshes/materials generated in code (use only for prototyping)


