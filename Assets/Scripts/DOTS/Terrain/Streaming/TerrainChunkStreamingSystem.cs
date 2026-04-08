using DOTS.Player.Components;
using DOTS.Terrain.Core;
using DOTS.Terrain.LOD;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

namespace DOTS.Terrain.Streaming
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct TerrainChunkStreamingSystem : ISystem
    {
        private EntityQuery playerQuery;
        private EntityQuery chunkQuery;

        public void OnCreate(ref SystemState state)
        {
            playerQuery = state.GetEntityQuery(ComponentType.ReadOnly<PlayerTag>(), ComponentType.ReadOnly<LocalTransform>());
            chunkQuery = state.GetEntityQuery(ComponentType.ReadOnly<TerrainChunk>(), ComponentType.ReadOnly<TerrainChunkBounds>(), ComponentType.ReadOnly<TerrainChunkGridInfo>());

            // Don't require player unconditionally - debug-freeze mode doesn't need it
        }

        public void OnUpdate(ref SystemState state)
        {
            // Check for debug config first
            var debugEnabled = false;
            var debugConfig = DOTS.Terrain.Debug.TerrainDebugConfig.Default;
            if (SystemAPI.TryGetSingleton<DOTS.Terrain.Debug.TerrainDebugConfig>(out var cfg))
            {
                debugConfig = cfg;
                debugEnabled = cfg.Enabled;
            }

            var radius = 2;
            if (SystemAPI.TryGetSingleton<ProjectFeatureConfigSingleton>(out var config))
            {
                radius = math.max(0, config.TerrainStreamingRadiusInChunks);
            }

            if (radius == 0)
            {
                return;
            }

            var entityManager = state.EntityManager;

            var elapsedTime = SystemAPI.Time.ElapsedTime;
            var frameCount = UnityEngine.Time.frameCount;

            // If debug freeze streaming is active, skip normal player-based streaming
            if (debugEnabled && debugConfig.FreezeStreaming)
            {
                // Use fixed center chunk from debug config
                ProcessStreamingWindow(ref state, debugConfig.FixedCenterChunk, debugConfig.StreamingRadiusInChunks, debugEnabled, elapsedTime, frameCount);
                return;
            }

            if (playerQuery.IsEmpty)
            {
                return;
            }

            var playerEntity = playerQuery.GetSingletonEntity();
            var playerTransform = entityManager.GetComponentData<LocalTransform>(playerEntity);
            var playerPos = playerTransform.Position;

            if (chunkQuery.IsEmpty)
            {
                return;
            }

            using var existingEntities = chunkQuery.ToEntityArray(Allocator.Temp);
            using var existingChunks = chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);

            // Always derive stride from LOD0 settings so centerCoord is consistent regardless of
            // which chunk entity happens to be first in the query (it may be a demoted LOD1/2 chunk).
            var lod0Resolution = entityManager.GetComponentData<TerrainChunkGridInfo>(existingEntities[0]).Resolution;
            var lod0VoxelSize  = entityManager.GetComponentData<TerrainChunkGridInfo>(existingEntities[0]).VoxelSize;
            if (SystemAPI.TryGetSingleton<TerrainLodSettings>(out var lodSettingsForStride))
            {
                lod0Resolution = lodSettingsForStride.Lod0Resolution;
                lod0VoxelSize  = lodSettingsForStride.Lod0VoxelSize;
            }

            var chunkStride = math.max(0, lod0Resolution.x - 1) * lod0VoxelSize;
            if (chunkStride <= 0f)
            {
                return;
            }

            // Center chunk volume vertically around BaseHeight so the SdGround isosurface stays in-range.
            var baseHeight = 0f;
            if (SystemAPI.TryGetSingleton<SDFTerrainFieldSettings>(out var fieldSettings))
            {
                baseHeight = fieldSettings.BaseHeight;
            }

            var centerCoord = new int2(
                (int)math.floor(playerPos.x / chunkStride),
                (int)math.floor(playerPos.z / chunkStride));

            ProcessStreamingWindow(ref state, centerCoord, radius, debugEnabled, elapsedTime, frameCount);
        }

        private void ProcessStreamingWindow(ref SystemState state, int2 centerCoord, int radius, bool debugEnabled, double elapsedTime, int frameCount)
        {
            var entityManager = state.EntityManager;

            if (chunkQuery.IsEmpty)
            {
                return;
            }

            using var existingEntities = chunkQuery.ToEntityArray(Allocator.Temp);
            using var existingChunks = chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);

            // Always use LOD0 settings for chunk stride, origin, and GridInfo on new chunks.
            // Reading from an arbitrary existing chunk risks picking a demoted LOD1/2 entity,
            // which produces wrong stride and resolution for all newly spawned chunks.
            var lod0Resolution = entityManager.GetComponentData<TerrainChunkGridInfo>(existingEntities[0]).Resolution;
            var lod0VoxelSize  = entityManager.GetComponentData<TerrainChunkGridInfo>(existingEntities[0]).VoxelSize;
            if (SystemAPI.TryGetSingleton<TerrainLodSettings>(out var lodSettings))
            {
                lod0Resolution = lodSettings.Lod0Resolution;
                lod0VoxelSize  = lodSettings.Lod0VoxelSize;
            }

            var chunkStride = math.max(0, lod0Resolution.x - 1) * lod0VoxelSize;
            if (chunkStride <= 0f)
            {
                return;
            }

            // Center chunk volume vertically around BaseHeight so the SdGround isosurface stays in-range.
            var baseHeight = 0f;
            if (SystemAPI.TryGetSingleton<SDFTerrainFieldSettings>(out var fieldSettings))
            {
                baseHeight = fieldSettings.BaseHeight;
            }

            var chunkVerticalSpan = math.max(0, lod0Resolution.y - 1) * lod0VoxelSize;
            var originY = baseHeight - (chunkVerticalSpan * 0.5f);

            var map = new NativeParallelHashMap<int2, Entity>(existingEntities.Length, Allocator.Temp);
            for (int i = 0; i < existingEntities.Length; i++)
            {
                var chunk = existingChunks[i];
                map.TryAdd(new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z), existingEntities[i]);
            }

            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Spawn missing chunks in the streaming window.
            for (int dz = -radius; dz <= radius; dz++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    var coord = new int2(centerCoord.x + dx, centerCoord.y + dz);
                    if (map.ContainsKey(coord))
                    {
                        continue;
                    }

                    var entity = ecb.CreateEntity();
                    ecb.AddComponent(entity, new TerrainChunk { ChunkCoord = new int3(coord.x, 0, coord.y) });
                    ecb.AddComponent(entity, TerrainChunkGridInfo.Create(lod0Resolution, lod0VoxelSize));
                    ecb.AddComponent(entity, new TerrainChunkLodState { CurrentLod = 0, TargetLod = 0, LastSwitchFrame = 0 });

                    var origin = new float3(coord.x * chunkStride, originY, coord.y * chunkStride);
                    ecb.AddComponent(entity, new TerrainChunkBounds { WorldOrigin = origin });
                    ecb.AddComponent<TerrainChunkNeedsDensityRebuild>(entity);
                    ecb.AddComponent(entity, LocalTransform.FromPosition(origin));

                    // Add debug state if debug enabled
                    if (debugEnabled)
                    {
                        ecb.AddComponent(entity, DOTS.Terrain.Debug.TerrainChunkDebugState.Create(coord));
                    }

                    // Add spawn timestamp for collider timing diagnostics
                    if (DebugSettings.EnableFallThroughDebug)
                    {
                        ecb.AddComponent(entity, new DOTS.Terrain.Debug.TerrainChunkSpawnTimestamp
                        {
                            SpawnElapsedTime = elapsedTime,
                            SpawnFrameCount = frameCount
                        });
                    }
                }
            }

            // Despawn chunks outside the window (and dispose blobs/meshes to avoid leaks).
            for (int i = 0; i < existingEntities.Length; i++)
            {
                var entity = existingEntities[i];
                var chunk = existingChunks[i];
                var coord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);

                if (math.abs(coord.x - centerCoord.x) <= radius && math.abs(coord.y - centerCoord.y) <= radius)
                {
                    continue;
                }

                if (entityManager.HasComponent<TerrainChunkDensity>(entity))
                {
                    var density = entityManager.GetComponentData<TerrainChunkDensity>(entity);
                    density.Dispose();
                }

                if (entityManager.HasComponent<TerrainChunkMeshData>(entity))
                {
                    var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                    meshData.Dispose();
                }

                if (entityManager.HasComponent<TerrainChunkColliderData>(entity))
                {
                    // Do not dispose collider blobs inline during SimulationSystemGroup.
                    // Physics systems may still read the previous world this frame.
                    // Remove the component and let collider lifecycle systems own disposal timing.
                    ecb.RemoveComponent<TerrainChunkColliderData>(entity);
                }

                // Remove PhysicsCollider before entity destruction so the physics broadphase
                // doesn't reference a disposed blob between ECB playback and world rebuild.
                if (entityManager.HasComponent<PhysicsCollider>(entity))
                {
                    ecb.RemoveComponent<PhysicsCollider>(entity);
                }

                if (entityManager.HasComponent<Mesh>(entity))
                {
                    var mesh = entityManager.GetComponentObject<Mesh>(entity);
                    if (mesh != null)
                    {
                        Object.Destroy(mesh);
                    }
                }

                ecb.DestroyEntity(entity);
            }

            ecb.Playback(entityManager);
            ecb.Dispose();
            map.Dispose();
        }
    }

    /// <summary>
    /// Lightweight singleton mirror of ProjectFeatureConfig values needed by unmanaged systems.
    /// Created by DotsSystemBootstrap.
    /// </summary>
    public struct ProjectFeatureConfigSingleton : IComponentData
    {
        public int TerrainStreamingRadiusInChunks;
        public float CameraFarClipPlane;
        public bool TerrainStreamingEnabled;
    }
}
