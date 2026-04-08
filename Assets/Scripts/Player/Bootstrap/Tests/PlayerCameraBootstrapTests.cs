using System.Collections;
using System.Text;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using DOTS.Player.Bootstrap;
using DOTS.Player.Components;
using DOTS.Terrain;

namespace DOTS.Player.Tests.Bootstrap
{
    [TestFixture]
    public class PlayerEntityBootstrapTests
    {
        private World testWorld;
        private EntityManager entityManager;

        [SetUp]
        public void SetUp()
        {
            CleanupDefaultWorld();

            DefaultWorldInitialization.Initialize("Test World", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;

            // Create player systems for testing (bypasses ProjectFeatureConfig)
            TestSystemBootstrap.CreateBootstrapSystemsOnly(testWorld);

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
            InitializeWorldWithoutSimulation("Physics Properties Test World", out var physicsWorld, out var physicsManager);

            using (var query = physicsManager.CreateEntityQuery(typeof(PlayerTag)))
            {
                var playerEntity = query.GetSingletonEntity();

                var gravityFactor = physicsManager.GetComponentData<PhysicsGravityFactor>(playerEntity);
                Assert.AreEqual(1f, gravityFactor.Value, 0.01f, "Gravity factor should be 1.0");

                var physicsSnapshot = CaptureBootstrapPhysicsSnapshot(physicsWorld);
                Assert.Greater(physicsSnapshot.FixedTimeStep, 0f, "Fixed timestep should be positive");

                var velocity = physicsManager.GetComponentData<PhysicsVelocity>(playerEntity);
                Assert.AreEqual(float3.zero, velocity.Linear, "Velocity should be zero immediately after bootstrap");
                Assert.AreEqual(float3.zero, velocity.Angular, "Angular velocity should be zero immediately after bootstrap");

                AdvanceSimulationOneStep(physicsWorld);

                var updatedVelocity = physicsManager.GetComponentData<PhysicsVelocity>(playerEntity);
                float expectedVerticalVelocity = physicsSnapshot.Gravity.y * physicsSnapshot.FixedTimeStep;

                Assert.AreEqual(expectedVerticalVelocity, updatedVelocity.Linear.y, 0.01f, "Single simulation step should apply one gravity integration");
                Assert.AreEqual(velocity.Linear.x, updatedVelocity.Linear.x, 0.001f, "Horizontal X velocity should remain constant");
                Assert.AreEqual(velocity.Linear.z, updatedVelocity.Linear.z, 0.001f, "Horizontal Z velocity should remain constant");
                Assert.AreEqual(float3.zero, updatedVelocity.Angular, "Angular velocity should remain zero after first step");
            }

            CleanupDefaultWorld();
            RestoreStandardTestWorld();

            yield break;
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

        [UnityTest]
        public IEnumerator Bootstrap_DisablesPreExistingMainCamera()
        {
            // Tear down current world to get a clean slate before bootstrap runs.
            TearDown();

            // Arrange: create a pre-existing scene camera tagged MainCamera,
            // simulating Unity's default "Main Camera" that ships in new scenes.
            var preExistingGO = new GameObject("Pre-existing Main Camera");
            var preExistingCam = preExistingGO.AddComponent<Camera>();
            preExistingGO.tag = "MainCamera";

            // Act: run bootstrap — it should find and disable the pre-existing camera.
            SetUp();
            yield return null;

            // Assert: Camera.main resolves to the ECS-managed camera.
            Assert.IsNotNull(Camera.main, "Camera.main should not be null after bootstrap");
            Assert.AreEqual("Main Camera (ECS Player)", Camera.main.name,
                "Camera.main should be the ECS-managed camera, not the pre-existing scene camera");

            // Assert: the old scene camera is disabled and untagged.
            Assert.IsFalse(preExistingCam.enabled,
                "Pre-existing MainCamera camera component should be disabled");
            Assert.AreNotEqual("MainCamera", preExistingGO.tag,
                "Pre-existing camera should no longer be tagged MainCamera");

            // Assert: exactly one enabled camera is tagged MainCamera.
            var remaining = GameObject.FindGameObjectsWithTag("MainCamera");
            Assert.AreEqual(1, remaining.Length, "Exactly one camera should remain tagged MainCamera");
            Assert.IsTrue(remaining[0].GetComponent<Camera>().enabled,
                "The remaining tagged camera should be enabled");

            // Cleanup pre-existing camera.
            Object.DestroyImmediate(preExistingGO);
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

        [UnityTest]
        public IEnumerator CenterAimRay_And_ReticleDot_AreAligned_With_MainCamera()
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            EnsureReticleInstalledForTests();

            // Let RuntimeInitialize + ReticleController build/bind its UI.
            yield return null;
            yield return new WaitForEndOfFrame();

            var mainCamera = Camera.main;
            Assert.IsNotNull(mainCamera, "Camera.main should exist for center aim and reticle checks.");

            const float probeDistance = 25f;
            bool gotCenterRay = CenterAimRayUtility.TryGetCenterRay(
                probeDistance,
                out var utilityCamera,
                out var origin,
                out var direction,
                out var end);

            Assert.IsTrue(gotCenterRay, "CenterAimRayUtility should produce a center ray when Camera.main exists.");
            Assert.AreEqual(mainCamera.GetInstanceID(), utilityCamera.GetInstanceID(),
                "CenterAimRayUtility should use the active main camera.");

            var viewportCenterRay = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            Assert.Less(Vector3.Distance((Vector3)origin, viewportCenterRay.origin), 0.001f,
                "Center ray origin should match camera viewport-center ray origin.");
            Assert.Less(Vector3.Distance((Vector3)direction, viewportCenterRay.direction.normalized), 0.001f,
                "Center ray direction should match camera viewport-center ray direction.");
            Assert.Less(Vector3.Distance((Vector3)end, viewportCenterRay.origin + viewportCenterRay.direction.normalized * probeDistance), 0.001f,
                "Center ray end should match origin + direction * distance.");

            var reticleDot = GameObject.Find("ReticleDot");
            Assert.IsNotNull(reticleDot, "Reticle dot should exist.");

            var dotRect = reticleDot.GetComponent<RectTransform>();
            Assert.IsNotNull(dotRect, "Reticle dot should have a RectTransform.");

            var canvas = reticleDot.GetComponentInParent<Canvas>();
            Assert.IsNotNull(canvas, "Reticle dot should have a parent canvas.");

            var screenPoint = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, dotRect.position);
            var expectedCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Assert.LessOrEqual(Vector2.Distance(screenPoint, expectedCenter), 2f,
                $"Reticle dot should remain centered on screen. Dot={screenPoint}, Expected={expectedCenter}");
        }

        #endregion

        #region PlayerVisualSync Tests

        [UnityTest]
        public IEnumerator PlayerEntity_SpawnHeightMatchesBootstrapConfiguration()
        {
            InitializeWorldWithoutSimulation("Spawn Test World", out var spawnWorld, out var spawnManager);

            using (var query = spawnManager.CreateEntityQuery(typeof(PlayerTag)))
            {
                Assert.AreEqual(1, query.CalculateEntityCount(), "Player should exist immediately after bootstrap");
                var playerEntity = query.GetSingletonEntity();
                var transform = spawnManager.GetComponentData<LocalTransform>(playerEntity);

                Assert.AreEqual(0f, transform.Position.x, 0.001f, "Spawn X should be zero");
                Assert.AreEqual(PlayerEntityBootstrap.PlayerStartHeight, transform.Position.y, 0.001f, "Spawn Y should be configured height prior to physics update");
                Assert.AreEqual(0f, transform.Position.z, 0.001f, "Spawn Z should be zero");
            }

            CleanupDefaultWorld();
            RestoreStandardTestWorld();

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayerEntity_InitialVelocityRemainsZeroBeforeSimulation()
        {
            InitializeWorldWithoutSimulation("Velocity Test World", out var spawnWorld, out var spawnManager);

            using (var query = spawnManager.CreateEntityQuery(typeof(PlayerTag)))
            {
                Assert.AreEqual(1, query.CalculateEntityCount(), "Player should exist immediately after bootstrap");
                var playerEntity = query.GetSingletonEntity();
                var velocity = spawnManager.GetComponentData<PhysicsVelocity>(playerEntity);

                Assert.AreEqual(0f, velocity.Linear.x, 0.0001f, "Initial X velocity should be zero before physics");
                Assert.AreEqual(0f, velocity.Linear.y, 0.0001f, "Initial Y velocity should remain zero before physics updates");
                Assert.AreEqual(0f, velocity.Linear.z, 0.0001f, "Initial Z velocity should be zero before physics");
            }

            CleanupDefaultWorld();
            RestoreStandardTestWorld();

            yield break;
        }

        [UnityTest]
        public IEnumerator PlayerEntity_PhysicsVelocityTimelineDiagnostics()
        {
            InitializeWorldWithoutSimulation("Physics Timeline Diagnostics World", out var timelineWorld, out var timelineManager);

            yield return SamplePhysicsVelocityTimeline(timelineWorld, timelineManager, "BootstrapOnly", 3);

            CleanupDefaultWorld();
            RestoreStandardTestWorld();
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

            // Allow transform systems and MonoBehaviour sync to run after LateUpdate.
            yield return null;
            yield return new WaitForEndOfFrame();

            var updatedTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var entityRotation = new Quaternion(
                updatedTransform.Rotation.value.x,
                updatedTransform.Rotation.value.y,
                updatedTransform.Rotation.value.z,
                updatedTransform.Rotation.value.w);
            var expectedVisualPosition = (Vector3)updatedTransform.Position + entityRotation * sync.visualOffset;
            Vector3 actualPosition = playerVisual.transform.position;
            float positionDelta = Vector3.Distance(actualPosition, expectedVisualPosition);
            Assert.LessOrEqual(positionDelta, 0.001f,
                $"Visual GameObject position should match entity transform + visual offset within tolerance. Expected: {expectedVisualPosition}, Visual: {actualPosition}, Delta: {positionDelta}");
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

            const float yawDegrees = 135f;
            const float pitchDegrees = -10f; // stored on PlayerViewComponent for camera usage

            var view = entityManager.GetComponentData<PlayerViewComponent>(playerEntity);
            view.YawDegrees = yawDegrees;
            view.PitchDegrees = pitchDegrees;
            entityManager.SetComponentData(playerEntity, view);

            var newRotation = quaternion.AxisAngle(math.up(), math.radians(yawDegrees));
            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            transform.Rotation = newRotation;
            entityManager.SetComponentData(playerEntity, transform);

            yield return null;
            yield return new WaitForEndOfFrame();

            var updatedTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var actualRotation = playerVisual.transform.rotation;
            float diff = math.length(updatedTransform.Rotation.value - ((quaternion)actualRotation).value);
            Assert.IsTrue(diff < 0.001f,
                $"Visual GameObject rotation should sync with entity. Entity: {updatedTransform.Rotation}, Visual: {actualRotation}, Diff: {diff}");

            var entityForward = math.mul(updatedTransform.Rotation, math.forward());
            var visualForward = playerVisual.transform.forward;
            Assert.Less(Vector3.Distance((Vector3)entityForward, visualForward), 0.001f,
                "Visual forward vector should match entity forward vector");
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
            RunSystemOnce<PlayerEntityBootstrap>();
        }

        private void AdvanceSimulationOneStep()
        {
            AdvanceSimulationOneStep(testWorld);
        }

        private static void AdvanceSimulationOneStep(World world)
        {
            if (world == null || !world.IsCreated)
            {
                return;
            }

            var simulation = world.GetExistingSystemManaged<SimulationSystemGroup>();
            var presentation = world.GetExistingSystemManaged<PresentationSystemGroup>();

            simulation?.Update();
            presentation?.Update();
        }

        private void RunSystemOnce<T>() where T : unmanaged, ISystem
        {
            if (testWorld == null || !testWorld.IsCreated)
            {
                return;
            }

            // Create the system if it doesn't exist (needed for [DisableAutoCreation] systems)
            var handle = testWorld.GetExistingSystem<T>();
            if (handle == SystemHandle.Null)
            {
                handle = testWorld.CreateSystem<T>();
            }
            
            if (handle != SystemHandle.Null)
            {
                handle.Update(testWorld.Unmanaged);
            }
        }

        private static void DestroyBootstrapVisuals()
        {
            DestroyImmediateIfExists("Player Visual (ECS Synced)");
            DestroyImmediateIfExists("Ground Visual (ECS Synced)");
            DestroyImmediateIfExists("Main Camera (ECS Player)");
        }

        private static void EnsureReticleInstalledForTests()
        {
            var method = typeof(ReticleBootstrap).GetMethod(
                "EnsureReticleInstalled",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, "ReticleBootstrap.EnsureReticleInstalled method should exist.");
            method.Invoke(null, null);
        }

        private static void DestroyImmediateIfExists(string name)
        {
            var go = GameObject.Find(name);
            if (go != null)
            {
                Object.DestroyImmediate(go);
            }
        }

        private PlayerBootstrapPhysicsSnapshot CaptureBootstrapPhysicsSnapshot(World worldOverride = null)
        {
            World world = worldOverride;
            if (world == null || !world.IsCreated)
            {
                if (testWorld != null && testWorld.IsCreated)
                {
                    world = testWorld;
                }
                else
                {
                    world = World.DefaultGameObjectInjectionWorld;
                }
            }

            Assert.IsNotNull(world, "World should exist when capturing bootstrap physics snapshot");
            return PlayerBootstrapPhysicsUtility.Capture(world, nameof(PlayerEntityBootstrapTests));
        }

        private void InitializeWorldWithoutSimulation(string worldName, out World world, out EntityManager manager)
        {
            CleanupDefaultWorld();

            DefaultWorldInitialization.Initialize(worldName, false);
            world = World.DefaultGameObjectInjectionWorld;
            manager = world.EntityManager;
            
            // Create player systems for testing (bypasses ProjectFeatureConfig)
            TestSystemBootstrap.CreateBootstrapSystemsOnly(world);
            
            // Run the bootstrap system once to create entities
            var bootstrapHandle = world.GetExistingSystem<PlayerEntityBootstrap>();
            if (bootstrapHandle != SystemHandle.Null)
            {
                bootstrapHandle.Update(world.Unmanaged);
            }
            
            var initGroup = world.GetExistingSystemManaged<InitializationSystemGroup>();
            initGroup.Update();
            ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(world);
        }

        private void RestoreStandardTestWorld()
        {
            DefaultWorldInitialization.Initialize("Test World", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;
            
            // Create player systems for testing (bypasses ProjectFeatureConfig)
            TestSystemBootstrap.CreateBootstrapSystemsOnly(testWorld);
            
            UpdateWorldOnce();
        }

private IEnumerator SamplePhysicsVelocityTimeline(World world, EntityManager manager, string label, int frames)
        {
            using var query = manager.CreateEntityQuery(typeof(PlayerTag));
            var playerEntity = query.GetSingletonEntity();
            var sb = new System.Text.StringBuilder();
            sb.Append($"[PlayerPhysicsTimeline::{label}] ");

            void AppendSample(int frameIndex)
            {
                var velocity = manager.GetComponentData<PhysicsVelocity>(playerEntity);
                sb.Append($"frame{frameIndex}=\u0394y:{velocity.Linear.y:F4} ");
            }

            AppendSample(0);

            for (int i = 1; i <= frames; i++)
            {
                AdvanceSimulationOneStep(world);
                yield return null;
                AppendSample(i);
            }

            Debug.Log(sb.ToString());
        }

        #endregion
    }
}

