# 16-BitCraft — Master Plan

**Status:** ACTIVE
**Last Updated:** 2026-07-21
**Owner:** Project-wide

> **Start here.** This document is the authoritative **project overview — the *what* and *when***:
> vision, phase-level direction, and the document map.

### Document authority (three-way split)

| Question | Authoritative doc |
|----------|-------------------|
| **What** are we building, and in **what order**? | **This document** |
| **Why** is it fun? What is the player fantasy and the loop? | [`GAME_DESIGN.md`](GAME_DESIGN.md) |
| What is the **current execution state**? | [`Tickets/TICKETS.md`](Tickets/TICKETS.md) |

Deliberate consequence: **ticket-by-ticket status does not live here.** This doc carries phase-level
direction only; anything that changes week to week belongs in `TICKETS.md`. Bug detail lives in
[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md). Keeping fast-moving state out is what stops this file going
stale (it previously drifted three weeks behind the board).

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

### Product thesis _(adopted from [`GAME_DESIGN.md`](GAME_DESIGN.md), 2026-07-21)_

The pillars say *what systems exist*. The thesis says *what they are for* — and it is the tiebreaker
whenever a roadmap call is ambiguous:

1. **Travel is the reward, not a tax.** In most crafting games, traversal is what you endure between
   the fun. Here it *is* the fun — a skill toy worth using for its own sake. Every other system
   serves that.
2. **Friction belongs in the world, never in locomotion.** Scarcity, distance, danger and survival
   pressure are the challenge. Sluggish movement is not challenge, it is tax.
3. **Building is how world-friction gets answered.** Every pressure the world applies has a
   *buildable* answer, and the infrastructure you raise **is** the progression — it extends reach,
   which reveals a farther need.

**The two joys are move better and build better** (`GAME_DESIGN.md` §6). Weight roadmap decisions
against both — a plan that deepens traversal while leaving building a debug tool is only half the
game. The causal loop these produce is `GAME_DESIGN.md` §4 and is the authority on player experience.

### The MVP "Wow Moment" — Vista Discovery ✅ _(delivered 2026-07-21)_

> *The player crests a rise in a grassy plain. In the far distance, a massive ancient stone hand — four fingers — reaches from the earth. Mountains rim the horizon. The air hazes with distance.*

See: [`Rendering/MVP_VISTA_MOMENT_SPEC.md`](Rendering/MVP_VISTA_MOMENT_SPEC.md) and reference image [`ChatGPT Image Apr 22, 2026, 09_34_36 PM.png`](../ChatGPT%20Image%20Apr%2022%2C%202026%2C%2009_34_36%20PM.png).

This single moment — player sees a strange, gigantic, ancient relic across an atmospheric plain — was
the target feeling for MVP, and it **shipped**: the meteor arrival sequence, the atmosphere authority
driving sky/disc/terrain from one palette, the mountain band, landmark draw distance, and the hero
hand guaranteed at (0, 900). Build record: [`Tickets/done/vista-moment.md`](Tickets/done/vista-moment.md).

**Scope note:** the MVP vista is a **look-at** beat — see the hand, travel toward it. *Entering* the
hand (WFC maze interior, ticket V5) was explicitly deferred out of MVP and now lands as World
Structure **Phase F** pocket interiors. Every visual system should still be evaluated against
whether it serves this moment.

Long-term the world features biome fields, constraint-based flora placement, WFC surface ruins, cave
networks, and persistent world state. The macro-shape spine for all of it — mountains, water,
persistence, WFC — is [`Terrain/WORLD_STRUCTURE_SPEC.md`](Terrain/WORLD_STRUCTURE_SPEC.md).
Historical vision doc: [`Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md`](Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md)

---

## 2. Phase-Level Status

> **Live execution truth is [`Tickets/TICKETS.md`](Tickets/TICKETS.md)** — open work-set, ticket
> states, backlog. This section is phase-level only and is refreshed when a phase's *character*
> changes, not when a ticket moves.

