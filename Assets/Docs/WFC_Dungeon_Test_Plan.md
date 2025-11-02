## WFC Dungeon Test Plan

### Purpose
Define a repeatable, lightweight test strategy to validate functional correctness, stability, and performance of the Wave Function Collapse (WFC) dungeon generator. Phase 1 uses prototyping tiles (current setup). Phase 2 validates a switch to `Models/TestFloor.fbx` and `Models/Wall_Intact.fbx` tiles.

### Scope
- **Generator**: WFC tile selection, constraint propagation, collapse loop, retries/fallbacks
- **Artifacts**: Generated grid, tile placement transforms, door/opening alignment, boundary enforcement
- **Not in scope (for now)**: Fancy erosion, advanced lighting, art polish

### Environments
- Use the project’s current Unity version and platform defaults.
- Test scene: `Scenes/Test.unity` (or current sandbox scene used to run WFC).
- Hardware: Local dev machine.

### Visualization Modes (Editor vs Runtime)
- **Production (runtime builds)**: Rendering is performed by `Scripts/DOTS/WFC/DungeonRenderingSystem.cs` instantiating baked DOTS entity prefabs from `DungeonPrefabRegistry`. `DungeonVisualizationSystem` is excluded from builds, and the DOTS fallback renderer has been removed.
- **Testing (Editor or explicit define)**: To visualize via temporary GameObjects, enable the compile symbol `TESTING_DUNGEON_VIZ` in Project Settings → Player → Scripting Define Symbols. This compiles and runs `DungeonVisualizationSystem` in Editor/tests only. Remove the symbol for production profiling.

### Assets Under Test
- WFC core (scripts/compute) that produces the dungeon grid and places tiles.
- Phase 1: Existing prototyping tiles/prefabs.
- Phase 2: `Models/TestFloor.fbx` and `Models/Wall_Intact.fbx` configured as tiles.

### Test Data
- Seeds: `0, 1, 2, 3, 42, 1337, 9001` (add more if needed).
- Grid sizes: `8x8`, `16x16`, `32x32`.
- Boundary modes: closed boundary vs. open edges (if supported).

### Acceptance Criteria
- **No overlaps**: No two tiles occupy the same grid cell or intersect in world space.
- **Aligned to grid**: All tile positions are integer-multiple grid steps; pivots where expected.
- **Constraint satisfaction**: All adjacencies respect WFC rules (e.g., walls vs. openings).
- **Connectivity**: From start cell, a path exists to every reachable room/corridor intended by rules.
- **Boundary enforcement**: Outer ring satisfies boundary policy (e.g., walls on edges when closed).
- **Determinism**: Same seed, size, and settings produce identical layouts.
- **Stability**: Generator resolves without infinite loops or excessive retries.
- **Performance**: Under target times (baseline below; measure and record actuals).

### Metrics to Capture
- Generation time per run (ms)
- Number of backtracks/retries
- Final tile count by type
- Violations detected by validators (should be 0)
- Memory allocations (optional; note spikes or GC pressure)

### Validation Checklist (Per Run)
- Seed: ____  Size: ____  Boundary: ____
- Time (ms): ____  Retries: ____
- [ ] No overlaps
- [ ] Grid alignment
- [ ] Adjacency constraints hold
- [ ] Boundary policy holds
- [ ] Connectivity holds (start → all intended cells)
- [ ] Deterministic rerun matches (optional immediate rerun)
- Notes/Screenshot: __________________________

### Test Procedure (Phase 1: Prototype Tiles)
1. Open `Scenes/Test.unity`.
2. In the generator UI/inspector, set grid size and seed; enable any debug gizmos.
3. Run Play Mode → generate once. Observe gizmos and console output.
4. Record metrics and check all items in the Validation Checklist.
5. Repeat for seeds and sizes listed in Test Data.
6. For determinism: regenerate with identical settings and confirm identical result (visually/logged).
7. Export evidence:
   - Screenshot(s) of final layout per seed/size into `Docs/TestRuns/YYYY-MM-DD/` (create folder if missing).
   - Append a short summary to `Assets/ConsoleLogs.txt` (seed, size, time, pass/fail, notes).

### Automated/Scripted Validation Hooks (Recommended)
- Provide a validator function that inspects the placed grid and returns a report:
  - Overlap test: Bounds or cell occupancy map
  - Adjacency test: Check neighbor compatibility per cell
  - Boundary test: Check edge cells match policy
  - Connectivity test: BFS/DFS from start to count reachable cells
- Log a concise summary in one line for easy CSV extraction.

### Baseline Performance Targets (Adjust after first measurements)
- 8x8: < 5 ms on dev PC
- 16x16: < 15 ms
- 32x32: < 40 ms

### Regression Strategy
- Freeze one seed/size pair per category as a regression set (e.g., 16x16 seed 42).
- After code changes, run regression set and confirm exact match (determinism + zero violations).

### Phase 2 Plan: FBX-Based Tiles
Goal: Replace prototype tiles with FBX models while preserving rules/metrics.

Setup
- Use `Models/TestFloor.fbx` for floor cells; `Models/Wall_Intact.fbx` for walls.
- Import settings (per model):
  - Scale Factor: 1.0 (so 1 Unity unit equals intended 1m)
  - Read/Write Enabled: Off unless needed at runtime
  - Generate Colliders: As needed (or add BoxCollider in prefab)
  - Lightmap UVs: As needed (optional)
- Prefabize models:
  - Create `Prefabs/Tile_Floor.prefab` from `TestFloor.fbx`
  - Create `Prefabs/Tile_Wall.prefab` from `Wall_Intact.fbx`
  - Ensure pivot alignment: pivot at cell center on the floor plane
  - Ensure exact dimensions match grid cell size (e.g., 1x1 units); document thickness/height

