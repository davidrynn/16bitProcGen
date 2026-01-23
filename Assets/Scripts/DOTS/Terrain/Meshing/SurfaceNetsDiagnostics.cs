using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Meshing
{
    /// <summary>
    /// Lightweight helper to sample triangle orientation statistics from Surface Nets meshes.
    /// Keeps all logic inside DOTS.Terrain so callers outside the assembly can reflect into it safely.
    /// </summary>
    public static class SurfaceNetsDiagnostics
    {
        /// <summary>
        /// Samples up to <paramref name="maxChunks"/> chunk meshes and counts triangle normals facing up vs. down.
        /// Returns false if no chunks were processed (e.g., no mesh data present).
        /// </summary>
        public static bool TrySample(EntityManager entityManager, int maxChunks, int maxTrianglesPerChunk, out int sampledChunks, out int upward, out int downward)
        {
            sampledChunks = 0;
            upward = 0;
            downward = 0;

            using var meshQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TerrainChunkMeshData>());
            var count = meshQuery.CalculateEntityCount();
            if (count == 0)
            {
                return false;
            }

            using var meshDataArray = meshQuery.ToComponentDataArray<TerrainChunkMeshData>(Allocator.Temp);

            for (int i = 0; i < meshDataArray.Length && sampledChunks < maxChunks; i++)
            {
                var mesh = meshDataArray[i].Mesh;
                if (!mesh.IsCreated)
                {
                    continue;
                }

                ref var vertices = ref mesh.Value.Vertices;
                ref var indices = ref mesh.Value.Indices;

                if (indices.Length < 3 || vertices.Length == 0)
                {
                    continue;
                }

                sampledChunks++;
                var triCount = math.min(indices.Length / 3, maxTrianglesPerChunk);

                for (int t = 0; t < triCount; t++)
                {
                    var ia = indices[t * 3];
                    var ib = indices[t * 3 + 1];
                    var ic = indices[t * 3 + 2];

                    if (ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length)
                    {
                        continue;
                    }

                    var a = vertices[ia];
                    var b = vertices[ib];
                    var c = vertices[ic];

                    var normal = math.cross(b - a, c - a);
                    if (normal.y >= 0f)
                    {
                        upward++;
                    }
                    else
                    {
                        downward++;
                    }
                }
            }

            return sampledChunks > 0;
        }
    }
}
