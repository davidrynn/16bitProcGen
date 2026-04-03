# 16-BitCraft — Master Plan
_Last updated: 2026-03-01_

> **Start here.** This document is the authoritative project overview: vision, current status, phase roadmap, and document map.  
> Sprint-level task detail lives in [`Assets/.cursor/plans/game-production-plan-7ea46cb6.plan.md`](../.cursor/plans/game-production-plan-7ea46cb6.plan.md).

---

## 1. Project Vision

A deterministic, stylized **16-bit retro sandbox** built in **Unity 6.2 / DOTS**. Core pillars:

| Pillar | Description |
|--------|-------------|
| **Exploration** | Vast procedural world with varied biomes, POIs, and secrets |
| **Destruction** | Satisfying terrain manipulation via the Magic Hand (SDF-based) |
| **Movement** | Skill-based Slingshot traversal system |
| **Crafting** | Meaningful resource gathering and item creation |
| **Minimalism** | Low-poly flat-shaded aesthetic, clear visual communication |

Long-term the world features biome fields, river networks, constraint-based flora placement, WFC surface ruins, 3D cave networks, and persistent world state via append-only edit journals.  
Full design: [`Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md`](Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md)

---

## 2. Current Status (as of 2026-03-01)

### ✅ Foundation Complete
- DOTS core infrastructure (entities, systems, ECB patterns, blob assets)
- Heightmap terrain pipeline (legacy path, stable)
- SDF terrain pipeline — density sampling, Surface Nets meshing, collider build
- WFC dungeon generation (compute shader + prefab instantiation)
- Weather system (rain, sandstorms)
- Terrain destruction (glob removal + TerrainGlobPhysicsSystem)
- Camera follow system (CameraFollowSystem, PlayerCameraSystem)
- GPU-instanced grass baseline (chunk tagging, deterministic scatter tests, indirect render path)
- 85+ automated NUnit tests (EditMode + PlayMode)

### 🔨 Phase 1 — In Progress (CRITICAL)

| Feature | Target Location | Status |
|---------|----------------|--------|
| Magic Hand System | `Scripts/Player/MagicHand/` | ❌ Not started |
| Slingshot Movement | `Scripts/Player/Movement/` | ❌ Not started |
| Resource Collection | `Scripts/Resources/` | ❌ Not started |
| Basic HUD | `Scripts/UI/HUD/` | ❌ Not started |

> The current player controller (`PlayerController.cs`) is a placeholder FPS rig — **replace** with Slingshot Movement, do not extend it.

### ❌ Phases 2–6 — Not Started
- **Phase 2:** Enhanced biomes (6+), procedural structures (ruins, caves, towers), world streaming + LOD, biome-driven grass streaming MVP
- **Phase 3:** Crafting system, tool system (drill/explosive/shaping/grapple hand), progression system
- **Phase 4:** World persistence (5-layer sparse delta model — see [`AI/PERSISTENCE_SPEC.md`](AI/PERSISTENCE_SPEC.md)), optional building system, visual polish, audio
- **Phase 5:** Content creation (50+ recipes, 20+ structures), balancing, tutorial
- **Phase 6:** Performance optimization (60 FPS target), bug fixing, release features

---

## 3. Document Map

### Active — Read These
| Document | Purpose |
|----------|---------|
| [`Assets/.cursor/plans/game-production-plan-7ea46cb6.plan.md`](../.cursor/plans/game-production-plan-7ea46cb6.plan.md) | **Sprint priorities, phase detail, to-do checklist — primary task driver** |
| [`Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md`](AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md) | Active SDF + Surface Nets terrain implementation spec |
| [`Assets/Docs/AI/PERSISTENCE_SPEC.md`](AI/PERSISTENCE_SPEC.md) | World persistence design — edit journals, entity state, NPC history, player data |
| [`Assets/Docs/AI/BIOME_GRASS_STREAMING_MVP_PLAN.md`](AI/BIOME_GRASS_STREAMING_MVP_PLAN.md) | Biome-based, infinite-terrain-safe grass streaming plan (MVP path, future-safe hooks) |
| [`Assets/Docs/DOCUMENT_INDEX.md`](DOCUMENT_INDEX.md) | Full index of all spec/debug/audit docs |
| [`Assets/Docs/KNOWN_ISSUES.md`](KNOWN_ISSUES.md) | Master bug and issue tracker |
| [`Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md`](../Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) | DOTS scene bootstrap patterns with physics setup |
| [`Assets/Scripts/DOTS/Test/Testing_Documentation.md`](../Scripts/DOTS/Test/Testing_Documentation.md) | Full test catalog (85+ tests) |
| [`CLAUDE.md`](../../CLAUDE.md) | AI assistant guidance (Claude Code) |
| [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) | AI assistant guidance (GitHub Copilot) |

