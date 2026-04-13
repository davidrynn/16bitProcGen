using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Shared world-bounds construction for matrix-instanced surface scatter rendering.
    /// Handles rotation and non-uniform scale per instance to avoid under-culling.
    /// </summary>
    public static class SurfaceScatterRenderBoundsUtility
    {
        public static bool TryBuildWorldBounds(
            IReadOnlyList<Matrix4x4> matrices,
            in Bounds meshBounds,
            float uniformScale,
            out Bounds worldBounds)
        {
            worldBounds = default;
            if (matrices == null || matrices.Count == 0)
            {
                return false;
            }

            var scale = math.max(0.01f, math.abs(uniformScale));
            var localCenter = meshBounds.center * scale;
            var localExtents = meshBounds.extents * scale;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < matrices.Count; i++)
            {
                var matrix = matrices[i];
                var worldCenter = matrix.MultiplyPoint3x4(localCenter);
                var worldExtents = TransformExtents(matrix, localExtents);

                min = Vector3.Min(min, worldCenter - worldExtents);
                max = Vector3.Max(max, worldCenter + worldExtents);
            }

            var size = max - min;
            size.x = math.max(size.x, 0.01f);
            size.y = math.max(size.y, 0.01f);
            size.z = math.max(size.z, 0.01f);

            worldBounds = new Bounds((min + max) * 0.5f, size);
            return true;
        }

        private static Vector3 TransformExtents(Matrix4x4 matrix, Vector3 localExtents)
        {
            var axisX = new Vector3(matrix.m00, matrix.m10, matrix.m20) * localExtents.x;
            var axisY = new Vector3(matrix.m01, matrix.m11, matrix.m21) * localExtents.y;
            var axisZ = new Vector3(matrix.m02, matrix.m12, matrix.m22) * localExtents.z;

            return new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
        }
    }
}
