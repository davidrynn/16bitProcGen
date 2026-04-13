using System.Collections.Generic;
using DOTS.Terrain.LOD;
using DOTS.Terrain.SurfaceScatter;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Renders accepted rock placements as instanced mesh draw calls each frame.
    /// Uses the same beginCameraRendering submission timing used by tree rendering.
    /// </summary>
    // SystemBase (not ISystem) is required: RenderPipelineManager.beginCameraRendering subscription
    // needs managed OnCreate/OnDestroy lifecycle. Mirrors TreeChunkRenderSystem.
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RockChunkRenderSystem : SystemBase
    {
        private const int CulledScatterLod = 3;

        private static readonly Matrix4x4[] InstanceBuffer = new Matrix4x4[1023];
        private static readonly List<Matrix4x4> PendingMatrices = new List<Matrix4x4>(4096);
        private static Material _pendingMaterial;
        private static Mesh _pendingMesh;
        private static Bounds _pendingWorldBounds;

        protected override void OnCreate()
        {
            RenderPipelineManager.beginCameraRendering += SubmitToCamera;
        }

        protected override void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= SubmitToCamera;
            ClearPendingSubmissionState();
        }

        private static void SubmitToCamera(ScriptableRenderContext ctx, Camera camera)
        {
            if (_pendingMesh == null || _pendingMaterial == null || PendingMatrices.Count == 0)
            {
                return;
            }

            var rp = new RenderParams(_pendingMaterial)
            {
                worldBounds = _pendingWorldBounds,
            };

            int remaining = PendingMatrices.Count;
            int offset = 0;
            while (remaining > 0)
            {
                int batch = Mathf.Min(remaining, 1023);
                for (int i = 0; i < batch; i++)
                {
                    InstanceBuffer[i] = PendingMatrices[offset + i];
                }

                Graphics.RenderMeshInstanced(rp, _pendingMesh, 0, InstanceBuffer, batch);
                offset += batch;
                remaining -= batch;
            }
        }

        public static bool TryBuildWorldBounds(
            IReadOnlyList<Matrix4x4> matrices,
            in Bounds meshBounds,
            float uniformScale,
            out Bounds worldBounds)
        {
            return SurfaceScatterRenderBoundsUtility.TryBuildWorldBounds(
                matrices,
                in meshBounds,
                uniformScale,
                out worldBounds);
        }

        public static bool TryPrepareSubmissionFrame(RockRenderConfig config)
        {
            if (config == null || config.Mesh == null || config.Material == null)
            {
                ClearPendingSubmissionState();
                return false;
            }

            _pendingMesh = config.Mesh;
            _pendingMaterial = config.Material;
            PendingMatrices.Clear();
            return true;
        }

        /// <summary>
        /// Test-only hook to append a pending matrix after frame prep.
        /// </summary>
        public static void AddPendingMatrixForTests(in Matrix4x4 matrix)
        {
            PendingMatrices.Add(matrix);
        }

        /// <summary>
        /// Test-only hook to verify stale submission state has been cleared.
        /// </summary>
        public static bool HasPendingSubmissionDataForTests()
        {
            return _pendingMesh != null
                   && _pendingMaterial != null
                   && PendingMatrices.Count > 0;
        }

        private static void ClearPendingSubmissionState()
        {
            PendingMatrices.Clear();
            _pendingMesh = null;
            _pendingMaterial = null;
            _pendingWorldBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<RockRenderConfig>(out var config))
            {
                ClearPendingSubmissionState();
                return;
            }

            if (!TryPrepareSubmissionFrame(config))
            {
                return;
            }

            foreach (var (lodStateRO, records) in SystemAPI.Query<RefRO<TerrainChunkLodState>, DynamicBuffer<RockPlacementRecord>>()
                         .WithAll<TerrainChunk>())
            {
                // LOD3 is fully culled by policy; draw only LOD0-2 scatter instances.
                if (lodStateRO.ValueRO.CurrentLod >= CulledScatterLod)
                {
                    continue;
                }

                foreach (var record in records)
                {
                    var scale = math.max(0.01f, record.UniformScale * config.UniformScale);
                    PendingMatrices.Add(Matrix4x4.TRS(
                        record.WorldPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * scale));
                }
            }

            // Per-instance matrices already contain full scale, so use neutral external scale.
            if (!TryBuildWorldBounds(PendingMatrices, config.Mesh.bounds, 1f, out _pendingWorldBounds))
            {
                _pendingWorldBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
            }
        }
    }
}
