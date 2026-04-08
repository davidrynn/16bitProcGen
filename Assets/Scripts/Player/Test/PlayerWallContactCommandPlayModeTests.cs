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
using DOTS.Terrain.Meshing;

namespace DOTS.Player.Test
{
    /// <summary>
    /// Captures the velocity command produced by PlayerMovementSystem while the player
    /// starts overlapped with a vertical wall collider.
    /// </summary>
    [TestFixture]
    public partial class PlayerWallContactCommandPlayModeTests
    {
        private World previousWorld;
        private World testWorld;
        private EntityManager entityManager;
        private InitializationSystemGroup initGroup;
        private SimulationSystemGroup simulationGroup;
        private FixedStepSimulationSystemGroup fixedStepGroup;
        private PhysicsSystemGroup physicsGroup;
        private double elapsedTime;

        private BlobAssetReference<Unity.Physics.Collider> playerColliderBlob;
        private BlobAssetReference<Unity.Physics.Collider> wallColliderBlob;
        private bool terrainPipelineSystemsInstalled;

        private const float FixedDeltaTime = 1f / 60f;
        private const float GroundSpeed = 10f;
        private const float AirControl = 0.2f;

        [SetUp]
        public void SetUp()
        {
            previousWorld = World.DefaultGameObjectInjectionWorld;

            DefaultWorldInitialization.Initialize("Player Wall Contact Command PlayMode Tests", false);
            testWorld = World.DefaultGameObjectInjectionWorld;
            entityManager = testWorld.EntityManager;

            initGroup = testWorld.GetExistingSystemManaged<InitializationSystemGroup>();
            simulationGroup = testWorld.GetExistingSystemManaged<SimulationSystemGroup>();
            fixedStepGroup = testWorld.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            fixedStepGroup.Timestep = FixedDeltaTime;
            physicsGroup = testWorld.GetExistingSystemManaged<PhysicsSystemGroup>();

            using (var physicsStepQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsStep>()))
            {
                if (physicsStepQuery.IsEmpty)
                {
                    var physicsStepEntity = entityManager.CreateEntity(typeof(PhysicsStep));
                    entityManager.SetComponentData(physicsStepEntity, PhysicsStep.Default);
                }
            }

            var movementHandle = testWorld.CreateSystem<PlayerMovementSystem>();
            var groundingHandle = testWorld.CreateSystem<PlayerGroundingSystem>();
            var captureHandle = testWorld.CreateSystem<CapturePostMovementVelocitySystem>();
            physicsGroup.AddSystemToUpdateList(groundingHandle);
            physicsGroup.AddSystemToUpdateList(movementHandle);
            physicsGroup.AddSystemToUpdateList(captureHandle);
            TrySortSystems(physicsGroup);

            elapsedTime = 0d;
            playerColliderBlob = default;
            wallColliderBlob = default;
            terrainPipelineSystemsInstalled = false;
        }

        [TearDown]
        public void TearDown()
        {
            if (playerColliderBlob.IsCreated)
            {
                playerColliderBlob.Dispose();
                playerColliderBlob = default;
            }

            if (wallColliderBlob.IsCreated)
            {
                wallColliderBlob.Dispose();
                wallColliderBlob = default;
            }

            if (testWorld != null && testWorld.IsCreated)
            {
                ScriptBehaviourUpdateOrder.RemoveWorldFromCurrentPlayerLoop(testWorld);
                testWorld.Dispose();
            }

            World.DefaultGameObjectInjectionWorld = previousWorld;
        }

