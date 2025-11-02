## WFC + Macro-Tile Audit Tasks (Step-by-Step)

### Context
- Macro-only pipeline is active. We use `DungeonPrefabRegistryAuthoring` (roomFloorPrefab, roomEdgePrefab, etc.) and `DungeonRenderingSystem` to spawn entity prefabs. `WFCSmokeHarness` drives generation for visual tests.

### Goals
- Remove redundancies, trim legacy paths, reduce noise, and make the WFC test path simpler and more reliable.

---

### 1) Keep one visualizer
- Decision: Use only `DungeonVisualizationSystem` (GameObject) for now.
- Action:
  - [x] Disable or delete `DungeonDOTSRenderingSystem.cs` from build (comment out class or remove file).

### 2) Remove code-created prefab path
- `DungeonPrefabCreator` is redundant in macro-only mode.
- Action:
  - [x] Remove `DungeonPrefabCreator.cs` and any references.
  - [x] Ensure `DungeonRenderingSystem` never falls back to code-created prefabs.

### 3) Simplify registry binding in `DungeonRenderingSystem`
- Current: tracks `usingAuthoringRegistry`, late-binds each frame.
- Target: early-return until `DungeonPrefabRegistry` singleton exists; bind once per frame without extra flags.
- Action:
  - [x] Remove `usingAuthoringRegistry` state.
  - [x] On each `OnUpdate`, `if (!HasSingleton<Registry>) return;` then read registry and proceed.

### 4) Make wall rotation neighbor check type-safe
- Current: `IsWallAt` assumes pattern index 1 == Wall. Brittle with re-ordered patterns.
- Target: store pattern TYPE per cell, not raw selected index.
- Action:
  - [x] When building `cellPatternMap`, store `DungeonPatternType` (e.g., `int` type) instead of `selectedPattern`.
  - [x] Update `IsWallAt` to test `(DungeonPatternType)storedType == DungeonPatternType.Wall`.

### 5) Visualization fallback gating
- We added a WFCCell->GameObject fallback in `DungeonVisualizationSystem` (debug helper).
- Action:
  - [x] Guard with a debug flag (`DebugSettings.EnableRenderingDebug`).

### 6) Reduce logging noise
- We forced renderer logs during diagnosis.
- Action:
  - [x] Wrap verbose logs with `EnableRenderingDebug`.
  - [x] Keep one-liners for milestones (bound registry, processed N cells) if useful.

### 7) Compute shader scaffolding
- `WFCGeneration.compute` + `PropagateConstraintsWithComputeShader` are placeholders.
- Action:
  - [ ] Remove or clearly comment as inactive until needed, to avoid implying GPU is in use.

### 8) Prune test systems
- Keep `WFCSmokeHarness` as the primary visual test.
- Action:
  - [ ] Wrap editor-only tests with `#if UNITY_EDITOR` and default them off, or remove: `SimpleRenderingTest`, `WFCSystemTest`, `WFCDungeonRenderingTest`.

### 9) Minor WFC loop cleanup
- Cache reads to reduce per-frame overhead (non-critical).
- Action:
  - [ ] Cache `patternCount` and any singleton reads once per update in `HybridWFCSystem`.

### 10) Constraints/weights clarity
- `CreateBasicDungeonConstraints()` returns empty; weights unused.
- Action:
  - [ ] Either implement constraint usage or delete dead paths and document the simplified macro-tile WFC behavior.

---

### Optional Quality-of-Life
- [ ] Expose `gridWidth/gridHeight/cellSize` in a single ScriptableObject for the harness.
- [ ] Add a scene-level gizmo to draw the WFC grid bounds.

---

### Done / Notes
- Collapse bias fixed: selection now chooses from possible patterns instead of preferring indices 0/1.


