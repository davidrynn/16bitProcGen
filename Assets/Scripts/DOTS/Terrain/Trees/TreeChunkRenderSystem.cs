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
    /// </summary>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class TreeChunkRenderSystem : SystemBase
    {
        private const int CulledScatterLod = 3;
        private const int MaxTreeVariants = 8;

        // Bucket layout: near meshes [0..MaxTreeVariants-1], far-LOD meshes
        // [MaxTreeVariants..TotalMeshBuckets-1]. See SURFACE_SCATTER_LOD_SPEC.md.
        private const int TotalMeshBuckets = MaxTreeVariants * SurfaceScatterLodUtility.LodLevelCount;

        // RenderMeshInstanced batch limit is 1023.
        private static readonly Matrix4x4[] _instanceBuffer = new Matrix4x4[1023];

        // Matrices collected each OnUpdate, flushed per-camera in SubmitToCamera.
        // Static so the camera callback (a static delegate) can access without a closure.
        private static readonly List<Matrix4x4>[] _pendingMatricesByVariant = CreateMatrixBuckets(TotalMeshBuckets);
        private static readonly Mesh[] _pendingMeshesByVariant = new Mesh[TotalMeshBuckets];
        private static readonly Bounds[] _pendingWorldBoundsByVariant = new Bounds[TotalMeshBuckets];
        private static readonly Bounds _tinyBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private const string ErrorShaderName = "Hidden/InternalErrorShader";
        private const string PreferredFallbackShaderName = "DOTS/VertexColorUnlitClip";
        private static Material _pendingMaterial;
        private static Material _runtimeFallbackMaterial;
        private static int _pendingVariantCount;

        private static List<Matrix4x4>[] CreateMatrixBuckets(int count)
        {
            var buckets = new List<Matrix4x4>[count];
            for (int i = 0; i < count; i++)
            {
                buckets[i] = new List<Matrix4x4>(1024);
            }

            return buckets;
        }

        protected override void OnCreate()
        {
            RenderPipelineManager.beginCameraRendering += SubmitToCamera;
        }

        protected override void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= SubmitToCamera;
            ClearPendingSubmissionState();

            if (_runtimeFallbackMaterial != null)
            {
                Object.Destroy(_runtimeFallbackMaterial);
                _runtimeFallbackMaterial = null;
            }
        }

        // Called by URP once per camera, just before it executes its render pass.
        private static void SubmitToCamera(ScriptableRenderContext ctx, Camera camera)
        {
            if (_pendingMaterial == null || _pendingVariantCount == 0)
                return;

            // Iterate every bucket (near + far blocks); empties are skipped below.
            for (int bucketIndex = 0; bucketIndex < TotalMeshBuckets; bucketIndex++)
            {
                var mesh = _pendingMeshesByVariant[bucketIndex];
                var matrices = _pendingMatricesByVariant[bucketIndex];
                if (mesh == null || matrices.Count == 0)
                {
                    continue;
                }

                var rp = new RenderParams(_pendingMaterial)
                {
                    worldBounds = _pendingWorldBoundsByVariant[bucketIndex],
                };

                int remaining = matrices.Count;
                int offset = 0;
                while (remaining > 0)
                {
                    int batch = Mathf.Min(remaining, 1023);
                    for (int i = 0; i < batch; i++)
                    {
                        _instanceBuffer[i] = matrices[offset + i];
                    }

                    Graphics.RenderMeshInstanced(rp, mesh, 0, _instanceBuffer, batch);
                    offset += batch;
                    remaining -= batch;
                }
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
            if (config == null || config.Material == null)
            {
                ClearPendingSubmissionState();
                return false;
            }

            var material = ResolveRenderableMaterial(config.Material);
            if (material == null)
            {
                ClearPendingSubmissionState();
                return false;
            }

            // RenderMeshInstanced throws if instancing is disabled on the material.
            // Auto-enable here so third-party materials remain plug-and-play.
            if (!EnsureMaterialInstancing(material))
            {
                ClearPendingSubmissionState();
                return false;
            }

            ResetPendingVariantState();

            var variantCount = CollectVariantMeshes(config);
            if (variantCount == 0)
            {
                ClearPendingSubmissionState();
                return false;
            }

            _pendingMaterial = material;
            _pendingVariantCount = variantCount;
            return true;
        }

        private static Material ResolveRenderableMaterial(Material sourceMaterial)
        {
            if (!UsesErrorShader(sourceMaterial))
            {
                return sourceMaterial;
            }

            if (_runtimeFallbackMaterial == null)
            {
                var fallbackShader = Shader.Find(PreferredFallbackShaderName)
                    ?? Shader.Find("Universal Render Pipeline/Lit")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Unlit/Color")
                    ?? Shader.Find("Sprites/Default");

                if (fallbackShader == null)
                {
                    return null;
                }

                _runtimeFallbackMaterial = new Material(fallbackShader)
                {
                    name = "TreeRuntimeFallbackMaterial",
                };

                if (_runtimeFallbackMaterial.HasProperty(BaseColorId))
                {
                    _runtimeFallbackMaterial.SetColor(BaseColorId, Color.white);
                }

                if (_runtimeFallbackMaterial.HasProperty(CutoffId))
                {
                    _runtimeFallbackMaterial.SetFloat(CutoffId, 0.5f);
                }
            }

            return _runtimeFallbackMaterial;
        }

        private static bool UsesErrorShader(Material material)
        {
            if (material == null)
            {
                return true;
            }

            var shader = material.shader;
            return shader == null || shader.name == ErrorShaderName;
        }

        private static bool EnsureMaterialInstancing(Material material)
        {
            if (material == null)
            {
                return false;
            }

            if (!material.enableInstancing)
            {
                material.enableInstancing = true;
            }

            return material.enableInstancing;
        }

        private static int CollectVariantMeshes(TreeRenderConfig config)
        {
            var variantCount = 0;
            if (config.MeshVariants != null)
            {
                for (int i = 0; i < config.MeshVariants.Length; i++)
                {
                    var mesh = config.MeshVariants[i];
                    if (mesh == null)
                    {
                        continue;
                    }

                    if (variantCount >= MaxTreeVariants)
                    {
                        break;
                    }

                    _pendingMeshesByVariant[variantCount] = mesh;
                    // Far mesh is looked up by SOURCE index i (not the compacted slot) so a
                    // null near entry cannot misalign the near/far pairing.
                    _pendingMeshesByVariant[variantCount + MaxTreeVariants] =
                        GetLodMeshForSourceIndex(config.LodMeshVariants, i);
                    variantCount++;
                }
            }

            if (variantCount == 0 && config.Mesh != null)
            {
                _pendingMeshesByVariant[0] = config.Mesh;
                _pendingMeshesByVariant[MaxTreeVariants] =
                    GetLodMeshForSourceIndex(config.LodMeshVariants, 0);
                variantCount = 1;
            }

            return variantCount;
        }

        private static Mesh GetLodMeshForSourceIndex(Mesh[] lodMeshVariants, int sourceIndex)
        {
            if (lodMeshVariants == null || sourceIndex >= lodMeshVariants.Length)
            {
                return null;
            }

            return lodMeshVariants[sourceIndex];
        }

        /// <summary>
        /// Test-only hook to append a pending matrix after frame prep.
        /// </summary>
        public static void AddPendingMatrixForTests(in Matrix4x4 matrix)
        {
            if (_pendingVariantCount == 0)
            {
                return;
            }

            _pendingMatricesByVariant[0].Add(matrix);
        }

        /// <summary>
        /// Test-only hook to verify stale submission state has been cleared.
        /// </summary>
        public static bool HasPendingSubmissionDataForTests()
        {
            if (_pendingMaterial == null)
            {
                return false;
            }

            for (int i = 0; i < TotalMeshBuckets; i++)
            {
                if (_pendingMeshesByVariant[i] != null && _pendingMatricesByVariant[i].Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Test-only hook to inspect which mesh is registered for a (variant, lodLevel) bucket.
        /// </summary>
        public static Mesh GetPendingMeshForTests(int variantIndex, int lodLevel)
        {
            return _pendingMeshesByVariant[
                SurfaceScatterLodUtility.GetBucketIndex(variantIndex, lodLevel, MaxTreeVariants)];
        }

        private static void ResetPendingVariantState()
        {
            _pendingVariantCount = 0;
            for (int i = 0; i < TotalMeshBuckets; i++)
            {
                _pendingMeshesByVariant[i] = null;
                _pendingMatricesByVariant[i].Clear();
                _pendingWorldBoundsByVariant[i] = _tinyBounds;
            }
        }

        private static void ClearPendingSubmissionState()
        {
            _pendingMaterial = null;
            ResetPendingVariantState();
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

            int variantCount = _pendingVariantCount;

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
                if (lodStateRO.ValueRO.CurrentLod >= CulledScatterLod)
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
                        if (_pendingMeshesByVariant[farBucket] != null)
                        {
                            bucketIndex = farBucket;
                        }
                    }

                    var mesh = _pendingMeshesByVariant[bucketIndex];
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

                    _pendingMatricesByVariant[bucketIndex].Add(Matrix4x4.TRS(
                        groundedPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * config.UniformScale));
                }
            }

            // Per-instance matrices already contain full scale, so use neutral external scale.
            for (int bucketIndex = 0; bucketIndex < TotalMeshBuckets; bucketIndex++)
            {
                var mesh = _pendingMeshesByVariant[bucketIndex];
                if (mesh == null)
                {
                    _pendingWorldBoundsByVariant[bucketIndex] = _tinyBounds;
                    continue;
                }

                if (!TryBuildWorldBounds(
                        _pendingMatricesByVariant[bucketIndex],
                        mesh.bounds,
                        1f,
                        out _pendingWorldBoundsByVariant[bucketIndex]))
                {
                    _pendingWorldBoundsByVariant[bucketIndex] = _tinyBounds;
                }
            }
        }
    }
}
