using Unity.Collections;
using Unity.Mathematics;
using DOTS.Terrain;

namespace DOTS.Terrain.Debug
{
    /// <summary>
    /// Shared border-vertex comparison math for Surface Nets chunk seams. Both
    /// <see cref="TerrainMeshSeamValidatorSystem"/> (logs mismatch stats) and
    /// <see cref="TerrainMeshBorderDebugSystem"/> (draws mismatched vertices) need to identify
    /// which mesh vertices sit on the shared east/north edge between two adjacent chunks and find
    /// their nearest counterpart on the other side; keeping that math in one place avoids the two
    /// consumers drifting apart on epsilon/threshold handling.
    /// </summary>
    public static class TerrainChunkMeshBorderUtility
    {
        /// <summary>Cardinal direction of the shared edge being compared (chunks are only ever validated against their +X/+Z neighbors to avoid double-checking each pair).</summary>
        public enum BorderDirection
        {
            East,
            North
        }

        /// <summary>World-space size of a chunk's mesh volume, derived from its grid resolution and voxel size.</summary>
        public static float3 ComputeChunkSize(in TerrainChunkGridInfo grid)
        {
            return new float3(
                (grid.Resolution.x - 1) * grid.VoxelSize,
                (grid.Resolution.y - 1) * grid.VoxelSize,
                (grid.Resolution.z - 1) * grid.VoxelSize);
        }

        /// <summary>
        /// True if a chunk-local vertex position lies on the edge shared with a neighbor in the
        /// given direction. The source chunk's shared edge is its max-X/max-Z face; the neighbor's
        /// is the corresponding min-X/min-Z face — these are the two faces Surface Nets is
        /// expected to stitch together seamlessly.
        /// </summary>
        public static bool IsOnSharedBorder(float3 localPos, float3 sourceChunkSize, float borderThreshold, BorderDirection direction, bool isSourceChunk)
        {
            if (direction == BorderDirection.East)
            {
                return isSourceChunk
                    ? localPos.x >= sourceChunkSize.x - borderThreshold
                    : localPos.x <= borderThreshold;
            }

            return isSourceChunk
                ? localPos.z >= sourceChunkSize.z - borderThreshold
                : localPos.z <= borderThreshold;
        }

        /// <summary>
        /// Collects world-space positions of a mesh's vertices that lie on the shared border edge.
        /// The returned list is in mesh-vertex order but is a filtered subset, so list index does
        /// not correspond to the original <c>mesh.Vertices</c> index — callers that need the
        /// original index (e.g. to color a specific mismatched vertex) should iterate with
        /// <see cref="IsOnSharedBorder"/> directly instead of calling this helper.
        /// </summary>
        public static NativeList<float3> CollectBorderVertices(
            ref TerrainChunkMeshBlob mesh,
            float3 worldOrigin,
            float3 sourceChunkSize,
            BorderDirection direction,
            bool isSourceChunk,
            float borderThreshold,
            Allocator allocator)
        {
            var result = new NativeList<float3>(allocator);

            for (int i = 0; i < mesh.Vertices.Length; i++)
            {
                var localPos = mesh.Vertices[i];
                if (IsOnSharedBorder(localPos, sourceChunkSize, borderThreshold, direction, isSourceChunk))
                {
                    result.Add(worldOrigin + localPos);
                }
            }

            return result;
        }

        /// <summary>Euclidean distance from a point to its nearest neighbor in a candidate list, or <see cref="float.MaxValue"/> if the list is empty.</summary>
        public static float ClosestDistance(float3 point, NativeList<float3> candidates)
        {
            var closest = float.MaxValue;
            for (int j = 0; j < candidates.Length; j++)
            {
                closest = math.min(closest, math.distance(point, candidates[j]));
            }

            return closest;
        }
    }
}
