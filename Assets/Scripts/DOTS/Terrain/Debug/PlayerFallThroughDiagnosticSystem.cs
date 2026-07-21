using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using DOTS.Player.Components;
using DOTS.Core;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Diagnostic system that tracks player position, grounding state, and surrounding chunk collider
    /// readiness to help identify the root cause of terrain fall-through issues.
    /// Logs detailed snapshots when unexpected ungrounding or below-surface positions are detected.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct PlayerFallThroughDiagnosticSystem : ISystem
    {
        private bool _prevIsGrounded;
        private int _framesSinceLastSnapshot;
        // Separate counter: the tunneling check shares no state with the snapshot trigger, and
        // gating it on _framesSinceLastSnapshot (which it never reset) made it log every single
        // frame once the cooldown elapsed — unusable during exactly the sustained high-speed
        // traverse it exists to catch.
        private int _framesSinceLastTunnelWarning;
        private const int SnapshotCooldownFrames = 30;

        // Penetration (in voxels) before BELOW_SURFACE counts as anomalous.
        //
        // Cannot be 0: the player's transform origin IS the capsule's base (Vertex0 = (0,0.5,0),
        // radius 0.5), so simply standing puts the sample point on the contact surface, where
        // Surface Nets' approximation of the analytical zero-crossing reads slightly negative.
        // At 0 this fired ~every cooldown while the player stood still, and because it shares
        // the cooldown with UNEXPECTED_UNGROUNDING it would mask real events.
        private const float BelowSurfaceVoxelTolerance = 1.0f;

        // Step displacement, in voxels, before tunneling is genuinely at risk.
        //
        // Deliberately NOT 1.0. Unity Physics expands the broadphase AABB by the full step
        // displacement (Motion.cs: Linear = LinearVelocity * timeStep), so the pair is still found
        // and speculative contacts resolve it against a shell that has real thickness — exceeding
        // one voxel per step is a yellow flag, not a breach. At 1.0 this fired on ROUTINE movement
        // (a single un-chained launch is 55 m/s = 0.92 m/step; the sky-drop reaches 85 m/s), so the
        // warning was permanently on and therefore carried no information.
        private const float TunnelWarnVoxelMultiple = 2.0f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsWorldSingleton>();
            state.RequireForUpdate<PlayerMovementState>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (!DebugSettings.EnableFallThroughDebug)
                return;

            _framesSinceLastSnapshot++;
            _framesSinceLastTunnelWarning++;

            var entityManager = state.EntityManager;

            // Tunneling is a PHYSICS-step phenomenon, so it must be measured against the fixed
            // step, not the variable frame delta this system sees in SimulationSystemGroup.
            var fixedStep = 1f / 60f;
            var fixedGroup = state.World.GetExistingSystemManaged<FixedStepSimulationSystemGroup>();
            if (fixedGroup != null && fixedGroup.RateManager != null)
                fixedStep = fixedGroup.RateManager.Timestep;

            // Get player state
            float3 playerPos = float3.zero;
            float3 velocityLinear = float3.zero;
            bool isGrounded = false;
            float fallTime = 0f;
            bool hasPlayer = false;

            foreach (var (transform, velocity, movementState) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<PlayerMovementState>>()
                         .WithAll<PlayerTag>())
            {
                playerPos = transform.ValueRO.Position;
                velocityLinear = velocity.ValueRO.Linear;
                isGrounded = movementState.ValueRO.IsGrounded;
                fallTime = movementState.ValueRO.FallTime;
                hasPlayer = true;
                break;
            }

            var velocityY = velocityLinear.y;
            var speed = math.length(velocityLinear);

            if (!hasPlayer)
                return;

            // Build chunk coord->entity map from existing chunks
            var chunkQuery = SystemAPI.QueryBuilder()
                .WithAll<TerrainChunk, TerrainChunkGridInfo, TerrainChunkBounds>()
                .Build();

            if (chunkQuery.IsEmpty)
            {
                _prevIsGrounded = isGrounded;
                return;
            }

            using var chunkEntities = chunkQuery.ToEntityArray(Allocator.Temp);
            using var chunkComponents = chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            using var gridInfos = chunkQuery.ToComponentDataArray<TerrainChunkGridInfo>(Allocator.Temp);

            // Stride is safe to read off any chunk: TerrainLodSettings keeps the world footprint
            // invariant across LODs ((res-1) * voxel = 15), so every chunk agrees regardless of its
            // current LOD.
            var gridInfo = gridInfos[0];
            var chunkStride = math.max(0, gridInfo.Resolution.x - 1) * gridInfo.VoxelSize;
            if (chunkStride <= 0f)
            {
                _prevIsGrounded = isGrounded;
                return;
            }

            var playerChunkCoord = new int2(
                (int)math.floor(playerPos.x / chunkStride),
                (int)math.floor(playerPos.z / chunkStride));

            // Voxel size, unlike stride, DOES vary by LOD (1.0 / 1.875 / 3.75), so it must come from
            // the chunk the player is actually in. Reading gridInfos[0] compared the tunneling
            // threshold against an arbitrary chunk's LOD, which made `vs voxel=` in the logs
            // meaningless (observed 2026-07-21: a LOD1 voxel reported while the player stood on LOD0).
            var voxelSize = gridInfo.VoxelSize;
            for (int i = 0; i < chunkComponents.Length; i++)
            {
                if (chunkComponents[i].ChunkCoord.x == playerChunkCoord.x &&
                    chunkComponents[i].ChunkCoord.z == playerChunkCoord.y)
                {
                    voxelSize = gridInfos[i].VoxelSize;
                    break;
                }
            }

            // Check trigger conditions
            bool unexpectedUngrounding = _prevIsGrounded && !isGrounded && velocityY <= 0f;

            // Check if player is below the terrain surface.
            //
            // Built through SDFTerrainField rather than calling an SDFMath function directly: this
            // system previously called SdGround (the legacy sine field) while the density sampler
            // had moved to SdLayeredGround, so BELOW_SURFACE was tested against a surface that was
            // never meshed — false positives AND false negatives. Mirroring how
            // TerrainChunkDensitySamplingSystem builds its field keeps the two from drifting again,
            // and passing the edit buffer means dug-out terrain doesn't read as solid ground.
            bool belowSurface = false;
            float signedDistance = float.NaN;   // logged: how deep, not just whether
            if (SystemAPI.TryGetSingleton<SDFTerrainFieldSettings>(out var fieldSettings))
            {
                var field = new SDFTerrainField
                {
                    BaseHeight = fieldSettings.BaseHeight,
                    Amplitude  = fieldSettings.Amplitude,
                    Frequency  = fieldSettings.Frequency,
                    NoiseValue = fieldSettings.NoiseValue
                };

                if (SystemAPI.HasSingleton<TerrainFieldSettings>() && SystemAPI.HasSingleton<TerrainGenerationContext>())
                {
                    field.LayeredSettings = SystemAPI.GetSingleton<TerrainFieldSettings>();
                    field.WorldSeed       = SystemAPI.GetSingleton<TerrainGenerationContext>().WorldSeed;
                    field.UseLayeredNoise = true;
                }

                var edits = SystemAPI.TryGetSingletonBuffer<SDFEdit>(out var editBuffer)
                    ? editBuffer.AsNativeArray()
                    : default;

                signedDistance = field.Sample(playerPos, edits);
                belowSurface = signedDistance < -BelowSurfaceVoxelTolerance * voxelSize;
            }

            // Tunneling risk check.
            //
            // Measured on the FULL velocity magnitude, not just the vertical component: a slingshot
            // arc near apex has velocity.y ~ 0 with hundreds of m/s horizontal, so the old
            // vertical-only test stayed silent on precisely the case that tunnels.
            var stepDistance = speed * fixedStep;
            var tunnelThreshold = voxelSize * TunnelWarnVoxelMultiple;
            if (stepDistance > tunnelThreshold && _framesSinceLastTunnelWarning >= SnapshotCooldownFrames)
            {
                _framesSinceLastTunnelWarning = 0;
                DebugSettings.LogFallThroughWarning(
                    $"TUNNELING RISK: speed*fixedStep={stepDistance:F3} > {TunnelWarnVoxelMultiple:F1}x voxel={tunnelThreshold:F2}. " +
                    $"speed={speed:F1} (h={math.length(velocityLinear.xz):F1}, y={velocityY:F1}), " +
                    $"fixedStep={fixedStep:F4}, pos={playerPos}");
            }

            // Log detailed snapshot on trigger
            if ((unexpectedUngrounding || belowSurface) && _framesSinceLastSnapshot >= SnapshotCooldownFrames)
            {
                _framesSinceLastSnapshot = 0;

                var map = new NativeParallelHashMap<int2, Entity>(chunkEntities.Length, Allocator.Temp);
                for (int i = 0; i < chunkEntities.Length; i++)
                {
                    var coord = new int2(chunkComponents[i].ChunkCoord.x, chunkComponents[i].ChunkCoord.z);
                    map.TryAdd(coord, chunkEntities[i]);
                }

                var trigger = unexpectedUngrounding ? "UNEXPECTED_UNGROUNDING" : "BELOW_SURFACE";
                DebugSettings.LogFallThroughWarning(
                    $"--- {trigger} SNAPSHOT ---\n" +
                    $"  Player pos={playerPos}, speed={speed:F1} (h={math.length(velocityLinear.xz):F1}, y={velocityY:F1}), " +
                    $"stepDist={speed * fixedStep:F2} vs voxel={voxelSize:F2}, " +
                    $"IsGrounded={isGrounded}, FallTime={fallTime:F3}\n" +
                    $"  Player chunk=({playerChunkCoord.x},{playerChunkCoord.y}), sdf={signedDistance:F2}\n" +
                    BuildChunkGridStatus(entityManager, map, playerChunkCoord));

                map.Dispose();
            }

            _prevIsGrounded = isGrounded;
        }

        private static string BuildChunkGridStatus(EntityManager entityManager, NativeParallelHashMap<int2, Entity> map, int2 center)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("  3x3 Chunk Grid Status:");

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    var coord = new int2(center.x + dx, center.y + dz);
                    var label = (dx == 0 && dz == 0) ? " [PLAYER]" : "";

                    if (map.TryGetValue(coord, out var entity))
                    {
                        var hasCollider = entityManager.HasComponent<PhysicsCollider>(entity);
                        var needsCollider = entityManager.HasComponent<TerrainChunkNeedsColliderBuild>(entity);
                        var hasMeshData = entityManager.HasComponent<TerrainChunkMeshData>(entity);
                        var needsDensity = entityManager.HasComponent<TerrainChunkNeedsDensityRebuild>(entity);

                        sb.AppendLine(
                            $"    ({coord.x},{coord.y}){label}: " +
                            $"Collider={hasCollider}, NeedsCollider={needsCollider}, " +
                            $"MeshData={hasMeshData}, NeedsDensity={needsDensity}");
                    }
                    else
                    {
                        sb.AppendLine($"    ({coord.x},{coord.y}){label}: NOT LOADED");
                    }
                }
            }

            return sb.ToString();
        }
    }
}
