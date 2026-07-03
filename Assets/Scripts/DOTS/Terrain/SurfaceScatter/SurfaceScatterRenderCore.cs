using System.Collections.Generic;
using UnityEngine;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Per-family mutable render-submission state: pending meshes/matrices/world-bounds keyed
    /// by near/far LOD bucket, plus the resolved material and its runtime fallback.
    ///
    /// <see cref="SurfaceScatterRenderCore"/> is stateless and only ever mutates the instance
    /// passed to it, so each scatter family (trees/rocks/pebbles) must construct and hold its
    /// own instance — sharing one across families would silently merge their draw data.
    /// </summary>
    public sealed class SurfaceScatterRenderState
    {
        /// <summary>RenderMeshInstanced batch limit (Unity API constraint).</summary>
        public const int MaxInstancesPerDrawCall = 1023;

        public readonly int MaxVariants;
        public readonly int TotalMeshBuckets;
        public readonly Matrix4x4[] InstanceBuffer = new Matrix4x4[MaxInstancesPerDrawCall];
        public readonly List<Matrix4x4>[] PendingMatricesByVariant;
        public readonly Mesh[] PendingMeshesByVariant;
        public readonly Bounds[] PendingWorldBoundsByVariant;
        public Material PendingMaterial;
        public Material RuntimeFallbackMaterial;
        public int PendingVariantCount;

        /// <param name="maxVariants">Per-family cap on distinct mesh variants.</param>
        /// <param name="matrixBucketCapacity">
        /// Initial List capacity per bucket — a perf hint only (Lists grow past it), tuned per
        /// family by expected instance density (e.g. pebbles are denser but smaller than trees).
        /// </param>
        public SurfaceScatterRenderState(int maxVariants, int matrixBucketCapacity)
        {
            MaxVariants = maxVariants;
            TotalMeshBuckets = maxVariants * SurfaceScatterLodUtility.LodLevelCount;

            PendingMatricesByVariant = new List<Matrix4x4>[TotalMeshBuckets];
            for (int i = 0; i < TotalMeshBuckets; i++)
            {
                PendingMatricesByVariant[i] = new List<Matrix4x4>(matrixBucketCapacity);
            }

            PendingMeshesByVariant = new Mesh[TotalMeshBuckets];
            PendingWorldBoundsByVariant = new Bounds[TotalMeshBuckets];
        }
    }

    /// <summary>
    /// Shared mechanics for matrix-instanced surface scatter rendering (trees/rocks/pebbles):
    /// near/far mesh bucket bookkeeping, material fallback resolution, and per-camera
    /// Graphics.RenderMeshInstanced submission.
    ///
    /// Pure functions operating on a caller-owned <see cref="SurfaceScatterRenderState"/> —
    /// this class holds no state of its own, so it cannot leak data between families.
    /// Family-specific behavior (variant-index selection from a type ID, per-instance TRS
    /// construction/grounding) intentionally stays in each XChunkRenderSystem's OnUpdate,
    /// since that is where the families' config/record schemas genuinely diverge (e.g. trees
    /// ground by mesh-bottom offset with a fixed config scale; rocks/pebbles apply a per-record
    /// scale with no grounding offset).
    /// </summary>
    public static class SurfaceScatterRenderCore
    {
        /// <summary>LOD bucket fully culled by policy — chunk LOD3+ draws no scatter instances.</summary>
        public const int CulledScatterLod = 3;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private const string ErrorShaderName = "Hidden/InternalErrorShader";
        private const string PreferredFallbackShaderName = "DOTS/VertexColorUnlitClip";
        private static readonly Bounds TinyBounds = new Bounds(Vector3.zero, Vector3.one * 0.01f);

        /// <summary>
        /// Submits all populated buckets in <paramref name="state"/> as instanced draw calls
        /// for the camera currently being rendered. Called from RenderPipelineManager.beginCameraRendering
        /// by each family's wrapper system — see TreeChunkRenderSystem's class summary for why
        /// submission happens there instead of directly in OnUpdate.
        /// </summary>
        public static void SubmitToCamera(SurfaceScatterRenderState state)
        {
            if (state.PendingMaterial == null || state.PendingVariantCount == 0)
            {
                return;
            }

            // Iterate every bucket (near + far blocks); empties are skipped below.
            for (int bucketIndex = 0; bucketIndex < state.TotalMeshBuckets; bucketIndex++)
            {
                var mesh = state.PendingMeshesByVariant[bucketIndex];
                var matrices = state.PendingMatricesByVariant[bucketIndex];
                if (mesh == null || matrices.Count == 0)
                {
                    continue;
                }

                var rp = new RenderParams(state.PendingMaterial)
                {
                    worldBounds = state.PendingWorldBoundsByVariant[bucketIndex],
                };

                int remaining = matrices.Count;
                int offset = 0;
                while (remaining > 0)
                {
                    int batch = Mathf.Min(remaining, SurfaceScatterRenderState.MaxInstancesPerDrawCall);
                    for (int i = 0; i < batch; i++)
                    {
                        state.InstanceBuffer[i] = matrices[offset + i];
                    }

                    Graphics.RenderMeshInstanced(rp, mesh, 0, state.InstanceBuffer, batch);
                    offset += batch;
                    remaining -= batch;
                }
            }
        }

        /// <summary>
        /// Initializes a render-submission frame from raw mesh/material inputs and clears
        /// stale state when the source data is missing/invalid. Callers unwrap their own
        /// TreeRenderConfig/RockRenderConfig/PebbleRenderConfig into these primitives so this
        /// stays independent of any single family's config type.
        /// </summary>
        public static bool TryPrepareSubmissionFrame(
            Mesh[] meshVariants,
            Mesh singleMesh,
            Mesh[] lodMeshVariants,
            Material sourceMaterial,
            string fallbackMaterialName,
            SurfaceScatterRenderState state)
        {
            if (sourceMaterial == null)
            {
                ClearPendingSubmissionState(state);
                return false;
            }

            var material = ResolveRenderableMaterial(sourceMaterial, fallbackMaterialName, state);
            if (material == null)
            {
                ClearPendingSubmissionState(state);
                return false;
            }

            // RenderMeshInstanced throws if instancing is disabled on the material.
            // Auto-enable here so third-party materials remain plug-and-play.
            if (!EnsureMaterialInstancing(material))
            {
                ClearPendingSubmissionState(state);
                return false;
            }

            ResetPendingVariantState(state);

            var variantCount = CollectVariantMeshes(meshVariants, singleMesh, lodMeshVariants, state);
            if (variantCount == 0)
            {
                ClearPendingSubmissionState(state);
                return false;
            }

            state.PendingMaterial = material;
            state.PendingVariantCount = variantCount;
            return true;
        }

        private static Material ResolveRenderableMaterial(
            Material sourceMaterial,
            string fallbackMaterialName,
            SurfaceScatterRenderState state)
        {
            if (!UsesErrorShader(sourceMaterial))
            {
                return sourceMaterial;
            }

            if (state.RuntimeFallbackMaterial == null)
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

                state.RuntimeFallbackMaterial = new Material(fallbackShader)
                {
                    name = fallbackMaterialName,
                };

                if (state.RuntimeFallbackMaterial.HasProperty(BaseColorId))
                {
                    state.RuntimeFallbackMaterial.SetColor(BaseColorId, Color.white);
                }

                if (state.RuntimeFallbackMaterial.HasProperty(CutoffId))
                {
                    state.RuntimeFallbackMaterial.SetFloat(CutoffId, 0.5f);
                }
            }

            return state.RuntimeFallbackMaterial;
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

        private static int CollectVariantMeshes(
            Mesh[] meshVariants,
            Mesh singleMesh,
            Mesh[] lodMeshVariants,
            SurfaceScatterRenderState state)
        {
            var variantCount = 0;
            if (meshVariants != null)
            {
                for (int i = 0; i < meshVariants.Length; i++)
                {
                    var mesh = meshVariants[i];
                    if (mesh == null)
                    {
                        continue;
                    }

                    if (variantCount >= state.MaxVariants)
                    {
                        break;
                    }

                    state.PendingMeshesByVariant[variantCount] = mesh;
                    // Far mesh is looked up by SOURCE index i (not the compacted slot) so a
                    // null near entry cannot misalign the near/far pairing.
                    state.PendingMeshesByVariant[variantCount + state.MaxVariants] =
                        GetLodMeshForSourceIndex(lodMeshVariants, i);
                    variantCount++;
                }
            }

            if (variantCount == 0 && singleMesh != null)
            {
                state.PendingMeshesByVariant[0] = singleMesh;
                state.PendingMeshesByVariant[state.MaxVariants] =
                    GetLodMeshForSourceIndex(lodMeshVariants, 0);
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

        /// <summary>Resets bucket bookkeeping without discarding the resolved material.</summary>
        public static void ResetPendingVariantState(SurfaceScatterRenderState state)
        {
            state.PendingVariantCount = 0;
            for (int i = 0; i < state.TotalMeshBuckets; i++)
            {
                state.PendingMeshesByVariant[i] = null;
                state.PendingMatricesByVariant[i].Clear();
                state.PendingWorldBoundsByVariant[i] = TinyBounds;
            }
        }

        /// <summary>Clears all submission state, including the resolved material — used on config loss.</summary>
        public static void ClearPendingSubmissionState(SurfaceScatterRenderState state)
        {
            state.PendingMaterial = null;
            ResetPendingVariantState(state);
        }

        /// <summary>
        /// Recomputes per-bucket world bounds from whatever matrices were queued this frame.
        /// Per-instance matrices already carry full scale, so this passes a neutral (1f)
        /// uniform scale into the bounds utility — the mesh-local bounds are transformed
        /// per-matrix instead.
        /// </summary>
        public static void RebuildWorldBounds(SurfaceScatterRenderState state)
        {
            for (int bucketIndex = 0; bucketIndex < state.TotalMeshBuckets; bucketIndex++)
            {
                var mesh = state.PendingMeshesByVariant[bucketIndex];
                if (mesh == null)
                {
                    state.PendingWorldBoundsByVariant[bucketIndex] = TinyBounds;
                    continue;
                }

                if (!SurfaceScatterRenderBoundsUtility.TryBuildWorldBounds(
                        state.PendingMatricesByVariant[bucketIndex],
                        mesh.bounds,
                        1f,
                        out state.PendingWorldBoundsByVariant[bucketIndex]))
                {
                    state.PendingWorldBoundsByVariant[bucketIndex] = TinyBounds;
                }
            }
        }

        /// <summary>Test-only hook to append a pending matrix after frame prep.</summary>
        public static void AddPendingMatrixForTests(SurfaceScatterRenderState state, in Matrix4x4 matrix)
        {
            if (state.PendingVariantCount == 0)
            {
                return;
            }

            state.PendingMatricesByVariant[0].Add(matrix);
        }

        /// <summary>Test-only hook to verify stale submission state has been cleared.</summary>
        public static bool HasPendingSubmissionDataForTests(SurfaceScatterRenderState state)
        {
            if (state.PendingMaterial == null)
            {
                return false;
            }

            for (int i = 0; i < state.TotalMeshBuckets; i++)
            {
                if (state.PendingMeshesByVariant[i] != null && state.PendingMatricesByVariant[i].Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Test-only hook to inspect which mesh is registered for a (variant, lodLevel) bucket.</summary>
        public static Mesh GetPendingMeshForTests(SurfaceScatterRenderState state, int variantIndex, int lodLevel)
        {
            return state.PendingMeshesByVariant[
                SurfaceScatterLodUtility.GetBucketIndex(variantIndex, lodLevel, state.MaxVariants)];
        }
    }
}
