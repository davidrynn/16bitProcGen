# Smoke_BasicPlayable Scene Setup Checklist

> Target scene path: `Assets/Tests/Scenes/Smoke_BasicPlayable.unity`

## 1) Create the scene
1. Create a new empty scene.
2. Save it as `Assets/Tests/Scenes/Smoke_BasicPlayable.unity`.

## 2) Add DOTS system bootstrap
1. Create an empty GameObject named `DOTS Bootstrap`.
2. Add the `DotsSystemBootstrap` component.
3. Create a new `ProjectFeatureConfig` asset (via **Assets → Create → Config → ProjectFeatureConfig**).
4. Save the asset as `Assets/Tests/Configs/SmokeProjectFeatureConfig.asset`.
5. Assign the config asset to the `DotsSystemBootstrap` component.
6. Configure the toggles (minimal for the smoke test):
   - **EnablePlayerSystem:** ✅
   - **EnablePlayerBootstrapFixedRateInstaller:** ✅
   - **EnablePlayerEntityBootstrap:** ✅
   - **EnablePlayerEntityBootstrapPureEcs:** ❌
   - **EnablePlayerInputSystem:** ❌ (test injects input directly)
   - **EnablePlayerLookSystem:** ✅
   - **EnablePlayerMovementSystem:** ✅
   - **EnablePlayerGroundingSystem:** ✅
   - **EnablePlayerCameraSystem:** ✅
   - **EnablePlayerCinemachineCameraSystem:** ❌ (unless you use Cinemachine)
   - **EnableCameraFollowSystem:** ✅
   - **EnableTerrainSystem:** ❌ (not required for existence checks)
   - **EnableDungeonSystem / EnableWeatherSystem / EnableRenderingSystem:** ❌

## 3) Add terrain bootstrap
1. Create an empty GameObject named `Terrain Bootstrap`.
2. Add the `TerrainBootstrapAuthoring` component.
3. Leave defaults unless you want a larger grid.

## 4) Expected Play Mode result
- Player entity spawns at `(0, 2, 0)` via `PlayerEntityBootstrap`.
- Camera entity exists (tagged `MainCameraTag`).
- Terrain chunk entities exist (`TerrainChunk`) plus `SDFTerrainFieldSettings` singleton.
- PlayMode test should move the player forward when input is injected.
