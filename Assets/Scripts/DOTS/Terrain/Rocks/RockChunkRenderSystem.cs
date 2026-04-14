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
        private const int MaxRockVariants = 8;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private const string ErrorShaderName = "Hidden/InternalErrorShader";
        private const string PreferredFallbackShaderName = "DOTS/VertexColorUnlitClip";
        private static readonly Bounds TinyBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);

        private static readonly Matrix4x4[] InstanceBuffer = new Matrix4x4[1023];
        private static readonly List<Matrix4x4>[] PendingMatricesByVariant = CreateMatrixBuckets(MaxRockVariants);
        private static readonly Mesh[] PendingMeshesByVariant = new Mesh[MaxRockVariants];
        private static readonly Bounds[] PendingWorldBoundsByVariant = new Bounds[MaxRockVariants];
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

        private static void SubmitToCamera(ScriptableRenderContext ctx, Camera camera)
        {
            if (_pendingMaterial == null || _pendingVariantCount == 0)
            {
                return;
            }

            for (int variantIndex = 0; variantIndex < _pendingVariantCount; variantIndex++)
            {
                var mesh = PendingMeshesByVariant[variantIndex];
                var matrices = PendingMatricesByVariant[variantIndex];
                if (mesh == null || matrices.Count == 0)
                {
                    continue;
                }

                var rp = new RenderParams(_pendingMaterial)
                {
                    worldBounds = PendingWorldBoundsByVariant[variantIndex],
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

        public static bool TryPrepareSubmissionFrame(RockRenderConfig config)
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
                    name = "RockRuntimeFallbackMaterial",
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

        private static int CollectVariantMeshes(RockRenderConfig config)
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

                    if (variantCount >= MaxRockVariants)
                    {
                        break;
                    }

                    PendingMeshesByVariant[variantCount] = mesh;
                    variantCount++;
                }
            }

            if (variantCount == 0 && config.Mesh != null)
            {
                PendingMeshesByVariant[0] = config.Mesh;
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

            for (int i = 0; i < _pendingVariantCount; i++)
            {
                if (PendingMeshesByVariant[i] != null && PendingMatricesByVariant[i].Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ResetPendingVariantState()
        {
            _pendingVariantCount = 0;
            for (int i = 0; i < MaxRockVariants; i++)
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
            if (!SystemAPI.ManagedAPI.TryGetSingleton<RockRenderConfig>(out var config))
            {
                ClearPendingSubmissionState();
                return;
            }

            if (!TryPrepareSubmissionFrame(config))
            {
                return;
            }

            int variantCount = _pendingVariantCount;

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
                    int variantIndex = variantCount == 1
                        ? 0
                        : record.RockTypeId % variantCount;

                    var mesh = PendingMeshesByVariant[variantIndex];
                    if (mesh == null)
                    {
                        continue;
                    }

                    var scale = math.max(0.01f, record.UniformScale * config.UniformScale);
                    PendingMatricesByVariant[variantIndex].Add(Matrix4x4.TRS(
                        record.WorldPosition,
                        Quaternion.Euler(0f, math.degrees(record.YawRadians), 0f),
                        Vector3.one * scale));
                }
            }

            for (int variantIndex = 0; variantIndex < variantCount; variantIndex++)
            {
                var mesh = PendingMeshesByVariant[variantIndex];
                if (mesh == null)
                {
                    PendingWorldBoundsByVariant[variantIndex] = TinyBounds;
                    continue;
                }

                // Per-instance matrices already contain full scale, so use neutral external scale.
                if (!TryBuildWorldBounds(PendingMatricesByVariant[variantIndex], mesh.bounds, 1f, out PendingWorldBoundsByVariant[variantIndex]))
                {
                    PendingWorldBoundsByVariant[variantIndex] = TinyBounds;
                }
            }
        }
    }
}
