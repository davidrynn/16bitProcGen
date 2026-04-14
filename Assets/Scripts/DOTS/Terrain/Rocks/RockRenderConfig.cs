using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Managed singleton holding meshes/material used to render far rock instances.
    /// Rendering stays in managed presentation systems by design.
    /// </summary>
    public class RockRenderConfig : IComponentData
    {
        /// <summary>
        /// Preferred multi-variant mesh list. Non-null entries are used as variant IDs 0..N-1.
        /// </summary>
        public Mesh[] MeshVariants;

        /// <summary>
        /// Legacy single-mesh fallback used when MeshVariants is empty.
        /// </summary>
        public Mesh Mesh;
        public Material Material;
        public float UniformScale = 1f;
    }
}
