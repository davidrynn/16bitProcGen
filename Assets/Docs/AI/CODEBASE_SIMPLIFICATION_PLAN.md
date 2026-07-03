# Codebase Simplification & Cleanup Plan

**Status:** PHASE 3 IN PROGRESS — verdicts approved (§6.5 2026-07-02), executing batches per §3 protocol
**Last Updated:** 2026-07-02
**Owner:** David + AI assistant

**Purpose:** Living plan for simplifying and clarifying the codebase — correcting system names, archiving dead code, repairing structure, and ordering documentation — using a token-efficient AI workflow. The goal is a codebase the owner — who has limited Unity/ECS experience — can navigate and understand unaided: fewer moving parts, self-explanatory names and structure, modern ECS idiom, and documentation that reflects reality. All cleanup planning (current and future rounds) lives in this doc: the phase workflow is defined once in §2–§5, and each round's manifests, decisions, and batch logs accumulate in §6.

---

## 1. Goals, Scope & Non-Goals

**Goals** (every Phase 2 verdict should cite which goal the change serves):

1. **Understandable by a Unity/ECS newcomer** — names that explain themselves, folder structure that teaches the architecture, XML summaries on public types, why-comments on DOTS constraints. The bar is not just "matches the convention" but "a newcomer can infer what it does."
2. **Lighter** — less dead weight, fewer duplicate code paths, fewer stale docs competing for attention.
3. **Aligned with modern Unity 6 / DOTS best practices** — current ECS idiom per the `unity-ecs-patterns` skill, not just correct naming.

**Scope:**

- Renaming systems/components/files to match the conventions in `/CLAUDE.md` (naming table, one-class-per-file, unique class names).
- Identifying and archiving dead or superseded code (moved out of active namespaces, not deleted without review).
- Folder/namespace structure repair — misplaced files, namespace≠folder mismatches, junk-drawer folders — against `PROJECT_STRUCTURE_DOTS.md` and the CLAUDE.md namespace table.
- Detecting and consolidating functionally overlapping code (two systems/utilities doing the same job) where the decision log (§6.5) explicitly approves it.
- Documentation ordering: staleness review of `Assets/Docs/`, archive moves, index repair — executed per `DOCUMENTATION_SYSTEM_SPEC.md` rules.
- Flagging outdated ECS idiom (checked against the `unity-ecs-patterns` skill) into the manifests; *fixing* idiom requires a per-item decision-log verdict since it edges beyond pure renaming.

**Non-Goals:**

- No behavior changes. Every batch must be a pure refactor — identical runtime behavior, verified by compile + EditMode tests.
- No performance work (tracked separately, e.g. scatter LOD specs).
- No architectural migrations (e.g. heightmap → SDF consolidation) — those need their own specs.
- No touching `Assets/Docs/Archives/` content beyond moving things into it.

Out-of-scope observations are **recorded, not dropped**: when any phase surfaces an improvement that would change behavior or exceed this plan's scope (a worthwhile `SystemBase`→`ISystem` conversion, a performance smell, an architectural simplification), it goes into §6.7 as a suggestion for separate follow-up work — it is never silently applied inside a cleanup batch.

---

## 2. Strategy: Separate Judgment from Execution

The expensive part of AI cleanup is discovery and decisions, not edits. Three phases, paying for intelligence only where judgment happens:

| Phase | What | Who/What runs it | Cost profile |
|-------|------|------------------|--------------|
| 1. Inventory | Produce manifests (§6.1–§6.4), change nothing | Scripts + analyzers + agent sweeps | Near-free except the overlap audit (see §3) |
| 2. Decisions | Turn manifests into an approved rename map / archive list | Human + capable model, one sitting | The only "expensive" phase — one session |
| 3. Execution | Apply approved batches mechanically | Scripts + Haiku subagents, compile check per batch | Cheap |

Rules that keep this cheap:

- **Scripts over models.** Anything deterministic (`git mv`, project-wide rename, grep sweeps, file-age reports) runs as a script. The model writes the script once; the shell runs it for free.
- **Fresh, scoped sessions.** Each execution batch runs in a fresh context that loads only this doc — no mega-session accumulating history.
- **Cheap model for mechanical work.** Haiku subagents execute pre-decided batches; the orchestrator (capable model) only reads one-line verdicts and handles failures.
- **Manifest before mutation.** Nothing is renamed, moved, or archived unless it appears in an approved table in §6 with a decision-log entry.

## 3. Phase Definitions

### Phase 1 — Inventory (read-only)

Deliverables, written into §6:

1. **System naming + idiom audit** — every `ISystem`/`SystemBase` class vs. the CLAUDE.md naming convention; flag mismatches, non-`partial`, filename≠classname, duplicate-ish names. The auditor loads the `unity-ecs-patterns` skill first so idiom flags (legacy ECB patterns, `SystemBase` where `ISystem` fits, missed enableable components) cite correct modern syntax rather than memory.
2. **Dead-code candidates** — signals, not verdicts: zero inbound references (Roslyn/IDE analysis), superseded per spec docs, `[DisableAutoCreation]` systems nothing enables, files untouched since before major pivots (`git log` age report).
3. **Folder/namespace structure audit** — actual layout vs. `PROJECT_STRUCTURE_DOTS.md` and the CLAUDE.md namespace table. Script dumps `(path, namespace, class)` triples and diffs against the expected structure; the model reviews only the exceptions: misplaced files, namespace≠folder mismatches, junk-drawer folders holding unrelated code.
4. **Functional overlap audit** — the one judgment-priced part of Phase 1, kept bounded by a two-tier sweep: cheap agents produce a per-folder responsibility map (one line per class: what it does), then a single capable-model pass reads *only the summaries* (~350 one-liners, not 350 files) and flags overlap candidates — duplicate placement paths, parallel math/utility helpers, systems with the same purpose under different names.
5. **Doc staleness list** — Active docs whose status is stale (e.g. COMPLETE/IMPLEMENTED specs still in active folders), overlapping/duplicate topics, index drift vs. actual files.

Free tooling to prefer over model reads: Roslyn/Rider unused-symbol inspection, `git log --format= --name-only | sort | uniq -c` for churn, `git log -1 --format=%ci -- <path>` for age, grep for `Resources.Load`/kernel strings.

### Phase 2 — Decisions (human + model, one sitting)

