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

        // RenderMeshInstanced batch limit is 1023.
        private static readonly Matrix4x4[] _instanceBuffer = new Matrix4x4[1023];

        // Matrices collected each OnUpdate, flushed per-camera in SubmitToCamera.
        // Static so the camera callback (a static delegate) can access without a closure.
        private static readonly List<Matrix4x4>[] _pendingMatricesByVariant = CreateMatrixBuckets(MaxTreeVariants);
        private static readonly Mesh[] _pendingMeshesByVariant = new Mesh[MaxTreeVariants];
        private static readonly Bounds[] _pendingWorldBoundsByVariant = new Bounds[MaxTreeVariants];
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

            for (int variantIndex = 0; variantIndex < _pendingVariantCount; variantIndex++)
            {
                var mesh = _pendingMeshesByVariant[variantIndex];
                var matrices = _pendingMatricesByVariant[variantIndex];
                if (mesh == null || matrices.Count == 0)
                {
                    continue;
                }

                var rp = new RenderParams(_pendingMaterial)
                {
                    worldBounds = _pendingWorldBoundsByVariant[variantIndex],
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
                    variantCount++;
                }
            }

            if (variantCount == 0 && config.Mesh != null)
            {
                _pendingMeshesByVariant[0] = config.Mesh;
                variantCount = 1;
            }

            return variantCount;
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

            for (int i = 0; i < _pendingVariantCount; i++)
            {
                if (_pendingMeshesByVariant[i] != null && _pendingMatricesByVariant[i].Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResetPendingVariantState()
        {
            _pendingVariantCount = 0;
            for (int i = 0; i < MaxTreeVariants; i++)
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

                    var mesh = _pendingMeshesByVariant[variantIndex];
                    if (mesh == null)
                    {
                        continue;
                    }

                    // Ground instances by mesh bottom instead of pivot so centered-pivot assets
                    // (common in third-party packs) do not render half-buried as canopy domes.
                    var yOffset = -mesh.bounds.min.y * config.UniformScale;
                    var groundedPosition = record.WorldPosition + new float3(0f, yOffset, 0f);

                    _pendingMatricesByVariant[variantIndex].Add(Matrix4x4.TRS(
                        groundedPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * config.UniformScale));
                }
            }

            // Per-instance matrices already contain full scale, so use neutral external scale.
            for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
            {
                var mesh = _pendingMeshesByVariant[variantIndex];
                if (mesh == null)
                {
                    _pendingWorldBoundsByVariant[variantIndex] = _tinyBounds;
                    continue;
                }

                if (!TryBuildWorldBounds(
                        _pendingMatricesByVariant[variantIndex],
                        mesh.bounds,
                        1f,
                        out _pendingWorldBoundsByVariant[variantIndex]))
                {
                    _pendingWorldBoundsByVariant[variantIndex] = _tinyBounds;
                }
            }
        }
    }
}
