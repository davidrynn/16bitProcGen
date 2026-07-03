using System.Collections.Generic;
using DOTS.Terrain.LOD;
using DOTS.Terrain.SurfaceScatter;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Renders accepted pebble-cluster placements as instanced mesh draw calls each frame.
    /// Structural clone of RockChunkRenderSystem (same beginCameraRendering submission
    /// timing, same near/far bucket layout) — kept as a separate per-family system per
    /// TERRAIN_SURFACE_SCATTER_PLAN §7.1/§7.2 (shared lifecycle pattern, family-owned code).
    ///
    /// The near/far bucket layout, material-fallback resolution, and per-camera submission
    /// mechanics are shared with TreeChunkRenderSystem/RockChunkRenderSystem via
    /// <see cref="SurfaceScatterRenderCore"/>. What stays here is genuinely pebble-specific:
    /// PebbleTypeId maps to a variant index by modulo (matching rocks), and each instance
    /// applies its own record.UniformScale on top of the config scale with no mesh-bottom
    /// grounding.
    /// </summary>
    // SystemBase (not ISystem) is required: RenderPipelineManager.beginCameraRendering subscription
    // needs managed OnCreate/OnDestroy lifecycle. Mirrors TreeChunkRenderSystem/RockChunkRenderSystem.
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PebbleChunkRenderSystem : SystemBase
    {
        private const int MaxPebbleVariants = 8;
        private const string FallbackMaterialName = "PebbleRuntimeFallbackMaterial";

        // Static so the camera callback (a static delegate) can access without a closure, and
        // so the test-only static hooks below can reach it. This state is exclusive to the
        // pebble family — SurfaceScatterRenderCore is stateless and never shares state across
        // the Tree/Rock/Pebble render systems.
        // Bucket capacity is smaller than trees/rocks (256 vs 1024): pebble clusters are denser
        // per-chunk but individually cheaper, and were tuned lower to avoid over-allocating.
        private static readonly SurfaceScatterRenderState _state =
            new SurfaceScatterRenderState(MaxPebbleVariants, matrixBucketCapacity: 256);

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

        public static bool TryPrepareSubmissionFrame(PebbleRenderConfig config)
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
            if (!SystemAPI.ManagedAPI.TryGetSingleton<PebbleRenderConfig>(out var config))
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

            foreach (var (lodStateRO, records) in SystemAPI.Query<RefRO<TerrainChunkLodState>, DynamicBuffer<PebblePlacementRecord>>()
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
                        : record.PebbleTypeId % variantCount;

                    int bucketIndex = variantIndex;
                    if (lodActive
                        && SurfaceScatterLodUtility.SelectLodLevel(
                            math.distancesq(record.WorldPosition, cameraPosition),
                            config.LodSwapDistance) == SurfaceScatterLodUtility.FarLod)
                    {
                        // Variants without an authored far mesh fall back to near.
                        int farBucket = variantIndex + MaxPebbleVariants;
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

                    var scale = math.max(0.01f, record.UniformScale * config.UniformScale);
                    _state.PendingMatricesByVariant[bucketIndex].Add(Matrix4x4.TRS(
                        record.WorldPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * scale));
                }
            }

            SurfaceScatterRenderCore.RebuildWorldBounds(_state);
        }
    }
}