- Review Phase 1 manifests together; every row gets a verdict: **rename to X / archive / keep / needs-investigation** (overlap rows: **merge into X / extract shared utility / keep both — intentional, document why**).
- Output: approved tables in §6.1–§6.4 + rationale entries in §6.5.
- No verdict → no action. "Needs-investigation" rows roll into a future round rather than blocking the batch.

### Phase 3 — Execution (batched, verified)

Batch protocol — every batch, no exceptions:

1. Batch = 5–10 related items from an approved table (one namespace or one doc folder at a time). **Exception: consolidations (§6.4 rows) are one per batch** — merging code is a real change, not a mechanical rename, and must be trivially bisectable.
2. Any worker touching system *code* (not just file moves) loads the `unity-ecs-patterns` skill before editing.
3. Renames/moves via `git mv` **with the `.meta` file** (or via Unity editor/MCP) — never orphan a `.meta`.
4. After code edits: grep for the **old name as a string literal** (`Resources.Load` paths, compute kernel names, animator params don't produce compile errors).
5. Unity compile + EditMode tests pass (`unity-test-runner` skill / CLI).
6. One commit per batch, message referencing this doc and the table rows applied.
7. Log the batch in §6.6. A failed batch is reverted, marked in the log, and its rows go back to needs-investigation.

## 4. Unity/DOTS Hazards Checklist

- `.meta` must move with its asset — orphaned `.meta` = broken GUID = lost scene/prefab references.
- MonoBehaviour class rename ⇒ filename must match; serialized refs survive only if the `.meta` (GUID) is preserved.
- Pure `ISystem` structs are the safest renames (nothing serializes them); MonoBehaviours and ScriptableObjects are the riskiest.
- String-based lookups are invisible to the compiler: `Resources.Load` paths, compute shader kernel/constant names, animator parameters. Grep after every batch (§3 step 3).
- Namespace changes can break `asmdef` references and source-generator output — keep namespace moves in their own batches.
- BlobAsset/dispose sites and `[UpdateBefore/After]` ordering comments must move with relocated code, not be dropped.

## 5. Execution Model (Orchestration)

- **Orchestrator:** capable model (current session tier) — dispatches batches, reads verdicts, resolves failures. Kept small: this doc makes each batch self-describing.
- **Workers:** Haiku subagents per batch — "apply rows N–M from §6.x, run the grep sweep, run tests, report pass/fail + anomalies in ≤5 lines."
- **Scripts:** inventory scans, renames, index updates — shell-priced, not model-priced.
- Escalation: a worker never makes judgment calls; anything ambiguous returns to the orchestrator, and anything scope-changing returns to the human via §6.5.

---

## 6. Living Sections (filled per round)

### 6.1 Rename Map

> Populated by Phase 1 (round 1, 2026-07-02), approved in Phase 2. No renames outside this table.
> Inventory facts behind these rows: 348 `.cs` files, 453 top-level types, 74 ISystem/SystemBase systems (all `partial`), ~71 files `[DisableAutoCreation]` + manual creation via `DotsSystemBootstrap`.

**Class renames (judgment — misleading or colliding names):**

| # | Current | Proposed | Kind | Verdict | Batch |
|---|---------|----------|------|---------|-------|
| R1 | `TerrainSeamValidatorSystem` (validates *density* seams) | `TerrainDensitySeamValidatorSystem` — mirrors sibling `TerrainMeshSeamValidatorSystem`; the two are genuinely distinct but currently near-indistinguishable by name | class | **rename** (goal 1) | |
| R2 | `WeatherSystem` | `WeatherSimulationSystem` — distinct from HybridWeatherSystem (state sim vs GPU effects) but names read as duplicates | class | **rename** (goal 1) | |
| R3 | `HybridWeatherSystem` | `WeatherGpuEffectsSystem` — "Hybrid" says nothing; it applies weather via compute shaders | class | **rename** (goal 1) | |
| R4 | `HybridWFCSystem` | `WFCCollapseSystem` — "Hybrid" is misleading: the compute-shader path is an unimplemented stub; system is pure CPU today | class | **rename** (goal 1; WFC kept — work resuming) | |
| R5 | `DungeonRenderingSystem` | `DungeonEntitySpawningSystem` — spawns gameplay prefab entities from collapsed WFC cells; doesn't render | class | **rename** (goal 1) | |
| R6 | `DungeonVisualizationSystem` | `DungeonDebugVisualizationSystem` — debug-only GameObject spawner, near-synonym of R5's current name | class | **rename** (goal 1) | |
| R7 | `HybridTerrainGenerationSystem` | `LegacyHeightmapTerrainGenerationSystem` — self-documents as `[LEGACY]` | class | **rename** (goal 1; part of R40 batch) | |
| R8 | `CameraFollowSystem` | `TestCameraFollowSystem` (+ move to a test area) — self-documented test-only, disabled by default; alternative: archive (A8) | class | **resolved via A8** (archived) | |
| R9 | `ChunkProcessor` | `TerrainDataValidationSystem` — survivor of C5 merge (ISystem + DebugSettings logging; `TerrainSystem` archived via A17) | class | **rename** (goal 1; part of C5 batch) | |
| R10 | root `TerrainChunk` (MonoBehaviour, no namespace) | collides with the live SDF `TerrainChunk` component, violating unique-name rule | class | **resolved via A12** (archived) | |
| R11 | `PlayerEntityBootstrap_PureECS` | underscore suffix violates convention | class | **resolved via A10** (archived) | |

**Namespace repairs (mechanical — file content only, no moves):**

| # | Current | Proposed | Kind | Verdict | Batch |
|---|---------|----------|------|---------|-------|
| R20 | `CameraFollowSystem.cs` — no namespace | `DOTS.Player.Systems` (only namespace-less file in its folder) | namespace | **resolved via A8** (file archived) | |
| R21 | `TerrainModificationSystem.cs`, `PlayerModificationComponent.cs` — no namespace | `DOTS.Terrain.Modification` (match siblings) | namespace | **resolved via A18** (files archived) | |
| R22 | `ComputeShaderManager.cs` — no namespace | `DOTS.Compute` | namespace | **approved** (goal 1) | |
| R23 | `DOTS/Biome/BiomeBuilder.cs`, `BiomeComponent.cs` — no namespace | `DOTS.Biome` | namespace | **resolved via A19** (files archived) | |
| R24 | `DotsSystemBootstrap.cs`, `ProjectFeatureConfig.cs` — no namespace (folder has its own asmdef `DOTS.Core.Authoring`) | `DOTS.Core.Authoring` | namespace | **approved** (goal 1) | |
| R25 | `StructureAnchorDebugGizmos.cs` — the one namespace-less file in its folder (the two Terrain gizmos are archived via A14) | `DOTS.Structures` | namespace | **approved** (goal 1; scope reduced by A14) | |
| R26 | `DebugSettings.cs`, `DebugController.cs`, `DebugTestController.cs` — `DOTS.Terrain.Core` | `DOTS.Core` — DebugSettings is used project-wide (WFC/Weather/Player/Rendering), the `Terrain.` prefix is actively misleading | namespace | **approved** (goal 1; own batch — project-wide `using` update) | |
| R27 | `Assets/Editor/*` no-namespace files | match sibling convention (`DOTS.*.Editor`); note `BiomeDataEditor`/`NoiseVisualizerEditor` may be archived with A12 if their targets (root `BiomeData`, `INoiseFunction`) go — check inside the A12 batch | namespace | **approved** (goal 1; survivors only) | |
| R28 | `Player/Bootstrap/Tests/**` — `DOTS.Player.Tests.Bootstrap` | `DOTS.Player.Bootstrap.Tests` — match its own asmdef name | namespace | **approved** (goal 1) | |

**Folder/structure moves (each its own batch; asmdef-boundary notes in row):**

| # | Current | Proposed | Kind | Verdict | Batch |
|---|---------|----------|------|---------|-------|
| R40 | `DOTS/Core/` legacy heightmap files (~12: `TerrainData`, `TerrainDataBuilder`, `TerrainEntityManager`, `TerrainHeightData`, `TerrainModification[Data]`, `TerrainComputeBufferManager`, `TerrainCleanupSystem`, `TerrainChunkData`, `ModificationType`, `ChunkProcessor`→R9, plus Generation's `HybridTerrainGenerationSystem`→R7 + `TerrainGenerationSettings`) | `DOTS/Terrain/Legacy/` + namespace `DOTS.Terrain.Legacy` — quarantine, not retire (retirement = S11 follow-up spec) | folder | **approved** (goals 1, 2) | |
| R41 | `DOTS/World/` (namespace `DOTS.Impostors`) | rename folder `DOTS/Impostors/` — folder catches up to namespace | folder | **approved** (goal 1) | |
| R42 | `DOTS/Terrain/Rendering/` (7 files, all grass) | `DOTS/Terrain/Grass/` + namespace `DOTS.Terrain.Grass` — folder name currently lies about content | folder | **approved** (goal 1) | |
| R43 | `Assets/Scripts/Terrain/Rendering/TerrainChunkRenderSettings.cs` (stray top-level tree, same namespace as R42's folder) | move to `DOTS/Terrain/Rendering/` after R42 frees the name — **asmdef change** `Core` → `DOTS.Terrain`, needs reference check | folder | **approved** (goal 1; verify asmdef refs in batch) | |
| R44 | `DOTS/Terrain/Diagnostics/` (1 file, self-identifies as `DOTS.Terrain.Debug`) | merge into `DOTS/Terrain/Debug/` | folder | **approved** (goal 1) | |
| R45 | `Assets/Scripts/Rendering/Sky/**` (namespace `DOTS.Rendering.Sky`, compiled into root `Core.asmdef`) | keep in place; document as intentional exception in CLAUDE.md table (R49) — self-consistent tree, moving risks asmdef churn for no learnability gain | folder | **keep — documented exception** | |
| R46 | `Assets/Scripts/Player/**` with `DOTS.Player.*` namespaces (folder lacks `DOTS/` segment) | keep as documented exception (R49) | folder | **keep — documented exception** | |
| R47 | WFC: folder `DOTS/WFC`, namespace `DOTS.Terrain.WFC` | keep namespace; update CLAUDE.md table to `DOTS.Terrain.WFC` (cheap, WFC work resuming soon — don't churn it) | folder/ns | **table update only** (via R49) | |
| R48 | Six test locations, five conventions (`DOTS/Test`, `DOTS/Tests`, `DOTS/TestHelpers`, `Player/Test`, `Player/Bootstrap/Tests`, `Assets/Tests/PlayMode`) | consolidate to two; scope AFTER this round's archives shrink the problem (A1 removes `Test/Archive`, A2 most of `DOTS/Debug`) | folder | **needs-investigation → round 2** | |
| R49 | CLAUDE.md "Namespace Structure" table — 6 of its 10 entries match no actual code; ~20 real namespaces undocumented (`DOTS.Structures`, `DOTS.Impostors`, `DOTS.Rendering.Sky`, `DOTS.Terrain.{LOD,Meshing,Streaming,Pebbles,Rocks,Trees,SurfaceScatter,…}`) | rewrite table to post-cleanup reality; record R45/R46 exceptions | doc | **approved** (goal 1; final batch of round) | |

**File hygiene (split multi-type files / fix filename≠classname):**

| # | Current | Proposed | Kind | Verdict | Batch |
|---|---------|----------|------|---------|-------|
| R60 | `Player/Components/PlayerComponents.cs` — 8 top-level types | split per one-class-per-file rule | file-split | **approved** (goal 1) | |
| R61 | `DOTS/WFC/WFCComponent.cs` — 13 top-level types | split | file-split | **approved** (goal 1) | |
| R62 | `DOTS/WFC/DungeonTypes.cs` — 4 types, filename matches none | keep as-is (WFC work resuming; small related types) | file-split | **waived** — revisit with WFC work | |
| R63 | `DungeonRenderingSystem.cs` (+`DungeonGenerationRequest`), `DungeonVisualizationSystem.cs` (+`DungeonVisualized`), `Terrain/Debug/FallThroughDiagnosticComponents.cs` (2 components) | extract non-system types to own files (FallThroughDiagnosticComponents: rename file is enough — both types are diagnostics components, filename is honest) | file-split | **approved** (goal 1; Dungeon splits ride the R5/R6 rename batch) | |
| R64 | `Meshing/SurfaceNets.cs` contains only `SurfaceNetsJob` | rename file `SurfaceNetsJob.cs` (with `.meta`) | file-rename | **approved** (goal 1) | |
| R65 | `Player/Bootstrap/Tests/PlayerCameraBootstrapTests.cs` contains `PlayerEntityBootstrapTests` | rename file to match class | file-rename | **approved** (goal 1) | |

### 6.2 Archive List (code)

> Signals from the dead-code sweep (reference greps incl. scene/prefab GUID checks, `DotsSystemBootstrap` wiring, git age). Archive = move out of active namespaces (or delete where noted); nothing executes without a verdict.

| # | Path | Signal (why suspected dead) | Verdict | Batch |
|---|------|------------------------------|---------|-------|
| A1 | `DOTS/Test/Archive/**` (36 files incl. `Manual/`) | Already quarantined: own `DOTS.Terrain.Deprecated.asmdef`, Editor-only, `autoReferenced:false`, referenced by no other asmdef. Manual MonoBehaviour harnesses without assertions; modern NUnit suite duplicates coverage. Folder name says Archive. **High** | **archive** (goal 2) | |
| A2 | `DOTS/Debug/` — 7 of 8 files (`DungeonVisualizer`, `EntitiesTest`, `EntityDebugger`, `GlobVisualTest`, `QuickVisualTest`, `SystemDiscoveryTool`, `TerrainHeightVisualizer`) | Zero code refs and zero scene/prefab GUID hits; **no isolating asmdef** so they compile into every build. Keep `SimpleVisualDebugTest` (referenced by `TestHelpers/VisualTestSceneSetup`). **High** | **archive 7, keep SimpleVisualDebugTest** (goal 2) | |
| A3 | `Player/Bootstrap/SimplePlayerMovementSystem.cs` | Double-gated: config flag default-false **and** `#if SIMPLE_PLAYER_MOVEMENT_ENABLED` where the symbol is defined nowhere — unreachable code today. **High** | **archive** (goal 2; remove its `#if` block from bootstrap too) | |
| A4 | `Assets/Scripts/PixelationEffect.cs` | Zero code/scene/prefab refs; untouched since 2025-02. **High** | **archive** (goal 2) | |
| A5 | `Assets/Scripts/TerrainTestCameraController.cs` | Zero code/scene/prefab refs. **High** | **archive** (goal 2) | |
| A6 | `Player/Bootstrap/PlayerCameraBootstrap.cs` | Zero scene attachments; sibling `_WithVisuals` variant IS attached (`sdftest.unity`); base referenced only from markdown guides. **Medium-High** | **archive** (goal 2; update BOOTSTRAP_GUIDE refs) | |
| A7 | `Player/Systems/PlayerCameraSystem.cs` | `ProjectFeatureConfig` comment: "Replaced by CameraEffectResolverSystem"; flag default-false. **Medium-High** | **archive** (goal 2; remove config flag + bootstrap wiring) | |
| A8 | `Player/Systems/CameraFollowSystem.cs` | Same "replaced by" comment, default-false, self-documented test-only. **Medium** | **archive** (goal 2; same flag/wiring removal) | |
| A9 | `Player/Systems/PlayerCinemachineCameraSystem.cs` | Default-false, distinct concern (Cinemachine binding). Owner 2026-07-02: not using Cinemachine, unsure it's useful at all. | **archive** (goal 2; owner call — would be rewritten if ever adopted) | |
| A10 | `Player/Bootstrap/PlayerEntityBootstrap_PureECS.cs` | Default-false alternate implementation; forces `state.Enabled=false` in OnCreate; re-introduces a ground-plane bug pattern the active bootstrap explicitly removed. **Medium** | **archive** (goal 2) | |
| A11 | `Assets/Scripts/LegacyWeatherSystem.cs` | Self-declared legacy; sole reference is `GameManager` (itself in the A12 cluster). **Medium-High** | **archive** (goal 2; with A12 batch) | |
| A12 | Legacy pre-DOTS root cluster (11 files): `GameManager`, `BiomeManager`, `BiomeData`, `BiomeType`, `NoiseType`, `INoiseFunction`, `TerrainChunk`, `TerrainManagerLegacy`, `TerrainType`, `LODTestScript`, `SceneDiagnostics` | Internally cross-referenced (not zero-ref) but architecturally pre-ECS. `SampleScene.unity` — the ONLY scene in Build Settings — runs this cluster (see S4). | **archive cluster + re-point Build Settings at DOTS scene** (goals 1, 2); batch must also handle `SampleScene.unity`, dependent editors (`BiomeDataEditor`, `NoiseVisualizerEditor` — check), and any `.asset` instances of `BiomeData` | |
| A13 | `DOTS/Generation/TerrainGenerationSystem.cs` | `OnUpdate` disables itself and returns before any logic — body is unreachable dead code; superseded by `HybridTerrainGenerationSystem`. Also carries a blob-dispose leak (S1). **High** | **archive** (goal 2; remove bootstrap creation call) | |
| A14 | `Terrain/SDF/TerrainHeightDebugGizmos.cs`, `Terrain/Trees/TreePlacementDebugGizmos.cs` | Both self-labeled "throw-away, DELETE after phase"; phases concluded. **High** | **archive** (goal 2) | |
| A15 | `Assets/Materials/Tests/Tests.asmdef` | Empty assembly — folder contains no scripts. **High** | **archive** (goal 2) | |
| A16 | `DOTS/WFC/HybridWFCSystem.cs` | Never created by any production code; known gap per `TICKETS.md:472`. | **keep — WFC paused, work resuming shortly** (owner 2026-07-02); bootstrap gap stays with the ticket | |
| A17 | `DOTS/Core/TerrainSystem.cs` | Loser of C5 merge (raw `Debug.Log`, SystemBase; survivor `ChunkProcessor` is ISystem + DebugSettings). **High** | **archive** (goal 2; C5 batch removes its bootstrap creation, survivor keeps the validation behavior) | |
| A18 | `DOTS/Modification/PlayerModificationComponent.cs` (+ `GlobRemovalType`), `DOTS/Modification/TerrainModificationSystem.cs` | Self-marked `[Obsolete]` / "LEGACY" in their own comments — old heightmap glob-removal path, superseded by the SDF edit systems (`SDFEdit`/`TerrainEditInputSystem`). `TerrainGlobPhysicsSystem` NOT included (still live). **Medium-High** | **archive** (goal 2; remove bootstrap wiring; verify TerrainGlobPhysicsSystem doesn't depend on them) | |
| A19 | `DOTS/Biome/BiomeBuilder.cs`, `BiomeComponent.cs` | Self-marked LEGACY in own comments; consumed only by the legacy heightmap pipeline and tests, unused by the active SDF path. **Medium** | **AMENDED 2026-07-02: quarantine with R40, not archive** — hard-referenced by `TerrainEntityManager`/`TerrainCleanupSystem` (both quarantined, not deleted); EditMode Biome/Generation tests keep passing against the moved code | |
| A20 | `Assets/Scripts/DOTS/DEPRECATED_FILES.md` | Pre-existing deprecation ledger (2026-01-23) — independently confirms A1/A13/A18/R40 signals. Its "don't delete, archived tests reference these" guard dissolves once A1 removes those tests. | **update per batch, then archive the ledger at end of round** — superseded by this plan | |

> **Archive mechanics (decided 2026-07-02):** "archive" for code = delete in a dedicated, revertable commit — git history is the archive. No code `Archive/` folder is created; the §6.2 rows plus batch commits are the review trail. Every archive batch greps for references first and removes any `DotsSystemBootstrap`/`ProjectFeatureConfig` wiring for the archived systems (flag removal from the config class is part of the batch; a stale flag pointing at deleted code is worse than no flag).

### 6.3 Doc Reorder List

> Per `DOCUMENTATION_SYSTEM_SPEC.md` rules. 84 active docs audited; index has zero broken links but 30 unindexed files.

| # | Doc | Issue (stale status / duplicate / misplaced / unindexed) | Verdict | Batch |
|---|-----|-----------------------------------------------------------|---------|-------|
| D1 | Seam-debug 4-set: `TERRAIN_SEAM_DEBUG_SPEC_OBSOLETE.md`, `_SPEC_v1.md`, `_MESH_SPEC.md`, `_IMPLEMENTATION_REPORT.md` (AI/) | Concluded lineage; OBSOLETE one is even listed under the index's Archives heading while physically in `AI/` | **archive as a set** | 19 |
| D2 | `AI/SKYBOXSPEC.md`, `AI/SKYBOXTESTS.md` | Phase 1/2 complete; forward work lives in `ATMOSPHERE_COLOR_AUTHORITY_SPEC.md`. Keep `SKYBOXPLAN.md` active until Phase 3 lands | **archive 2, keep PLAN** | 19 |
| D3 | Resolved bugfix specs: `AI/PLAYER_TERRAIN_FALLTHROUGH_DEBUG_SPEC.md` (ROOT CAUSE FIXED), `AI/TERRAIN_EDIT_CONTROLS_SPEC.md` (IMPLEMENTED), `AI/CAMERA_IDENTITY_FIX_SPEC.md` (confirmed fixed 2026-02) | stale status in active folder | **archive** | 19 |
| D4 | `AI/STRUCTURE PLACEMENT/RELIC_RENDER_REFACTOR_SPEC.md` | IMPLEMENTED; explicitly superseded by `RELIC_LOD_IMPOSTOR_SPEC.md` | **archive** | 19 |
| D5 | `AI/TerrainHeightMaps/TERRAIN_PLAINS_NOISE_ALGORITHM.md` | COMPLETE; values absorbed by MVP checklist | **archive** | 19 |
| D6 | Root-level stale set: `CURSOR_NamespaceFlattening_Refactor.md`, `multiplayer_evaluation_spec.md`, `ISystem_Usage_Report.md`, `StructureReview.md` (fold open findings into this plan), `DebugTraces/`, `Unity6_Compatibility_Notes.md` | stale/complete records at root | **archive**; `DOCUMENTATION_CHANGELOG.md` → **revive** (record this cleanup round in it) | 19 |
| D7 | `mvp/mvp_feature_list.md`, `mvp/wfc_good_vs_evil_rules.md` | stale ideation; then rename `mvp/` (1 live biome spec left) to `Biomes/` | **archive 2 + rename folder** | 19 |
| D8 | LOD cluster (4 files, 2 folders): root `DOTS_Terrain_LOD_SPEC.md` (checklist's actual source of truth) vs `AI/TERRAIN_LOD_SPEC.md` + `AI/DOTS_Terrain_LOD_Plan.md` + `AI/DOTS_Terrain_LOD_Implementation_Checklist.md` | genuine naming trap — an agent can easily edit the wrong "LOD_SPEC" | **content-diff, keep root SPEC + Checklist, archive the 2 AI/ originals** (merge any unique content into root first) | 19 |
| D9 | ~16 unindexed live docs incl. `TICKETS.md`, `AI/VISTA_GROUND_PLANE_FOG_INVESTIGATION.md`, `AI/GRASS_ECS_SPEC.md`, `AI/SKYBOXPLAN.md`, `AI/TERRAIN_VOXEL_CHUNK_EDIT_SPEC.md`, biome-noise trio, `MVP Movement/` 2 specs | index drift (one-directional: missing entries only) | **add to `DOCUMENT_INDEX.md`** | |
| D10 | Folders `AI/STRUCTURE PLACEMENT/` and `MVP Movement/` | spaces in folder names force %20 links | **rename `STRUCTURE_PLACEMENT/`, `MVP_Movement/` + fix links** | |
| D11 | `Assets/Docs/ai-patterns-other-project/` (5 files) | reference pack from an unrelated project (SmolbeanPlanet3D); false-positive magnet in searches | **relocate to `Assets/Docs/Reference/External/SmolbeanPlanet3D/`** | |
| D12 | ~15 core docs with no Status/Last-Updated header (incl. `AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md`, `PROJECT_STRUCTURE_DOTS.md`, `WFC/MAP_WFC.md`) | fails SYSTEM_SPEC metadata rule; `PROJECT_STRUCTURE_DOTS.md` also stale vs reality | **backfill metadata; rewrite PROJECT_STRUCTURE_DOTS after code moves land** | |
| D13 | `ArtAndDOTS_Pipeline.md` (root) | agent asset-pipeline guide sitting at root | **move to `AI/`** | |

### 6.4 Overlap / Consolidation Candidates

> From the functional overlap audit (§3 Phase 1 item 4) and structure audit exceptions. Verdicts: **merge into X / extract shared utility / keep both (intentional — record why)**. Approved rows execute one-per-batch (§3).

| # | Code A | Code B | Overlapping purpose | Verdict | Batch |
|---|--------|--------|---------------------|---------|-------|
| C1 | `TreeChunkRenderSystem` | `RockChunkRenderSystem`, `PebbleChunkRenderSystem` | 3-way structural clones: same LOD bucket layout, `RenderMeshInstanced` batching, per-camera submission; only config/placement types differ | **extract shared utility** (goals 2, 3) — systems become thin wrappers | |
| C2 | `TreePlacementAlgorithm` (private `TryFindSurfaceHeight`/`ComputeNormal`/`CandidateHash`) | `SurfaceScatterPlacementMath` (the shared versions) | trees never migrated to the shared helper; rocks/pebbles did | **merge into SurfaceScatterPlacementMath** (goal 2) — placement determinism must be verified identical (`SurfaceScatterJitterRegressionTests` + record-hash comparison) | |
| C3 | `TreePlacementDeltaUtility` | `RockPlacementDeltaUtility` | identical remove/sort delta-apply code; only the stage predicate differs | **extract shared utility** (goal 2) — predicate passed in | |
| C4 | Tree/Rock/Pebble `PlacementGenerationSystem` + `PlacementInvalidationSystem` (6 files) | each other | same query/tag-version-gate/ECB flow; lifecycle already shared via `SurfaceScatterLifecycleUtility` — remaining glue intentionally thin | **keep both (intentional)** — per-family systems are the ECS-idiomatic shape; utilities already hold the shared logic | |
| C5 | `ChunkProcessor` | `TerrainSystem` (DOTS/Core) | near-identical no-op validators — both iterate `TerrainData` and warn on `resolution <= 0`; differ only in logging call | **merge into `ChunkProcessor`** (ISystem + DebugSettings; renamed per R9), archive `TerrainSystem` (A17), drop its bootstrap creation | |
| C6 | `TerrainMeshBorderDebugSystem.GetMismatchedPositions` | `TerrainMeshSeamValidatorSystem` border matching | duplicated east/north border-vertex comparison logic | **extract shared utility** (goal 2) | |
| C7 | `SDFTerrainFieldSettings` (legacy sine ground SDF) | `TerrainFieldSettings` (layered noise) | dual ground-SDF singletons, both required at runtime; code comments say legacy fields "remove in Stage 3" | **keep both — defer** to `TERRAIN_ECS_NEXT_STEPS_SPEC.md` Stage 3 (behavior-adjacent; tracked, not a cleanup batch) | |
| C8 | Root biome trio (`BiomeData`/`BiomeManager`/`BiomeType`) | `DOTS/Biome` (`BiomeBuilder`/`BiomeComponent`) | same feature split across two unnamespaced locations | **resolved via A12 + A19** (both sides archived) | |
| C9 | Six test locations / five namespace conventions | each other | one concern (testing) scattered; see R48 | **resolved via R48** (needs-investigation → round 2) | |

### 6.5 Decision Log

> One entry per Phase 2 sitting or mid-execution judgment call. Newest first.

- **2026-07-02** — **Phase 2 sitting completed** (owner + AI, this chat). Cascading calls: (1) legacy pre-DOTS cluster **archived** + Build Settings re-pointed at a DOTS scene (A12/S4); (2) heightmap pipeline **quarantined to `DOTS/Terrain/Legacy`**, not retired — retirement is S11 follow-up (R40); (3) WFC **kept, paused** — owner: "we will do further work on WFC shortly"; bootstrap gap stays with `TICKETS.md:472` (A16/S3); (4) camera sprawl: **all superseded camera/bootstrap variants archived incl. `PlayerCinemachineCameraSystem`** — owner: "currently not using cinemachine, not sure it's even useful" (A6–A10). Archive mechanics: delete in dedicated revertable commits, git history is the archive (see note under §6.2). R45/R46 folder-vs-namespace exceptions kept and documented rather than moved. R48/C9 test consolidation deferred to round 2. R62 file split waived (WFC churn imminent). Every other row: verdict as recorded in its table. Phase 3 execution started same day.
- **2026-07-02** — **Phase 1 inventory completed.** Method: deterministic scripts (type/namespace/attribute extraction, git age+churn) + 8 scoped agent sweeps (naming/idiom audit with `unity-ecs-patterns` loaded, dead-code reference+GUID sweep, structure audit, doc staleness audit, 4 responsibility mappers covering all 348 files) + one orchestrator overlap pass over the responsibility summaries. Manifests written to §6.1–§6.4 (verdicts intentionally empty); out-of-scope findings to §6.7. Notable facts for Phase 2: (a) nearly all systems are `[DisableAutoCreation]` + manually created in `DotsSystemBootstrap`, so "never created" is a reliable deadness signal; (b) the only Build Settings scene runs the pre-DOTS prototype, not `DotsSystemBootstrap` (S4); (c) the `unity-ecs-patterns`/`unity-test-runner` skills exist only in the main worktree's `.claude/skills/`, not in this cleanup worktree — execution-phase workers must load them from `C:\UnityWorkspace\16bitProcGen\.claude\skills\`.
- **2026-07-02** — Goals made explicit per owner review: learnability for a Unity/ECS newcomer, lighter codebase, modern best practices (§1); verdicts must cite the goal served. Added §6.7 so behavior-changing improvement ideas are recorded for follow-up instead of dropped or silently applied.
- **2026-07-02** — Scope expanded per review: folder/namespace structure audit and functional overlap audit added to Phase 1; `unity-ecs-patterns` skill made mandatory for the idiom audit and any code-touching worker; consolidation candidates get their own table (§6.4) and run one-per-batch.
- **2026-07-02** — Plan created; workflow and batch protocol agreed. No inventory run yet.

### 6.6 Batch Log

| Batch | Date | Rows applied | Tests | Commit | Notes |
|-------|------|--------------|-------|--------|-------|
| 0 (baseline) | 2026-07-02 | — | EditMode 219/219 green | a9ec90e | Baseline via Unity 6000.4.1 CLI on the cleanup worktree (first import built its Library) |
| 1 | 2026-07-02 | A1 | 219/219 | 1e94f49 | Deleted Test/Archive tree (29 files + Deprecated.asmdef); only comment mentions remain; DEPRECATED_FILES.md annotated |
| 2 | 2026-07-02 | A2, A4, A5, A14, A15 | 219/219 | 309ffac | 12 zero-ref files deleted; gizmo MonoBehaviours GUID-verified unattached; SimpleVisualDebugTest kept |
| 3 | 2026-07-02 | A3, A6–A10 | 219/219 | 944ceae | 6 camera/bootstrap variants deleted + bootstrap wiring + 5 config flags removed; comment-only mentions left in place |
| 4 | 2026-07-02 | A11, A12, S4 | 219/219 | c538fb5 | Prototype cluster + SampleScene + TerrainChunk.prefab + 11 BiomeData assets + 2 dependent editors deleted; Build Settings → Basic Terrain Scene. **Amendments:** BiomeType/TerrainType/NoiseType enums KEPT (referenced by active Sky code, live glob physics, quarantined Biome files); SceneDiagnostics KEPT (attached in live smoke scene) minus its CheckLegacyTerrain() method |
| 5 | 2026-07-02 | A13, A18 | 210/210 (9 tests of deleted obsolete component removed with it) | fb35c9a | GlobRemovalType extracted to own file (live glob physics uses it); TerrainGlobPhysicsSystem [UpdateAfter] on deleted system stripped with why-comment; bootstrap + config wiring removed. Commit message says 211 — correct count is 210 |
| 6 | 2026-07-02 | C5, A17, R9 | 210/210 (first run failed: missed `[UpdateAfter(typeof(TerrainSystem))]` in HybridTerrainGenerationSystem — fixed, re-ran green) | efb3b4f | Validators merged; survivor TerrainDataValidationSystem in the unconditional slot; warnings now DebugSettings-gated (approved). Sweep lesson: use precise `typeof(X)` greps, no line-exclusion filters |
| 7 | 2026-07-02 | R40, R7, A19 | 210/210 (after 3 compile-fix iterations) | 777d2b4 | 15 files → DOTS/Terrain/Legacy + ns DOTS.Terrain.Legacy; R7 rename + [FormerlySerializedAs] flag; 17 consumers updated. Hazards hit & fixed: UnityEngine.TerrainData ambiguity (aliases), DOTS.Terrain.Debug namespace shadowing UnityEngine.Debug, fully-qualified old-ns refs |
| 8 | 2026-07-02 | R22, R24, R25, R27, R28 | 210/210 (after removing over-added usings) | c29908a | Namespaces added; key finding: NOTHING outside DOTS.Core.Authoring's own assembly uses the config/bootstrap types in code — all other grep matches were comments. Lesson: before adding `using X` to a consumer, confirm its asmdef references X's assembly |
| — (verify) | 2026-07-02 | PlayMode regression check after batches 1–8 | branch **86/96** = baseline (a9ec90e) **86/96**, identical failure lists — **zero regressions** | — | 9 failures all pre-existing: 6 headless-CLI artifacts (WaitForEndOfFrame; player-visual spawn needs rendered frames), 1 skip, 2 genuine pre-existing failures → S14/S15 |
| 9 | 2026-07-02 | R26 | 210/210 | d660fe8 | DebugSettings/DebugController/DebugTestController → DOTS.Core; 45 usings + 7 qualified refs updated project-wide |
| 10 | 2026-07-02 | R41, R44 | 210/210 | 8f69e31 | DOTS/World → DOTS/Impostors; Diagnostics folder merged into Terrain/Debug; metas preserved |
| 11 | 2026-07-02 | R42, R43 | 210/210 | c9b01d7 | Grass folder+namespace renamed. **R43 AMENDED:** TerrainChunkRenderSettings stays put — physical move would need circular asmdef ref (DOTS.Terrain already references Core); namespace collision resolved by R42 alone, relocation deferred to S10 |
| 12 | 2026-07-02 | R1–R6, R63 | 210/210 | d8f67b3 | 6 system renames + 3 flag renames w/ [FormerlySerializedAs] + DungeonGenerationRequest extracted. **R63 AMENDED:** DungeonVisualized stays in its preprocessor-guarded editor-only file |
| 13 | 2026-07-02 | R60, R64, R65 | 210/210 | bf9141a | PlayerComponents.cs split 8-ways (byte-identical definitions); SurfaceNetsJob.cs + PlayerEntityBootstrapTests.cs file renames |
| 14 | 2026-07-02 | R61 | 210/210 | 47e0146 | WFCComponent.cs split into 13 one-type files |
| 15 | 2026-07-02 | C2 | 210/210 (incl. jitter regression + tree determinism tests) | 75628ef | Trees adopt shared placement math; private copies verified constant-identical before deletion |
| 16 | 2026-07-02 | C3 | 210/210 | 74b8c6c | SurfaceScatterDeltaUtility extracted (generic over new IStableLocalIdRecord); family predicates stay; Burst-safe constrained struct calls |
| 17 | 2026-07-02 | C1 | 210/210 | 7674534 | SurfaceScatterRenderCore + per-family SurfaceScatterRenderState extracted; systems 450/415/416 → 208/185/188 lines; divergences (clamp vs modulo, grounding, capacities) preserved exactly |
| 18 | 2026-07-02 | C6 | 210/210 | 569d48b | TerrainChunkMeshBorderUtility extracted; the two implementations were exactly equivalent (same epsilon/predicates) |
| 19 | 2026-07-02 | D1–D8 | n/a (docs only) | — | 21 docs archived into 8 new `Archives/*_2026` subfolders + `DebugTraces/` moved whole; every archived doc got an ARCHIVED (2026-07-02) status line. `mvp/` renamed to `Biomes/` (1 file left). LOD cluster content-diffed: unique namespace/edit-authoritative-promotion/neighbor-clamp/debug-flag notes merged into root `DOTS_Terrain_LOD_SPEC.md` before archiving the 2 AI/ originals. `StructureReview.md` open finding folded into §6.7 as S16. `DOCUMENT_INDEX.md` updated for every moved doc; `DOCUMENTATION_CHANGELOG.md` given a new 2026-07-02 entry |

### 6.7 Improvement Suggestions (out of scope — follow-up work)

> Behavior-changing or scope-exceeding improvements noticed during any phase (see §1 Non-Goals). Never applied in a cleanup batch; each row is a candidate for its own spec/ticket.

| # | Where | Suggestion | Goal served (§1) | Disposition (open / spun off to spec-ticket / rejected) |
|---|-------|------------|------------------|----------------------------------------------------------|
| S1 | `HybridTerrainGenerationSystem`, `TerrainGenerationSystem` | **Bug:** blob-dispose leak — both set `heightData = BlobAssetReference.Null` without `.Dispose()`, violating CLAUDE.md's own pitfall rule. SDF sibling `TerrainChunkDensitySamplingSystem` does it correctly and is the template. Moot for whichever file A13 deletes | 3 | open |
| S2 | `GrassChunkRenderSystem` | Only Grass/Rock/Pebble/Tree render system **without** `[DisableAutoCreation]` — silently auto-creates in every world instead of being config-gated. Add attribute + wire into `DotsSystemBootstrap` | 3 | open |
| S3 | `HybridWFCSystem` / `DotsSystemBootstrap` | Dungeon pipeline silently broken: system never created anywhere though `DungeonRenderingSystem` orders after it. Already tracked at `TICKETS.md:472` — wire under `EnableDungeonSystem` or decide to shelve WFC (A16) | 2, 3 | open (ticket exists) |
| S4 | `ProjectSettings/EditorBuildSettings.asset` | The only build scene (`SampleScene.unity`) runs the legacy pre-DOTS `GameManager` cluster; `DotsSystemBootstrap` lives only in scenes not in Build Settings. Re-point Build Settings at a DOTS scene — prerequisite for the A12 cluster verdict | 1, 2 | open |
| S5 | `TerrainCleanupSystem`, `TerrainSystem`, `WeatherSystem`, `RelicLodSelectionSystem`, `RelicRealizationSystem` (after ForEach swap), Tree/Rock/Pebble render systems | `SystemBase` where `ISystem` fits (no real managed-state need per idiom audit). Legit SystemBase stays: `GrassChunkRenderSystem`, `HybridWeatherSystem`, `DungeonRendering/Visualization`, `HybridWFCSystem`, `GroundPlaneImpostorSystem`, `ScreenEffectResolverSystem`, `HybridTerrainGenerationSystem` | 3 | open |
| S6 | `WeatherSystem`, `HybridWeatherSystem`, `HybridWFCSystem`, `DungeonRenderingSystem`, `DungeonVisualizationSystem`, `HybridTerrainGenerationSystem`, `RelicRealizationSystem` | Legacy query idiom: `GetEntityQuery(...).ToEntityArray()` + manual Get/Set loops, `Entities.ForEach` → `SystemAPI.Query`. Also per-call `EntityQuery` allocations in `TerrainEditInputSystem`, `TerrainModificationSystem` | 3 | open |
| S7 | Terrain pipeline (`TerrainChunkNeeds*` tags), `GrassChunkNeedsRebuild` | Structural-change churn: one-shot request tags added/removed via ECB every rebuild — candidates for `IEnableableComponent` + `SetComponentEnabled`. Wide blast radius; needs its own spec | 3 | open |
| S8 | `GlideState`, `SlingshotChargeState` | Same transient-state-as-structural-change pattern; `LandingImpactEvent` already models the correct enableable approach | 3 | open |
| S9 | `PlayerMovementSystem`, `PlayerInputSystem`, `CameraFollowSystem`, `StructureAnchorPlanningSystem`, `GlideSystem`, `PlayerBootstrapFixedRateInstaller` | `[BurstCompile]` hygiene: struct-level attribute with no method-level attribute (no-op/misleading) on the first four; missing-but-feasible Burst on the last two | 3 | open |
| S10 | `DOTS.Terrain.asmdef` | Monolith swallows nearly every DOTS feature (Structures, WFC, Weather, Impostors, Biome, Compute, Debug), contradicting `PROJECT_STRUCTURE_DOTS.md`'s "modular assemblies" claim. Silver lining: makes §6.1 namespace renames low-risk. Modularization = separate spec | 1, 3 | open |
| S11 | Heightmap pipeline (`HybridTerrainGenerationSystem` + `DOTS/Core` data types + `TerrainEntityManager`/`TerrainDataBuilder`) | Retire the legacy heightmap path entirely once confirmed the SDF path covers all needs — the architectural migration CLAUDE.md anticipates. Own spec; A12/A13/R40 only quarantine it | 2 | open |
| S12 | `HybridWFCSystem.PropagateConstraintsWithComputeShader` | Unused GPU stub ("TODO: implement") + O(n) linear neighbor scans per frame — if WFC survives A16, replace scans with a position-keyed lookup | 3 | open |
| S14 | `TreePlacementEditModeTests.GeneratePlacements_VariantAndYaw_AssignedWithinExpectedRange` | **Pre-existing test failure** (fails on pre-cleanup baseline too; found during round-1 PlayMode verification): no accepted placement ever selects a non-zero tree variant — variant-selection logic or test expectation is wrong | 3 | open |
| S15 | `PlayerWallContactCommandPlayModeTests.GroundedJump_DoesNotImmediatelyRegroundWhileStillAscending` | **Pre-existing test failure** (fails on baseline too): grounded jump does not switch to Ballistic on the takeoff frame — possible regrounding bug in movement/grounding hand-off | 3 | open |
| S16 | `Assets/Scripts/Terrain/` (Bootstrap/Meshing/Rendering/Diagnostics subfolders) vs `Assets/Scripts/DOTS/Terrain/` | Folded in from archived `StructureReview.md` (D6, 2026-07-02): a parallel non-DOTS terrain tree with its own Bootstrap/Meshing/Rendering/Diagnostics folders still coexists alongside the DOTS terrain tree, duplicating bootstrap/registration responsibility (`Assets/Scripts/Terrain/Bootstrap` vs `Assets/Scripts/Player/Bootstrap`). Root-level loose MonoBehaviours (`GameManager.cs`, `TerrainManager.cs`, `WeatherSystem.cs`, `PixelationEffect.cs`) and the DOTS/Test-Tests-TestHelpers intermingling that StructureReview also flagged were already resolved by A1/A11/A12 — this row is the one StructureReview finding still open. Needs its own audit/spec before archiving or merging | 1, 3 | open |

---

## 7. Acceptance Criteria (per round)

- Every applied change traces to an approved row in §6.1–§6.4 and a §6.6 batch entry with passing tests.
- No orphaned `.meta` files (`git status` clean of unmatched meta churn).
- `DOCUMENT_INDEX.md` and folder indexes reflect all doc moves; superseded docs marked per `DOCUMENTATION_SYSTEM_SPEC.md`.
- Zero behavior change: EditMode suite green before and after the round; no new console errors on domain reload.
- **Learnability check:** a developer new to Unity/ECS can locate the system responsible for a given behavior from names and folder structure alone, without grepping — spot-check a few behaviors after each round.
- Out-of-scope improvements observed during the round are captured in §6.7, not lost and not silently applied.
- Another agent reading only this doc can answer: what was renamed, what was archived, what's still pending.

## Related Docs

- [/CLAUDE.md](/CLAUDE.md) — naming conventions and DOTS rules this plan enforces
- [DOCUMENT_INDEX.md](../DOCUMENT_INDEX.md) — discovery surface updated by doc batches
- [DOCUMENTATION_SYSTEM_SPEC.md](../DOCUMENTATION_SYSTEM_SPEC.md) — canonical-doc and archive rules for §6.3 work
- [PROJECT_STRUCTURE_DOTS.md](../PROJECT_STRUCTURE_DOTS.md) — target folder layout for code moves
