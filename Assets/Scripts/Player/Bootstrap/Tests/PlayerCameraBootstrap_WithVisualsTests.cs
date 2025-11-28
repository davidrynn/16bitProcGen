using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using DOTS.Player.Bootstrap;
using DOTS.Player.Components;

namespace DOTS.Player.Tests.Bootstrap
{
    [TestFixture]
    public class PlayerEntityBootstrapTests
    {
        private World testWorld;
        private EntityManager entityManager;
        private InitializationSystemGroup initializationGroup;
        private SimulationSystemGroup simulationGroup;
        private PresentationSystemGroup presentationGroup;

        [SetUp]
        public void SetUp()
        {
            CleanupDefaultWorld();

            DefaultWorldInitialization.Initialize("Test World", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;

            initializationGroup = testWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            simulationGroup = testWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            presentationGroup = testWorld.GetExistingSystemManaged<PresentationSystemGroup>();

            UpdateWorldOnce();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyBootstrapVisuals();

            if (testWorld != null && testWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
                testWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = null;
        }

        #region Bootstrap Initialization Tests

        [UnityTest]
        public IEnumerator PlayerBootstrap_CreatesPlayerEntity()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            Assert.AreEqual(1, query.CalculateEntityCount(), "Should create exactly one player entity");
        }

        [UnityTest]
        public IEnumerator PlayerBootstrap_CreatesCameraEntity()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            Assert.AreEqual(1, query.CalculateEntityCount(), "Should create exactly one camera entity");
        }

        [UnityTest]
        public IEnumerator PlayerBootstrap_CreatesGroundEntity()
        {
            yield return null;
            Assert.IsTrue(GetGroundEntity().HasValue, "Should create ground entity");
        }

        [UnityTest]
        public IEnumerator PlayerBootstrap_CreatesCameraGameObject()
        {
            yield return null;
            var cameraGameObject = GameObject.Find("Main Camera (ECS Player)");
            Assert.IsNotNull(cameraGameObject, "Should create a Camera GameObject");
            Assert.IsNotNull(cameraGameObject.GetComponent<AudioListener>(), "Camera should have AudioListener");
        }

        #endregion

        #region Player Entity Tests

