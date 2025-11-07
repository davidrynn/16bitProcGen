# AI Assistant Instructions (Cursor-Only Version)

## 1. Purpose & Overview
This document defines how **Cursor AI Agent** collaborates on this **Unity DOTS 16-bit Crafting Game** project.  
It establishes behavioral and technical standards for all AI-assisted edits, ensuring safe, incremental, and educational code contributions.

### Quick Reference: DOTS-First Principles
**This project uses runtime-spawn ECS, not editor scene hierarchies.**

| Aspect | Traditional Unity | This Project (DOTS-First) |
|--------|------------------|---------------------------|
| **Entities** | Place GameObjects in scenes | Spawn entities at runtime via systems |
| **World Layout** | Hand-placed in editor | Procedurally generated (WFC, noise) |
| **Testing** | Test with scene setup | Test with empty scene + code spawning |
| **SubScenes** | For level layout | Only for prefab/asset baking |
| **Debugging** | GameObject hierarchy | Entity Debugger, Systems window |
| **Bootstrap** | Many MonoBehaviours | One minimal bootstrap → pure ECS |

**Core Rule:** If it's gameplay, it spawns at runtime. If it's an asset (mesh/material), it's baked. No middle ground.

---

## 2. Working Style & Core Principles

### 2.1 Development Philosophy
- **Work incrementally** – Small, reversible, testable changes only.
- **Stay scoped** – Implement exactly what's in the SPEC, no extra features.
- **Ask first** – Pause and clarify when requirements, APIs, or behavior are uncertain.
- **Respect removals** – Never restore deleted components or code paths.
- **Maintain architectural intent** – Follow established project conventions.

### 2.2 Communication Style
- Be **collaborative but concise** – explain the "what" and "why" of each change.
- Avoid assumptions beyond the SPEC.
- Ask questions when uncertain; never fabricate details.

---

## 3. Technical Standards

### 3.1 DOTS Architecture & Runtime Philosophy

#### 3.1.1 Code-First, Runtime-Spawn ECS for Gameplay
**All gameplay entities are created by systems at runtime—not placed in editor hierarchy.**

- **Player, camera, projectiles, loot, terrain chunks, grass, FX** → spawned by systems
- **No hand-placed entity GameObjects** in main scenes (except bootstrap/config)
- **Deterministic injection** for inputs (components), noise seeds, chunk sizes, etc.
- Tests must not depend on editor scene setup

**Rationale:** This matches production gameplay exactly. Procedural generation means the world doesn't exist until runtime. Testing against editor-placed entities creates false confidence and editor-specific bugs.

#### 3.1.2 Minimal Bootstrap Pattern
- Keep **one tiny "bootstrap" MonoBehaviour** as entry point
- After `Start()`, everything runs in ECS
- Bootstrap only:
  - Initializes configuration singletons
  - Triggers initial spawn requests
  - Sets up persistent managers (if needed)
- **No MonoBehaviour gameplay logic** beyond bootstrap

**Example Implementation:** See `Assets/Scripts/Player/Bootstrap/PlayerCameraBootstrap.cs`
- Spawns player entity with `PlayerTag`, `LocalTransform`, `LocalToWorld`
- Spawns camera entity with `MainCameraTag`, `LocalTransform`, `LocalToWorld`
- Systems (like `CameraFollowSystem`) automatically start working when their required components exist
- Zero scene setup required beyond attaching the bootstrap script to a GameObject

**Setup Guide:** See `Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md` for complete instructions on creating pure-code DOTS scenes.

#### 3.1.3 Selective Baking for Heavy/Static Assets Only
**Use baking for assets you'll instantiate at runtime, NOT for world layout.**

**DO bake:**
- Entity prefabs for meshes/materials (tree models, rocks, ruins, dungeon tiles)
- Material property overrides, LODs, lightmapping data
- BlobAssets from designer data (biome tables, spawn weights, WFC tile rules)

**WHY:** Baker resolves Hybrid Renderer state once up-front—zero setup cost at runtime.

