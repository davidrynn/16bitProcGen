using DOTS.Player.Components;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
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

            state.RequireForUpdate(playerQuery);
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

            // If debug freeze streaming is active, skip normal player-based streaming
            if (debugEnabled && debugConfig.FreezeStreaming)
            {
                // Use fixed center chunk from debug config
                ProcessStreamingWindow(ref state, debugConfig.FixedCenterChunk, debugConfig.StreamingRadiusInChunks, debugEnabled);
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

            // Infer chunk stride from the first existing chunk.
            var anyGridInfo = entityManager.GetComponentData<TerrainChunkGridInfo>(existingEntities[0]);
            // Chunks share border samples; the world-space span of a chunk is (resolution-1) * voxelSize.
            var chunkStride = math.max(0, anyGridInfo.Resolution.x - 1) * anyGridInfo.VoxelSize;
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

            var chunkVerticalSpan = math.max(0, anyGridInfo.Resolution.y - 1) * anyGridInfo.VoxelSize;
            var originY = baseHeight - (chunkVerticalSpan * 0.5f);

            var centerCoord = new int2(
                (int)math.floor(playerPos.x / chunkStride),
                (int)math.floor(playerPos.z / chunkStride));

            ProcessStreamingWindow(ref state, centerCoord, radius, debugEnabled);
        }

        private void ProcessStreamingWindow(ref SystemState state, int2 centerCoord, int radius, bool debugEnabled)
        {
            var entityManager = state.EntityManager;

            if (chunkQuery.IsEmpty)
            {
                return;
            }

            using var existingEntities = chunkQuery.ToEntityArray(Allocator.Temp);
            using var existingChunks = chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);

            // Infer chunk stride from the first existing chunk.
            var anyGridInfo = entityManager.GetComponentData<TerrainChunkGridInfo>(existingEntities[0]);
            // Chunks share border samples; the world-space span of a chunk is (resolution-1) * voxelSize.
            var chunkStride = math.max(0, anyGridInfo.Resolution.x - 1) * anyGridInfo.VoxelSize;
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

            var chunkVerticalSpan = math.max(0, anyGridInfo.Resolution.y - 1) * anyGridInfo.VoxelSize;
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
                    ecb.AddComponent(entity, TerrainChunkGridInfo.Create(anyGridInfo.Resolution, anyGridInfo.VoxelSize));

                    var origin = new float3(coord.x * chunkStride, originY, coord.y * chunkStride);
                    ecb.AddComponent(entity, new TerrainChunkBounds { WorldOrigin = origin });
                    ecb.AddComponent<TerrainChunkNeedsDensityRebuild>(entity);
                    ecb.AddComponent(entity, LocalTransform.FromPosition(origin));

                    // Add debug state if debug enabled
                    if (debugEnabled)
                    {
                        ecb.AddComponent(entity, DOTS.Terrain.Debug.TerrainChunkDebugState.Create(coord));
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
    }
}
