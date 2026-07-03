using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Core;
using DOTS.Terrain.Meshing;
using BorderDirection = DOTS.Terrain.Debug.TerrainChunkMeshBorderUtility.BorderDirection;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Validates that adjacent chunks have matching vertex positions at shared mesh borders.
    /// Runs after mesh building to detect Surface Nets meshing bugs at chunk boundaries.
    /// Only active when TerrainDebugConfig.Enabled is true.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainChunkMeshBuildSystem))]
    public partial struct TerrainMeshSeamValidatorSystem : ISystem
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
                .WithAll<TerrainChunk, TerrainChunkMeshData, TerrainChunkBounds, TerrainChunkGridInfo>()
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

            // For each chunk, validate mesh borders with east and north neighbors
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var entity = chunkEntities[i];
                var chunk = chunkComponents[i];
                var coord = new int2(chunk.ChunkCoord.x, chunk.ChunkCoord.z);

                if (!entityManager.HasComponent<TerrainChunkMeshData>(entity))
                {
                    continue;
                }

                var meshData = entityManager.GetComponentData<TerrainChunkMeshData>(entity);
                if (!meshData.HasMesh)
                {
                    continue;
                }

                // Check east neighbor (x+1)
                var eastCoord = new int2(coord.x + 1, coord.y);
                if (map.TryGetValue(eastCoord, out var eastEntity))
                {
                    ValidateMeshBorder(ref state, entity, eastEntity, coord, eastCoord, BorderDirection.East, debugConfig);
                }

                // Check north neighbor (z+1)
                var northCoord = new int2(coord.x, coord.y + 1);
                if (map.TryGetValue(northCoord, out var northEntity))
                {
                    ValidateMeshBorder(ref state, entity, northEntity, coord, northCoord, BorderDirection.North, debugConfig);
                }
            }

            map.Dispose();
        }

        private static void ValidateMeshBorder(ref SystemState state, Entity entityA, Entity entityB,
            int2 coordA, int2 coordB, BorderDirection direction, TerrainDebugConfig config)
        {
            var entityManager = state.EntityManager;

            var meshDataA = entityManager.GetComponentData<TerrainChunkMeshData>(entityA);
            var meshDataB = entityManager.GetComponentData<TerrainChunkMeshData>(entityB);

            if (!meshDataA.HasMesh || !meshDataB.HasMesh)
            {
                return;
            }

            var boundsA = entityManager.GetComponentData<TerrainChunkBounds>(entityA);
            var boundsB = entityManager.GetComponentData<TerrainChunkBounds>(entityB);
            var gridA = entityManager.GetComponentData<TerrainChunkGridInfo>(entityA);

            ref var blobA = ref meshDataA.Mesh.Value;
            ref var blobB = ref meshDataB.Mesh.Value;

            // Extract border vertices from both chunks
            var borderThreshold = gridA.VoxelSize;
            var chunkSizeA = TerrainChunkMeshBorderUtility.ComputeChunkSize(gridA);

            // Get border vertices based on direction
            using var borderVertsA = TerrainChunkMeshBorderUtility.CollectBorderVertices(ref blobA, boundsA.WorldOrigin, chunkSizeA, direction, true, borderThreshold, Allocator.Temp);
            using var borderVertsB = TerrainChunkMeshBorderUtility.CollectBorderVertices(ref blobB, boundsB.WorldOrigin, chunkSizeA, direction, false, borderThreshold, Allocator.Temp);

            if (borderVertsA.Length == 0 || borderVertsB.Length == 0)
            {
                return;
            }

            // Compare vertices at matching world positions
            var maxPosDelta = 0f;
            var countPosMismatches = 0;
            var totalMatched = 0;

            for (int i = 0; i < borderVertsA.Length; i++)
            {
                var closestDist = TerrainChunkMeshBorderUtility.ClosestDistance(borderVertsA[i], borderVertsB);

                if (closestDist < borderThreshold * 2f) // Within reasonable matching distance
                {
                    totalMatched++;
                    if (closestDist > config.MeshSeamPositionEpsilon)
                    {
                        maxPosDelta = math.max(maxPosDelta, closestDist);
                        countPosMismatches++;
                    }
                }
            }

            if (countPosMismatches > 0 && config.EnableSeamLogging)
            {
                var dirStr = direction == BorderDirection.East ? "East" : "North";
                DebugSettings.LogSeamWarning($"MESH_SEAM_MISMATCH: A{coordA} ↔ B{coordB} ({dirStr}) maxPosΔ={maxPosDelta:F6} mismatches={countPosMismatches}/{totalMatched} ε={config.MeshSeamPositionEpsilon}");
            }
            else if (config.EnableSeamLogging && totalMatched > 0)
            {
                var dirStr = direction == BorderDirection.East ? "East" : "North";
                DebugSettings.LogSeam($"MESH_SEAM_OK: A{coordA} ↔ B{coordB} ({dirStr}) matched={totalMatched} borderA={borderVertsA.Length} borderB={borderVertsB.Length}");
            }
        }
    }
}