**DON'T bake:**
- World layout (you're procedural/infinite)
- Entity positions/counts (spawn at runtime)
- Scene hierarchies representing gameplay state

#### 3.1.4 SubScene Usage Patterns
**SubScenes are for asset preparation, NOT world layout.**

- ❌ **Don't use SubScenes** for hand-placed dungeon layouts (you're procedural)
- ✅ **Do use a separate "Assets" SubScene** that only bakes:
  - Prefabs (DungeonPrefabRegistry with floor/wall/door prefabs)
  - BlobAssets (WFC rules, pattern data, biome configs)
- Load only the prefab assets/BlobAssets at runtime, never an authored layout
- Keep SubScenes out of the hot path for generation

#### 3.1.5 Visualization Systems: Editor-Only, Not Production
**Avoid creating editor-only visualization systems that don't match the shipping game.**

- Systems that create GameObjects from entities are **debugging scaffolding only**
- Mark them `#if UNITY_EDITOR` and understand they're not representative
- **Prefer pure DOTS debugging:**
  - Window > Entities > Systems (monitor system execution)
  - Window > Entities > Entity Debugger (inspect entities)
  - `Debug.DrawLine/DrawRay` in systems for visual debugging
  - Custom debug systems using Gizmos (not GameObject conversion)

**Rationale:** GameObject visualization systems create:
- Editor-only bugs (like duplication in subscenes)
- Performance misrepresentation
- Maintenance burden for non-production code
- False sense of "seeing" gameplay that doesn't match builds

**Exception:** If visualization is needed for artist/designer workflow, ensure:
- `HideFlags.DontSave` to prevent scene pollution
- Check for existing instances before creating new ones
- Clear documentation that it's editor-only tooling

### 3.2 Unity DOTS Practices
- All **Systems** must be `partial`.
- **One class per file**; filename matches class name.
- Maintain unique class names and namespaces.
- After large refactors, **clear Library, Temp, and obj** and restart Unity.
- Do not define systems/components in Markdown or non-code files.
- Use **BlobAssets** for immutable shared data (WFC rules, lookup tables)
- Prefer **Entity.Instantiate()** over manual entity creation for prefabs

### 3.3 Code Quality
- Remove legacy paths and redundant logic.
- Prefer **type-safe constructs** (e.g., enums vs. ints).
- Wrap verbose logs and features in debug flags.
- Keep output silent unless debugging.
- No unrelated formatting or refactors.
- **Runtime code must not depend on UnityEngine.Object** (except bootstrap)

### 3.4 Build & Editor Hygiene
- Wrap editor-only systems with `#if UNITY_EDITOR`.
- Use `DebugSettings` for runtime toggles.
- Maintain a clean console.
- No automatic code generation without request.
- **Editor-only systems must not affect runtime behavior**

---

## 4. Debug & Testing

### 4.1 Testing Pyramid (DOTS-First)

**Test in environments that match production. Avoid editor-dependent test setups.**

#### 4.1.1 Unit Tests (EditMode) - Fastest, Most Isolated
**Pure ECS tests with fresh World + EntityManager.**

```csharp
[TestFixture]
public class WFCSystemTests
{
    private World testWorld;
    private EntityManager entityManager;
    
    [SetUp]
    public void Setup()
    {
        testWorld = new World("TestWorld");
        entityManager = testWorld.EntityManager;
    }
    
    [TearDown]
    public void Teardown()
    {
        testWorld.Dispose();
    }
}
```

**Guidelines:**
- No scene loading
- No MonoBehaviours
- No authoring components
- Test system logic directly
- Mock/inject dependencies via components
- Fast iteration (<100ms per test)

#### 4.1.2 PlayMode Tests (Runtime-Spawn) - Integration Tests
**Empty scene + bootstrap MonoBehaviour; spawn all entities by code.**

