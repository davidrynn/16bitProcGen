using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain.Meshing;
using BorderDirection = DOTS.Terrain.Debug.TerrainChunkMeshBorderUtility.BorderDirection;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Draws debug visuals for terrain chunk mesh borders when EnableMeshDebugOverlay is true.
    /// Shows chunk boundaries, border vertices, and helps visualize mesh seam issues.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(TerrainMeshSeamValidatorSystem))]
    public partial struct TerrainMeshBorderDebugSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<TerrainDebugConfig>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var debugConfig = SystemAPI.GetSingleton<TerrainDebugConfig>();
            if (!debugConfig.Enabled || !debugConfig.EnableMeshDebugOverlay)
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

            // Build coord -> entity map for neighbor lookup
            var map = new NativeParallelHashMap<int2, Entity>(chunkEntities.Length, Allocator.Temp);
            for (int i = 0; i < chunkEntities.Length; i++)
            {
                var coord = new int2(chunkComponents[i].ChunkCoord.x, chunkComponents[i].ChunkCoord.z);
                map.TryAdd(coord, chunkEntities[i]);
            }

            // Draw debug visuals for each chunk
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

                var bounds = entityManager.GetComponentData<TerrainChunkBounds>(entity);
                var grid = entityManager.GetComponentData<TerrainChunkGridInfo>(entity);

                ref var mesh = ref meshData.Mesh.Value;
                var chunkSize = TerrainChunkMeshBorderUtility.ComputeChunkSize(grid);

                // Draw chunk boundary box (green)
                DrawChunkBounds(bounds.WorldOrigin, chunkSize, Color.green);

                // Draw border vertices
                var borderThreshold = grid.VoxelSize;

                // Get neighbor entities for mismatch detection
                var hasEastNeighbor = map.TryGetValue(new int2(coord.x + 1, coord.y), out var eastEntity);
                var hasNorthNeighbor = map.TryGetValue(new int2(coord.x, coord.y + 1), out var northEntity);

                // Collect mismatch info for coloring
                using var eastMismatches = hasEastNeighbor
                    ? GetMismatchedPositions(ref state, entity, eastEntity, BorderDirection.East, debugConfig)
                    : new NativeHashSet<int>(0, Allocator.Temp);
                using var northMismatches = hasNorthNeighbor
                    ? GetMismatchedPositions(ref state, entity, northEntity, BorderDirection.North, debugConfig)
                    : new NativeHashSet<int>(0, Allocator.Temp);

                // Draw all border vertices
                for (int v = 0; v < mesh.Vertices.Length; v++)
                {
                    var localPos = mesh.Vertices[v];
                    var worldPos = bounds.WorldOrigin + localPos;

                    var onEast = localPos.x >= chunkSize.x - borderThreshold;
                    var onWest = localPos.x <= borderThreshold;
                    var onNorth = localPos.z >= chunkSize.z - borderThreshold;
                    var onSouth = localPos.z <= borderThreshold;

                    if (!onEast && !onWest && !onNorth && !onSouth)
                    {
                        continue;
                    }

                    // Determine vertex color based on border and mismatch status
                    var color = Color.yellow; // Default: matched border vertex

                    if (onEast && eastMismatches.Contains(v))
                    {
                        color = Color.red; // Mismatched east border vertex
                    }
                    else if (onNorth && northMismatches.Contains(v))
                    {
                        color = Color.red; // Mismatched north border vertex
                    }

                    // Draw vertex as a small cross
                    DrawVertexMarker(worldPos, 0.1f, color);
                }
            }

            map.Dispose();
        }

        private static NativeHashSet<int> GetMismatchedPositions(
            ref SystemState state,
            Entity entityA,
            Entity entityB,
            BorderDirection direction,
            TerrainDebugConfig config)
        {
            var result = new NativeHashSet<int>(64, Allocator.Temp);
            var entityManager = state.EntityManager;

            var meshDataA = entityManager.GetComponentData<TerrainChunkMeshData>(entityA);
            var meshDataB = entityManager.GetComponentData<TerrainChunkMeshData>(entityB);

            if (!meshDataA.HasMesh || !meshDataB.HasMesh)
            {
                return result;
            }

            var boundsA = entityManager.GetComponentData<TerrainChunkBounds>(entityA);
            var boundsB = entityManager.GetComponentData<TerrainChunkBounds>(entityB);
            var gridA = entityManager.GetComponentData<TerrainChunkGridInfo>(entityA);

            ref var blobA = ref meshDataA.Mesh.Value;
            ref var blobB = ref meshDataB.Mesh.Value;

            var borderThreshold = gridA.VoxelSize;
            var chunkSize = TerrainChunkMeshBorderUtility.ComputeChunkSize(gridA);

            using var borderVertsB = TerrainChunkMeshBorderUtility.CollectBorderVertices(
                ref blobB, boundsB.WorldOrigin, chunkSize, direction, isSourceChunk: false, borderThreshold, Allocator.Temp);

            // Walk chunk A's vertices directly (rather than via CollectBorderVertices) so the
            // result set can key mismatches by their original TerrainChunkMeshBlob.Vertices index —
            // the caller needs that index to color the exact mismatched vertex when drawing.
            for (int i = 0; i < blobA.Vertices.Length; i++)
            {
                var localPos = blobA.Vertices[i];
                if (!TerrainChunkMeshBorderUtility.IsOnSharedBorder(localPos, chunkSize, borderThreshold, direction, isSourceChunk: true))
                {
                    continue;
                }

                var worldPosA = boundsA.WorldOrigin + localPos;
                var closestDist = TerrainChunkMeshBorderUtility.ClosestDistance(worldPosA, borderVertsB);

                // If no matching vertex found within tolerance, mark as mismatch
                if (closestDist > config.MeshSeamPositionEpsilon && closestDist < borderThreshold * 2f)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        private static void DrawChunkBounds(float3 origin, float3 size, Color color)
        {
            var p000 = origin;
            var p001 = origin + new float3(0, 0, size.z);
            var p010 = origin + new float3(0, size.y, 0);
            var p011 = origin + new float3(0, size.y, size.z);
            var p100 = origin + new float3(size.x, 0, 0);
            var p101 = origin + new float3(size.x, 0, size.z);
            var p110 = origin + new float3(size.x, size.y, 0);
            var p111 = origin + size;

            // Bottom face
            UnityEngine.Debug.DrawLine(p000, p100, color);
            UnityEngine.Debug.DrawLine(p100, p101, color);
            UnityEngine.Debug.DrawLine(p101, p001, color);
            UnityEngine.Debug.DrawLine(p001, p000, color);

            // Top face
            UnityEngine.Debug.DrawLine(p010, p110, color);
            UnityEngine.Debug.DrawLine(p110, p111, color);
            UnityEngine.Debug.DrawLine(p111, p011, color);
            UnityEngine.Debug.DrawLine(p011, p010, color);

            // Vertical edges
            UnityEngine.Debug.DrawLine(p000, p010, color);
            UnityEngine.Debug.DrawLine(p100, p110, color);
            UnityEngine.Debug.DrawLine(p101, p111, color);
            UnityEngine.Debug.DrawLine(p001, p011, color);
        }

        private static void DrawVertexMarker(float3 pos, float size, Color color)
        {
            var halfSize = size * 0.5f;
            UnityEngine.Debug.DrawLine(pos - new float3(halfSize, 0, 0), pos + new float3(halfSize, 0, 0), color);
            UnityEngine.Debug.DrawLine(pos - new float3(0, halfSize, 0), pos + new float3(0, halfSize, 0), color);
            UnityEngine.Debug.DrawLine(pos - new float3(0, 0, halfSize), pos + new float3(0, 0, halfSize), color);
        }
    }
}
