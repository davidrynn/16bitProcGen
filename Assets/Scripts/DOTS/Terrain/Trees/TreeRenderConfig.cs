using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Singleton managed component holding the mesh and material used to render all
    /// MVP plains trees via instanced draw calls. Not Burst-compatible by design —
    /// rendering is handled in a managed SystemBase.
    /// </summary>
    public class TreeRenderConfig : IComponentData
    {
        public Mesh     Mesh;
        public Material Material;
        /// <summary>Applied to all instances — tune in the TreeVisualBootstrap inspector for visual size.</summary>
        public float    UniformScale;
    }
}
