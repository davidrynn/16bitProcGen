using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Singleton managed component holding the meshes and material used to render
    /// plains trees via instanced draw calls. Not Burst-compatible by design —
    /// rendering is handled in a managed SystemBase.
    /// </summary>
    public class TreeRenderConfig : IComponentData
    {
        /// <summary>
        /// Preferred multi-variant mesh list. Non-null entries are used as variant IDs 0..N-1.
        /// </summary>
        public Mesh[]   MeshVariants;

        /// <summary>
        /// Legacy single-mesh fallback used when MeshVariants is empty.
        /// </summary>
        public Mesh     Mesh;

        /// <summary>
        /// Optional far-LOD meshes, parallel BY SOURCE INDEX to MeshVariants (a far mesh
        /// follows its near mesh through null-entry compaction). Null entry → that variant
        /// never swaps. In the legacy single-mesh path, entry 0 is the far mesh.
        /// </summary>
        public Mesh[]   LodMeshVariants;

        public Material Material;
        /// <summary>Applied to all instances — tune in the TreeVisualBootstrap inspector for visual size.</summary>
        public float    UniformScale;

        /// <summary>
        /// Camera distance beyond which far-LOD meshes are drawn. Zero or less disables
        /// the swap entirely (default), preserving pre-LOD behavior.
        /// </summary>
        public float    LodSwapDistance;
    }
}
