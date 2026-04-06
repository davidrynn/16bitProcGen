using System.Collections;
using DOTS.Player.Components;
using DOTS.Terrain.Core;
using DOTS.Terrain.Streaming;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UDebug = UnityEngine.Debug;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Scene-level repro harness for validating player collision against a vertical SDF terrain wall.
    /// Use in Play Mode with a visible terrain scene (for example: Basic Terrain Scene).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainWallReproBootstrap : MonoBehaviour
    {
        [Header("Activation")]
        [SerializeField] private bool applyScenarioOnStart = true;
        [SerializeField] private bool disableStreamingDuringRepro = true;
        [SerializeField] private int maxWaitFrames = 600;

        [Header("Terrain Shape")]
        [SerializeField] private bool forceFlatGroundAtY0 = true;
        [SerializeField] private float baseHeight = 0f;
        [SerializeField] private float3 wallCenter = new float3(8f, 4f, 0f);
        [SerializeField] private float3 wallHalfExtents = new float3(0.6f, 4f, 10f);

        [Header("Player Repro Drive")]
        [SerializeField] private bool repositionPlayer = true;
        [SerializeField] private float3 playerStartPosition = new float3(2f, 3f, 0f);
        [SerializeField] private float playerYawDegrees = 0f;
        [SerializeField] private bool autoDriveIntoWall = true;
        [SerializeField] private float2 driveInput = new float2(1f, 0f);
        [SerializeField] private float autoDriveSeconds = 8f;

        [Header("Crossing Detection")]
        [SerializeField] private float playerRadius = 0.5f;
        [SerializeField] private float crossingMargin = 0.05f;
        [SerializeField] private float wallProbeHeight = 2f;
        [SerializeField] private float periodicLogInterval = 1f;

        [Header("Debug Flags")]
        [SerializeField] private bool enableFallThroughDebugLogs = true;
        [SerializeField] private bool enableColliderPipelineDebugLogs = true;

        private World world;
        private EntityManager entityManager;
        private Entity playerEntity;
        private Entity editBufferEntity;
        private bool scenarioApplied;
        private bool crossingDetected;
        private float remainingDriveTime;
        private float nextLogTime;

        private float CrossingThresholdX => wallCenter.x + wallHalfExtents.x + playerRadius + crossingMargin;

        private void Start()
        {
            if (!applyScenarioOnStart)
            {
                return;
            }

            StartCoroutine(ApplyWhenReady());
        }

        private IEnumerator ApplyWhenReady()
        {
            for (int frame = 0; frame < math.max(1, maxWaitFrames); frame++)
            {
                if (TryResolveWorld() && TryFindPlayer(out playerEntity) && HasAnyTerrainChunk())
                {
                    ApplyScenario();
                    yield break;
                }

                yield return null;
            }

            UDebug.LogError("[TerrainWallRepro] Timed out waiting for world/player/chunks.");
        }

        private void Update()
        {
            if (!scenarioApplied)
            {
                return;
            }

            if (!TryResolveWorld() || !entityManager.Exists(playerEntity))
            {
                return;
            }

            if (autoDriveIntoWall && remainingDriveTime > 0f)
            {
                ApplyDriveInput(playerEntity, driveInput);
                remainingDriveTime -= Time.deltaTime;
            }

            if (entityManager.HasComponent<LocalTransform>(playerEntity))
            {
                var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
                var movementMode = entityManager.HasComponent<PlayerMovementState>(playerEntity)
                    ? entityManager.GetComponentData<PlayerMovementState>(playerEntity).Mode
                    : PlayerMovementMode.Ground;

                if (!crossingDetected && playerTransform.Position.x > CrossingThresholdX)
                {
                    crossingDetected = true;
                    UDebug.LogError(
                        $"[TerrainWallRepro] CROSSING DETECTED. playerX={playerTransform.Position.x:F3} threshold={CrossingThresholdX:F3} " +
                        $"wallCenterX={wallCenter.x:F3} wallHalfX={wallHalfExtents.x:F3} mode={movementMode}");
                }
            }

            if (Time.time >= nextLogTime)
            {
                EmitPeriodicStatus();
                nextLogTime = Time.time + math.max(0.1f, periodicLogInterval);
            }
        }

        private bool TryResolveWorld()
        {
            world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                return false;
            }

            entityManager = world.EntityManager;
            return true;
        }

        private bool HasAnyTerrainChunk()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunk>());
            return !query.IsEmptyIgnoreFilter;
        }

        private bool TryFindPlayer(out Entity entity)
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());

            if (query.IsEmptyIgnoreFilter)
            {
                entity = Entity.Null;
                return false;
            }

            entity = query.GetSingletonEntity();
            return true;
        }

        private void ApplyScenario()
        {
            if (enableFallThroughDebugLogs)
            {
                DebugSettings.EnableFallThroughDebug = true;
            }

            if (enableColliderPipelineDebugLogs)
            {
                DebugSettings.EnableTerrainColliderPipelineDebug = true;
            }

            if (disableStreamingDuringRepro)
            {
                TryDisableStreaming();
            }

            EnsureFieldSettings();
            EnsureColliderSettings();
            EnsureEditBuffer();
            ApplyVerticalWallEdit();
            MarkAllChunksForDensityRebuild();

            if (repositionPlayer && entityManager.Exists(playerEntity))
            {
                RepositionAndResetPlayer(playerEntity);
            }

            remainingDriveTime = math.max(0f, autoDriveSeconds);
            nextLogTime = Time.time;
            scenarioApplied = true;

            UDebug.Log(
                $"[TerrainWallRepro] Scenario applied. floorY={baseHeight:F2}, wallCenter={wallCenter}, wallHalfExtents={wallHalfExtents}, " +
                $"crossThresholdX={CrossingThresholdX:F3}, autoDriveSeconds={remainingDriveTime:F2}");
        }

        private void TryDisableStreaming()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<ProjectFeatureConfigSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var singletonEntity = query.GetSingletonEntity();
            var config = entityManager.GetComponentData<ProjectFeatureConfigSingleton>(singletonEntity);
            config.TerrainStreamingRadiusInChunks = 0;
            entityManager.SetComponentData(singletonEntity, config);
        }

        private void EnsureFieldSettings()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(SDFTerrainFieldSettings));
                entityManager.SetComponentData(entity, BuildFieldSettings());
                return;
            }

            var singletonEntity = query.GetSingletonEntity();
            entityManager.SetComponentData(singletonEntity, BuildFieldSettings());
        }

        private SDFTerrainFieldSettings BuildFieldSettings()
        {
            if (forceFlatGroundAtY0)
            {
                return new SDFTerrainFieldSettings
                {
                    BaseHeight = baseHeight,
                    Amplitude = 0f,
                    Frequency = 0f,
                    NoiseValue = 0f
                };
            }

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFTerrainFieldSettings>());
            if (query.IsEmptyIgnoreFilter)
            {
                return new SDFTerrainFieldSettings
                {
                    BaseHeight = baseHeight,
                    Amplitude = 0f,
                    Frequency = 0f,
                    NoiseValue = 0f
                };
            }

            return entityManager.GetComponentData<SDFTerrainFieldSettings>(query.GetSingletonEntity());
        }

        private void EnsureColliderSettings()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainColliderSettings>());
            if (query.IsEmptyIgnoreFilter)
            {
                var entity = entityManager.CreateEntity(typeof(TerrainColliderSettings));
                entityManager.SetComponentData(entity, new TerrainColliderSettings
                {
                    Enabled = true,
                    MaxCollidersPerFrame = 32,
                    EnableDetailedStaticMeshCollision = true
                });
                return;
            }

            var singletonEntity = query.GetSingletonEntity();
            entityManager.SetComponentData(singletonEntity, new TerrainColliderSettings
            {
                Enabled = true,
                MaxCollidersPerFrame = 32,
                EnableDetailedStaticMeshCollision = true
            });
        }

        private void EnsureEditBuffer()
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<SDFEdit>());
            if (query.IsEmptyIgnoreFilter)
            {
                editBufferEntity = entityManager.CreateEntity();
                entityManager.AddBuffer<SDFEdit>(editBufferEntity);
                return;
            }

            editBufferEntity = query.GetSingletonEntity();
        }

        private void ApplyVerticalWallEdit()
        {
            var edits = entityManager.GetBuffer<SDFEdit>(editBufferEntity);
            edits.Clear();
            edits.Add(SDFEdit.CreateBox(wallCenter, wallHalfExtents, SDFEditOperation.Add));
        }

        private void MarkAllChunksForDensityRebuild()
        {
            using var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkGridInfo>(),
                ComponentType.ReadOnly<TerrainChunkBounds>());
            using var chunks = query.ToEntityArray(Allocator.Temp);

            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk = chunks[i];
                if (!entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(chunk))
                {
                    entityManager.AddComponent<TerrainChunkNeedsDensityRebuild>(chunk);
                }
            }
        }

        private void RepositionAndResetPlayer(Entity player)
        {
            entityManager.SetComponentData(player, LocalTransform.FromPosition(playerStartPosition));

            if (entityManager.HasComponent<PlayerViewComponent>(player))
            {
                var view = entityManager.GetComponentData<PlayerViewComponent>(player);
                view.YawDegrees = playerYawDegrees;
                view.PitchDegrees = 0f;
                entityManager.SetComponentData(player, view);
            }

            if (entityManager.HasComponent<PlayerMovementState>(player))
            {
                var movementState = entityManager.GetComponentData<PlayerMovementState>(player);
                movementState.PreviousPosition = playerStartPosition;
                movementState.IsGrounded = false;
                movementState.FallTime = 0f;
                entityManager.SetComponentData(player, movementState);
            }

            if (entityManager.HasComponent<PhysicsVelocity>(player))
            {
                entityManager.SetComponentData(player, new PhysicsVelocity
                {
                    Linear = float3.zero,
                    Angular = float3.zero
                });
            }

            ApplyDriveInput(player, autoDriveIntoWall ? driveInput : float2.zero);
        }

        private void ApplyDriveInput(Entity player, float2 moveInput)
        {
            if (!entityManager.HasComponent<PlayerInputComponent>(player))
            {
                return;
            }

            var input = entityManager.GetComponentData<PlayerInputComponent>(player);
            input.Move = moveInput;
            input.JumpPressed = false;
            entityManager.SetComponentData(player, input);
        }

        private void EmitPeriodicStatus()
        {
            if (!entityManager.Exists(playerEntity) || !entityManager.HasComponent<LocalTransform>(playerEntity))
            {
                return;
            }

            var transform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var position = transform.Position;
            var velocity = entityManager.HasComponent<PhysicsVelocity>(playerEntity)
                ? entityManager.GetComponentData<PhysicsVelocity>(playerEntity).Linear
                : float3.zero;

            var grounded = entityManager.HasComponent<PlayerMovementState>(playerEntity) &&
                           entityManager.GetComponentData<PlayerMovementState>(playerEntity).IsGrounded;
            var movementMode = entityManager.HasComponent<PlayerMovementState>(playerEntity)
                ? entityManager.GetComponentData<PlayerMovementState>(playerEntity).Mode
                : PlayerMovementMode.Ground;

            ProbeWallFaces(position.z, out var hitFromLeft, out var hitFromRight, out var leftHitPosition, out var rightHitPosition);

            UDebug.Log(
                $"[TerrainWallRepro] pos={position} vel={velocity} grounded={grounded} mode={movementMode} " +
                $"wallHitL={hitFromLeft}@{leftHitPosition} wallHitR={hitFromRight}@{rightHitPosition} crossed={crossingDetected}");
        }

        private void ProbeWallFaces(float sampleZ, out bool hitFromLeft, out bool hitFromRight, out float3 leftHitPosition, out float3 rightHitPosition)
        {
            hitFromLeft = false;
            hitFromRight = false;
            leftHitPosition = float3.zero;
            rightHitPosition = float3.zero;

            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<PhysicsWorldSingleton>());
            if (query.IsEmptyIgnoreFilter)
            {
                return;
            }

            var worldSingleton = query.GetSingleton<PhysicsWorldSingleton>();

            var leftToRight = new RaycastInput
            {
                Start = new float3(wallCenter.x - 8f, wallProbeHeight, sampleZ),
                End = new float3(wallCenter.x + 8f, wallProbeHeight, sampleZ),
                Filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = 2u, GroupIndex = 0 }
            };

            var rightToLeft = new RaycastInput
            {
                Start = new float3(wallCenter.x + 8f, wallProbeHeight, sampleZ),
                End = new float3(wallCenter.x - 8f, wallProbeHeight, sampleZ),
                Filter = new CollisionFilter { BelongsTo = uint.MaxValue, CollidesWith = 2u, GroupIndex = 0 }
            };

            if (worldSingleton.PhysicsWorld.CastRay(leftToRight, out var leftHit))
            {
                hitFromLeft = true;
                leftHitPosition = leftHit.Position;
            }

            if (worldSingleton.PhysicsWorld.CastRay(rightToLeft, out var rightHit))
            {
                hitFromRight = true;
                rightHitPosition = rightHit.Position;
            }
        }
    }
}