### ✅ Foundation — built and load-bearing
- DOTS core infrastructure (entities, systems, ECB patterns, blob assets)
- SDF terrain pipeline — density sampling, Surface Nets meshing, streaming, LOD, collider build
- Heightmap terrain pipeline — **legacy, quarantined** in `DOTS.Terrain.Legacy`; do not extend
- Structure placement — deterministic anchor planning, authored anchors, relic realization, LOD
- Surface scatter families (grass / trees / rocks / pebbles) with LOD selection
- Slingshot traversal **+ glide** (`GlideSystem`, enabled) — launch, air control, glide, landing
- Camera system (`CameraEffectResolverSystem` — the only camera driver)
- Weather system (rain, sandstorms); terrain glob destruction physics
- **Atmosphere authority** — one palette driving sky, ground disc, terrain and landmarks (V9/R6/V15/V17)
- **Meteor arrival sequence** — diegetic loading shell + burning descent (V13/V14)
- **World macro-structure `H`** — seeded heightfield, C#/HLSL parity pair, shader globals, corridor
  mask ([`Terrain/WORLD_STRUCTURE_SPEC.md`](Terrain/WORLD_STRUCTURE_SPEC.md) Phase A).
  ⚠️ **Foundation only — currently wired to zero consumers and producing zero visual change.**
- ~300 automated NUnit tests (EditMode + PlayMode, two assemblies)

### ⚠️ Built but not what the label implies
- **WFC dungeon generation.** The data layer (possibility masks, pattern/constraint blobs, edge
  rules) and the spawn path (`DungeonPrefabRegistry` → `DungeonEntitySpawningSystem`) are sound. The
  **collapse core is not WFC** — no minimum-entropy selection, no propagation wave, stochastic
  collapse hacks, contradictions silently become holes, and results are frame-coupled (violating the
  determinism invariant). The compute-propagation path is an unimplemented stub. Phase F must budget
  a **rewrite of the collapse core**, not a bug-fix pass — triage in `WORLD_STRUCTURE_SPEC.md` §10.1.

### 🔨 Phase 1 — the loop does not close yet

The vista hook is delivered, but it starts a loop that has no second beat: **you arrive, you travel
to the hand, and there is nothing to do there.** Per `GAME_DESIGN.md` §4 the loop is
arrive → travel → discover → gather/survive → **build** → progress. Only *arrive* and *travel* exist.

| Phase 1 gap | State |
|---|---|
| **Terrain interaction / Magic Hand** | Debug input bridge only (`TerrainEditInputSystem`, SDF sphere/cube edits). Not a shipped mechanic; carries BUG-004/008/010/012. The binary edit layer ([`Terrain/TERRAIN_BINARY_EDIT_LAYER_SPEC.md`](Terrain/TERRAIN_BINARY_EDIT_LAYER_SPEC.md)) is specced with zero code |
| **Building** | Nothing. Magic grid is design-stage (`Structures/MAGIC_GRID_SPEC.md`); WFC fast-build blocked behind the collapse rewrite |
| **Resource collection** | Nothing |
| **Basic HUD** | Nothing |
| **Persistence** | Nothing — edits vanish on quit. Design ready (`WORLD_STRUCTURE_SPEC.md` §9 is the MVP authority) |
| **Audio** | Nothing at all — no pipeline exists. Already blocking V13's comet SFX |

**Cross-cutting drag:** frame rate. Sub-20 FPS in `Basic Terrain Scene`; disabling scatter (~92% of
verts) helps but is *still* not good, so a secondary bottleneck remains unidentified — BUG-017.

