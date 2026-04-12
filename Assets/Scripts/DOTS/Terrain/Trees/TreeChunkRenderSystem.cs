using System.Collections.Generic;
using DOTS.Terrain.LOD;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Renders accepted tree placements as instanced mesh draw calls each frame.
    ///
    /// Architecture note — why beginCameraRendering:
    /// Graphics.RenderMeshInstanced called directly from PresentationSystemGroup.OnUpdate
    /// (which runs in PreLateUpdate) is NOT picked up by the URP Game camera. URP renders
    /// Game cameras in PostLateUpdate via its own SRP render pass, and draw calls submitted
    /// outside of a RenderPipelineManager callback are dropped for those cameras. Scene View
    /// cameras are handled separately by the editor and happen to see the queued calls, which
    /// is why trees appeared in Scene View only.
    ///
    /// Fix: collect matrices during OnUpdate, then flush to each camera inside
    /// RenderPipelineManager.beginCameraRendering — the callback guaranteed to fire just
    /// before URP executes the render pass for that camera.
    ///
    /// This is an intentional MVP simplification. Replace with Entities Graphics
    /// (RenderMeshArray + MaterialMeshInfo per entity) post-MVP if tree counts grow
    /// beyond ~4000 visible at once, or if per-tree LOD/culling is needed.
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TreeChunkRenderSystem : SystemBase
    {
        // RenderMeshInstanced batch limit is 1023.
        private static readonly Matrix4x4[] _instanceBuffer = new Matrix4x4[1023];

        // Matrices collected each OnUpdate, flushed per-camera in SubmitToCamera.
        // Static so the camera callback (a static delegate) can access without a closure.
        private static readonly List<Matrix4x4> _pendingMatrices = new List<Matrix4x4>(4096);
        private static Material _pendingMaterial;
        private static Mesh    _pendingMesh;
        private static Bounds  _pendingWorldBounds;

        protected override void OnCreate()
        {
            RenderPipelineManager.beginCameraRendering += SubmitToCamera;
        }

        protected override void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= SubmitToCamera;
            ClearPendingSubmissionState();
        }

        // Called by URP once per camera, just before it executes its render pass.
        private static void SubmitToCamera(ScriptableRenderContext ctx, Camera camera)
        {
            if (_pendingMesh == null || _pendingMaterial == null || _pendingMatrices.Count == 0)
                return;

            var rp = new RenderParams(_pendingMaterial)
            {
                worldBounds = _pendingWorldBounds,
            };

            int remaining = _pendingMatrices.Count;
            int offset    = 0;
            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, 1023);
                for (int i = 0; i < batch; i++)
                    _instanceBuffer[i] = _pendingMatrices[offset + i];
                Graphics.RenderMeshInstanced(rp, _pendingMesh, 0, _instanceBuffer, batch);
                offset    += batch;
                remaining -= batch;
            }
        }

        /// <summary>
        /// Builds a conservative world-space AABB for all pending instances so SRP batch culling
        /// does not incorrectly reject draws when the world streams far from (0,0,0).
        /// </summary>
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
            var scaledExtents = meshBounds.extents * scale;

            var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

            for (int i = 0; i < matrices.Count; i++)
            {
                var matrix = matrices[i];
                var position = new Vector3(matrix.m03, matrix.m13, matrix.m23);
                min = Vector3.Min(min, position - scaledExtents);
                max = Vector3.Max(max, position + scaledExtents);
            }

            var size = max - min;
            size.x = math.max(size.x, 0.01f);
            size.y = math.max(size.y, 0.01f);
            size.z = math.max(size.z, 0.01f);

            worldBounds = new Bounds((min + max) * 0.5f, size);
            return true;
        }

        /// <summary>
        /// Initializes a render-submission frame from config and clears stale static state
        /// when config is missing/invalid.
        /// </summary>
        public static bool TryPrepareSubmissionFrame(TreeRenderConfig config)
        {
            if (config == null || config.Mesh == null || config.Material == null)
            {
                ClearPendingSubmissionState();
                return false;
            }

            _pendingMesh = config.Mesh;
            _pendingMaterial = config.Material;
            _pendingMatrices.Clear();
            return true;
        }

        /// <summary>
        /// Test-only hook to append a pending matrix after frame prep.
        /// </summary>
        public static void AddPendingMatrixForTests(in Matrix4x4 matrix)
        {
            _pendingMatrices.Add(matrix);
        }

        /// <summary>
        /// Test-only hook to verify stale submission state has been cleared.
        /// </summary>
        public static bool HasPendingSubmissionDataForTests()
        {
            return _pendingMesh != null
                   && _pendingMaterial != null
                   && _pendingMatrices.Count > 0;
        }

        private static void ClearPendingSubmissionState()
        {
            _pendingMesh = null;
            _pendingMaterial = null;
            _pendingMatrices.Clear();
            _pendingWorldBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<TreeRenderConfig>(out var config))
            {
                ClearPendingSubmissionState();
                return;
            }

            if (!TryPrepareSubmissionFrame(config))
            {
                return;
            }

            foreach (var (lodStateRO, records) in SystemAPI.Query<RefRO<TerrainChunkLodState>, DynamicBuffer<TreePlacementRecord>>()
                .WithAll<TerrainChunk>())
            {
                if (lodStateRO.ValueRO.CurrentLod >= 3)
                {
                    continue;
                }

                foreach (var record in records)
                {
                    _pendingMatrices.Add(Matrix4x4.TRS(
                        record.WorldPosition,
                        Quaternion.identity,
                        Vector3.one * config.UniformScale));
                }
            }

            if (!TryBuildWorldBounds(_pendingMatrices, config.Mesh.bounds, config.UniformScale, out _pendingWorldBounds))
            {
                _pendingWorldBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
            }
        }
    }
}
