using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using DOTS.Player.Bootstrap;
using DOTS.Player.Components;

namespace Tests.PlayMode
{
    /// <summary>
    /// Minimal test to verify PlayerEntityBootstrap creates a player entity.
    /// This test runs in PlayMode and doesn't require a scene - it creates a world and runs the system directly.
    /// </summary>
    public class PlayerEntityBootstrap_MinimalTest
    {
        private World testWorld;
        private EntityManager entityManager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Clean up any existing default world
            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(World.DefaultGameObjectInjectionWorld);
                World.DefaultGameObjectInjectionWorld.Dispose();
            }
            World.DefaultGameObjectInjectionWorld = null;

            // Create a fresh test world
            DefaultWorldInitialization.Initialize("PlayerBootstrapTestWorld", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            Assert.IsNotNull(testWorld, "Test world should be created");
            Assert.IsTrue(testWorld.IsCreated, "Test world should be created");

            entityManager = testWorld.EntityManager;

            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Clean up GameObjects created by PlayerEntityBootstrap before disposing world
            DestroyBootstrapVisuals();
            
            // Clean up test world
            if (testWorld != null && testWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
                testWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = null;

            yield return null;
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

        [UnityTest]
        public IEnumerator PlayerEntityBootstrap_CreatesPlayerEntity()
        {
            // Create the system
            var systemHandle = testWorld.CreateSystem<PlayerEntityBootstrap>();
            Assert.AreNotEqual(SystemHandle.Null, systemHandle, "System should be created");

            // Manually run the system once
            systemHandle.Update(testWorld.Unmanaged);

            yield return null;

            // Verify player entity was created
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerCount = query.CalculateEntityCount();
            
            Assert.AreEqual(1, playerCount, $"Should create exactly one player entity, but found {playerCount}");
            
            // Verify player has required components
            var playerEntity = query.GetSingletonEntity();
            Assert.IsTrue(entityManager.HasComponent<PlayerTag>(playerEntity), "Player should have PlayerTag");
            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(playerEntity), "Player should have LocalTransform");
            Assert.IsTrue(entityManager.HasComponent<PlayerMovementConfig>(playerEntity), "Player should have PlayerMovementConfig");
            Assert.IsTrue(entityManager.HasComponent<PlayerInputComponent>(playerEntity), "Player should have PlayerInputComponent");
            Assert.IsTrue(entityManager.HasComponent<PlayerMovementState>(playerEntity), "Player should have PlayerMovementState");
            Assert.IsTrue(entityManager.HasComponent<PhysicsVelocity>(playerEntity), "Player should have PhysicsVelocity");
            Assert.IsTrue(entityManager.HasComponent<PhysicsMass>(playerEntity), "Player should have PhysicsMass");
            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(playerEntity), "Player should have PhysicsCollider");
        }

        [UnityTest]
        public IEnumerator PlayerEntityBootstrap_OnlyCreatesOnePlayer()
        {
            // Create the system
            var systemHandle = testWorld.CreateSystem<PlayerEntityBootstrap>();

            // Run the system multiple times
            systemHandle.Update(testWorld.Unmanaged);
            yield return null;
            systemHandle.Update(testWorld.Unmanaged);
            yield return null;
            systemHandle.Update(testWorld.Unmanaged);
            yield return null;

            // Should still only have one player (system disables itself after first run)
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerCount = query.CalculateEntityCount();
            
            Assert.AreEqual(1, playerCount, "Should only create one player entity even if system runs multiple times");
        }

        [UnityTest]
        public IEnumerator PlayerEntityBootstrap_CreatesCameraEntity()
        {
            // Create and run the system
            var systemHandle = testWorld.CreateSystem<PlayerEntityBootstrap>();
            systemHandle.Update(testWorld.Unmanaged);

            yield return null;

            // Verify camera entity was created
            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraCount = query.CalculateEntityCount();
            
            Assert.AreEqual(1, cameraCount, $"Should create exactly one camera entity, but found {cameraCount}");
        }
    }
}

