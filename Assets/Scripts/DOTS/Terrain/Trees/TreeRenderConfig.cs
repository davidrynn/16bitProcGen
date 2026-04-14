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
        public Material Material;
        /// <summary>Applied to all instances — tune in the TreeVisualBootstrap inspector for visual size.</summary>
        public float    UniformScale;
    }
}