Constraints & Orientation
- Define cardinal orientation for walls: forward along +Z when rotation = (0,0,0).
- Standardize rotation increments to 90°; avoid non-orthogonal rotations.
- Door/opening policy for this phase: walls are solid, floors accept adjacency on all sides.

Generator Integration
- Replace prototype tile references with FBX-based prefabs.
- Maintain the same rule set for adjacency to isolate asset effects.
- Validate placement using the same validators and checklist.

FBX Validation Extras
- [ ] Mesh bounds match expected cell size
- [ ] No visual gaps between adjacent tiles
- [ ] Pivot alignment doesn’t cause offsets or floating
- [ ] Colliders (if used) match geometry footprint

### Implementation (Phase 2: FBX Tiles — Floors & Walls Only)
This section outlines the concrete steps to replace prototype/code-generated prefabs with FBX-based prefabs and generate a dungeon using only floors and walls.

References
- Spawning/rendering (production): `Scripts/DOTS/WFC/DungeonRenderingSystem.cs` (`SpawnDungeonElement`, `OnStartRunning`)
- Testing-only visualization (Editor/define): `Scripts/DOTS/WFC/DungeonVisualizationSystem.cs` (compiled only with `UNITY_EDITOR` or `TESTING_DUNGEON_VIZ`)
- Prefab registry and baking: `Scripts/Authoring/DungeonPrefabRegistryAuthoring.cs`

High-level Approach
- Introduce an authoring-time registry that bakes references to FBX-based GameObject prefabs into entity prefab references.
- Update `DungeonRenderingSystem` to consume the registry; code-only fallback is deprecated for production and may be removed later.
- Limit WFC output to only two patterns: floor (0) and wall (1), so spawning only ever uses FBX floor/wall.

Step-by-step
1) Asset prep and prefabs
   - Import `Models/TestFloor.fbx` and `Models/Wall_Intact.fbx`.
   - Create `Prefabs/Tile_Floor.prefab` and `Prefabs/Tile_Wall.prefab`:
     - Drag FBX into an empty GameObject (or directly use the FBX prefab if suitable).
     - Set transform so the pivot is at the center of a 1×1 cell on the floor plane.
     - Ensure scale = 1 so 1 Unity unit equals one grid cell.
     - Add optional `BoxCollider` sized to footprint.
     - Add `DungeonElementAuthoring` and set `elementType` to Floor or Wall respectively.

2) Authoring registry (new)
   - Create an authoring MonoBehaviour `DungeonPrefabRegistryAuthoring` with fields:
     - `GameObject floorPrefab`, `GameObject wallPrefab` (assign the above prefabs)
   - Baker creates a singleton `DungeonPrefabRegistry : IComponentData` with baked `Entity floorPrefab`, `Entity wallPrefab` and adds `Prefab` tag to those baked entities automatically.
   - Place one instance of `DungeonPrefabRegistryAuthoring` in the test scene so it bakes at build/play.

3) DungeonRenderingSystem integration
   - In `OnStartRunning()`, read the singleton `DungeonPrefabRegistry` and use its baked entity prefabs.
   - Ensure the scene has one `DungeonPrefabRegistryAuthoring` so the registry bakes correctly.
   - In `SpawnDungeonElement`, continue to map pattern 0 → floor, 1 → wall. For walls, keep orthogonal orientations using 0°/90° Y rotations.

4) Restricting to floors & walls only
   - Ensure the WFC pipeline emits only patterns {0, 1} during this phase:
     - In the pattern setup/constraint data, exclude door/corridor/corner from the pattern list, or map them to floor to keep density simple.
     - Log a warning if non-floor/wall patterns appear; do not rely on any removed DOTS fallback.

5) Grid alignment and transforms
   - Confirm `cellSize = 1.0` in WFC components and requests (e.g., `WFCComponent.cellSize`, `DungeonGenerationRequest.cellSize`).
   - Keep `LocalTransform.Position = (x, 0, y)` as-is; FBX pivot and scale must make the mesh land exactly in that cell.
   - If models are not exactly 1×1, either adjust import scale or add a per-tile offset in the prefab root.

6) Validation pass
   - Run the Phase 1 procedure with the FBX registry present.
   - Checklist items must pass; pay special attention to overlaps and visual gaps.
   - Capture metrics and screenshots; compare against Phase 1 baseline.

Deliverables (code & scene)
- New: `Scripts/Authoring/DungeonPrefabRegistryAuthoring.cs` (authoring + baker + data)
- Scene: One placed `DungeonPrefabRegistryAuthoring` referencing `Prefabs/Tile_Floor.prefab` and `Prefabs/Tile_Wall.prefab`
- Updates: `DungeonRenderingSystem.cs` to consume registry or fallback to code-created prefabs

Roll-back plan
- Remove the registry GameObject from the scene to revert to code-only prefabs immediately.

### Risks & Open Questions
- Pivot and scaling mismatches may cause subtle alignment errors.
- If tiles aren’t exactly cell-sized, need per-tile offsets or grid size adjustment.
- If determinism depends on mesh/prefab load order, guard with explicit seed initialization.

### Reporting Template (Copy per run)
```
Date: ________  Scene: Test.unity  Phase: 1 | 2
Seed: ____  Grid: ____  Boundary: ____  Time(ms): ____  Retries: ____
Overlaps: 0/1  Adjacency: pass/fail  Boundary: pass/fail  Connectivity: pass/fail  Determinism: pass/fail
Notes: ______________________________________________________________
Screenshots: Docs/TestRuns/YYYY-MM-DD/<name>.png
```


