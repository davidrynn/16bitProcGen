 # Copilot Instructions for 16bitProcGen

## Project snapshot
- Unity Entities 1.x + GPU compute shaders power a retro 16-bit terrain/dungeon sandbox; gameplay scripts live under `Assets/Scripts`, design docs under `Assets/Docs`, and Cursor plans under `Assets/.cursor/plans`.
- Default stance is **pure DOTS-first**: keep runtime code in systems and components; only fall back to MonoBehaviours where Unity still requires it (eg. authoring, UI, scene bootstrap).
- `16bit_production_plan_with_emotion.md` (Downloads) and `Assets/.cursor/plans/game-production-plan-7ea46cb6.plan.md` drive the roadmap—scan the “Immediate Next Steps” before picking up work.

## Core architecture
- Terrain: `TerrainEntityManager` spawns entities using `TerrainDataBuilder`; `HybridTerrainGenerationSystem` orchestrates compute-shader noise via `ComputeShaderManager`, writes `TerrainHeightData` blobs, then mesh rebuilds gate on `needsGeneration/needsMeshUpdate`.
- WFC dungeons: `HybridWFCSystem` carries collapse state with deterministic seeding (`DebugSettings.UseFixedWFCSeed`/`FixedWFCSeed`); `DungeonRenderingSystem`/`DungeonVisualizationSystem` materialize baked prefabs from the `DungeonPrefabRegistry` singleton.
- GPU integration expects shader assets in `Assets/Resources/Shaders/**` with names that match the `Resources.Load` lookups (`TerrainNoise`, `WFCGeneration`, etc.).
- World transforms: `TerrainTransformSystem` syncs `TerrainData.worldPosition/rotation/scale` into `LocalTransform`; authoring-side prefabs must bake transform components.

## Architecture & safety rails
- Keep every DOTS system `partial` and one-per-file (`Assets/README.md`); expanding behavior means extending the existing partial class, not spinning up new systems with overlapping jobs.
- Aim for **burst-friendly, jobified paths**: if a MonoBehaviour currently drives logic, prefer migrating that behavior into DOTS unless Unity tooling blocks it.
- Blob assets (`TerrainHeightData`, `TerrainModificationData`, `WFCPatternData`) are reference-counted—dispose or null old blobs before reassigning.
- Use `DOTS.Terrain.Core.DebugSettings` toggles (`EnableWFCDebug`, `EnableRenderingDebug`, etc.) instead of direct `Debug.Log` to keep console noise manageable.

## Developer workflows
- Play Mode validation remains the primary loop: open `Assets/Scenes/TestScenes/WFCTestScene.unity` or the terrain test scenes and drive them via `AutoTestSetup`, `HybridTestSetup`, or `WFCTestSetup` MonoBehaviours.
- Terrain quick-check: pair `TerrainEntityManager` with `HybridGenerationTest`; inspector toggles let you force regeneration (`Space` key hook) and adjust `maxChunksPerFrame` via `TerrainGenerationSettings`.
- WFC regression: run `WFCTestSetup` or `WFCDungeonRenderingTest` to verify deterministic collapses and prefab placement (watch for seed `12345` in console output).
- Compute shader smoke tests live in `Scripts/DOTS/Test/BasicComputeShaderTest.cs` and `SimpleComputeTest.cs`; these ensure kernel names stay in sync with `.compute` files.

## Making changes
- Prefer augmenting established systems (`HybridTerrainGenerationSystem`, `HybridWFCSystem`, `DungeonRenderingSystem`, `TerrainTransformSystem`) rather than duplicating pipelines; they already track perf counters and job scheduling flags.
- Structural entity mutations inside systems should use an `EntityCommandBuffer` for Burst safety (follow `DungeonRenderingSystem` pattern); reserve direct `EntityManager` work for MonoBehaviours like `TerrainEntityManager`.
- Compute kernels/constants must stay mirrored between C# and shader code; a mismatch will surface as `FindKernel` failures during `ComputeShaderManager.InitializeKernels()`.
- When introducing new debug switches, add them to `DebugSettings` with defaults `false` and gate any verbose logging behind them.

## Roadmap cues for AI agents
- Immediate focus (per production plan): Magic Hand interaction, slingshot traversal, resource collection, and HUD scaffolding—expect to create new DOTS components/systems under `Scripts/Player/**` and `Scripts/Resources/**`.
- Short-term world work includes biome blending and structure placement; reuse existing blob/component patterns instead of bespoke data stores.
- If architecture conflicts with plan best practices, prioritize the plan—refactors toward full DOTS compliance are encouraged even if legacy hybrid code suggests otherwise.

## Reference material
- `Assets/SPEC.md`, `Assets/DOTS_Migration_Plan.md`, and `Assets/Docs/ArtAndDOTS_Pipeline.md` outline current ECS migration status and art pipeline expectations.
- `Assets/Scripts/DOTS/Test/Testing_Documentation.md` documents every diagnostic harness and how to execute it.
- Cursor-automation rules live in `Assets/AI_Instructions.md`; align behavior changes with the plan + these instructions before editing.
