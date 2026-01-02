using System;
using System.Collections;
using DOTS.Player.Bootstrap;
using DOTS.Player.Components;
using DOTS.Terrain;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Tests.PlayMode
{
    public class Smoke_BasicPlayable_Tests
    {
        private const string ScenePath = "Assets/Scenes/Tests/Smoke_BasicPlayable.unity";        
        private const string SceneName = "Smoke_BasicPlayable";
        private const float TimeoutSeconds = 10f;
        private const int MovementFrames = 12;
        private const float MovementEpsilon = 0.05f;

        private Scene loadedScene;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
#if UNITY_EDITOR
            var loadOp = EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            var loadOp = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
#endif
            Assert.IsNotNull(loadOp, $"Failed to start loading scene at path '{ScenePath}'.");
            yield return loadOp;
            loadedScene = SceneManager.GetActiveScene();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!loadedScene.IsValid())
            {
                yield break;
            }

            if (loadedScene.isLoaded)
            {
                var cleanupScene = SceneManager.CreateScene("Smoke_BasicPlayable_Cleanup");
                SceneManager.SetActiveScene(cleanupScene);
                yield return SceneManager.UnloadSceneAsync(loadedScene);
                if (cleanupScene.IsValid() && cleanupScene.isLoaded)
                {
                    yield return SceneManager.UnloadSceneAsync(cleanupScene);
                }
            }
        }

        [UnityTest]
        public IEnumerator Smoke_BasicPlayable_LoadsAndMovesPlayer()
        {
            yield return WaitForCondition(
                () => World.DefaultGameObjectInjectionWorld != null,
                TimeoutSeconds,
                "Default DOTS world was not created. Ensure a DOTS bootstrap exists in the scene.");

            var world = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(world, "Default DOTS world was unexpectedly null.");
            Assert.IsTrue(world.IsCreated, "World was not created.");

            var entityManager = world.EntityManager;

            // DIAGNOSTIC: Check if PlayerEntityBootstrap system exists
            var playerBootstrapHandle = world.GetExistingSystem<PlayerEntityBootstrap>();
            if (playerBootstrapHandle == SystemHandle.Null)
            {
                Debug.LogError("[Smoke Test] PlayerEntityBootstrap system not found in world!");
                Debug.LogError("[Smoke Test] Check DotsSystemBootstrap configuration - system may not be created.");
                Assert.Fail("PlayerEntityBootstrap system not found. Check DotsSystemBootstrap configuration.");
            }
            else
            {
                Debug.Log($"[Smoke Test] PlayerEntityBootstrap system found: {playerBootstrapHandle}");
            }

            // DIAGNOSTIC: Try to manually run InitializationSystemGroup to ensure systems execute
            var initGroup = world.GetExistingSystemManaged<InitializationSystemGroup>();
            if (initGroup != null)
            {
                Debug.Log("[Smoke Test] Manually running InitializationSystemGroup to ensure PlayerEntityBootstrap executes...");
                initGroup.Update();
            }
            else
            {
                Debug.LogWarning("[Smoke Test] InitializationSystemGroup not found - systems may not execute automatically.");
            }

            // Give it a frame to process
            yield return null;

            using var playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            using var cameraQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MainCameraTag>());
            using var terrainQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            using var fieldSettingsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());

            // DIAGNOSTIC: Check current state before waiting
            var initialPlayerCount = playerQuery.CalculateEntityCount();
            Debug.Log($"[Smoke Test] Initial player entity count: {initialPlayerCount}");

            yield return WaitForCondition(
                () => !playerQuery.IsEmptyIgnoreFilter,
                TimeoutSeconds,
                $"Player entity not found (PlayerTag). Initial count was {initialPlayerCount}. Ensure PlayerEntityBootstrap is enabled via DotsSystemBootstrap.");

            // DIAGNOSTIC: Verify player was actually created
            var finalPlayerCount = playerQuery.CalculateEntityCount();
            Debug.Log($"[Smoke Test] Final player entity count: {finalPlayerCount}");
            Assert.AreEqual(1, finalPlayerCount, $"Expected exactly 1 player entity, but found {finalPlayerCount}");

            yield return WaitForCondition(
                () => !cameraQuery.IsEmptyIgnoreFilter,
                TimeoutSeconds,
                "Camera entity not found (MainCameraTag). Ensure PlayerEntityBootstrap creates the camera entity.");

            yield return WaitForCondition(
                () => !terrainQuery.IsEmptyIgnoreFilter,
                TimeoutSeconds,
                "Terrain chunk entities not found (TerrainChunk). Ensure TerrainBootstrapAuthoring exists in the scene.");

            yield return WaitForCondition(
                () => !fieldSettingsQuery.IsEmptyIgnoreFilter,
                TimeoutSeconds,
                "SDF terrain field settings not found (SDFTerrainFieldSettings). Ensure TerrainBootstrapAuthoring runs at startup.");

            var playerEntity = playerQuery.GetSingletonEntity();
            
            // DIAGNOSTIC: Log player entity details
            Debug.Log($"[Smoke Test] Player entity found: {playerEntity}");
            Debug.Log($"[Smoke Test] Player has LocalTransform: {entityManager.HasComponent<LocalTransform>(playerEntity)}");
            Debug.Log($"[Smoke Test] Player has PlayerInputComponent: {entityManager.HasComponent<PlayerInputComponent>(playerEntity)}");
            Debug.Log($"[Smoke Test] Player has PlayerMovementState: {entityManager.HasComponent<PlayerMovementState>(playerEntity)}");
            
            Assert.IsTrue(entityManager.HasComponent<PlayerInputComponent>(playerEntity),
                "Player entity missing PlayerInputComponent. Movement input cannot be injected.");
            Assert.IsTrue(entityManager.HasComponent<PlayerMovementState>(playerEntity),
                "Player entity missing PlayerMovementState. Ensure PlayerEntityBootstrap is used for the test scene.");
            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(playerEntity),
                "Player entity missing LocalTransform.");

            var initialTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var initialPosition = initialTransform.Position;
            Debug.Log($"[Smoke Test] Initial player position: {initialPosition}");

            for (var i = 0; i < MovementFrames; i++)
            {
                var input = entityManager.GetComponentData<PlayerInputComponent>(playerEntity);
                input.Move = new float2(0f, 1f);
                entityManager.SetComponentData(playerEntity, input);
                yield return null;
            }

            var finalPosition = entityManager.GetComponentData<LocalTransform>(playerEntity).Position;
            var delta = finalPosition - initialPosition;
            var planarDelta = new float2(delta.x, delta.z);
            Debug.Log($"[Smoke Test] Final player position: {finalPosition}, Delta: {delta}, Planar Delta: {planarDelta}, Length: {math.length(planarDelta)}");
            
            Assert.Greater(math.length(planarDelta), MovementEpsilon,
                $"Player did not move enough. Delta XZ: {planarDelta}. Ensure PlayerMovementSystem and PlayerGroundingSystem are enabled.");
        }

        private static IEnumerator WaitForCondition(Func<bool> condition, float timeoutSeconds, string timeoutMessage)
        {
            var startTime = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
            {
                if (condition())
                {
                    yield break;
                }

                yield return null;
            }

            Assert.Fail(timeoutMessage);
        }
    }
}
