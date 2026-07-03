# Project Structure Review (DOTS/ECS Focus)

## Scope
This review focuses on the top-level folder layout under `Assets/` with an emphasis on DOTS/ECS organization. The goal is to highlight structural inconsistencies and signs of drift from a unified DOTS/ECS architecture.

## Observations & Inconsistencies

### 1) Mixed DOTS and non-DOTS gameplay code at the same level
- `Assets/Scripts/` contains DOTS-focused subtrees (`Assets/Scripts/DOTS`, `Assets/Scripts/Player/Systems`, etc.) **and** legacy/MonoBehaviour-style files sitting directly in `Assets/Scripts/` (e.g., `GameManager.cs`, `TerrainManager.cs`, `WeatherSystem.cs`, `PixelationEffect.cs`).
- This creates ambiguity about the canonical gameplay entry points and whether systems are intended to run under DOTS/ECS or classic Unity behaviours.

### 2) DOTS systems split across feature folders and legacy terrain folders
- DOTS terrain-related code lives in `Assets/Scripts/DOTS/Core`, `Assets/Scripts/DOTS/Generation`, `Assets/Scripts/DOTS/Modification`, and `Assets/Scripts/DOTS/WFC`.
- Simultaneously, non-DOTS terrain code exists in `Assets/Scripts/Terrain/` with its own `Bootstrap`, `Meshing`, `Rendering`, and `SDF` subfolders.
- The split suggests two parallel terrain architectures with overlapping responsibilities (terrain meshing/rendering generation vs DOTS generation systems), making it unclear which one is authoritative.

### 3) Testing and helper content intermingled with production systems
- `Assets/Scripts/DOTS/Test`, `Assets/Scripts/DOTS/Tests`, and `Assets/Scripts/DOTS/TestHelpers` contain runtime harnesses and docs alongside core systems.
- `Assets/Scripts/Player/Test` and various test markdown files are embedded inside production system trees.
- This mixing makes it harder to separate runtime code from tooling/test harnesses, especially in DOTS where assembly definitions and compile targets are typically isolated.

### 4) Multiple overlapping “bootstrap” concepts
- There is a `Assets/Scripts/Terrain/Bootstrap` and `Assets/Scripts/Player/Bootstrap` containing multiple bootstrap guides and installers.
- There is also `Assets/Scripts/Player/PlayerComponentRegistration.cs` and `Assets/Scripts/Terrain/Rendering/UnityComponentRegistrations.cs` suggesting additional bootstrapping/registration in different locations.
- The duplication of bootstrap responsibility across feature folders indicates inconsistent startup patterns and refactor churn.

### 5) Docs in multiple locations without a single canonical home
- Documentation exists in `Assets/Docs/` (with `Archives`, `WFC`, `AI`, `DebugTraces`), but also in multiple `Assets/Scripts/**` folders (e.g., `PURE_ECS_MIGRATION.md`, `BOOTSTRAP_GUIDE.md`, `SeamlessTerrain_Solution.md`, `VERIFICATION_SUMMARY.md`).
- There is also a root-level `TERRAIN_SYSTEMS_CODE_AUDIT.md` and `Assets/README.md`.
- This dispersion makes it difficult to find authoritative guidance on DOTS architecture decisions or current standards.

### 6) DOTS assembly definitions coexist with legacy or mixed assemblies
- DOTS assemblies appear under `Assets/Scripts/DOTS` (`DOTS.Terrain.asmdef`, `DOTS.Terrain.Tests.asmdef`) while non-DOTS assemblies live under `Assets/Scripts/Player` (`Player.asmdef`, `DOTS.Player.Bootstrap.asmdef`, `DOTS.Player.Components.asmdef`).
- The presence of DOTS-named assemblies outside the DOTS root, and multiple assemblies for the same feature (player), suggests a partially migrated layout rather than a consolidated DOTS module.

### 7) Art/Prefabs/Scenes are not clearly mapped to DOTS vs non-DOTS
- `Assets/Prefabs`, `Assets/Scenes`, and `Assets/SubScenes` are present but there’s no obvious separation between DOTS subscenes and classic scenes beyond the top-level folder name.
- The existence of `Assets/SubScenes` suggests DOTS usage, but the extent of DOTS vs non-DOTS scene content is not distinguishable from the folder structure alone.

## Summary
The current layout shows evidence of multiple architecture iterations: DOTS/ECS systems, legacy MonoBehaviour code, and overlapping bootstrap pipelines all coexist across the same top-level script roots. Documentation and tests are distributed across feature folders and `Assets/Docs`, and DOTS assemblies are not consistently grouped. This makes it difficult to identify the current “source of truth” for systems, especially for terrain and player subsystems.

## Suggested Placement Rules for New DOTS Files (During Refactor)
These suggestions focus on **where to place new DOTS/ECS files** while refactors are in progress, without requiring immediate structural overhauls.

### A) Default location for new DOTS systems/components
- **Place new ECS code under** `Assets/Scripts/DOTS/<Feature>/...` (e.g., `Assets/Scripts/DOTS/Terrain`, `Assets/Scripts/DOTS/Player`), even if legacy counterparts exist elsewhere.
- Prefer `Systems`, `Components`, `Authoring`, and `Baking` subfolders inside each feature to mirror DOTS conventions and reduce ambiguity.

### B) Keep DOTS test and harness code isolated
- **Place new DOTS tests** under `Assets/Scripts/DOTS/Tests` and/or a dedicated `Assets/Scripts/DOTS/TestHelpers` folder, rather than mixing them into production feature trees.
- If a test is feature-specific (e.g., WFC), prefer `Assets/Scripts/DOTS/<Feature>/Tests` instead of `Assets/Scripts/DOTS/<Feature>` root.

### C) Prefer DOTS-first locations for bootstrap and registrations
- **Place new DOTS bootstrap/installers** under `Assets/Scripts/DOTS/<Feature>/Bootstrap` to avoid cross-feature bootstrapping and redundant registrations.
- Keep Unity-component bridge registrations under the same feature folder as the related ECS system.

### D) Document new DOTS changes in one place
- **Place new DOTS documentation** under `Assets/Docs/DOTS/` (or add this folder) and link to it from `Assets/Docs/README` (if created later).
- Avoid adding new architecture notes under `Assets/Scripts/**` unless tightly scoped to a specific system implementation.

### E) Prefer DOTS assembly definitions in DOTS feature roots
- If new assemblies are needed for DOTS systems, **place `.asmdef` files inside the corresponding `Assets/Scripts/DOTS/<Feature>` folder** to keep assembly boundaries aligned with feature ownership.