```csharp
[UnityTest]
public IEnumerator DungeonGenerationSpawnsEntities()
{
    // Empty scene, runtime spawning only
    var world = World.DefaultGameObjectInjectionWorld;
    var em = world.EntityManager;
    
    // Create request entity
    var request = em.CreateEntity();
    em.AddComponentData(request, new DungeonGenerationRequest 
    { 
        isActive = true,
        size = new int2(10, 10),
        cellSize = 1f
    });
    
    yield return new WaitForSeconds(0.5f);
    
    // Assert entities spawned
    var query = em.CreateEntityQuery(typeof(DungeonElementComponent));
    Assert.Greater(query.CalculateEntityCount(), 0);
}
```

**Guidelines:**
- Mirrors build/headless environment
- No editor scene dependencies
- Spawn all entities programmatically
- Test full system integration
- Deterministic setup (no random editor state)

#### 4.1.3 Asset-Fidelity Tests - Validate Baked Assets
**Load baked asset SubScene (prefabs, BlobAssets) and assert correctness.**

```csharp
[UnityTest]
public IEnumerator BakedPrefabsHaveRenderComponents()
{
    // Load only the asset SubScene (prefabs/blobs)
    var subScene = LoadSubScene("Assets_Dungeon");
    yield return new WaitForSeconds(0.1f);
    
    var registry = GetSingleton<DungeonPrefabRegistry>();
    Assert.AreNotEqual(Entity.Null, registry.roomFloorPrefab);
    Assert.IsTrue(HasComponent<RenderMesh>(registry.roomFloorPrefab));
}
```

**Guidelines:**
- Tests baking correctness, not gameplay
- Validates prefabs instantiate/render
- Checks BlobAssets load and parse
- No authored geometry/layouts

### 4.2 Debug Defaults
| Setting | Default | Description |
|----------|----------|-------------|
| `EnableTestSystems` | false | Disable test systems by default |
| `EnableDebugLogging` | false | No console spam |
| `EnableWFCDebug` | false | WFC tracing off |
| `EnableRenderingDebug` | false | Rendering debug off |

### 4.3 DOTS Debugging Tools (Not GameObject Conversion)
**Use native DOTS tools instead of creating GameObject visualizations.**

- **Window > Entities > Systems** - Monitor system execution, timing, dependencies
- **Window > Entities > Entity Debugger** - Inspect entity data, components, queries
- **Window > Entities > Archetypes** - View entity archetypes and memory layout
- **Debug.DrawLine/DrawRay** - Visual debugging in Scene view (from systems)
- **Gizmos.Draw*** in `#if UNITY_EDITOR` blocks - Custom visual debugging
- **Burst Inspector** - Profile generated code

**Avoid:** Creating parallel GameObject hierarchies just to see entities in the inspector. This creates editor-only bugs and doesn't match production.

### 4.4 Testing Guidelines
- Always **write or extend tests before code**.
- Use isolated, configurable test cases.
- **Tests must not require editor scene setup** (except asset-fidelity tests).
- Keep test naming clear and consistent.
- Prefer EditMode tests for speed; PlayMode for integration.
- **No hand-placed test entities** - spawn programmatically.

---

## 5. Agent Contract

### 5.1 Task Lifecycle (Always)
1. **SPEC (delta)** – Write 8–10 bullets describing: Objective, Scope, Inputs/Outputs, Files Touched, Edge Cases, and Definition of Done.  
2. **TEST FIRST** – Add or modify tests to confirm expected and current behaviors.  
3. **CODE** – Implement only enough to make the tests pass.  
4. **REVIEW** – Summarize changes (files/symbols), note untested branches, TODOs, and performance notes.

### 5.2 Change Budget & Scope
- Max **50 lines** and **3 files** per edit cycle.  
- Exceeding budget → **pause and propose** updated SPEC/TEST plan.  
- Only modify explicitly listed files.  
- Do not reformat or refactor unrelated code.

