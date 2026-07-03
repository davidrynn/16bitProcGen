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
    ///
    /// The near/far bucket layout, material-fallback resolution, and per-camera submission
    /// mechanics are shared with TreeChunkRenderSystem/PebbleChunkRenderSystem via
    /// <see cref="SurfaceScatterRenderCore"/>. What stays here is genuinely rock-specific:
    /// RockTypeId maps to a variant index by modulo (unlike trees, which clamp), and each
    /// instance applies its own record.UniformScale on top of the config scale with no
    /// mesh-bottom grounding (rocks have a RockPlacementRecord.UniformScale field; trees
    /// don't).
    /// </summary>
    // SystemBase (not ISystem) is required: RenderPipelineManager.beginCameraRendering subscription
    // needs managed OnCreate/OnDestroy lifecycle. Mirrors TreeChunkRenderSystem.
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class RockChunkRenderSystem : SystemBase
    {
        private const int MaxRockVariants = 8;
        private const string FallbackMaterialName = "RockRuntimeFallbackMaterial";

        // Static so the camera callback (a static delegate) can access without a closure, and
        // so the test-only static hooks below can reach it. This state is exclusive to the
        // rock family — SurfaceScatterRenderCore is stateless and never shares state across
        // the Tree/Rock/Pebble render systems.
        private static readonly SurfaceScatterRenderState _state =
            new SurfaceScatterRenderState(MaxRockVariants, matrixBucketCapacity: 1024);

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

        public static bool TryPrepareSubmissionFrame(RockRenderConfig config)
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
            if (!SystemAPI.ManagedAPI.TryGetSingleton<RockRenderConfig>(out var config))
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

            foreach (var (lodStateRO, records) in SystemAPI.Query<RefRO<TerrainChunkLodState>, DynamicBuffer<RockPlacementRecord>>()
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
                        : record.RockTypeId % variantCount;

                    int bucketIndex = variantIndex;
                    if (lodActive
                        && SurfaceScatterLodUtility.SelectLodLevel(
                            math.distancesq(record.WorldPosition, cameraPosition),
                            config.LodSwapDistance) == SurfaceScatterLodUtility.FarLod)
                    {
                        // Variants without an authored far mesh fall back to near.
                        int farBucket = variantIndex + MaxRockVariants;
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
