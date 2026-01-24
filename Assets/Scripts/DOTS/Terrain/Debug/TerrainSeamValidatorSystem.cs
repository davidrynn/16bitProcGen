using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Validates that adjacent chunks have matching density values at shared borders.
    /// Runs after density sampling to detect sampling/origin/off-by-one bugs.
    /// Only active when TerrainDebugConfig.Enabled is true.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkDensitySamplingSystem))]
    public partial struct TerrainSeamValidatorSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainDebugConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var debugConfig = SystemAPI.GetSingleton<TerrainDebugConfig>();
            if (!debugConfig.Enabled)
            {
                return;
            }

            var entityManager = state.EntityManager;
            var chunkQuery = SystemAPI.QueryBuilder()
                .WithAll<TerrainChunk, TerrainChunkDensity, TerrainChunkBounds>()
                .Build();

            if (chunkQuery.IsEmpty)
            {
                return;
            }

            using var chunkEntities = chunkQuery.ToEntityArray(Allocator.Temp);
            using var chunkComponents = chunkQuery.ToComponentDataArray<TerrainChunk>(Allocator.Temp);

            // Build coord -> entity map
            var map = new NativeParallelHashMap<int2, Entity>(chunkEntities.Length, Allocator.Temp);
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var coord = new int2(chunkComponents[i].ChunkCoord.x, chunkComponents[i].ChunkCoord.z);
                map.TryAdd(coord, chunkEntities[i]);
            }

            // For each chunk, validate borders with east and north neighbors
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var entity = chunkEntities[i];
                var chunk = chunkComponents[i];
                var coord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);

                if (!entityManager.HasComponent<TerrainChunkDensity>(entity))
                {
                    continue;
                }

                var density = entityManager.GetComponentData<TerrainChunkDensity>(entity);
                if (!density.IsCreated)
                {
                    continue;
                }

                // Check east neighbor (x+1)
                var eastCoord = new int2(coord.x + 1, coord.y);
                if (map.TryGetValue(eastCoord, out var eastEntity))
                {
                    ValidateBorder(ref state, entity, eastEntity, coord, eastCoord, BorderDirection.East, debugConfig);
                }

                // Check north neighbor (z+1)
                var northCoord = new int2(coord.x, coord.y + 1);
                if (map.TryGetValue(northCoord, out var northEntity))
                {
                    ValidateBorder(ref state, entity, northEntity, coord, northCoord, BorderDirection.North, debugConfig);
                }
            }

            map.Dispose();
        }

        private static void ValidateBorder(ref SystemState state, Entity entityA, Entity entityB,
            int2 coordA, int2 coordB, BorderDirection direction, TerrainDebugConfig config)
        {
            var entityManager = state.EntityManager;

            var densityA = entityManager.GetComponentData<TerrainChunkDensity>(entityA);
            var densityB = entityManager.GetComponentData<TerrainChunkDensity>(entityB);

            if (!densityA.IsCreated || !densityB.IsCreated)
            {
                return;
            }

            ref var blobA = ref densityA.Data.Value;
            ref var blobB = ref densityB.Data.Value;

            var resA = blobA.Resolution;
            var resB = blobB.Resolution;

            if (resA.x != resB.x || resA.y != resB.y || resA.z != resB.z)
            {
                if (config.EnableSeamLogging)
                {
                    DebugSettings.LogSeamWarning($"Resolution mismatch: A{coordA} res={resA} vs B{coordB} res={resB}");
                }
                return;
            }

            // For east border: compare x=max(A) with x=0(B)
            // For north border: compare z=max(A) with z=0(B)
            var maxAbsDelta = 0f;
            var countAboveEpsilon = 0;
            var sampleCount = 0;

            if (direction == BorderDirection.East)
            {
                // Compare rightmost column of A with leftmost column of B
                var xA = resA.x - 1;
                var xB = 0;

                for (int y = 0; y < resA.y; y++)
                {
                    for (int z = 0; z < resA.z; z++)
                    {
                        var valA = blobA.GetDensity(xA, y, z);
                        var valB = blobB.GetDensity(xB, y, z);
                        var delta = math.abs(valA - valB);

                        maxAbsDelta = math.max(maxAbsDelta, delta);
                        if (delta > config.SeamEpsilon)
                        {
                            countAboveEpsilon++;
                        }
                        sampleCount++;
                    }
                }
            }
            else // North
            {
                // Compare farthest z column of A with nearest z column of B
                var zA = resA.z - 1;
                var zB = 0;

                for (int y = 0; y < resA.y; y++)
                {
                    for (int x = 0; x < resA.x; x++)
                    {
                        var valA = blobA.GetDensity(x, y, zA);
                        var valB = blobB.GetDensity(x, y, zB);
                        var delta = math.abs(valA - valB);

                        maxAbsDelta = math.max(maxAbsDelta, delta);
                        if (delta > config.SeamEpsilon)
                        {
                            countAboveEpsilon++;
                        }
                        sampleCount++;
                    }
                }
            }

            if (countAboveEpsilon > 0 && config.EnableSeamLogging)
            {
                var dirStr = direction == BorderDirection.East ? "East" : "North";
                DebugSettings.LogSeamWarning($"SEAM_MISMATCH: A{coordA} ↔ B{coordB} ({dirStr}) maxΔ={maxAbsDelta:F6} samples={countAboveEpsilon}/{sampleCount} above ε={config.SeamEpsilon}");
            }
        }

        private enum BorderDirection
        {
            East,
            North
        }
    }
}
