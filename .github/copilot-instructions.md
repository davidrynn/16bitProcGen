# Copilot Instructions for 16bitProcGen
tools: ['vscode', 'execute', 'read', 'edit', 'search', 'web', 'agent', 'todo']
## Precedence
If instructions conflict: safety > architecture rules > performance > style > logging verbosity. If possible, explain to user that there is a conflict and which instruction took precedence.

## Project Snapshot
Unity 6.2+ (2022 LTS only if explicitly needed). Entities 1.3+, Entities Graphics (Hybrid Renderer v2) current. Retro 16-bit procedural terrain/dungeon sandbox. **DOTS-first:** runtime logic in systems/components; MonoBehaviours only for authoring, UI, and bootstrap.

**Key Directories**
- Assets/Scripts/ — gameplay code (DOTS systems, components, managers)
- Assets/Scripts/DOTS/ — core ECS systems (terrain, WFC, weather, modification)
- Assets/Scripts/Player/ — player systems (movement, camera, input, bootstrap)
- Assets/Resources/Shaders/ — compute shaders (TerrainNoise, WFCGeneration, etc.)
- Assets/Docs/ — specs and architecture docs
- Assets/.cursor/plans/ — production roadmap and feature plans

**Production Roadmap**
Assets/.cursor/plans/game-production-plan-7ea46cb6.plan.md drives priorities. Review "Immediate Next Steps" before work.

## Core Architecture

**Terrain Generation (Heightmap path)**
Legacy/parallel heightmap flow: TerrainEntityManager → TerrainDataBuilder → HybridTerrainGenerationSystem → GPU compute → BlobAsset → Mesh.
- Entity spawn: TerrainEntityManager (MonoBehaviour) builds chunk entities via TerrainDataBuilder.
- GPU compute: HybridTerrainGenerationSystem orchestrates shaders through ComputeShaderManager.
- Data: results stored in TerrainHeightData blob assets (dispose properly).
- Mesh gating: needsGeneration / needsMeshUpdate flags on TerrainData.
- Transform sync: TerrainTransformSystem pushes TerrainData worldPosition/rotation/scale into LocalTransform.

**Terrain Generation (SDF / Surface Nets path)**
Primary path for destructible terrain. Follow Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md. Use the SDF + Surface Nets ECS pipeline; do not extend HybridTerrainGenerationSystem for SDF unless the spec explicitly instructs. Keep heightmap and SDF pipelines decoupled; SDF work should align with the spec’s data flow, components, and systems.

**WFC Dungeon Flow**
Collapse state → compute shader → prefab instantiation → rendering.
- HybridWFCSystem with deterministic seeding (DebugSettings.UseFixedWFCSeed, default 12345) using Unity.Mathematics.Random.
- DungeonPrefabRegistry supplies baked entity prefabs.
- DungeonRenderingSystem / DungeonVisualizationSystem handle instantiation.
- WFCPatternData blob assets store patterns.

**GPU Compute Contracts**
- Shaders live in Assets/Resources/Shaders/ with names matching Resources.Load.
- Kernels/constants must mirror C# ↔ .compute. Shader list: TerrainNoise, WFCGeneration, TerrainModification, WeatherEffects, StructureGeneration, TerrainErosion, TerrainGlobRemoval.
- Kernel mismatches surface as FindKernel failures in ComputeShaderManager.InitializeKernels().

**Configuration**
TerrainGenerationSettings (Resources/TerrainGenerationSettings.asset): performance knobs (maxChunksPerFrame, buffers), noise params, mesh height scale, debug toggles, terrain thresholds. Prefer inspector values over hardcoding.

## DOTS Architecture Standards

**System Rules (Always)**
- Systems are partial, one class per file, filename matches class name.
- Use ISystem for new systems; prefer struct systems over SystemBase unless inheritance is needed.
- Use EntityCommandBuffer for structural changes; keep Burst-safe paths (avoid managed refs in jobs).
- Use unique class names (no namespace-only disambiguation).

**BlobAssets**
- Reference-counted: dispose before reassigning. Types: TerrainHeightData, TerrainModificationData, WFCPatternData.
- Creation: BlobBuilder → CreateBlobAssetReference → assign. On reassign: if (oldBlob.IsCreated) oldBlob.Dispose().

**Debug Logging**
- Never use Debug.Log in systems. Use DOTS.Terrain.Core.DebugSettings.* loggers (Terrain, WFC, Weather, Rendering, Test). Add new flags to DebugSettings instead of preprocessor directives. Defaults: all false; EnableTestSystems gates test-only systems.

## Making Changes

**Extend, Don’t Duplicate**
- Prefer augmenting existing systems: HybridTerrainGenerationSystem, HybridWFCSystem, DungeonRenderingSystem, TerrainTransformSystem, TerrainModificationSystem, TerrainGlobPhysicsSystem.
- Extend via partial class in a new file; avoid overlapping systems.

**Structural Changes**
- Obtain ECB from EndSimulationEntityCommandBufferSystem.Singleton; perform structural ops deferred. Reserve direct EntityManager for MonoBehaviours like TerrainEntityManager.

**Compute Kernels**
- Add kernel to shader file under Assets/Resources/Shaders/; mirror constants/buffer layouts in C#; update ComputeShaderManager cache if needed; add smoke test; test with debug logging enabled.

**Debug Features**
- Add toggle to DebugSettings (default false); gate logging; add helper LogYourSystem methods; update DebugController if inspector control is required.

## Current Development Focus

Phase 1: Core Player Experience (from plan)
1) Magic Hand System (Scripts/Player/MagicHand/): MagicHandComponent, MagicHandInputSystem, MagicHandVisualizationSystem.
2) Slingshot Movement (Scripts/Player/Movement/): SlingshotMovementComponent, SlingshotInputSystem, SlingshotTrajectorySystem.
3) Resource Collection (Scripts/Resources/): extend TerrainGlobComponent; integrate with TerrainGlobPhysicsSystem.
4) Basic HUD: resource counters, hand charge indicator.

Short-term world work: biome blending/transitions; structure placement beyond dungeons; reuse blob/component patterns for data storage.

Architecture principle: if legacy code conflicts with DOTS-first, prefer pure DOTS unless Unity tooling blocks it.

## Reference Docs

- Assets/Scripts/DOTS/Test/Testing_Documentation.md — test harness catalog
- Assets/Docs/AI_Instructions.md — SPEC → TEST → CODE workflow
- Assets/Scripts/Player/Bootstrap/BOOTSTRAP_GUIDE.md — pure-code DOTS scene setup patterns
- Assets/README.md — DOTS authoring checklist
- Assets/Docs/AI/TERRAIN_ECS_NEXT_STEPS_SPEC.md — SDF terrain roadmap

## Common Pitfalls

- Missing partial on systems → source generators fail.
- Debug.Log in systems → use DebugSettings.
- BlobAsset leaks → dispose before reassigning.
- Compute shader name/kernel mismatches → verify Resources.Load names and kernel strings.
- Structural changes without ECB → breaks Burst in jobs.
- Duplicating existing systems instead of extending.
- Hardcoded config → use TerrainGenerationSettings.

## Version / Platform Targets

- Unity: 6.2+ primary; 2022 LTS only if explicitly required.
- Packages: com.unity.entities 1.3+, com.unity.entities.graphics current Hybrid Renderer v2, com.unity.mathematics, com.unity.collections, com.unity.jobs, com.unity.burst.
- Render pipeline: Built-in or URP (project-configurable).
