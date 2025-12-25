# ISystem Usage Report

## Summary of ISystem Implementations

- CameraFollowSystem
- ChunkProcessor
- PlayerBootstrapFixedRateInstaller
- PlayerCameraSystem
- PlayerCinemachineCameraSystem
- PlayerEntityBootstrap
- PlayerEntityBootstrap_PureECS
- PlayerGroundingSystem
- PlayerInputSystem
- PlayerLookSystem
- PlayerMovementSystem
- SimplePlayerMovementSystem
- TerrainChunkDensitySamplingSystem
- TerrainChunkMeshBuildSystem
- TerrainChunkMeshUploadSystem
- TerrainChunkRenderPrepSystem
- TerrainEditInputSystem
- TerrainGlobPhysicsSystem
- TerrainModificationSystem

## Detailed Usage Table

| System Name | File Path | Line Number | Declaration/Usage Context |
| --- | --- | --- | --- |
| PlayerLookSystem | Assets/Docs/Archives/FirstPersonController/FirstPerson_Fix_SystemOrdering.md | 35 | Documentation example declaration (`public partial struct PlayerLookSystem : ISystem`). |
| PlayerCameraSystem | Assets/Docs/Archives/FirstPersonController/FirstPerson_Fix_SystemOrdering.md | 51 | Documentation example declaration (`public partial struct PlayerCameraSystem : ISystem`). |
| TerrainChunkDensitySamplingSystem | Assets/Docs/SDF_SurfaceNets_ECS_Overview.md | 50 | Documentation overview bullet describing the system as `ISystem`. |
| TerrainChunkMeshBuildSystem | Assets/Docs/SDF_SurfaceNets_ECS_Overview.md | 53 | Documentation overview bullet describing the system as `ISystem`. |
| TerrainChunkRenderPrepSystem | Assets/Docs/SDF_SurfaceNets_ECS_Overview.md | 57 | Documentation overview bullet describing the system as `ISystem`. |
| TerrainChunkMeshUploadSystem | Assets/Docs/SDF_SurfaceNets_ECS_Overview.md | 58 | Documentation overview bullet describing the system as `ISystem`. |
| CameraFollowSystem | Assets/Scripts/Player/Systems/CameraFollowSystem.cs | 14 | ISystem declaration (`public partial struct CameraFollowSystem : ISystem`). |
| PlayerGroundingSystem | Assets/Scripts/Player/Systems/PlayerGroundingSystem.cs | 21 | ISystem declaration (`public partial struct PlayerGroundingSystem : ISystem`). |
| PlayerInputSystem | Assets/Scripts/Player/Systems/PlayerInputSystem.cs | 17 | ISystem declaration (`public partial struct PlayerInputSystem : ISystem`). |
| PlayerCameraSystem | Assets/Scripts/Player/Systems/PlayerCameraSystem.cs | 16 | ISystem declaration (`public partial struct PlayerCameraSystem : ISystem`). |
| PlayerCinemachineCameraSystem | Assets/Scripts/Player/Systems/PlayerCinemachineCameraSystem.cs | 15 | ISystem declaration (`public partial struct PlayerCinemachineCameraSystem : ISystem`). |
| PlayerMovementSystem | Assets/Scripts/Player/Systems/PlayerMovementSystem.cs | 23 | ISystem declaration (`public partial struct PlayerMovementSystem : ISystem`). |
| PlayerLookSystem | Assets/Scripts/Player/Systems/PlayerLookSystem.cs | 16 | ISystem declaration (`public partial struct PlayerLookSystem : ISystem`). |
| PlayerBootstrapFixedRateInstaller | Assets/Scripts/Player/Bootstrap/PlayerBootstrapFixedRateInstaller.cs | 11 | ISystem declaration (`public partial struct PlayerBootstrapFixedRateInstaller : ISystem`). |
| PlayerBootstrapFixedRateInstaller | Assets/Scripts/Player/Bootstrap/PlayerBootstrapFixedRateInstaller.cs | 21 | Comment referencing ISystem requirements. |
| PlayerEntityBootstrap_PureECS | Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap_PureECS.cs | 34 | ISystem declaration (`public partial struct PlayerEntityBootstrap_PureECS : ISystem`). |
| SimplePlayerMovementSystem | Assets/Scripts/Player/Bootstrap/SimplePlayerMovementSystem.cs | 24 | ISystem declaration (`public partial struct SimplePlayerMovementSystem : ISystem`). |
| PlayerEntityBootstrap | Assets/Scripts/Player/Bootstrap/PlayerEntityBootstrap.cs | 16 | ISystem declaration (`public partial struct PlayerEntityBootstrap : ISystem`). |
| ISystem (generic constraint) | Assets/Scripts/Player/Bootstrap/Tests/PlayerCameraBootstrapTests.cs | 502 | Generic constraint in `RunSystemOnce<T>()` test helper. |
| TerrainGlobPhysicsSystem | Assets/Scripts/DOTS/TestHelpers/GlobPhysicsTestSetup.cs | 213 | Log message mentioning ISystem active in world update list. |
| TerrainModificationSystem | Assets/Scripts/DOTS/TestHelpers/GlobPhysicsTestSetup.cs | 214 | Log message mentioning ISystem active in world update list. |
| ChunkProcessor | Assets/Scripts/DOTS/Core/ChunkProcessor.cs | 10 | ISystem declaration (`public partial struct ChunkProcessor : ISystem`). |
| TerrainGlobPhysicsSystem | Assets/Scripts/DOTS/Modification/TerrainGlobPhysicsSystem.cs | 16 | ISystem declaration (`public partial struct TerrainGlobPhysicsSystem : ISystem`). |
| TerrainModificationSystem | Assets/Scripts/DOTS/Modification/TerrainModificationSystem.cs | 12 | ISystem declaration (`public partial struct TerrainModificationSystem : ISystem`). |
| TerrainChunkDensitySamplingSystem | Assets/Scripts/Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs | 11 | ISystem declaration (`public partial struct TerrainChunkDensitySamplingSystem : ISystem`). |
| TerrainEditInputSystem | Assets/Scripts/Terrain/SDF/Systems/TerrainEditInputSystem.cs | 14 | ISystem declaration (`public partial struct TerrainEditInputSystem : ISystem`). |
| TerrainChunkMeshUploadSystem | Assets/Scripts/Terrain/Meshing/TerrainChunkMeshUploadSystem.cs | 16 | ISystem declaration (`public partial struct TerrainChunkMeshUploadSystem : ISystem`). |
| TerrainChunkMeshBuildSystem | Assets/Scripts/Terrain/Meshing/TerrainChunkMeshBuildSystem.cs | 11 | ISystem declaration (`public partial struct TerrainChunkMeshBuildSystem : ISystem`). |
| TerrainChunkRenderPrepSystem | Assets/Scripts/Terrain/Meshing/TerrainChunkRenderPrepSystem.cs | 14 | ISystem declaration (`public partial struct TerrainChunkRenderPrepSystem : ISystem`). |
| ISystem (documentation checklist) | Assets/README.md | 4 | Checklist entry referencing ISystem usage. |
