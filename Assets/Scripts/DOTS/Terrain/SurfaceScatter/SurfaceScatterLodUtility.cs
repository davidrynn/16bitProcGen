using System.Collections.Generic;
using UnityEngine;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Pure distance-LOD selection shared by scatter render systems (trees, rocks).
    /// Selection is stateless and recomputed per instance per frame — no hysteresis is
    /// needed because the RenderMeshInstanced path rebuilds its buckets every frame
    /// anyway, so a boundary re-evaluation has no churn cost (unlike the per-entity
    /// MaterialMeshInfo swaps in RelicLodSelectionSystem).
    /// See Assets/Docs/AI/TerrainHeightMaps/SURFACE_SCATTER_LOD_SPEC.md.
    /// </summary>
    public static class SurfaceScatterLodUtility
    {
        public const int NearLod = 0;
        public const int FarLod = 1;
        public const int LodLevelCount = 2;

        /// <summary>
        /// Selects near/far LOD from squared camera distance. The boundary is exclusive
        /// (exactly at the swap distance stays near); a swap distance of zero or less
        /// disables LOD entirely so unconfigured projects keep current behavior.
        /// </summary>
        public static int SelectLodLevel(float distanceSq, float lodSwapDistance)
        {
            if (lodSwapDistance <= 0f)
            {
                return NearLod;
            }

            return distanceSq > lodSwapDistance * lodSwapDistance ? FarLod : NearLod;
        }

        /// <summary>
        /// Maps (variant, lodLevel) to a flat bucket index: near block [0..maxVariants-1],
        /// far block [maxVariants..2*maxVariants-1].
        /// </summary>
        public static int GetBucketIndex(int variantIndex, int lodLevel, int maxVariants)
        {
            return lodLevel * maxVariants + variantIndex;
        }

        /// <summary>
        /// Builds a far-LOD array index-aligned to <paramref name="nearVariants"/> by pairing
        /// each near mesh with a candidate named "&lt;near mesh name&gt;&lt;farSuffix&gt;".
        /// Existing (manually assigned) entries are never overwritten — automation only fills
        /// empty slots, so authors can always override or opt out per variant by clearing the
        /// candidate from the model file. Pure logic so the pairing contract is testable
        /// without AssetDatabase; editor glue supplies the candidate list.
        /// </summary>
        public static Mesh[] AutoPairFarMeshes(
            Mesh[] nearVariants,
            IReadOnlyList<Mesh> candidates,
            Mesh[] existingLodVariants,
            string farSuffix = "_Far")
        {
            if (nearVariants == null || nearVariants.Length == 0)
            {
                return existingLodVariants;
            }

            var result = new Mesh[nearVariants.Length];
            if (existingLodVariants != null)
            {
                for (int i = 0; i < result.Length && i < existingLodVariants.Length; i++)
                {
                    result[i] = existingLodVariants[i];
                }
            }

            if (candidates == null)
            {
                return result;
            }

            for (int i = 0; i < nearVariants.Length; i++)
            {
                if (result[i] != null || nearVariants[i] == null)
                {
                    continue;
                }

                string wanted = nearVariants[i].name + farSuffix;
                for (int c = 0; c < candidates.Count; c++)
                {
                    Mesh candidate = candidates[c];
                    if (candidate != null && candidate != nearVariants[i] && candidate.name == wanted)
                    {
                        result[i] = candidate;
                        break;
                    }
                }
            }

            return result;
        }
    }
}
