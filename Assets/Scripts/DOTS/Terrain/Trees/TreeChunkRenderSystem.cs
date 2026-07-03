using System.Collections.Generic;
using DOTS.Terrain.LOD;
using DOTS.Terrain.SurfaceScatter;
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
    ///
    /// The near/far bucket layout, material-fallback resolution, and per-camera submission
    /// mechanics are shared with RockChunkRenderSystem/PebbleChunkRenderSystem via
    /// <see cref="SurfaceScatterRenderCore"/>. What stays here is genuinely tree-specific:
    /// TreeTypeId is clamped (not modulo'd, unlike rocks/pebbles) into a variant index, and
    /// instances are grounded by mesh-bottom offset with a single config-wide scale (trees
    /// have no per-record UniformScale field, unlike rocks/pebbles).
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TreeChunkRenderSystem : SystemBase
    {
        private const int MaxTreeVariants = 8;
        private const string FallbackMaterialName = "TreeRuntimeFallbackMaterial";

        // Static so the camera callback (a static delegate) can access without a closure, and
        // so the test-only static hooks below can reach it. This state is exclusive to the
        // tree family — SurfaceScatterRenderCore is stateless and never shares state across
        // the Tree/Rock/Pebble render systems.
        private static readonly SurfaceScatterRenderState _state =
            new SurfaceScatterRenderState(MaxTreeVariants, matrixBucketCapacity: 1024);

        protected override void OnCreate()
        {
            RenderPipelineManager.beginCameraRendering += SubmitToCamera;
        }

        protected override void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= SubmitToCamera;
            SurfaceScatterRenderCore.ClearPendingSubmissionState(_state);

            if (_state.RuntimeFallbackMaterial != null)
            {
                Object.Destroy(_state.RuntimeFallbackMaterial);
                _state.RuntimeFallbackMaterial = null;
            }
        }

        // Called by URP once per camera, just before it executes its render pass.
        private static void SubmitToCamera(ScriptableRenderContext ctx, Camera camera)
        {
            SurfaceScatterRenderCore.SubmitToCamera(_state);
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

        /// <summary>
        /// Initializes a render-submission frame from config and clears stale static state
        /// when config is missing/invalid.
        /// </summary>
        public static bool TryPrepareSubmissionFrame(TreeRenderConfig config)
        {
            if (config == null)
            {
                SurfaceScatterRenderCore.ClearPendingSubmissionState(_state);
                return false;
            }

            return SurfaceScatterRenderCore.TryPrepareSubmissionFrame(
                config.MeshVariants,
                config.Mesh,
                config.LodMeshVariants,
                config.Material,
                FallbackMaterialName,
                _state);
        }

        /// <summary>
        /// Test-only hook to append a pending matrix after frame prep.
        /// </summary>
        public static void AddPendingMatrixForTests(in Matrix4x4 matrix)
        {
            SurfaceScatterRenderCore.AddPendingMatrixForTests(_state, matrix);
        }

        /// <summary>
        /// Test-only hook to verify stale submission state has been cleared.
        /// </summary>
        public static bool HasPendingSubmissionDataForTests()
        {
            return SurfaceScatterRenderCore.HasPendingSubmissionDataForTests(_state);
        }

        /// <summary>
        /// Test-only hook to inspect which mesh is registered for a (variant, lodLevel) bucket.
        /// </summary>
        public static Mesh GetPendingMeshForTests(int variantIndex, int lodLevel)
        {
            return SurfaceScatterRenderCore.GetPendingMeshForTests(_state, variantIndex, lodLevel);
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<TreeRenderConfig>(out var config))
            {
                SurfaceScatterRenderCore.ClearPendingSubmissionState(_state);
                return;
            }

            if (!TryPrepareSubmissionFrame(config))
            {
                return;
            }

            int variantCount = _state.PendingVariantCount;

            // LOD buckets are selected once per frame against Camera.main, not per rendering camera.
            // This is correct for the single gameplay camera (cheaper than re-bucketing per camera),
            // but secondary cameras (scene view, split-screen) get near/far chosen for the main camera's
            // viewpoint. Acceptable for the single-camera MVP; revisit if multi-camera ships.
            // No camera (headless/tests) → LOD disabled for the frame; all instances stay near.
            var lodCamera = Camera.main;
            bool lodActive = config.LodSwapDistance > 0f && lodCamera != null;
            float3 cameraPosition = lodActive ? (float3)lodCamera.transform.position : float3.zero;

            foreach (var (lodStateRO, records) in SystemAPI.Query<RefRO<TerrainChunkLodState>, DynamicBuffer<TreePlacementRecord>>()
                .WithAll<TerrainChunk>())
            {
                // LOD3 is fully culled by policy; draw only LOD0-2 scatter instances.
                if (lodStateRO.ValueRO.CurrentLod >= SurfaceScatterRenderCore.CulledScatterLod)
                {
                    continue;
                }

                foreach (var record in records)
                {
                    int variantIndex = variantCount == 1
                        ? 0
                        : math.clamp((int)record.TreeTypeId, 0, variantCount - 1);

                    int bucketIndex = variantIndex;
                    if (lodActive
                        && SurfaceScatterLodUtility.SelectLodLevel(
                            math.distancesq(record.WorldPosition, cameraPosition),
                            config.LodSwapDistance) == SurfaceScatterLodUtility.FarLod)
                    {
                        // Variants without an authored far mesh fall back to near.
                        int farBucket = variantIndex + MaxTreeVariants;
                        if (_state.PendingMeshesByVariant[farBucket] != null)
                        {
                            bucketIndex = farBucket;
                        }
                    }

                    var mesh = _state.PendingMeshesByVariant[bucketIndex];
                    if (mesh == null)
                    {
                        continue;
                    }

                    // Ground instances by mesh bottom instead of pivot so centered-pivot assets
                    // (common in third-party packs) do not render half-buried as canopy domes.
                    // Offset uses the mesh actually drawn — keep far-mesh bounds close to the
                    // near mesh to avoid a vertical pop at the swap distance.
                    var yOffset = -mesh.bounds.min.y * config.UniformScale;
                    var groundedPosition = record.WorldPosition + new float3(0f, yOffset, 0f);

                    _state.PendingMatricesByVariant[bucketIndex].Add(Matrix4x4.TRS(
                        groundedPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * config.UniformScale));
                }
            }

            SurfaceScatterRenderCore.RebuildWorldBounds(_state);
        }
    }
}
