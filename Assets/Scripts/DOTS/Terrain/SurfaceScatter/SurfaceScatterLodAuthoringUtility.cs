#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Editor-only authoring helper that auto-fills far-LOD mesh arrays on scatter
    /// bootstraps by pairing each near mesh with the "&lt;name&gt;_Far" sub-asset from the
    /// same model file (e.g. Boulder_01 → Boulder_01_Far inside Boulders.fbx).
    /// Wrapped in UNITY_EDITOR because candidate discovery needs AssetDatabase — at
    /// runtime unreferenced sub-assets are not addressable, which is why the pairing
    /// cannot happen in the bootstrap's Start path.
    /// Pairing rules live in <see cref="SurfaceScatterLodUtility.AutoPairFarMeshes"/>.
    /// </summary>
    public static class SurfaceScatterLodAuthoringUtility
    {
        /// <summary>
        /// Fills empty slots of <paramref name="lodVariants"/> from "_Far" sibling
        /// sub-assets of the near meshes. Returns true when the array changed so the
        /// caller knows to mark the component dirty. Manual entries are preserved.
        /// </summary>
        public static bool TryAutoPair(Mesh[] nearVariants, ref Mesh[] lodVariants, string farSuffix = "_Far")
        {
            if (nearVariants == null || nearVariants.Length == 0)
            {
                return false;
            }

            var candidates = new List<Mesh>();
            var seenPaths = new HashSet<string>();
            foreach (Mesh near in nearVariants)
            {
                if (near == null)
                {
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(near);
                if (string.IsNullOrEmpty(path) || !seenPaths.Add(path))
                {
                    continue;
                }

                foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (asset is Mesh mesh)
                    {
                        candidates.Add(mesh);
                    }
                }
            }

            Mesh[] paired = SurfaceScatterLodUtility.AutoPairFarMeshes(nearVariants, candidates, lodVariants, farSuffix);
            if (ArraysEqual(paired, lodVariants))
            {
                return false;
            }

            lodVariants = paired;
            return true;
        }

        private static bool ArraysEqual(Mesh[] a, Mesh[] b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a == null || b == null || a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
