| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\Core\ChunkProcessor.cs | 9 | public partial struct ChunkProcessor : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\Debug\SystemDiscoveryTool.cs | 25 | [Tooltip("Show unmanaged systems (ISystem)")] |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\Debug\SystemDiscoveryTool.cs | 61 | Unmanaged,  // ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\Debug\SystemDiscoveryTool.cs | 138 | else if (systemType.GetInterfaces().Any(i => i.Name == "ISystem")) |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\Modification\TerrainGlobPhysicsSystem.cs | 15 | public partial struct TerrainGlobPhysicsSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\Modification\TerrainModificationSystem.cs | 11 | public partial struct TerrainModificationSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\TestHelpers\GlobPhysicsTestSetup.cs | 213 | Debug.Log("TerrainGlobPhysicsSystem: (ISystem active in world update list)"); |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\DOTS\TestHelpers\GlobPhysicsTestSetup.cs | 214 | Debug.Log("TerrainModificationSystem: (ISystem active in world update list)"); |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Bootstrap\PlayerBootstrapFixedRateInstaller.cs | 10 | public partial struct PlayerBootstrapFixedRateInstaller : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Bootstrap\PlayerBootstrapFixedRateInstaller.cs | 20 | // to satisfy ISystem requirements should the system be re-enabled in the future. |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Bootstrap\PlayerEntityBootstrap_PureECS.cs | 34 | public partial struct PlayerEntityBootstrap_PureECS : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Bootstrap\PlayerEntityBootstrap.cs | 16 | public partial struct PlayerEntityBootstrap : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Bootstrap\SimplePlayerMovementSystem.cs | 23 | public partial struct SimplePlayerMovementSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Bootstrap\Tests\PlayerCameraBootstrapTests.cs | 502 | private void RunSystemOnce<T>() where T : unmanaged, ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\CameraFollowSystem.cs | 13 | public partial struct CameraFollowSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\PlayerCameraSystem.cs | 15 | public partial struct PlayerCameraSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\PlayerCinemachineCameraSystem.cs | 14 | public partial struct PlayerCinemachineCameraSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\PlayerGroundingSystem.cs | 20 | public partial struct PlayerGroundingSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\PlayerInputSystem.cs | 16 | public partial struct PlayerInputSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\PlayerLookSystem.cs | 15 | public partial struct PlayerLookSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Player\Systems\PlayerMovementSystem.cs | 22 | public partial struct PlayerMovementSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Terrain\Meshing\TerrainChunkMeshBuildSystem.cs | 10 | public partial struct TerrainChunkMeshBuildSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Terrain\Meshing\TerrainChunkMeshUploadSystem.cs | 15 | public partial struct TerrainChunkMeshUploadSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Terrain\Meshing\TerrainChunkRenderPrepSystem.cs | 13 | public partial struct TerrainChunkRenderPrepSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Terrain\SDF\Systems\TerrainChunkDensitySamplingSystem.cs | 10 | public partial struct TerrainChunkDensitySamplingSystem : ISystem |
| C:\UnityWorkspace\16bitProcGen\Assets\Scripts\Terrain\SDF\Systems\TerrainEditInputSystem.cs | 13 | public partial struct TerrainEditInputSystem : ISystem |
# ISystem Usage Report

This document lists all usages and declarations of `ISystem` in the project, including file locations and code context.

| File | Line | Code |
|------|------|------|
| DOTS/Core/ChunkProcessor.cs | 9 | public partial struct ChunkProcessor : ISystem |
| DOTS/Debug/SystemDiscoveryTool.cs | 25 | [Tooltip("Show unmanaged systems (ISystem)")] |
| DOTS/Debug/SystemDiscoveryTool.cs | 61 | Unmanaged,  // ISystem |
| DOTS/Debug/SystemDiscoveryTool.cs | 138 | else if (systemType.GetInterfaces().Any(i => i.Name == "ISystem")) |
| DOTS/Modification/TerrainGlobPhysicsSystem.cs | 15 | public partial struct TerrainGlobPhysicsSystem : ISystem |
| DOTS/Modification/TerrainModificationSystem.cs | 11 | public partial struct TerrainModificationSystem : ISystem |
| DOTS/TestHelpers/GlobPhysicsTestSetup.cs | 213 | Debug.Log("TerrainGlobPhysicsSystem: (ISystem active in world update list)"); |
| DOTS/TestHelpers/GlobPhysicsTestSetup.cs | 214 | Debug.Log("TerrainModificationSystem: (ISystem active in world update list)"); |
| Player/Bootstrap/PlayerBootstrapFixedRateInstaller.cs | 10 | public partial struct PlayerBootstrapFixedRateInstaller : ISystem |
| Player/Bootstrap/PlayerBootstrapFixedRateInstaller.cs | 20 | // to satisfy ISystem requirements should the system be re-enabled in the future. |
| Player/Bootstrap/PlayerEntityBootstrap_PureECS.cs | 34 | public partial struct PlayerEntityBootstrap_PureECS : ISystem |
| Player/Bootstrap/PlayerEntityBootstrap.cs | 16 | public partial struct PlayerEntityBootstrap : ISystem |
| Player/Bootstrap/SimplePlayerMovementSystem.cs | 23 | public partial struct SimplePlayerMovementSystem : ISystem |
| Player/Bootstrap/Tests/PlayerCameraBootstrapTests.cs | 502 | private void RunSystemOnce<T>() where T : unmanaged, ISystem |
| Player/Systems/CameraFollowSystem.cs | 13 | public partial struct CameraFollowSystem : ISystem |
| Player/Systems/PlayerCameraSystem.cs | 15 | public partial struct PlayerCameraSystem : ISystem |
| Player/Systems/PlayerCinemachineCameraSystem.cs | 14 | public partial struct PlayerCinemachineCameraSystem : ISystem |
| Player/Systems/PlayerGroundingSystem.cs | 20 | public partial struct PlayerGroundingSystem : ISystem |
| Player/Systems/PlayerInputSystem.cs | 16 | public partial struct PlayerInputSystem : ISystem |
| Player/Systems/PlayerLookSystem.cs | 15 | public partial struct PlayerLookSystem : ISystem |
| Player/Systems/PlayerMovementSystem.cs | 22 | public partial struct PlayerMovementSystem : ISystem |
| Terrain/Meshing/TerrainChunkMeshBuildSystem.cs | 10 | public partial struct TerrainChunkMeshBuildSystem : ISystem |
| Terrain/Meshing/TerrainChunkMeshUploadSystem.cs | 15 | public partial struct TerrainChunkMeshUploadSystem : ISystem |
| Terrain/Meshing/TerrainChunkRenderPrepSystem.cs | 13 | public partial struct TerrainChunkRenderPrepSystem : ISystem |
| Terrain/SDF/Systems/TerrainChunkDensitySamplingSystem.cs | 10 | public partial struct TerrainChunkDensitySamplingSystem : ISystem |
| Terrain/SDF/Systems/TerrainEditInputSystem.cs | 13 | public partial struct TerrainEditInputSystem : ISystem |

_This list was generated by scanning all C# files in Assets/Scripts for ISystem usage._
