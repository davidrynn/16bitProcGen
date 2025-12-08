# Terrain Systems Code Audit

## 1. Summary
- Two parallel terrain stacks exist: an older GameObject-based chunk manager/mesher and several DOTS compute-driven pipelines, while a newer SDF/Surface Nets DOTS pipeline appears to be the active direction.
- Multiple DOTS generation paths coexist, including a disabled `TerrainGenerationSystem` and an experimental `HybridTerrainGenerationSystem` with manual input hooks, creating dead code and maintenance drag.
- Archived/manual DOTS test harnesses remain in the repo, further obscuring which systems are current.
- The classic `TerrainManager`/`TerrainChunk` pair owns chunk streaming, biome evaluation, LOD, texture authoring, and mesh rebuilds in a single MonoBehaviour flow, leading to tight coupling and heavy per-frame work.

## 2. Redundant / Obsolete Code
- **Location(s):** `Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs`
  - **Issue:** The DOTS terrain generation system returns immediately every frame because `disableTerrainGenerationSystem` is hardcoded `true`, so none of its compute-shader generation logic runs. This makes the system effectively dead while still compiled and updated in the world list.
  - **Evidence:** Early-return guard in `OnUpdate` prevents any processing.【F:Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs†L37-L60】
  - **Recommendation:** Remove or archive this system, or re-enable it behind a configuration flag so only one DOTS generation path remains.

- **Location(s):** `Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs`
  - **Issue:** This hybrid compute/DOTS system still polls `Input.GetKeyDown(KeyCode.Space)` to force regeneration and performs manual buffer initialization each update, signalling it was built for experiments rather than production. It overlaps with the newer SDF/Surface Nets pipeline but is still active in the simulation group.
  - **Evidence:** Manual input trigger and per-frame compute-manager fetch in `OnUpdate` while running in `SimulationSystemGroup`.【F:Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs†L53-L88】
  - **Recommendation:** Decide whether to retire this hybrid path in favor of the SDF pipeline; if kept, gate the debug input behind a development flag and move setup to `OnCreate`.

- **Location(s):** `Assets/Scripts/DOTS/Test/Archive/Manual/HybridGenerationTest.cs` (and other files under `Assets/Scripts/DOTS/Test/Archive/Manual`)
  - **Issue:** Manual MonoBehaviour test harnesses for the hybrid compute pipeline remain under `Archive`, creating noise and potential confusion about supported entry points.
  - **Evidence:** Archived test MonoBehaviour that spins up `HybridTerrainGenerationSystem` and bespoke entity managers for experimentation.【F:Assets/Scripts/DOTS/Test/Archive/Manual/HybridGenerationTest.cs†L9-L67】
  - **Recommendation:** Move these to a clearly marked legacy/experimental folder outside the main scripts tree or delete after extracting any still-relevant setup utilities.

- **Location(s):** `Assets/Scripts/TerrainManager.cs` and `Assets/Scripts/TerrainChunk.cs`
  - **Issue:** Full GameObject-based terrain streaming, biome sampling, and LOD mesh rebuilding coexist with DOTS pipelines. The chunk generator instantiates prefabs, rebuilds meshes and colliders on LOD changes, and manages visibility per-frame—duplicating responsibilities present in DOTS chunk systems.
  - **Evidence:** `TerrainManager.Update` drives visibility and LOD for a dictionary of `TerrainChunk` instances; `TerrainChunk.UpdateLOD` regenerates meshes at multiple resolutions when distance thresholds change.【F:Assets/Scripts/TerrainManager.cs†L60-L129】【F:Assets/Scripts/TerrainChunk.cs†L216-L238】
  - **Recommendation:** Decide whether the classic path should remain supported; if DOTS is primary, mark these as legacy and remove from default scenes/prefabs to avoid split investment.

## 3. General Code Health
- **Terrain generation/debug systems (DOTS compute path)**
  - **Issue:** `HybridTerrainGenerationSystem` mixes runtime logic with debug controls and per-frame singleton lookups, increasing frame costs and risking accidental regeneration in builds.
  - **Location(s):** `HybridTerrainGenerationSystem.OnUpdate` input polling and manager retrieval.【F:Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs†L53-L88】
  - **Impact:** Unpredictable behavior in release builds; unnecessary per-frame work; hard to reason about lifecycle.
  - **Recommendation:** Move debug hooks behind `#if UNITY_EDITOR` or a config component; cache dependencies in `OnCreate`; split performance metrics into a separate system/utility.

