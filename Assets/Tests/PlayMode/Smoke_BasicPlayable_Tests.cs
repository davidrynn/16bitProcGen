using System;
using System.Collections;
using DOTS.Player.Bootstrap;
using DOTS.Player.Components;
using DOTS.Terrain;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
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
                var unloadLoadedSceneOp = SceneManager.UnloadSceneAsync(loadedScene);
                if (unloadLoadedSceneOp != null)
                {
                    yield return unloadLoadedSceneOp;
                }

                // Ensure the temporary cleanup scene does not remain loaded after the test.
                // Unity will keep at least one scene loaded; unloading the active scene is safe here.
                var unloadCleanupSceneOp = SceneManager.UnloadSceneAsync(cleanupScene);
                if (unloadCleanupSceneOp != null)
                {
                    yield return unloadCleanupSceneOp;
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

            var entityManager = world.EntityManager;
            Assert.IsTrue(world.IsCreated, "World was not properly created.");
            using var playerQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PlayerTag>());
            using var cameraQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MainCameraTag>());
            using var terrainQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            using var fieldSettingsQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());

            yield return WaitForCondition(
                () => !playerQuery.IsEmptyIgnoreFilter,
                TimeoutSeconds,
                "Player entity not found (PlayerTag). Ensure PlayerEntityBootstrap is enabled via DotsSystemBootstrap.");

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
