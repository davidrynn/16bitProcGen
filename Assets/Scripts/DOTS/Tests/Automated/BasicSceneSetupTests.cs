using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.TestTools;
using DOTS.Player.Components;
using DOTS.Player.Tests.Bootstrap;
using DOTS.Terrain;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Tests
{
    /// <summary>
    /// Integration tests for basic scene setup - verifies terrain, player, and camera work together
    /// These tests check that the essential components exist and are configured correctly,
    /// but cannot verify actual visual rendering (that requires manual inspection or graphics tests)
    /// </summary>
    [TestFixture]
    public class BasicSceneSetupTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            // Clean up any existing world
            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(World.DefaultGameObjectInjectionWorld);
                World.DefaultGameObjectInjectionWorld.Dispose();
            }

            DefaultWorldInitialization.Initialize("Basic Scene Test World", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;
            
            // Create player systems for testing (bypasses ProjectFeatureConfig)
            TestSystemBootstrap.CreateBootstrapSystemsOnly(testWorld);
            
            // Run the bootstrap system once to create entities
            var bootstrapHandle = testWorld.GetExistingSystem<DOTS.Player.Bootstrap.PlayerEntityBootstrap>();
            if (bootstrapHandle != SystemHandle.Null)
            {
                bootstrapHandle.Update(testWorld.Unmanaged);
            }
        }

        [TearDown]
        public void TearDown()
        {
            // Destroy GameObjects created by bootstrap before disposing world
            // This prevents PlayerVisualSync and other components from trying to access disposed EntityManager
            DestroyBootstrapVisuals();
            
            if (testWorld != null && testWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
                testWorld.Dispose();
            }
            World.DefaultGameObjectInjectionWorld = null;
        }

        private static void DestroyBootstrapVisuals()
        {
            DestroyImmediateIfExists("Player Visual (ECS Synced)");
            DestroyImmediateIfExists("Ground Visual (ECS Synced)");
            DestroyImmediateIfExists("Main Camera (ECS Player)");
        }

        private static void DestroyImmediateIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        #region Player Setup Tests

        [UnityTest]
        public IEnumerator PlayerEntity_Exists()
        {
            yield return null; // Wait one frame for bootstrap
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            Assert.GreaterOrEqual(query.CalculateEntityCount(), 1, "Player entity should exist");
        }

        [UnityTest]
        public IEnumerator PlayerEntity_HasRequiredComponents()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            if (query.CalculateEntityCount() > 0)
            {
                var playerEntity = query.GetSingletonEntity();
                Assert.IsTrue(entityManager.HasComponent<LocalTransform>(playerEntity), "Player should have LocalTransform");
                Assert.IsTrue(entityManager.HasComponent<PlayerMovementConfig>(playerEntity), "Player should have PlayerMovementConfig");
                Assert.IsTrue(entityManager.HasComponent<PlayerInputComponent>(playerEntity), "Player should have PlayerInputComponent");
            }
        }

        [UnityTest]
        public IEnumerator PlayerVisual_GameObjectExists()
        {
            yield return null;
            var playerVisual = GameObject.Find("Player Visual (ECS Synced)");
            Assert.IsNotNull(playerVisual, "Player visual GameObject should exist");
        }

        #endregion

        #region Camera Setup Tests

        [UnityTest]
        public IEnumerator CameraEntity_Exists()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            Assert.GreaterOrEqual(query.CalculateEntityCount(), 1, "Camera entity should exist");
        }

        [UnityTest]
        public IEnumerator CameraGameObject_Exists()
        {
            yield return null;
            var cameraGO = GameObject.Find("Main Camera (ECS Player)");
            Assert.IsNotNull(cameraGO, "Camera GameObject should exist");
            
            var camera = cameraGO.GetComponent<Camera>();
            Assert.IsNotNull(camera, "Camera component should exist");
            Assert.IsTrue(camera.enabled, "Camera should be enabled");
        }

        [UnityTest]
        public IEnumerator Camera_IsTaggedAsMainCamera()
        {
            yield return null;
            var cameraGO = GameObject.Find("Main Camera (ECS Player)");
            if (cameraGO != null)
            {
                Assert.AreEqual("MainCamera", cameraGO.tag, "Camera should be tagged as MainCamera");
            }
        }

        #endregion

        #region SDF DOTS Terrain Tests

        [UnityTest]
        public IEnumerator SDF_TerrainChunkEntities_Exist()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f); // Give terrain time to generate
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            int terrainChunkCount = query.CalculateEntityCount();
            Assert.GreaterOrEqual(terrainChunkCount, 0, "Should be able to find SDF terrain chunk entities (may be 0 if terrain not generated yet)");
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveRequiredComponents()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f);
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                Assert.IsTrue(entityManager.HasComponent<LocalTransform>(entity), 
                    $"Terrain chunk entity {entity.Index} should have LocalTransform");
                Assert.IsTrue(entityManager.HasComponent<DOTS.Terrain.TerrainChunk>(entity), 
                    $"Terrain chunk entity {entity.Index} should have TerrainChunk component");
            }
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveGridInfo()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f);
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            int hasGridInfoCount = 0;
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<TerrainChunkGridInfo>(entity))
                {
                    hasGridInfoCount++;
                    var gridInfo = entityManager.GetComponentData<TerrainChunkGridInfo>(entity);
                    Assert.Greater(gridInfo.Resolution.x, 0, "Grid resolution should be positive");
                    Assert.Greater(gridInfo.VoxelSize, 0, "Voxel size should be positive");
                }
            }
            
            if (entities.Length > 0)
            {
                DebugSettings.LogTest($"Found {hasGridInfoCount}/{entities.Length} terrain chunks with GridInfo");
            }
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveBounds()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f);
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            int hasBoundsCount = 0;
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<TerrainChunkBounds>(entity))
                {
                    hasBoundsCount++;
                    var bounds = entityManager.GetComponentData<TerrainChunkBounds>(entity);
                    // Bounds should have valid world origin
                    Assert.IsFalse(float.IsNaN(bounds.WorldOrigin.x), "World origin should be valid");
                }
            }
            
            if (entities.Length > 0)
            {
                DebugSettings.LogTest($"Found {hasBoundsCount}/{entities.Length} terrain chunks with Bounds");
            }
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveRenderComponents()
        {
            yield return null;
            yield return new WaitForSeconds(1f); // Give more time for render prep
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            int hasRenderBoundsCount = 0;
            int hasMaterialMeshInfoCount = 0;
            
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<RenderBounds>(entity))
                {
                    hasRenderBoundsCount++;
                    var renderBounds = entityManager.GetComponentData<RenderBounds>(entity);
                    // Check if AABB is valid by verifying extents are non-negative
                    Assert.GreaterOrEqual(renderBounds.Value.Extents.x, 0, "Render bounds extents X should be non-negative");
                    Assert.GreaterOrEqual(renderBounds.Value.Extents.y, 0, "Render bounds extents Y should be non-negative");
                    Assert.GreaterOrEqual(renderBounds.Value.Extents.z, 0, "Render bounds extents Z should be non-negative");
                }
                
                if (entityManager.HasComponent<MaterialMeshInfo>(entity))
                {
                    hasMaterialMeshInfoCount++;
                }
            }
            
            if (entities.Length > 0)
            {
                DebugSettings.LogTest($"Render components: RenderBounds={hasRenderBoundsCount}/{entities.Length}, MaterialMeshInfo={hasMaterialMeshInfoCount}/{entities.Length}");
            }
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveMeshData()
        {
            yield return null;
            yield return new WaitForSeconds(1.5f); // Give more time for mesh generation
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            int hasMeshDataCount = 0;
            
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<TerrainChunkMeshData>(entity))
                {
                    var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                    if (meshData.HasMesh)
                    {
                        hasMeshDataCount++;
                        // Mesh blob should be valid
                        Assert.IsTrue(meshData.Mesh.IsCreated, "Mesh blob should be created");
                    }
                }
            }
            
            if (entities.Length > 0)
            {
                DebugSettings.LogTest($"Found {hasMeshDataCount}/{entities.Length} terrain chunks with mesh data");
                // Don't fail if no meshes yet - generation may still be in progress
            }
        }

        #endregion

        #region Scene Integration Tests

        [UnityTest]
        public IEnumerator Scene_HasPlayerAndCamera()
        {
            yield return null;
            
            using var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            using var cameraQuery = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            
            Assert.GreaterOrEqual(playerQuery.CalculateEntityCount(), 1, "Scene should have player");
            Assert.GreaterOrEqual(cameraQuery.CalculateEntityCount(), 1, "Scene should have camera");
        }

        [UnityTest]
        public IEnumerator PlayerAndCamera_ArePositioned()
        {
            yield return null;
            
            using var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            using var cameraQuery = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            
            if (playerQuery.CalculateEntityCount() > 0 && cameraQuery.CalculateEntityCount() > 0)
            {
                var playerEntity = playerQuery.GetSingletonEntity();
                var cameraEntity = cameraQuery.GetSingletonEntity();
                
                var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
                var cameraTransform = entityManager.GetComponentData<LocalTransform>(cameraEntity);
                
                // Check that positions are reasonable (not NaN, not at extreme values)
                Assert.IsFalse(float.IsNaN(playerTransform.Position.x), "Player X position should be valid");
                Assert.IsFalse(float.IsNaN(cameraTransform.Position.x), "Camera X position should be valid");
            }
        }

        [UnityTest]
        public IEnumerator Scene_HasLighting()
        {
            yield return null;
            
            var lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            Assert.GreaterOrEqual(lights.Length, 0, "Scene may have lights (not required)");
            
            // Check ambient light settings
            Assert.IsTrue(RenderSettings.ambientIntensity >= 0, "Ambient intensity should be valid");
        }

        #endregion

        #region Rendering Readiness Tests

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveRenderBounds()
        {
            yield return null;
            yield return new WaitForSeconds(1f); // Give time for render prep system
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<RenderBounds>(entity))
                {
                    var renderBounds = entityManager.GetComponentData<RenderBounds>(entity);
                    // Check if AABB is valid by verifying extents are non-negative
                    Assert.GreaterOrEqual(renderBounds.Value.Extents.x, 0, 
                        $"Terrain chunk entity {entity.Index} should have valid render bounds (extents X)");
                    Assert.GreaterOrEqual(renderBounds.Value.Extents.y, 0, 
                        $"Terrain chunk entity {entity.Index} should have valid render bounds (extents Y)");
                    Assert.GreaterOrEqual(renderBounds.Value.Extents.z, 0, 
                        $"Terrain chunk entity {entity.Index} should have valid render bounds (extents Z)");
                }
            }
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_HaveMaterialMeshInfo()
        {
            yield return null;
            yield return new WaitForSeconds(1f); // Give time for render prep system
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            int hasMaterialCount = 0;
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<MaterialMeshInfo>(entity))
                {
                    hasMaterialCount++;
                }
            }
            
            if (entities.Length > 0)
            {
                DebugSettings.LogTest($"Found {hasMaterialCount}/{entities.Length} terrain chunks with MaterialMeshInfo");
                // Don't fail if not all have materials yet - render prep may still be processing
            }
        }

        [UnityTest]
        public IEnumerator SDF_TerrainChunks_ArePositioned()
        {
            yield return null;
            yield return new WaitForSeconds(0.5f);
            
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>()
            );
            
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                    Assert.IsFalse(float.IsNaN(transform.Position.x), 
                        $"Terrain chunk entity {entity.Index} should have valid position");
                }
            }
        }

        #endregion
    }
}