### 5.3 Unknowns & Safety
- If any symbol/API/path is unclear → **stop and ask up to 5 questions**.  
- Never invent APIs. Cite file/line references.  
- Public signatures are **pinned**; modifications require a SPEC delta and `MIGRATION_NOTES.md`.

### 5.4 Diff Gate (Before Apply)
Each proposed edit must include:
- File list
- Hunk count
- 3–5 bullet rationale linking each change → SPEC objectives

### 5.5 Testing & Debug
- Generate a **TEST_PLAN.md** before implementing code.  
- Wrap verbose logs under debug flags.  
- `#if UNITY_EDITOR` for all editor-specific systems.

---

## 6. Cursor Prompts

### 6.1 SPEC Clarifier (Ask Mode)
> Produce a SPEC **delta only** for the following task.  
> Include: Objective, Scope, Inputs/Outputs, Files Touched, Edge Cases, DoD, Risks.  
> Ask up to 5 clarifying questions.  
> **Task:** <paste task>

### 6.2 Constrained Edit (Agent Mode)
> Implement **only** what's necessary to make the new tests pass.  
> Follow the **Agent Contract** (change limits, safety rules, no formatting).  
> If additional edits are needed, stop and propose an updated TEST_PLAN.md.

---

## 7. DOTS-First Workflow Summary

### 7.1 Scene Setup (Edit Mode)

**Minimal Pure-Code Setup (Player/Camera Example):**
```
MainScene/
└── Bootstrap GameObject
    └── PlayerCameraBootstrap.cs (spawns player + camera entities at runtime)
```

**Extended Setup (For Procedural Generation with Prefabs):**
```
MainScene/
├── Bootstrap (MonoBehaviour)
│   └── Initializes singletons, triggers spawn requests
├── DungeonPrefabRegistry (Authoring)
│   └── References to prefabs (baked to entities)
└── Static elements (lighting, settings)

Assets_SubScene/ (for baking only)
├── Floor Prefab (GameObject → baked to entity)
├── Wall Prefab (GameObject → baked to entity)
└── Door Prefab (GameObject → baked to entity)
```

**No hand-placed gameplay entities. No layout. Just configuration.**

**See `Assets/Scripts/Player/Bootstrap/` for working examples and setup guides.**

### 7.2 Runtime (Play Mode)
1. **Bootstrap.Start()** creates spawn request entities
2. **WFCSystem** generates dungeon layout (entity data)
3. **DungeonRenderingSystem** instantiates entity prefabs (floors, walls, doors)
4. **Entities.Graphics** renders everything automatically
5. Player spawns and interacts with pure DOTS entities

**Result:** Hundreds of entities appear, render, and run—no GameObjects except bootstrap.

### 7.3 Testing Flow
1. **Unit tests (EditMode):** Test WFC logic with fresh World
2. **PlayMode tests:** Spawn entities programmatically, assert behavior
3. **Asset tests:** Validate baked prefabs have correct components

**Never depend on editor scene setup for gameplay tests.**

### 7.4 What NOT to Do
❌ Place gameplay entities in scene hierarchy  
❌ Create GameObject visualization systems (unless truly needed)  
❌ Test with hand-placed entities  
❌ Use SubScenes for world layout (you're procedural)  
❌ Depend on `UnityEngine.Object` in runtime gameplay code  

### 7.5 What TO Do
✅ Spawn everything at runtime via systems  
✅ Use baking for prefabs/assets only  
✅ Test with empty scenes + programmatic spawning  
✅ Use DOTS debugging tools (Entity Debugger, Systems window)  
✅ Keep MonoBehaviours limited to bootstrap only  

---

## 8. Maintenance
- Treat this file as the behavioral contract.  
- Update quarterly or after major ECS/system changes.  
- This replaces `.cursorrules` for all Cursor agent operations.
- **DOTS-first principles (Section 3.1) are mandatory** for all new systems.

---

*File purpose: Defines mandatory behavioral and safety standards for Cursor AI in this Unity DOTS project. Emphasizes runtime-first, DOTS-native development that matches production builds.*

