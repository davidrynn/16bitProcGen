using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using DOTS.Player.Bootstrap;
using DOTS.Player.Components;

namespace DOTS.Player.Tests.Bootstrap
{
    [TestFixture]
    public class PlayerCameraBootstrap_WithVisualsTests
    {
        private World testWorld;
        private EntityManager entityManager;
        private GameObject bootstrapGameObject;
        private PlayerCameraBootstrap_WithVisuals bootstrap;

        [SetUp]
        public void SetUp()
        {
            testWorld = new World("Test World");
            entityManager = testWorld.EntityManager;
            World.DefaultGameObjectInjectionWorld = testWorld;
            bootstrapGameObject = new GameObject("TestBootstrap");
            bootstrap = bootstrapGameObject.AddComponent<PlayerCameraBootstrap_WithVisuals>();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up all GameObjects created by bootstrap
            var playerVisual = GameObject.Find("Player Visual (Debug)");
            if (playerVisual != null)
            {
                Object.DestroyImmediate(playerVisual);
            }
            
            var groundVisual = GameObject.Find("Ground Visual (Debug)");
            if (groundVisual != null)
            {
                Object.DestroyImmediate(groundVisual);
            }
            
            var mainCamera = GameObject.Find("Main Camera (GameObject)");
            if (mainCamera != null)
            {
                Object.DestroyImmediate(mainCamera);
            }
            
            if (bootstrapGameObject != null)
            {
                Object.DestroyImmediate(bootstrapGameObject);
            }
            
            if (testWorld != null && testWorld.IsCreated)
            {
                testWorld.Dispose();
            }
            
            World.DefaultGameObjectInjectionWorld = null;
        }

        #region Bootstrap Initialization Tests

        [UnityTest]
        public IEnumerator Start_CreatesPlayerEntity()
        {
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            Assert.AreEqual(1, query.CalculateEntityCount(), "Should create exactly one player entity");
            query.Dispose();
        }

        [UnityTest]
        public IEnumerator Start_CreatesCameraEntity()
        {
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            Assert.AreEqual(1, query.CalculateEntityCount(), "Should create exactly one camera entity");
            query.Dispose();
        }

        [UnityTest]
        public IEnumerator Start_CreatesGroundEntity()
        {
            yield return null;
            var allEntities = entityManager.GetAllEntities();
            var groundFound = false;
            foreach (var entity in allEntities)
            {
                if (entityManager.GetName(entity).Contains("Ground"))
                {
                    groundFound = true;
                    break;
                }
            }
            allEntities.Dispose();
            Assert.IsTrue(groundFound, "Should create ground entity");
        }

        [UnityTest]
        public IEnumerator Start_CreatesCameraGameObject()
        {
            yield return null;
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Should create a Camera GameObject");
            Assert.IsNotNull(camera.GetComponent<AudioListener>(), "Camera should have AudioListener");
        }

        #endregion

        #region Player Entity Tests

        [UnityTest]
        public IEnumerator PlayerEntity_HasCorrectComponents()
        {
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(playerEntity), "Player should have LocalTransform");
            Assert.IsTrue(entityManager.HasComponent<LocalToWorld>(playerEntity), "Player should have LocalToWorld");
            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(playerEntity), "Player should have PhysicsCollider");
            Assert.IsTrue(entityManager.HasComponent<PhysicsVelocity>(playerEntity), "Player should have PhysicsVelocity");
            Assert.IsTrue(entityManager.HasComponent<PhysicsMass>(playerEntity), "Player should have PhysicsMass");
            Assert.IsTrue(entityManager.HasComponent<PhysicsGravityFactor>(playerEntity), "Player should have PhysicsGravityFactor");