        [UnityTest]
        public IEnumerator StaticWallCollider_IsDetectedFromHorizontalAndVerticalDirections()
        {
            wallColliderBlob = Unity.Physics.BoxCollider.Create(
                new BoxGeometry
                {
                    Center = float3.zero,
                    Size = new float3(0.2f, 4f, 4f),
                    Orientation = quaternion.identity,
                    BevelRadius = 0f
                },
                CollisionFilter.Default);

            var wall = entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsWorldIndex));
            entityManager.SetComponentData(wall, LocalTransform.FromPosition(new float3(2f, 2f, 0f)));
            entityManager.SetComponentData(wall, new PhysicsCollider { Value = wallColliderBlob });

            TickWorldOnce();

            using var worldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            Assert.IsFalse(worldQuery.IsEmpty, "Expected PhysicsWorldSingleton to exist.");
            var physicsWorld = worldQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;

            bool hitFromLeft = physicsWorld.CastRay(new RaycastInput
            {
                Start = new float3(-5f, 2f, 0f),
                End = new float3(5f, 2f, 0f),
                Filter = CollisionFilter.Default
            });

            bool hitFromFront = physicsWorld.CastRay(new RaycastInput
            {
                Start = new float3(2f, 2f, -5f),
                End = new float3(2f, 2f, 5f),
                Filter = CollisionFilter.Default
            });

            bool hitFromAbove = physicsWorld.CastRay(new RaycastInput
            {
                Start = new float3(2f, 8f, 0f),
                End = new float3(2f, -4f, 0f),
                Filter = CollisionFilter.Default
            });

            Assert.IsTrue(hitFromLeft, "Expected horizontal X-direction raycast to hit wall collider.");
            Assert.IsTrue(hitFromFront, "Expected horizontal Z-direction raycast to hit wall collider.");
            Assert.IsTrue(hitFromAbove, "Expected vertical Y-direction raycast to hit wall collider.");

            entityManager.DestroyEntity(wall);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverlappedWall_UngroundedGroundMode_CommandsAirControlLerp()
        {
            // When IsGrounded=false, the movement system uses the air-control path
            // regardless of Mode. The Mode field is currently unused by PlayerMovementSystem.
            // On the first tick with zero initial velocity, the air-control lerp produces a
            // small fraction of GroundSpeed. The wall probe may further clamp the into-wall
            // component if the physics world is ready.
            var player = CreateWallOverlapScenario(PlayerMovementMode.Ground, isGrounded: false);

            TickWorldOnce();

            var sample = entityManager.GetComponentData<MovementCommandSample>(player);
            var airLerp = math.lerp(0f, GroundSpeed, math.saturate(AirControl * FixedDeltaTime));

            Assert.Greater(sample.CaptureCount, 0, "Expected capture system to sample velocity after movement update.");
            // Velocity should be at most the air-control command in the into-wall direction.
            // Depending on overlap/depenetration timing, a small outward (negative X) value is valid.
            Assert.LessOrEqual(sample.LastLinearX, airLerp + 1e-3f,
                "Ungrounded movement should use air-control lerp, not full ground speed.");
            Assert.Greater(sample.LastLinearX, -1f,
                "Velocity magnitude should remain bounded while resolving wall overlap.");

            entityManager.DestroyEntity(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OverlappedWall_UngroundedNonGroundMode_CommandsAirControlLerp()
        {
            var player = CreateWallOverlapScenario(PlayerMovementMode.Slingshot, isGrounded: false);

            TickWorldOnce();

            var sample = entityManager.GetComponentData<MovementCommandSample>(player);
            var expected = math.lerp(0f, GroundSpeed, math.saturate(AirControl * FixedDeltaTime));

            Assert.Greater(sample.CaptureCount, 0, "Expected capture system to sample velocity after movement update.");
            Assert.LessOrEqual(sample.LastLinearX, expected + 1e-4f,
                "Non-ground mode should not exceed air-control lerp command in the into-wall direction.");
            Assert.Greater(sample.LastLinearX, -1f,
                "Velocity should remain bounded while overlap resolution pushes away from wall.");
            Assert.Less(sample.LastLinearX, 1f,
                "Air-control command in a single tick should remain small compared with full ground speed.");

            entityManager.DestroyEntity(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TerrainWall_AutoDrive_DoesNotCrossFarSide()
        {
            EnsureTerrainPipelineSystems();
            EnsureSdfFieldSettings(baseHeight: 4f, amplitude: 0f, frequency: 0f, noiseValue: 0f);
            EnsureTerrainColliderSettings();

            var chunkEntity = Entity.Null;
            var playerEntity = Entity.Null;
            var editEntity = Entity.Null;

            const float wallCenterX = 6f;
            const float wallHalfX = 0.6f;
            const float playerRadius = 0.5f;
            var farSideThreshold = wallCenterX + wallHalfX + playerRadius + 0.05f;
            float maxObservedX = float.MinValue;

            try
            {
                editEntity = EnsureEditBufferEntity();
                var editBuffer = entityManager.GetBuffer<SDFEdit>(editEntity);
                editBuffer.Clear();
                editBuffer.Add(SDFEdit.CreateBox(
                    center: new float3(wallCenterX, 5f, 8f),
                    halfExtents: new float3(wallHalfX, 3.5f, 5f),
                    operation: SDFEditOperation.Add));

                chunkEntity = CreateTerrainChunkEntity(
                    resolution: new int3(20, 20, 20),
                    voxelSize: 1f,
                    worldOrigin: float3.zero);

                playerEntity = CreateTerrainDriverPlayer(
                    position: new float3(2f, 4.2f, 8f),
                    mode: PlayerMovementMode.Ground,
                    isGrounded: false);

                var colliderReady = TickUntil(300, () =>
                {
                    if (!entityManager.Exists(chunkEntity) || !entityManager.Exists(playerEntity))
                    {
                        return false;
                    }

                    if (!HasPhysicsWorldSingleton())
                    {
                        return false;
                    }

                    if (!entityManager.HasComponent<PhysicsCollider>(chunkEntity))
                    {
                        return false;
                    }

                    using var worldQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
                    var world = worldQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
                    var horizontalProbe = new RaycastInput
                    {
                        Start = new float3(0f, 5f, 8f),
                        End = new float3(12f, 5f, 8f),
                        Filter = CollisionFilter.Default
                    };
                    return world.CastRay(horizontalProbe);
                });

                Assert.IsTrue(colliderReady, "Terrain wall collider was not ready in time.");

                bool crossed = false;
                for (int i = 0; i < 360; i++)
                {
                    if (!entityManager.Exists(playerEntity))
                    {
                        Assert.Fail("Player entity disappeared during terrain wall drive test.");
                    }

                    var input = entityManager.GetComponentData<PlayerInputComponent>(playerEntity);
                    input.Move = new float2(1f, 0f);
                    input.JumpPressed = false;
                    entityManager.SetComponentData(playerEntity, input);

                    TickWorldOnce();

                    var x = entityManager.GetComponentData<LocalTransform>(playerEntity).Position.x;
                    maxObservedX = math.max(maxObservedX, x);
                    if (x > farSideThreshold)
                    {
                        crossed = true;
                        break;
                    }
                }

                Assert.IsFalse(
                    crossed,
                    $"Player crossed terrain wall. maxX={maxObservedX:F3}, threshold={farSideThreshold:F3}.");
            }
            finally
            {
                if (playerEntity != Entity.Null && entityManager.Exists(playerEntity))
                {
                    if (entityManager.HasComponent<PhysicsCollider>(playerEntity))
                    {
                        entityManager.RemoveComponent<PhysicsCollider>(playerEntity);
                    }
                    entityManager.DestroyEntity(playerEntity);
                }

                if (chunkEntity != Entity.Null && entityManager.Exists(chunkEntity))
                {
                    if (entityManager.HasComponent<TerrainChunkColliderData>(chunkEntity))
                    {
                        var colliderData = entityManager.GetComponentData<TerrainChunkColliderData>(chunkEntity);
                        if (colliderData.IsCreated)
                        {
                            colliderData.Dispose();
                        }
                        colliderData = default;
                        entityManager.SetComponentData(chunkEntity, colliderData);
                        entityManager.RemoveComponent<TerrainChunkColliderData>(chunkEntity);
                    }

                    if (entityManager.HasComponent<PhysicsCollider>(chunkEntity))
                    {
                        entityManager.RemoveComponent<PhysicsCollider>(chunkEntity);
                    }

                    if (entityManager.HasComponent<TerrainChunkDensity>(chunkEntity))
                    {
                        var density = entityManager.GetComponentData<TerrainChunkDensity>(chunkEntity);
                        density.Dispose();
                    }

                    if (entityManager.HasComponent<TerrainChunkMeshData>(chunkEntity))
                    {
                        var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(chunkEntity);
                        meshData.Dispose();
                    }

                    entityManager.DestroyEntity(chunkEntity);
                }

                if (editEntity != Entity.Null && entityManager.Exists(editEntity))
                {
                    var editBuffer = entityManager.GetBuffer<SDFEdit>(editEntity);
                    editBuffer.Clear();
                }
            }

            yield return null;
        }

        private Entity CreateWallOverlapScenario(PlayerMovementMode mode, bool isGrounded)
        {
            // Static vertical wall centered at X=2 with half-thickness 0.1 => near face at X=1.9.
            wallColliderBlob = Unity.Physics.BoxCollider.Create(
                new BoxGeometry
                {
                    Center = float3.zero,
                    Size = new float3(0.2f, 4f, 4f),
                    Orientation = quaternion.identity,
                    BevelRadius = 0f
                },
                CollisionFilter.Default);

            var wall = entityManager.CreateEntity(typeof(LocalTransform), typeof(PhysicsCollider), typeof(PhysicsWorldIndex));
            entityManager.SetComponentData(wall, LocalTransform.FromPosition(new float3(2f, 2f, 0f)));
            entityManager.SetComponentData(wall, new PhysicsCollider { Value = wallColliderBlob });

            playerColliderBlob = Unity.Physics.CapsuleCollider.Create(
                new CapsuleGeometry
                {
                    Vertex0 = new float3(0f, 0.5f, 0f),
                    Vertex1 = new float3(0f, 1.5f, 0f),
                    Radius = 0.5f
                },
                new CollisionFilter
                {
                    BelongsTo = 1u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                },
                Unity.Physics.Material.Default);

            var player = entityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerMovementConfig),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementState),
                typeof(PlayerViewComponent),
                typeof(LocalTransform),
                typeof(PhysicsVelocity),
                typeof(PhysicsMass),
                typeof(PhysicsGravityFactor),
                typeof(PhysicsDamping),
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex),
                typeof(MovementCommandSample));

            // Start with slight overlap into the wall: center X=1.45 => capsule front X=1.95 (> 1.9 wall face).
            entityManager.SetComponentData(player, LocalTransform.FromPosition(new float3(1.45f, 0f, 0f)));
            entityManager.SetComponentData(player, new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });
            entityManager.SetComponentData(player, new PhysicsMass
            {
                InverseMass = 1f / 70f,
                InverseInertia = new float3(1f),
                Transform = RigidTransform.identity,
                AngularExpansionFactor = 0f,
                CenterOfMass = float3.zero
            });
            entityManager.SetComponentData(player, new PhysicsGravityFactor { Value = 0f });
            entityManager.SetComponentData(player, new PhysicsDamping { Linear = 0f, Angular = 0f });
            entityManager.SetComponentData(player, new PhysicsCollider { Value = playerColliderBlob });

            entityManager.SetComponentData(player, new PlayerMovementConfig
            {
                GroundSpeed = GroundSpeed,
                JumpImpulse = 5f,
                AirControl = AirControl,
                SlingshotImpulse = 0f,
                SwimSpeed = 0f,
                ZeroGDamping = 0f,
                MouseSensitivity = 0f,
                MaxPitchDegrees = 85f,
                GroundProbeDistance = 1.3f
            });
            entityManager.SetComponentData(player, new PlayerInputComponent
            {
                Move = new float2(1f, 0f),
                Look = float2.zero,
                JumpPressed = false
            });
            entityManager.SetComponentData(player, new PlayerMovementState
            {
                Mode = mode,
                IsGrounded = isGrounded,
                FallTime = 0f,
                PreviousPosition = new float3(1.45f, 0f, 0f)
            });
            entityManager.SetComponentData(player, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });
            entityManager.SetComponentData(player, new MovementCommandSample());

            return player;
        }

        private Entity CreateTerrainDriverPlayer(float3 position, PlayerMovementMode mode, bool isGrounded)
        {
            playerColliderBlob = Unity.Physics.CapsuleCollider.Create(
                new CapsuleGeometry
                {
                    Vertex0 = new float3(0f, 0.5f, 0f),
                    Vertex1 = new float3(0f, 1.5f, 0f),
                    Radius = 0.5f
                },
                new CollisionFilter
                {
                    BelongsTo = 1u,
                    CollidesWith = ~0u,
                    GroupIndex = 0
                },
                Unity.Physics.Material.Default);

            var player = entityManager.CreateEntity(
                typeof(PlayerTag),
                typeof(PlayerMovementConfig),
                typeof(PlayerInputComponent),
                typeof(PlayerMovementState),
                typeof(PlayerViewComponent),
                typeof(LocalTransform),
                typeof(PhysicsVelocity),
                typeof(PhysicsMass),
                typeof(PhysicsGravityFactor),
                typeof(PhysicsDamping),
                typeof(PhysicsCollider),
                typeof(PhysicsWorldIndex),
                typeof(MovementCommandSample));

            entityManager.SetComponentData(player, LocalTransform.FromPosition(position));
            entityManager.SetComponentData(player, new PhysicsVelocity
            {
                Linear = float3.zero,
                Angular = float3.zero
            });
            entityManager.SetComponentData(player, new PhysicsMass
            {
                InverseMass = 1f / 70f,
                InverseInertia = new float3(1f),
                Transform = RigidTransform.identity,
                AngularExpansionFactor = 0f,
                CenterOfMass = float3.zero
            });
            entityManager.SetComponentData(player, new PhysicsGravityFactor { Value = 1f });
            entityManager.SetComponentData(player, new PhysicsDamping { Linear = 0f, Angular = 0f });
            entityManager.SetComponentData(player, new PhysicsCollider { Value = playerColliderBlob });

            entityManager.SetComponentData(player, new PlayerMovementConfig
            {
                GroundSpeed = GroundSpeed,
                JumpImpulse = 5f,
                AirControl = AirControl,
                SlingshotImpulse = 0f,
                SwimSpeed = 0f,
                ZeroGDamping = 0f,
                MouseSensitivity = 0f,
                MaxPitchDegrees = 85f,
                GroundProbeDistance = 1.3f
            });
            entityManager.SetComponentData(player, new PlayerInputComponent
            {
                Move = new float2(1f, 0f),
                Look = float2.zero,
                JumpPressed = false
            });
            entityManager.SetComponentData(player, new PlayerMovementState
            {
                Mode = mode,
                IsGrounded = isGrounded,
                FallTime = 0f,
                PreviousPosition = position
            });
            entityManager.SetComponentData(player, new PlayerViewComponent
            {
                YawDegrees = 0f,
                PitchDegrees = 0f
            });
            entityManager.SetComponentData(player, new MovementCommandSample());

            return player;
        }

        private void EnsureTerrainPipelineSystems()
        {
            if (terrainPipelineSystemsInstalled)
            {
                return;
            }

            var densityHandle = testWorld.CreateSystem<TerrainChunkDensitySamplingSystem>();
            var meshHandle = testWorld.CreateSystem<TerrainChunkMeshBuildSystem>();
            var colliderHandle = testWorld.CreateSystem<TerrainChunkColliderBuildSystem>();

            simulationGroup.AddSystemToUpdateList(densityHandle);
            simulationGroup.AddSystemToUpdateList(meshHandle);
            simulationGroup.AddSystemToUpdateList(colliderHandle);

            TrySortSystems(simulationGroup);
            terrainPipelineSystemsInstalled = true;
        }

        private void EnsureSdfFieldSettings(float baseHeight, float amplitude, float frequency, float noiseValue)
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());
            if (query.IsEmpty)
            {
                var entity = entityManager.CreateEntity(typeof(SDFTerrainFieldSettings));
                entityManager.SetComponentData(entity, new SDFTerrainFieldSettings
                {
                    BaseHeight = baseHeight,
                    Amplitude = amplitude,
                    Frequency = frequency,
                    NoiseValue = noiseValue
                });
            }
            else
            {
                var entity = query.GetSingletonEntity();
                entityManager.SetComponentData(entity, new SDFTerrainFieldSettings
                {
                    BaseHeight = baseHeight,
                    Amplitude = amplitude,
                    Frequency = frequency,
                    NoiseValue = noiseValue
                });
            }
        }

        private void EnsureTerrainColliderSettings()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainColliderSettings>());
            if (query.IsEmpty)
            {
                var entity = entityManager.CreateEntity(typeof(TerrainColliderSettings));
                entityManager.SetComponentData(entity, new TerrainColliderSettings
                {
                    Enabled = true,
                    MaxCollidersPerFrame = 16,
                    EnableDetailedStaticMeshCollision = true
                });
            }
            else
            {
                var entity = query.GetSingletonEntity();
                entityManager.SetComponentData(entity, new TerrainColliderSettings
                {
                    Enabled = true,
                    MaxCollidersPerFrame = 16,
                    EnableDetailedStaticMeshCollision = true
                });
            }
        }

        private Entity EnsureEditBufferEntity()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFEdit>());
            if (!query.IsEmpty)
            {
                return query.GetSingletonEntity();
            }

            var entity = entityManager.CreateEntity();
            entityManager.AddBuffer<SDFEdit>(entity);
            return entity;
        }

        private Entity CreateTerrainChunkEntity(int3 resolution, float voxelSize, float3 worldOrigin)
        {
            var entity = entityManager.CreateEntity(
                typeof(TerrainChunk),
                typeof(TerrainChunkGridInfo),
                typeof(TerrainChunkBounds),
                typeof(TerrainChunkNeedsDensityRebuild),
                typeof(LocalTransform),
                typeof(PhysicsWorldIndex));

            entityManager.SetComponentData(entity, new TerrainChunk { ChunkCoord = int3.zero });
            entityManager.SetComponentData(entity, TerrainChunkGridInfo.Create(resolution, voxelSize));
            entityManager.SetComponentData(entity, new TerrainChunkBounds { WorldOrigin = worldOrigin });
            entityManager.SetComponentData(entity, LocalTransform.FromPosition(worldOrigin));
            return entity;
        }

        private bool HasPhysicsWorldSingleton()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            return !query.IsEmpty;
        }

        private bool TickUntil(int maxFrames, System.Func<bool> condition)
        {
            for (int i = 0; i < maxFrames; i++)
            {
                TickWorldOnce();
                if (condition())
                {
                    return true;
                }
            }

            return false;
        }

        private void TickWorldOnce()
        {
            elapsedTime += FixedDeltaTime;
            testWorld.SetTime(new TimeData(elapsedTime, FixedDeltaTime));
            initGroup.Update();
            simulationGroup.Update();
            fixedStepGroup.Update();
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

        private struct MovementCommandSample : IComponentData
        {
            public float LastLinearX;
            public int CaptureCount;
        }

        [DisableAutoCreation]
        [UpdateInGroup(typeof(PhysicsSystemGroup))]
        [UpdateAfter(typeof(PlayerMovementSystem))]
        [UpdateBefore(typeof(PhysicsSimulationGroup))]
        private partial struct CapturePostMovementVelocitySystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate<MovementCommandSample>();
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach (var (velocity, sample) in SystemAPI.Query<RefRO<PhysicsVelocity>, RefRW<MovementCommandSample>>())
                {
                    sample.ValueRW.LastLinearX = velocity.ValueRO.Linear.x;
                    sample.ValueRW.CaptureCount++;
                }
            }
        }
    }
}