### Reference — Terrain & Art
| Document | Purpose |
|----------|---------|
| [`Assets/Docs/SDF_SurfaceNets_ECS_Overview.md`](SDF_SurfaceNets_ECS_Overview.md) | SDF terrain architecture overview |
| [`Assets/Docs/ArtAndDOTS_Pipeline.md`](ArtAndDOTS_Pipeline.md) | 16-bit art + DOTS integration guide |
| [`Assets/Docs/Unity6_Compatibility_Notes.md`](Unity6_Compatibility_Notes.md) | Unity 6 DOTS compatibility fixes |
| [`Assets/Docs/PROJECT_STRUCTURE_DOTS.md`](PROJECT_STRUCTURE_DOTS.md) | DOTS-first folder layout reference |

### Archived — Historical / Superseded
| Document | Why Archived |
|----------|-------------|
| [`Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md`](Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md) | Long-term vision doc; active work driven by SDF spec |
| [`Archives/AI_Instructions.md`](Archives/AI_Instructions.md) | Superseded by `CLAUDE.md` |
| [`Archives/DOTS_Migration_Plan.md`](Archives/DOTS_Migration_Plan.md) | Legacy heightmap migration; SDF pipeline is now primary |
| [`Archives/PROJECT_NOTES_2025-11.md`](Archives/PROJECT_NOTES_2025-11.md) | Nov 2025 session notes (camera system, test org) |

---

## 4. Architecture Quick Reference

> Full rules: `CLAUDE.md` and `.github/copilot-instructions.md`

**DOTS-First — Non-Negotiable**
- All runtime gameplay entities are spawned by systems, never placed in editor hierarchy
- MonoBehaviours only for: bootstrap entry points, UI, and authoring components
- One small bootstrap MonoBehaviour → pure ECS after `Start()`

**System Authoring Pattern**
```csharp
[BurstCompile]
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial struct MySystem : ISystem
{
    public void OnCreate(ref SystemState state) { state.RequireForUpdate<MyComponent>(); }

    [BurstCompile]
    public void OnUpdate(ref SystemState state) { /* implementation */ }
}
```

**Key Rules**
- Systems must be `partial` — source generators require it
- Never use `Debug.Log` in systems — use `DebugSettings.LogTerrain/LogWFC/LogRendering/etc.`
- BlobAssets: `if (blob.IsCreated) blob.Dispose()` before reassigning
- Structural changes in jobs: use ECB from `EndSimulationEntityCommandBufferSystem.Singleton`
- SDF pipeline is **primary** for destructible terrain; do not extend heightmap path for SDF work

**Terrain Pipelines**
- **Heightmap (legacy/stable):** `TerrainEntityManager → TerrainDataBuilder → HybridTerrainGenerationSystem → GPU compute → BlobAsset → Mesh`
- **SDF/Surface Nets (primary):** `TerrainChunkDensitySamplingSystem → TerrainChunkMeshBuildSystem → TerrainChunkMeshUploadSystem → TerrainChunkColliderBuildSystem`

---

## 5. Immediate Next Steps

From the production plan — **Weeks 1–4:**

1. **Magic Hand System** — raycast targeting, charge mechanic, visual feedback, integrate with `TerrainModificationSystem`
2. **Slingshot Movement** — grip → pull-back → trajectory preview → launch physics, replace FPS controller
3. **Resource Collection** — extend `TerrainGlobComponent`, automatic pickup, inventory component
4. **Basic HUD** — resource counters, hand charge indicator, slingshot charge indicator

**Before starting any feature:** review `Assets/Docs/AI_Instructions.md` workflow (SPEC → TEST → CODE).

---

## 6. Open Issues / Known Problems

See [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) for the full tracker. Notable items flagged in session notes:

- WASD movement does not orient relative to camera facing direction (A always moves -X)
- `TerrainGenerationSettings` not found in Resources folder at runtime — falls back to defaults
- Treasure/loot balance and discovery feel needs design work

---

_This document should be updated at the start of each major work session to reflect current phase status._