### ❌ Phases 2–6 — Not Started
- **Phase 2:** Enhanced biomes (6+), procedural structures (ruins, caves, towers), reachable mountains + vertical streaming, biome-driven grass streaming
- **Phase 3:** Crafting system, tool system (drill/explosive/shaping/grapple hand), progression system
- **Phase 4:** Full world persistence (5-layer model — [`Persistence/PERSISTENCE_SPEC.md`](Persistence/PERSISTENCE_SPEC.md), post-MVP), building system depth, visual polish, audio
- **Phase 5:** Content creation (50+ recipes, 20+ structures), balancing, tutorial
- **Phase 6:** Performance optimization (60 FPS target), bug fixing, release features

---

## 3. Document Map

### Authorities — the three docs that settle arguments
| Document | Authoritative for |
|----------|-------------------|
| **This document** | Project overview, phase-level direction — the *what* and *when* |
| [`Assets/Docs/GAME_DESIGN.md`](GAME_DESIGN.md) | **Gameplay intent** — player fantasy, core loop, design principles; the *why it's fun* |
| [`Assets/Docs/Tickets/TICKETS.md`](Tickets/TICKETS.md) | **Current execution state** — work-sets, tickets, backlog |

### Active — Read These
| Document | Purpose |
|----------|---------|
| [`Assets/Docs/Terrain/WORLD_STRUCTURE_SPEC.md`](Terrain/WORLD_STRUCTURE_SPEC.md) | **World-depth authority** — the `H` macro-structure spine unifying mountains, water, persistence and the WFC resume |
| [`Assets/Docs/Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md`](Terrain/TERRAIN_ECS_NEXT_STEPS_SPEC.md) | Active SDF + Surface Nets terrain implementation spec |
| [`Assets/Docs/Terrain/TERRAIN_BINARY_EDIT_LAYER_SPEC.md`](Terrain/TERRAIN_BINARY_EDIT_LAYER_SPEC.md) | Binary voxel edit layer — hard-edged boxy terrain edits for Magic Hand; additive to SDF pipeline, no density rebuild on edit |
| [`Assets/Docs/Persistence/PERSISTENCE_SPEC.md`](Persistence/PERSISTENCE_SPEC.md) | World persistence design — edit journals, entity state, NPC history, player data |
| [`Assets/Docs/Biomes/BIOME_GRASS_STREAMING_MVP_PLAN.md`](Biomes/BIOME_GRASS_STREAMING_MVP_PLAN.md) | Biome-based, infinite-terrain-safe grass streaming plan (MVP path, future-safe hooks) |
| [`Assets/Docs/DOCUMENT_INDEX.md`](DOCUMENT_INDEX.md) | Full index of all spec/debug/audit docs |
| [`Assets/Docs/KNOWN_ISSUES.md`](KNOWN_ISSUES.md) | Master bug and issue tracker |
| [`Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md`](../Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md) | DOTS scene bootstrap patterns with physics setup |
| [`Assets/Scripts/DOTS/Tests/README.md`](../Scripts/DOTS/Tests/README.md) | Test surface: all NUnit suites (EditMode + PlayMode, two assemblies) |
| [`CLAUDE.md`](../../CLAUDE.md) | AI assistant guidance (Claude Code) |
| [`.github/copilot-instructions.md`](../../.github/copilot-instructions.md) | AI assistant guidance (GitHub Copilot) |

### Reference — Terrain & Art
| Document | Purpose |
|----------|---------|
| [`Assets/Docs/Terrain/SDF_SurfaceNets_ECS_Overview.md`](Terrain/SDF_SurfaceNets_ECS_Overview.md) | SDF terrain architecture overview |
| [`Assets/Docs/Process/ArtAndDOTS_Pipeline.md`](Process/ArtAndDOTS_Pipeline.md) | 16-bit art + DOTS integration guide |
| [`Assets/Docs/PROJECT_STRUCTURE_DOTS.md`](PROJECT_STRUCTURE_DOTS.md) | DOTS-first folder layout reference |