- **Classic terrain manager/mesher**
  - **Issue:** `TerrainManager` handles biome lookup, chunk instantiation, visibility culling, texture creation, and LOD orchestration in one MonoBehaviour. `TerrainChunk` rebuilds full meshes and mesh colliders on every LOD change.
  - **Location(s):** Monolithic update loop and chunk creation in `TerrainManager`; LOD mesh regeneration in `TerrainChunk`.
  - **Impact:** Tight coupling, hard to test/extend, and runtime allocations/collider rebuilds on LOD switches can stall the main thread as view distance grows.
  - **Recommendation:** If kept, split responsibilities (biome sampling, chunk pooling, LOD policy, rendering) into separate components and cache generated meshes per LOD level to avoid repeated rebuilds.

- **Archived manual tests**
  - **Issue:** Archived MonoBehaviour tests live alongside runtime code, bringing in scene dependencies and confusing the supported entry points for terrain generation.
  - **Location(s):** `Assets/Scripts/DOTS/Test/Archive/Manual/*`.
  - **Impact:** Increased cognitive load; risk of outdated patterns being copied.
  - **Recommendation:** Relocate to a documentation-only folder or delete; keep only minimal automated tests under `Assets/Scripts/DOTS/Tests`.

## 4. DOTS vs Non-DOTS Architecture
- The current DOTS terrain pipeline centers on SDF density fields and Surface Nets meshing (`TerrainChunkMeshBuildSystem`, `TerrainChunkRenderPrepSystem`) that build `TerrainChunkMeshData`, apply bounds/transforms, and tag chunks for render upload in the simulation group.【F:Assets/Scripts/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs†L8-L53】【F:Assets/Scripts/Terrain/Meshing/TerrainChunkRenderPrepSystem.cs†L9-L87】
- In parallel, the classic `TerrainManager`/`TerrainChunk` flow spawns GameObject prefabs, samples biomes, and rebuilds meshes/LODs on the main thread.【F:Assets/Scripts/TerrainManager.cs†L55-L129】【F:Assets/Scripts/TerrainChunk.cs†L216-L238】
- An intermediate DOTS compute path (`HybridTerrainGenerationSystem` and the disabled `TerrainGenerationSystem`) still exists but overlaps with the newer SDF pipeline and retains debug scaffolding.【F:Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs†L37-L60】【F:Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs†L53-L88】
- **Direction:** Treat the SDF/Surface Nets DOTS pipeline as primary; mark the GameObject terrain path as legacy and phase out the compute-based hybrid systems unless they provide unique GPU features. Shared utilities (biome sampling, noise evaluation, LOD policies) should be extracted into reusable services consumed by whichever runtime path remains.

## 5. Suggested Refactor Roadmap
1. **Retire disabled DOTS generation system**  
   - **Type:** Cleanup  
   - **Files:** `Assets/Scripts/DOTS/Generation/TerrainGenerationSystem.cs`  
   - **Effort:** Small  
   - **Risk:** Low  
   - **Notes:** Remove from update groups or delete; if needed later, gate via build-time define.

2. **Decide fate of HybridTerrainGenerationSystem**  
   - **Type:** Consolidation  
   - **Files:** `Assets/Scripts/DOTS/Generation/HybridTerrainGenerationSystem.cs`  
   - **Effort:** Medium  
   - **Risk:** Medium  
   - **Notes:** If replaced by SDF pipeline, archive it; otherwise, strip debug input and move setup out of `OnUpdate`.

3. **Isolate legacy GameObject terrain path**  
   - **Type:** Cleanup/clarity  
   - **Files:** `Assets/Scripts/TerrainManager.cs`, `Assets/Scripts/TerrainChunk.cs`, related prefabs/scenes  
   - **Effort:** Medium  
   - **Risk:** Medium  
   - **Notes:** Move into a `Legacy` folder, remove from default scenes, and document supported entry points.

4. **Split responsibilities in classic terrain manager (if kept)**  
   - **Type:** Clarity/performance  
   - **Files:** `Assets/Scripts/TerrainManager.cs`, `Assets/Scripts/TerrainChunk.cs`  
   - **Effort:** Medium  
   - **Risk:** Medium  
   - **Notes:** Extract biome sampling, chunk pooling, and LOD policy into separate components; cache meshes per LOD to avoid collider rebuilds on every change.

5. **Clean archived manual DOTS tests**  
   - **Type:** Cleanup  
   - **Files:** `Assets/Scripts/DOTS/Test/Archive/Manual/*`  
   - **Effort:** Small  
   - **Risk:** Low  
   - **Notes:** Remove or relocate to documentation; ensure automated tests remain discoverable.

6. **Document the primary terrain pipeline**  
   - **Type:** Clarity  
   - **Files:** Project docs (e.g., `Assets/Docs`), README  
   - **Effort:** Small  
   - **Risk:** Low  
   - **Notes:** Add a short “Current Terrain Stack” section describing the SDF DOTS flow and explicitly marking legacy systems.
