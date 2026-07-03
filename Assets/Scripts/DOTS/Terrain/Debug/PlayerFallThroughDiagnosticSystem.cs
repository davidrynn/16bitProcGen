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
        private const int SnapshotCooldownFrames = 30;

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

            var entityManager = state.EntityManager;
            var deltaTime = SystemAPI.Time.DeltaTime;

            // Get player state
            float3 playerPos = float3.zero;
            float velocityY = 0f;
            bool isGrounded = false;
            float fallTime = 0f;
            bool hasPlayer = false;

            foreach (var (transform, velocity, movementState) in
                     SystemAPI.Query<RefRO<LocalTransform>, RefRO<PhysicsVelocity>, RefRO<PlayerMovementState>>()
                         .WithAll<PlayerTag>())
            {
                playerPos = transform.ValueRO.Position;
                velocityY = velocity.ValueRO.Linear.y;
                isGrounded = movementState.ValueRO.IsGrounded;
                fallTime = movementState.ValueRO.FallTime;
                hasPlayer = true;
                break;
            }

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

            // Determine chunk stride from first chunk
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

            // Check trigger conditions
            bool unexpectedUngrounding = _prevIsGrounded && !isGrounded && velocityY <= 0f;

            // Check if player is below analytical terrain surface
            bool belowSurface = false;
            if (SystemAPI.TryGetSingleton<SDFTerrainFieldSettings>(out var fieldSettings))
            {
                var sd = SDFMath.SdGround(playerPos, fieldSettings.Amplitude, fieldSettings.Frequency,
                    fieldSettings.BaseHeight, fieldSettings.NoiseValue);
                belowSurface = sd < 0f;
            }

            // Tunneling risk check
            var voxelSize = gridInfo.VoxelSize;
            if (math.abs(velocityY) * deltaTime > voxelSize)
            {
                if (_framesSinceLastSnapshot >= SnapshotCooldownFrames)
                {
                    DebugSettings.LogFallThroughWarning(
                        $"TUNNELING RISK: |velocity.y|*dt={math.abs(velocityY) * deltaTime:F3} > voxelSize={voxelSize:F2}. " +
                        $"velocity.y={velocityY:F3}, pos={playerPos}");
                }
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
                    $"  Player pos={playerPos}, velocity.y={velocityY:F3}, IsGrounded={isGrounded}, FallTime={fallTime:F3}\n" +
                    $"  Player chunk=({playerChunkCoord.x},{playerChunkCoord.y})\n" +
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
