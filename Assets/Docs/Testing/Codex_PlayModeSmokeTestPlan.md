# Codex PlayMode Smoke Test Plan (Phase 0 Notes)

## Target scene
- **Scene:** `Assets/Tests/Scenes/Smoke_BasicPlayable.unity`
- **Bootstrap requirements:**
  - `DotsSystemBootstrap` (MonoBehaviour) with a `ProjectFeatureConfig` that enables the player systems.
  - `TerrainBootstrapAuthoring` (MonoBehaviour) to seed SDF terrain chunk entities and the field settings singleton.

## Entity/component identifiers
- **Player entity:** `DOTS.Player.Components.PlayerTag`
  - Required movement components for the test: `PlayerInputComponent`, `PlayerMovementState`, `LocalTransform`, `PhysicsVelocity`.
- **Camera entity:** `DOTS.Player.Components.MainCameraTag`
- **Terrain entities:**
  - At least one `DOTS.Terrain.SDF.TerrainChunk`
  - `DOTS.Terrain.SDF.SDFTerrainFieldSettings` singleton

## Movement observation strategy
- Inject movement by writing to `PlayerInputComponent.Move` (e.g., `(0, 1)` forward) on the player entity for a few frames.
- The scene/config should **disable `PlayerInputSystem`** so the test can own the input values.
- Assert that player `LocalTransform.Position` changes on XZ by at least a small epsilon after N frames.

## Project-specific gotchas
- **All DOTS systems are `[DisableAutoCreation]`**, so they only run when enabled by `DotsSystemBootstrap` with a `ProjectFeatureConfig` asset.
- `PlayerEntityBootstrap` is the preferred runtime spawn path for tests (adds `PlayerMovementState` and camera entity).
- `TerrainBootstrapAuthoring` runs via `Start()` and requires `World.DefaultGameObjectInjectionWorld` to exist.
- Tests will load the scene via `EditorSceneManager.LoadSceneAsyncInPlayMode` in the editor to avoid Build Settings dependencies.
