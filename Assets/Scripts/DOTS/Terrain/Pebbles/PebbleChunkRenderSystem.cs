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
    /// </summary>
    // SystemBase (not ISystem) is required: RenderPipelineManager.beginCameraRendering subscription
    // needs managed OnCreate/OnDestroy lifecycle. Mirrors TreeChunkRenderSystem/RockChunkRenderSystem.
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PebbleChunkRenderSystem : SystemBase
    {
        private const int CulledScatterLod = 3;
        private const int MaxPebbleVariants = 8;

        // Bucket layout: near meshes [0..MaxPebbleVariants-1], far-LOD meshes
        // [MaxPebbleVariants..TotalMeshBuckets-1]. See SURFACE_SCATTER_LOD_SPEC.md.
        private const int TotalMeshBuckets = MaxPebbleVariants * SurfaceScatterLodUtility.LodLevelCount;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private const string ErrorShaderName = "Hidden/InternalErrorShader";
        private const string PreferredFallbackShaderName = "DOTS/VertexColorUnlitClip";
        private static readonly Bounds TinyBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);

        private static readonly Matrix4x4[] InstanceBuffer = new Matrix4x4[1023];
        private static readonly List<Matrix4x4>[] PendingMatricesByVariant = CreateMatrixBuckets(TotalMeshBuckets);
        private static readonly Mesh[] PendingMeshesByVariant = new Mesh[TotalMeshBuckets];
        private static readonly Bounds[] PendingWorldBoundsByVariant = new Bounds[TotalMeshBuckets];
        private static Material _pendingMaterial;
        private static Material _runtimeFallbackMaterial;
        private static int _pendingVariantCount;

        private static List<Matrix4x4>[] CreateMatrixBuckets(int count)
        {
            var buckets = new List<Matrix4x4>[count];
            for (int i = 0; i < count; i++)
            {
                buckets[i] = new List<Matrix4x4>(256);
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

        private static void SubmitToCamera(ScriptableRenderContext ctx, Camera camera)
        {
            if (_pendingMaterial == null || _pendingVariantCount == 0)
            {
                return;
            }

            for (int bucketIndex = 0; bucketIndex < TotalMeshBuckets; bucketIndex++)
            {
                var mesh = PendingMeshesByVariant[bucketIndex];
                var matrices = PendingMatricesByVariant[bucketIndex];
                if (mesh == null || matrices.Count == 0)
                {
                    continue;
                }

                var rp = new RenderParams(_pendingMaterial)
                {
                    worldBounds = PendingWorldBoundsByVariant[bucketIndex],
                };

                int remaining = matrices.Count;
                int offset = 0;
                while (remaining > 0)
                {
                    int batch = Mathf.Min(remaining, 1023);
                    for (int i = 0; i < batch; i++)
                    {
                        InstanceBuffer[i] = matrices[offset + i];
                    }

                    Graphics.RenderMeshInstanced(rp, mesh, 0, InstanceBuffer, batch);
                    offset += batch;
                    remaining -= batch;
                }
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

        public static bool TryPrepareSubmissionFrame(PebbleRenderConfig config)
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
                    name = "PebbleRuntimeFallbackMaterial",
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

        private static int CollectVariantMeshes(PebbleRenderConfig config)
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

                    if (variantCount >= MaxPebbleVariants)
                    {
                        break;
                    }

                    PendingMeshesByVariant[variantCount] = mesh;
                    // Far mesh is looked up by SOURCE index i (not the compacted slot) so a
                    // null near entry cannot misalign the near/far pairing.
                    PendingMeshesByVariant[variantCount + MaxPebbleVariants] =
                        GetLodMeshForSourceIndex(config.LodMeshVariants, i);
                    variantCount++;
                }
            }

            if (variantCount == 0 && config.Mesh != null)
            {
                PendingMeshesByVariant[0] = config.Mesh;
                PendingMeshesByVariant[MaxPebbleVariants] =
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

            PendingMatricesByVariant[0].Add(matrix);
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
                if (PendingMeshesByVariant[i] != null && PendingMatricesByVariant[i].Count > 0)
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
            return PendingMeshesByVariant[
                SurfaceScatterLodUtility.GetBucketIndex(variantIndex, lodLevel, MaxPebbleVariants)];
        }

        private static void ResetPendingVariantState()
        {
            _pendingVariantCount = 0;
            for (int i = 0; i < TotalMeshBuckets; i++)
            {
                PendingMeshesByVariant[i] = null;
                PendingMatricesByVariant[i].Clear();
                PendingWorldBoundsByVariant[i] = TinyBounds;
            }
        }

        private static void ClearPendingSubmissionState()
        {
            _pendingMaterial = null;
            ResetPendingVariantState();
        }

        protected override void OnUpdate()
        {
            if (!SystemAPI.ManagedAPI.TryGetSingleton<PebbleRenderConfig>(out var config))
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

            foreach (var (lodStateRO, records) in SystemAPI.Query<RefRO<TerrainChunkLodState>, DynamicBuffer<PebblePlacementRecord>>()
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
                        : record.PebbleTypeId % variantCount;

                    int bucketIndex = variantIndex;
                    if (lodActive
                        && SurfaceScatterLodUtility.SelectLodLevel(
                            math.distancesq(record.WorldPosition, cameraPosition),
                            config.LodSwapDistance) == SurfaceScatterLodUtility.FarLod)
                    {
                        // Variants without an authored far mesh fall back to near.
                        int farBucket = variantIndex + MaxPebbleVariants;
                        if (PendingMeshesByVariant[farBucket] != null)
                        {
                            bucketIndex = farBucket;
                        }
                    }

                    var mesh = PendingMeshesByVariant[bucketIndex];
                    if (mesh == null)
                    {
                        continue;
                    }

                    var scale = math.max(0.01f, record.UniformScale * config.UniformScale);
                    PendingMatricesByVariant[bucketIndex].Add(Matrix4x4.TRS(
                        record.WorldPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * scale));
                }
            }

            for (int bucketIndex = 0; bucketIndex < TotalMeshBuckets; bucketIndex++)
            {
                var mesh = PendingMeshesByVariant[bucketIndex];
                if (mesh == null)
                {
                    PendingWorldBoundsByVariant[bucketIndex] = TinyBounds;
                    continue;
                }

                // Per-instance matrices already contain full scale, so use neutral external scale.
                if (!TryBuildWorldBounds(PendingMatricesByVariant[bucketIndex], mesh.bounds, 1f, out PendingWorldBoundsByVariant[bucketIndex]))
                {
                    PendingWorldBoundsByVariant[bucketIndex] = TinyBounds;
                }
            }
        }
    }
}