        [UnityTest]
        public IEnumerator PlayerEntity_HasCorrectComponents()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(playerEntity), "Player should have LocalTransform");
            Assert.IsTrue(entityManager.HasComponent<LocalToWorld>(playerEntity), "Player should have LocalToWorld");
            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(playerEntity), "Player should have PhysicsCollider");
            Assert.IsTrue(entityManager.HasComponent<PhysicsVelocity>(playerEntity), "Player should have PhysicsVelocity");
            Assert.IsTrue(entityManager.HasComponent<PhysicsMass>(playerEntity), "Player should have PhysicsMass");
            Assert.IsTrue(entityManager.HasComponent<PhysicsGravityFactor>(playerEntity), "Player should have PhysicsGravityFactor");
        }

        public IEnumerator PlayerEntity_HasCapsuleCollider()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            var collider = entityManager.GetComponentData<PhysicsCollider>(playerEntity);

            Assert.IsTrue(collider.IsValid, "Physics collider should be valid");
            Assert.AreEqual(ColliderType.Capsule, collider.Value.Value.Type, "Should be a capsule collider");
        }

        [UnityTest]
        public IEnumerator PlayerEntity_HasCorrectPhysicsProperties()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            var gravityFactor = entityManager.GetComponentData<PhysicsGravityFactor>(playerEntity);
            Assert.AreEqual(1f, gravityFactor.Value, 0.01f, "Gravity factor should be 1.0");

            var velocity = entityManager.GetComponentData<PhysicsVelocity>(playerEntity);
            float gravityY = -9.81f;
            var physicsStepQuery = entityManager.CreateEntityQuery(typeof(Unity.Physics.PhysicsStep));
            if (!physicsStepQuery.IsEmpty)
            {
                gravityY = physicsStepQuery.GetSingleton<Unity.Physics.PhysicsStep>().Gravity.y;
            }

            float fixedTimestep = testWorld.Time.DeltaTime;
            var fixedStepGroup = testWorld.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedStepGroup != null && fixedStepGroup.Timestep > 0f)
            {
                fixedTimestep = fixedStepGroup.Timestep;
            }

            float expectedVerticalVelocity = gravityY * fixedTimestep;

            Assert.AreEqual(0f, velocity.Linear.x, 0.001f, "Initial X velocity should be zero");
            Assert.AreEqual(expectedVerticalVelocity, velocity.Linear.y, 0.05f, "Initial Y velocity should match gravity step");
            Assert.AreEqual(0f, velocity.Linear.z, 0.001f, "Initial Z velocity should be zero");
            Assert.AreEqual(float3.zero, velocity.Angular, "Initial angular velocity should be zero");
        }

        #endregion

        #region Camera Entity Tests

        [UnityTest]
        public IEnumerator CameraEntity_HasCorrectComponents()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();

            Assert.IsTrue(entityManager.HasComponent<LocalTransform>(cameraEntity), "Camera should have LocalTransform");
            Assert.IsTrue(entityManager.HasComponent<LocalToWorld>(cameraEntity), "Camera should have LocalToWorld");
        }

        [UnityTest]
        public IEnumerator CameraEntity_HasCorrectInitialPosition()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();
            var transform = entityManager.GetComponentData<LocalTransform>(cameraEntity);

            using var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = playerQuery.GetSingletonEntity();
            var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var cameraSettings = entityManager.GetComponentData<PlayerCameraSettings>(playerEntity);

            var expectedOffsetPosition = playerTransform.Position + cameraSettings.FirstPersonOffset;

            Assert.AreEqual(expectedOffsetPosition.x, transform.Position.x, 0.01f, "Camera X position should match player offset");
            Assert.AreEqual(expectedOffsetPosition.y, transform.Position.y, 0.01f, "Camera Y position should match player offset");
            Assert.AreEqual(expectedOffsetPosition.z, transform.Position.z, 0.01f, "Camera Z position should match player offset");
        }

        [UnityTest]
        public IEnumerator CameraEntity_LooksAtPlayer()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();
            var transform = entityManager.GetComponentData<LocalTransform>(cameraEntity);

            using var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = playerQuery.GetSingletonEntity();
            var view = entityManager.GetComponentData<PlayerViewComponent>(playerEntity);

            quaternion expectedRotation = math.mul(
                quaternion.AxisAngle(math.up(), math.radians(view.YawDegrees)),
                quaternion.AxisAngle(math.right(), math.radians(view.PitchDegrees))
            );

            var rotationDiff = math.length(expectedRotation.value - transform.Rotation.value);
            Assert.Less(rotationDiff, 0.001f, $"Camera rotation should match player view rotation (diff: {rotationDiff})");
        }

        #endregion

        #region Ground Entity Tests

        [UnityTest]
        public IEnumerator GroundEntity_HasPhysicsCollider()
        {
            yield return null;
            var groundEntity = GetGroundEntity();
            Assert.IsTrue(groundEntity.HasValue, "Ground entity should exist");
            Assert.IsTrue(entityManager.HasComponent<PhysicsCollider>(groundEntity.Value), "Ground should have PhysicsCollider");

            var collider = entityManager.GetComponentData<PhysicsCollider>(groundEntity.Value);
            Assert.IsTrue(collider.IsValid, "Ground physics collider should be valid");
            Assert.AreEqual(ColliderType.Box, collider.Value.Value.Type, "Ground should have box collider");
        }

        [UnityTest]
        public IEnumerator GroundEntity_HasCorrectPosition()
        {
            yield return null;
            var groundEntity = GetGroundEntity();
            Assert.IsTrue(groundEntity.HasValue, "Ground entity should exist");

            var transform = entityManager.GetComponentData<LocalTransform>(groundEntity.Value);
            Assert.AreEqual(new float3(0, 0, 0), transform.Position, "Ground position should be at origin");
        }

        #endregion

        #region Visual GameObject Tests

        [UnityTest]
        public IEnumerator PlayerVisual_Created()
        {
            yield return null;
            var playerVisual = GameObject.Find("Player Visual (ECS Synced)");
            Assert.IsNotNull(playerVisual, "Player visual GameObject should be created");

            var sync = playerVisual.GetComponent<PlayerVisualSync>();
            Assert.IsNotNull(sync, "Player visual should have PlayerVisualSync component");
        }

        [UnityTest]
        public IEnumerator GroundVisual_Created()
        {
            yield return null;
            var groundVisual = GameObject.Find("Ground Visual (ECS Synced)");
            Assert.IsNotNull(groundVisual, "Ground visual GameObject should be created");
            Assert.IsNull(groundVisual.GetComponent<UnityEngine.Collider>(), "Ground visual should not have a collider");
        }

        [UnityTest]
        public IEnumerator CameraGameObject_SyncsWithEntity()
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var cameraGameObject = GameObject.Find("Main Camera (ECS Player)");
            Assert.IsNotNull(cameraGameObject, "Camera should exist");

            using var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var cameraEntity = query.GetSingletonEntity();
            var entityTransform = entityManager.GetComponentData<LocalTransform>(cameraEntity);

            var expectedPosition = entityTransform.Position;
            Assert.AreEqual((Vector3)expectedPosition, cameraGameObject.transform.position, "Camera position should sync with entity");

            var rotDiff = math.length(entityTransform.Rotation.value - ((quaternion)cameraGameObject.transform.rotation).value);
            Assert.IsTrue(rotDiff < 0.001f, $"Camera rotation should sync with entity (diff: {rotDiff})");
        }

        #endregion

        #region PlayerVisualSync Tests

        [UnityTest]
        public IEnumerator PlayerEntity_SpawnHeightMatchesBootstrapConfiguration()
        {
            // Dispose the world prepared by SetUp so we can observe the spawn position prior to the first physics step.
            DestroyBootstrapVisuals();
            if (testWorld != null && testWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
                testWorld.Dispose();
            }
            World.DefaultGameObjectInjectionWorld = null;

            // Re-initialize without running Simulation/Presentation groups so gravity has not been applied yet.
            DefaultWorldInitialization.Initialize("Spawn Test World", false);
            var spawnWorld = World.DefaultGameObjectInjectionWorld;
            var spawnManager = spawnWorld.EntityManager;
            var spawnInitializationGroup = spawnWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            spawnInitializationGroup.Update();

            using (var query = spawnManager.CreateEntityQuery(typeof(PlayerTag)))
            {
                Assert.AreEqual(1, query.CalculateEntityCount(), "Player should exist immediately after bootstrap");
                var playerEntity = query.GetSingletonEntity();
                var transform = spawnManager.GetComponentData<LocalTransform>(playerEntity);

                Assert.AreEqual(0f, transform.Position.x, 0.001f, "Spawn X should be zero");
                Assert.AreEqual(2f, transform.Position.y, 0.001f, "Spawn Y should be configured height prior to physics update");
                Assert.AreEqual(0f, transform.Position.z, 0.001f, "Spawn Z should be zero");
            }

            // Clean up the temporary world.
            DestroyBootstrapVisuals();
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(spawnWorld);
            spawnWorld.Dispose();
            World.DefaultGameObjectInjectionWorld = null;

            // Recreate the standard test world so subsequent tests see the expected state.
            DefaultWorldInitialization.Initialize("Test World", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;
            initializationGroup = testWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            simulationGroup = testWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            presentationGroup = testWorld.GetExistingSystemManaged<PresentationSystemGroup>();
            UpdateWorldOnce();

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayerEntity_RemainsAlignedHorizontallyAfterFirstFrame()
        {
            yield return null;
            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);

            Assert.AreEqual(0f, transform.Position.x, 0.001f, "Player X should remain aligned with spawn");
            Assert.AreEqual(0f, transform.Position.z, 0.001f, "Player Z should remain aligned with spawn");
            Assert.GreaterOrEqual(transform.Position.y, 0f, "Player should remain above ground after physics step");
        }

        [UnityTest]
        public IEnumerator PlayerVisualSync_SyncsPositionWithEntity()
        {
            yield return null;

            var playerVisual = GameObject.Find("Player Visual (ECS Synced)");
            Assert.IsNotNull(playerVisual, "Player visual should exist");

            var sync = playerVisual.GetComponent<PlayerVisualSync>();
            Assert.IsNotNull(sync, "PlayerVisualSync component should exist");

            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            Assert.AreEqual(playerEntity, sync.targetEntity, "PlayerVisualSync should reference the player entity");

            var newPosition = new float3(5, 10, 15);
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Position = newPosition;
            entityManager.SetComponentData(playerEntity, transform);

            // Allow transform systems and MonoBehaviour sync to run.
            yield return null;
            yield return null;
            yield return null;

            Vector3 actualPosition = playerVisual.transform.position;
            Assert.AreEqual((Vector3)newPosition, actualPosition,
                $"Visual GameObject position should sync with entity. Expected: {newPosition}, Actual: {actualPosition}");
        }

        [UnityTest]
        public IEnumerator PlayerVisualSync_SyncsRotationWithEntity()
        {
            yield return null;

            var playerVisual = GameObject.Find("Player Visual (ECS Synced)");
            Assert.IsNotNull(playerVisual, "Player visual should exist");

            var sync = playerVisual.GetComponent<PlayerVisualSync>();
            Assert.IsNotNull(sync, "PlayerVisualSync component should exist");

            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            Assert.AreEqual(playerEntity, sync.targetEntity, "PlayerVisualSync should reference the player entity");

            var newRotation = quaternion.RotateY(math.PI / 2);
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Rotation = newRotation;
            entityManager.SetComponentData(playerEntity, transform);

            yield return null;
            yield return null;
            yield return null;

            var actualRotation = playerVisual.transform.rotation;
            float diff = math.length(newRotation.value - ((quaternion)actualRotation).value);
            Assert.IsTrue(diff < 0.001f,
                $"Visual GameObject rotation should sync with entity. Expected: {newRotation}, Actual: {actualRotation}, Diff: {diff}");
        }

        [UnityTest]
        public IEnumerator PlayerCameraSettings_Defaults()
        {
            yield return null;

            using var playerQuery = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = playerQuery.GetSingletonEntity();

            var settings = entityManager.GetComponentData<PlayerCameraSettings>(playerEntity);

            Assert.AreEqual(0f, settings.FirstPersonOffset.x, 0.001f, "First-person offset X should default to 0");
            Assert.AreEqual(1.6f, settings.FirstPersonOffset.y, 0.001f, "First-person offset Y should default to 1.6");
            Assert.AreEqual(0f, settings.FirstPersonOffset.z, 0.001f, "First-person offset Z should default to 0");

            Assert.AreEqual(0f, settings.ThirdPersonPivotOffset.x, 0.001f, "Third-person pivot offset X should default to 0");
            Assert.AreEqual(1.5f, settings.ThirdPersonPivotOffset.y, 0.001f, "Third-person pivot offset Y should default to 1.5");
            Assert.AreEqual(0f, settings.ThirdPersonPivotOffset.z, 0.001f, "Third-person pivot offset Z should default to 0");

            Assert.AreEqual(3.5f, settings.ThirdPersonDistance, 0.001f, "Third-person distance should default to 3.5");
            Assert.IsFalse(settings.IsThirdPerson, "Default camera mode should be first-person");
        }

        [UnityTest]
        public IEnumerator PlayerVisualSync_HandlesDestroyedEntity()
        {
            yield return null;

            var playerVisual = GameObject.Find("Player Visual (ECS Synced)");
            Assert.IsNotNull(playerVisual, "Player visual should exist");

            using var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();

            entityManager.DestroyEntity(playerEntity);

            yield return new WaitForEndOfFrame();
            Assert.IsNull(GameObject.Find("Player Visual (ECS Synced)"), "Player visual should be destroyed when entity is removed");
        }

        #endregion

        #region Helpers

        private static void CleanupDefaultWorld()
        {
            DestroyBootstrapVisuals();

            if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(World.DefaultGameObjectInjectionWorld);
                World.DefaultGameObjectInjectionWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = null;
        }

        private void UpdateWorldOnce()
        {
            initializationGroup.Update();
            simulationGroup.Update();
            presentationGroup.Update();
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

        private Entity? GetGroundEntity()
        {
            using var entities = entityManager.GetAllEntities(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (entityManager.GetName(entity).Contains("Ground"))
                {
                    return entity;
                }
            }
            return null;
        }

        #endregion
    }
}