### Archived — Historical / Superseded
| Document | Why Archived |
|----------|-------------|
| [`Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md`](Archives/TerrainDesign/Stylized_Procedural_Terrain_System_Design.md) | Long-term vision doc; active work driven by SDF spec |
| [`Archives/AI_Instructions.md`](Archives/AI_Instructions.md) | Superseded by `CLAUDE.md` |
| [`Archives/DOTS_Migration_Plan.md`](Archives/DOTS_Migration_Plan.md) | Legacy heightmap migration; SDF pipeline is now primary |
| [`Archives/PROJECT_NOTES_2025-11.md`](Archives/PROJECT_NOTES_2025-11.md) | Nov 2025 session notes (camera system, test org) |
| [`Archives/RootLegacy_2026/Unity6_Compatibility_Notes.md`](Archives/RootLegacy_2026/Unity6_Compatibility_Notes.md) | Unity 6 compatibility fixes long since merged (2026-07-02 doc cleanup) |
| [`Archives/RootLegacy_2026/GameProductionPlan_Cursor_2026-04.md`](Archives/RootLegacy_2026/GameProductionPlan_Cursor_2026-04.md) | Former Cursor plan / task driver — superseded by `TICKETS.md`; retains Phase 3–6 sketches + success metrics (2026-07-03) |

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
- **Heightmap (legacy/stable):** `TerrainEntityManager → TerrainDataBuilder → LegacyHeightmapTerrainGenerationSystem → GPU compute → BlobAsset → Mesh`
- **SDF/Surface Nets (primary):** `TerrainChunkDensitySamplingSystem → TerrainChunkMeshBuildSystem → TerrainChunkMeshUploadSystem → TerrainChunkColliderBuildSystem`

---

## 5. Direction — what MVP means now

**MVP definition (restated 2026-07-21):** *a player arrives, sees something extraordinary on the
horizon, enjoys travelling to it, and can meaningfully act on the world when they get there — and
that action persists.* The vista delivered the first half. **The MVP is not done until the second
half exists**, because a hook that starts a loop with no second beat is a demo, not a game.

Direction, in dependency order — not a schedule (the board sequences work):

1. **A terrain-interaction verb that ships.** Replace the debug input bridge with a real mechanic,
   and settle SDF-vs-binary-edit-layer while doing it — BUG-008 (unbounded edit replay) and BUG-012
   (carved-wall faceting) are both *consequences* of routing player edits through the density field,
   so the choice is a fix, not a feature. Everything below stands on this.
2. **Something to do with it** — resource collection, a first building affordance, and the HUD that
   makes both legible. This is what closes `GAME_DESIGN.md` §4's loop.
3. **Persistence** — the world remembers. Design is ready and cheap because Phase A already
   established the single config-hash surface (`WORLD_STRUCTURE_SPEC.md` §9).
4. **Frame rate** — BUG-017. Not a feature, but the window every judgement is made through, and the
   secondary bottleneck is still unidentified.

**Deliberately *not* MVP-blocking** (valuable, sequenced after): World Structure Phase B/C world
cohesion · lakes (Phase D — note swim mode is config-only and must actually be built) · WFC + pocket
interiors (Phase F — budget the collapse rewrite) · Camera Feel C1–C3 · the arms viewmodel A9 ·
audio · reachable mountains.

**Before starting any feature:** follow SPEC → TEST → CODE (see `CLAUDE.md`).

---

## 6. Open Issues / Known Problems

**[`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) is the authoritative tracker** — it is not summarized here, so
that this document cannot contradict it. Highest-severity open item: **BUG-017** (frame rate;
secondary bottleneck unidentified). The terrain-edit cluster **BUG-004 / 008 / 010 / 012** should be
read together before any editing work — see §5.

---

_Refresh this document when a **phase's character** changes — a phase completing, the MVP definition
moving, an authority doc appearing. Do **not** update it for ticket-level progress; that is
`TICKETS.md`'s job, and mixing the two is what made this file drift three weeks stale._


