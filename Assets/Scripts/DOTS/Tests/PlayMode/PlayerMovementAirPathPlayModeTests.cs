using System.Collections;
using NUnit.Framework;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine.TestTools;
using DOTS.Player.Components;
using DOTS.Player.Systems;
using DOTS.Terrain;

namespace DOTS.Tests.PlayMode
{
    /// <summary>
    /// PlayMode tests for validating PlayerMovementSystem branch selection between
    /// ground snap and air-control lerp.
    /// </summary>
    [TestFixture]
    public class PlayerMovementAirPathPlayModeTests
    {
        private World previousWorld;
        private World testWorld;
        private EntityManager entityManager;
        private PhysicsSystemGroup physicsGroup;
        private double elapsedTime;

        private const float FixedDeltaTime = 1f / 60f;

        [SetUp]
        public void SetUp()
        {
            previousWorld = World.DefaultGameObjectInjectionWorld;

            DefaultWorldInitialization.Initialize("Player Movement Air Path PlayMode Tests", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;

            physicsGroup = testWorld.GetExistingSystemManaged<PhysicsSystemGroup>();
            var movementHandle = testWorld.CreateSystem<PlayerMovementSystem>();
            physicsGroup.AddSystemToUpdateList(movementHandle);
            TrySortSystems(physicsGroup);

            elapsedTime = 0d;
        }

        [TearDown]
        public void TearDown()
        {
            if (testWorld != null && testWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
                testWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = previousWorld;
        }

        [UnityTest]
        public IEnumerator GroundedWithGroundMode_UsesGroundLerpRate()
        {
            // IsGrounded=true triggers the ground lerp branch (GroundLerpRate = 25/s).
            // With initialX=2, targetX=10, dt=1/60:
            //   groundLerp = saturate(25 * 1/60) ≈ 0.4167
            //   expected   = lerp(2, 10, 0.4167) ≈ 5.333
            // No PhysicsWorldSingleton is present in this test harness so the wall
            // probe does not fire, giving a clean lerp measurement.
            const float initialX = 2f;
            const float groundSpeed = 10f;
            var expected = math.lerp(initialX, groundSpeed, math.saturate(25f * FixedDeltaTime));

            var entity = CreateMovementEntity(
                mode: PlayerMovementMode.Grounded,
                isGrounded: true,
                initialLinearVelocity: new float3(initialX, 0f, 0f),
                moveInput: new float2(1f, 0f),
                groundSpeed: groundSpeed,
                airControl: 0.2f);

            TickPhysicsGroupOnce();

            var velocity = entityManager.GetComponentData<PhysicsVelocity>(entity);

            Assert.AreEqual(expected, velocity.Linear.x, 1e-3f,
                "Expected grounded movement to lerp toward GroundSpeed at GroundLerpRate, not snap instantly.");
            Assert.Less(velocity.Linear.x, groundSpeed - 1e-3f,
                "Ground lerp should not reach full GroundSpeed in a single tick.");

            entityManager.DestroyEntity(entity);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UngroundedWithNonGroundMode_UsesAirControlLerp()
        {
            const float airControl = 0.2f;
            const float groundSpeed = 10f;
            const float initialX = 2f;

            // Use Ballistic (airborne after slingshot launch) rather than SlingshotCharging
            // because SlingshotCharging now suppresses WASD input (player is stationary while charging).
            var entity = CreateMovementEntity(
                mode: PlayerMovementMode.Ballistic,
                isGrounded: false,
                initialLinearVelocity: new float3(initialX, 0f, 0f),
                moveInput: new float2(1f, 0f),
                groundSpeed: groundSpeed,
                airControl: airControl);

            TickPhysicsGroupOnce();

            var velocity = entityManager.GetComponentData<PhysicsVelocity>(entity);
            var expected = math.lerp(initialX, groundSpeed, math.saturate(airControl * FixedDeltaTime));

            // Air branch should gradually steer rather than snap.
            Assert.AreEqual(expected, velocity.Linear.x, 1e-4f,
                "Expected air-control branch to lerp horizontal velocity when ungrounded and mode is non-ground.");
            Assert.Less(velocity.Linear.x, groundSpeed - 1e-3f,
                "Air-control branch should not snap to full ground speed in one tick.");

            entityManager.DestroyEntity(entity);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BallisticWithZeroFallTime_StillUsesAirControlLerp()
        {
            const float airControl = 0.2f;
            const float groundSpeed = 10f;
            const float initialX = 2f;

            var entity = CreateMovementEntity(
                mode: PlayerMovementMode.Ballistic,
                isGrounded: false,
                initialLinearVelocity: new float3(initialX, 0f, 0f),
                moveInput: new float2(1f, 0f),
                groundSpeed: groundSpeed,
                airControl: airControl);

            // Regression guard: airborne traversal should not depend on FallTime
            // clearing GroundControlGraceTime.
            var movementState = entityManager.GetComponentData<PlayerMovementState>(entity);
            movementState.FallTime = 0f;
            entityManager.SetComponentData(entity, movementState);

            TickPhysicsGroupOnce();

            var velocity = entityManager.GetComponentData<PhysicsVelocity>(entity);
            var expected = math.lerp(initialX, groundSpeed, math.saturate(airControl * FixedDeltaTime));

            Assert.AreEqual(expected, velocity.Linear.x, 1e-4f,
                "Ballistic mode should immediately use air-control even when FallTime is zero.");
            Assert.Less(velocity.Linear.x, groundSpeed - 1e-3f,
                "Air-control branch should not snap to full ground speed in one tick.");

            entityManager.DestroyEntity(entity);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GroundedJump_ImmediatelyPromotesToBallistic()
        {
            var entity = CreateMovementEntity(
                mode: PlayerMovementMode.Grounded,
                isGrounded: true,
                initialLinearVelocity: float3.zero,
                moveInput: float2.zero,
                groundSpeed: 10f,
                airControl: 0.2f,
                jumpPressed: true);

            TickPhysicsGroupOnce();

            var movementState = entityManager.GetComponentData<PlayerMovementState>(entity);
            var velocity = entityManager.GetComponentData<PhysicsVelocity>(entity);
            var input = entityManager.GetComponentData<PlayerInputComponent>(entity);

            Assert.AreEqual(PlayerMovementMode.Ballistic, movementState.Mode,
                "Grounded jump should enter Ballistic on the takeoff frame so the jump animation can trigger from idle/run.");
            Assert.IsFalse(movementState.IsGrounded,
                "Grounded jump should clear IsGrounded on the takeoff frame.");
            Assert.AreEqual(5f, velocity.Linear.y, 1e-4f,
                "Grounded jump should apply the configured jump impulse immediately.");
            Assert.IsFalse(input.JumpPressed,
                "JumpPressed should be consumed after the jump impulse is applied.");

            entityManager.DestroyEntity(entity);
            yield return null;
        }

        private Entity CreateMovementEntity(
            PlayerMovementMode mode,
            bool isGrounded,
            float3 initialLinearVelocity,
            float2 moveInput,
            float groundSpeed,
            float airControl,
            bool jumpPressed = false)
        {
            var entity = entityManager.CreateEntity(
                typeof(PlayerMovementConfig),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementState),
                typeof(PlayerViewComponent),
                typeof(LocalTransform),
                typeof(PhysicsVelocity));

            entityManager.SetComponentData(entity, new PlayerMovementConfig
            {
                GroundSpeed = groundSpeed,
                JumpImpulse = 5f,
                AirControl = airControl,
                SlingshotImpulse = 0f,
                SwimSpeed = 0f,
                ZeroGDamping = 0f,
                MouseSensitivity = 0f,
                MaxPitchDegrees = 85f,
                GroundProbeDistance = 1.3f
            });

            entityManager.SetComponentData(entity, new PlayerInputComponent
            {
                Move = moveInput,
                Look = float2.zero,
                JumpPressed = jumpPressed
            });

            entityManager.SetComponentData(entity, new PlayerMovementState
            {
                Mode = mode,
                IsGrounded = isGrounded,
                // Set FallTime beyond GroundControlGraceTime (0.12s) for airborne entities
                // so the movement system uses the air-control path, not the ground grace path.
                FallTime = isGrounded ? 0f : 0.2f,
                PreviousPosition = float3.zero
            });

            // Yaw=0 makes right=(1,0,0), so Move.x=1 targets positive X ground speed.
            entityManager.SetComponentData(entity, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });

            entityManager.SetComponentData(entity, LocalTransform.FromPosition(float3.zero));
            entityManager.SetComponentData(entity, new PhysicsVelocity
            {
                Linear = initialLinearVelocity,
                Angular = float3.zero
            });

            return entity;
        }

        private void TickPhysicsGroupOnce()
        {
            elapsedTime += FixedDeltaTime;
            testWorld.SetTime(new TimeData(elapsedTime, FixedDeltaTime));
            physicsGroup.Update();
        }

        private static void TrySortSystems(ComponentSystemGroup group)
        {
            if (group == null)
            {
                return;
            }

            var sortMethod = group.GetType().GetMethod("SortSystems", System.Type.EmptyTypes);
            sortMethod?.Invoke(group, null);
        }
    }
}