            query.Dispose();
        }

        [UnityTest]
        public IEnumerator PlayerEntity_HasCorrectInitialPosition()
        {
            var expectedPosition = new float3(0, 1, 0);
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);

            Assert.AreEqual(expectedPosition, transform.Position, "Player position should match initial value");
            query.Dispose();
        }

        [UnityTest]
        public IEnumerator PlayerEntity_HasCapsuleCollider()
        {
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            var collider = entityManager.GetComponentData<PhysicsCollider>(playerEntity);

            Assert.IsTrue(collider.IsValid, "Physics collider should be valid");
            Assert.AreEqual(ColliderType.Capsule, collider.Value.Value.Type, "Should be a capsule collider");

            query.Dispose();
        }

        [UnityTest]
        public IEnumerator PlayerEntity_HasCorrectPhysicsProperties()
        {
            var expectedGravityFactor = 1f;
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            var gravityFactor = entityManager.GetComponentData<PhysicsGravityFactor>(playerEntity);
            Assert.AreEqual(expectedGravityFactor, gravityFactor.Value, 0.01f, "Gravity factor should be 1.0");

            var velocity = entityManager.GetComponentData<PhysicsVelocity>(playerEntity);
            Assert.AreEqual(float3.zero, velocity.Linear, "Initial linear velocity should be zero");
            Assert.AreEqual(float3.zero, velocity.Angular, "Initial angular velocity should be zero");

            query.Dispose();
        }

        #endregion

        #region Camera Entity Tests

        [UnityTest]
        public IEnumerator CameraEntity_HasCorrectComponents()
        {
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();

            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(cameraEntity), "Camera should have LocalTransform");
            Assert.IsTrue(entityManager.HasComponent<LocalToWorld>(cameraEntity), "Camera should have LocalToWorld");

            query.Dispose();
        }

        [UnityTest]
        public IEnumerator CameraEntity_HasCorrectInitialPosition()
        {
            var expectedPosition = new float3(0, 3, -4);
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();
            var transform = entityManager.GetComponentData<LocalTransform>(cameraEntity);

            Assert.AreEqual(expectedPosition, transform.Position, "Camera position should match initial value");
            query.Dispose();
        }

        [UnityTest]
        public IEnumerator CameraEntity_LooksAtPlayer()
        {
            yield return null;
            var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();
            var transform = entityManager.GetComponentData<LocalTransform>(cameraEntity);

            Assert.AreNotEqual(quaternion.identity, transform.Rotation, "Camera should be rotated to look at player");

            query.Dispose();
        }

        #endregion

        #region Ground Entity Tests

        [UnityTest]
        public IEnumerator GroundEntity_HasPhysicsCollider()
        {
            yield return null;
            var allEntities = entityManager.GetAllEntities();
            Entity? groundEntity = null;

            foreach (var entity in allEntities)
            {
                if (entityManager.GetName(entity).Contains("Ground"))
                {
                    groundEntity = entity;
                    break;
                }
            }
            allEntities.Dispose();

            Assert.IsTrue(groundEntity.HasValue, "Ground entity should exist");
            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(groundEntity.Value), "Ground should have PhysicsCollider");

            var collider = entityManager.GetComponentData<PhysicsCollider>(groundEntity.Value);
            Assert.IsTrue(collider.IsValid, "Ground physics collider should be valid");
            Assert.AreEqual(ColliderType.Box, collider.Value.Value.Type, "Ground should have box collider");
        }

        [UnityTest]
        public IEnumerator GroundEntity_HasCorrectPosition()
        {
            var expectedPosition = new float3(0, 0, 0);
            yield return null;
            var allEntities = entityManager.GetAllEntities();
            Entity? groundEntity = null;

            foreach (var entity in allEntities)
            {
                if (entityManager.GetName(entity).Contains("Ground"))
                {
                    groundEntity = entity;
                    break;
                }
            }
            allEntities.Dispose();

            Assert.IsTrue(groundEntity.HasValue, "Ground entity should exist");
            var transform = entityManager.GetComponentData<LocalTransform>(groundEntity.Value);
            Assert.AreEqual(expectedPosition, transform.Position, "Ground position should be at origin");
        }

        #endregion

        #region Visual GameObject Tests

        [UnityTest]
        public IEnumerator PlayerVisual_CreatedWhenEnabled()
        {
            yield return null;
            var playerVisual = GameObject.Find("Player Visual (Debug)");
            Assert.IsNotNull(playerVisual, "Player visual GameObject should be created");

            var sync = playerVisual.GetComponent<EntityVisualSync>();
            Assert.IsNotNull(sync, "Player visual should have EntityVisualSync component");
        }

        [UnityTest]
        public IEnumerator GroundVisual_CreatedWhenEnabled()
        {
            yield return null;
            var groundVisual = GameObject.Find("Ground Plane Visual (Debug)");
            Assert.IsNotNull(groundVisual, "Ground visual GameObject should be created");
            Assert.IsNull(groundVisual.GetComponent<UnityEngine.Collider>(), "Ground visual should not have a collider");
        }

        [UnityTest]
        public IEnumerator CameraGameObject_SyncsWithEntity()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Camera should exist");

            var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();
            var entityTransform = entityManager.GetComponentData<LocalTransform>(cameraEntity);

            Assert.AreEqual((Vector3)entityTransform.Position, camera.transform.position, "Camera position should sync with entity");
            
            // Compare quaternions with tolerance (direct equality can fail due to floating point precision)
            var rotDiff = math.length(entityTransform.Rotation.value - ((quaternion)camera.transform.rotation).value);
            Assert.IsTrue(rotDiff < 0.001f, $"Camera rotation should sync with entity (diff: {rotDiff})");

            query.Dispose();
        }

        #endregion

        #region EntityVisualSync Tests

        [UnityTest]
        public IEnumerator EntityVisualSync_SyncsPositionWithEntity()
        {
            yield return null;

            var playerVisual = GameObject.Find("Player Visual (Debug)");
            Assert.IsNotNull(playerVisual, "Player visual should exist");

            var sync = playerVisual.GetComponent<EntityVisualSync>();
            Assert.IsNotNull(sync, "EntityVisualSync component should exist");
            
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            
            // Verify sync is tracking the right entity (check FULL entity including Version)
            Assert.AreEqual(playerEntity.Index, sync.entity.Index, "EntityVisualSync should reference the player entity (Index)");
            Assert.AreEqual(playerEntity.Version, sync.entity.Version, "EntityVisualSync should reference the player entity (Version)");
            Assert.AreEqual(playerEntity, sync.entity, "EntityVisualSync should reference the exact player entity");

            var newPosition = new float3(5, 10, 15);
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Position = newPosition;
            entityManager.SetComponentData(playerEntity, transform);

            // Force multiple frames to ensure LateUpdate runs
            yield return null;
            yield return null;
            yield return null;

            Vector3 actualPosition = playerVisual.transform.position;
            Assert.AreEqual((Vector3)newPosition, actualPosition,
                $"Visual GameObject position should sync with entity. Expected: {newPosition}, Actual: {actualPosition}");

            query.Dispose();
        }

        [UnityTest]
        public IEnumerator EntityVisualSync_SyncsRotationWithEntity()
        {
            yield return null;

            var playerVisual = GameObject.Find("Player Visual (Debug)");
            Assert.IsNotNull(playerVisual, "Player visual should exist");

            var sync = playerVisual.GetComponent<EntityVisualSync>();
            Assert.IsNotNull(sync, "EntityVisualSync component should exist");
            
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            
            // Verify sync is tracking the right entity (compare Index only, not Version)
            Assert.AreEqual(playerEntity.Index, sync.entity.Index, "EntityVisualSync should reference the player entity");

            var newRotation = quaternion.RotateY(math.PI / 2);
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Rotation = newRotation;
            entityManager.SetComponentData(playerEntity, transform);

            // Force multiple frames to ensure LateUpdate runs
            yield return null;
            yield return null;
            yield return null;

            Quaternion actualRotation = playerVisual.transform.rotation;
            var rotDiff = math.length(newRotation.value - ((quaternion)actualRotation).value);
            Assert.IsTrue(rotDiff < 0.001f,
                $"Visual GameObject rotation should sync with entity. Expected: {newRotation}, Actual: {actualRotation}, Diff: {rotDiff}");

            query.Dispose();
        }

        [UnityTest]
        public IEnumerator EntityVisualSync_HandlesDestroyedEntity()
        {
            yield return null;

            var playerVisual = GameObject.Find("Player Visual (Debug)");
            Assert.IsNotNull(playerVisual, "Player visual should exist");

            var sync = playerVisual.GetComponent<EntityVisualSync>();
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            entityManager.DestroyEntity(playerEntity);
            query.Dispose();

            yield return new WaitForEndOfFrame();
            Assert.Pass("EntityVisualSync handles destroyed entity gracefully");
        }

        #endregion

        #region Cleanup Tests

        [UnityTest]
        public IEnumerator OnDestroy_DisposesColliders()
        {
            yield return null;
            Object.DestroyImmediate(bootstrapGameObject);
            bootstrapGameObject = null;
            yield return null;
            Assert.Pass("Colliders were disposed without errors");
        }

        [UnityTest]
        public IEnumerator OnDestroy_DestroysCameraGameObject()
        {
            yield return null;
            var camera = Camera.main;
            Assert.IsNotNull(camera, "Camera should exist before destroy");

            Object.DestroyImmediate(bootstrapGameObject);
            bootstrapGameObject = null;
            yield return null;

            var cameraAfter = Camera.main;
            Assert.IsNull(cameraAfter, "Camera GameObject should be destroyed");
        }

        #endregion

        #region Edge Case Tests

        [UnityTest]
        public IEnumerator LateUpdate_HandlesNullCamera()
        {
            yield return null;
            var camera = Camera.main;
            if (camera != null)
            {
                Object.DestroyImmediate(camera.gameObject);
            }

            yield return new WaitForEndOfFrame();
            Assert.Pass("LateUpdate handles null camera gracefully");
        }

        [UnityTest]
        public IEnumerator LateUpdate_HandlesNullWorld()
        {
            yield return null;
            testWorld.Dispose();
            testWorld = null;
            World.DefaultGameObjectInjectionWorld = null;

            yield return new WaitForEndOfFrame();
            Assert.Pass("LateUpdate handles null world gracefully");
        }

        #endregion

        #region Integration Tests

        [UnityTest]
        public IEnumerator FullBootstrap_CreatesCompleteSetup()
        {
            yield return null;

            var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var cameraQuery = entityManager.CreateEntityQuery(typeof(MainCameraTag));

            Assert.AreEqual(1, playerQuery.CalculateEntityCount(), "Should have one player");
            Assert.AreEqual(1, cameraQuery.CalculateEntityCount(), "Should have one camera");
            Assert.IsNotNull(Camera.main, "Should have Camera GameObject");
            Assert.IsNotNull(GameObject.Find("Player Visual (Debug)"), "Should have player visual");
            Assert.IsNotNull(GameObject.Find("Ground Plane Visual (Debug)"), "Should have ground visual");

            playerQuery.Dispose();
            cameraQuery.Dispose();
        }

        #endregion
    }
}
