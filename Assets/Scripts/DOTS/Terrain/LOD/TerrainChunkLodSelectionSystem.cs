using DOTS.Player.Components;
using DOTS.Terrain.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Terrain.LOD
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(Streaming.TerrainChunkStreamingSystem))]
    public partial struct TerrainChunkLodSelectionSystem : ISystem
    {
        private EntityQuery _playerQuery;
        private EntityQuery _chunkQuery;
        private bool _hasLoggedFootprintWarning;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainLodSettings>();
            _playerQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<PlayerTag>(),
                ComponentType.ReadOnly<LocalTransform>());
            _chunkQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<TerrainChunk>(),
                ComponentType.ReadOnly<TerrainChunkGridInfo>(),
                ComponentType.ReadWrite<TerrainChunkLodState>());
            _hasLoggedFootprintWarning = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_playerQuery.IsEmpty || _chunkQuery.IsEmpty)
                return;

            var settings = SystemAPI.GetSingleton<TerrainLodSettings>();

            if (!_hasLoggedFootprintWarning && !settings.HasInvariantFootprint())
            {
                _hasLoggedFootprintWarning = true;
                DebugSettings.LogWarning(
                    "[DOTS-LOD] TerrainLodSettings footprint mismatch detected. " +
                    "Expected invariant chunk span across LODs for seam stability.");
            }

            var playerTransform = state.EntityManager.GetComponentData<LocalTransform>(
                _playerQuery.GetSingletonEntity());

            // Always derive stride from LOD0 settings so playerChunkCoord is consistent
            // regardless of which chunk entity lands at index 0 (may be LOD1 after first frame).
            var stride = math.max(0, settings.Lod0Resolution.x - 1) * settings.Lod0VoxelSize;
            if (stride <= 0f)
                return;

            var playerChunkCoord = new int2(
                (int)math.floor(playerTransform.Position.x / stride),
                (int)math.floor(playerTransform.Position.z / stride));

            using var entities = _chunkQuery.ToEntityArray(Allocator.Temp);
            using var chunks   = _chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);
            using var states   = _chunkQuery.ToComponentDataArray<TerrainChunkLodState>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var coord = new int2(chunks[i].ChunkCoord.x, chunks[i].ChunkCoord.z);
                float dist = math.max(
                    math.abs(coord.x - playerChunkCoord.x),
                    math.abs(coord.y - playerChunkCoord.y));

                var newTarget = ComputeTargetLod(dist, settings, states[i].TargetLod);
                if (newTarget == states[i].TargetLod)
                    continue;

                var updated = states[i];
                updated.TargetLod = newTarget;
                state.EntityManager.SetComponentData(entities[i], updated);

                DebugSettings.LogLod($"Chunk ({coord.x},{coord.y}) dist={dist:F1} target LOD {states[i].TargetLod}→{newTarget}");
            }
        }

        /// <summary>
        /// Pure LOD selection with hysteresis. Promotion is immediate; demotion requires
        /// crossing the ring boundary plus the hysteresis band.
        /// </summary>
        public static int ComputeTargetLod(float dist, in TerrainLodSettings settings, int currentTargetLod)
        {
            int rawTarget;
            if (dist <= settings.Lod0MaxDist) rawTarget = 0;
            else if (dist <= settings.Lod1MaxDist) rawTarget = 1;
            else if (dist <= settings.Lod2MaxDist) rawTarget = 2;
            else rawTarget = settings.UseStreamingAsCullBoundary ? 2 : 3;

            // Promotion (moving closer) — immediate.
            if (rawTarget < currentTargetLod)
                return rawTarget;

            // Demotion (moving away) — only past threshold + hysteresis.
            if (rawTarget > currentTargetLod)
            {
                float demotionThreshold = rawTarget switch
                {
                    1 => settings.Lod0MaxDist,
                    2 => settings.Lod1MaxDist,
                    _ => settings.Lod2MaxDist
                };

                if (dist > demotionThreshold + settings.HysteresisChunks)
                    return rawTarget;
            }

            return currentTargetLod;
        }
    }
}
