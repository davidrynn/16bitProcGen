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

        /// <summary>
        /// Optional far-LOD meshes, parallel BY SOURCE INDEX to MeshVariants (a far mesh
        /// follows its near mesh through null-entry compaction). Null entry → that variant
        /// never swaps. In the legacy single-mesh path, entry 0 is the far mesh.
        /// </summary>
        public Mesh[] LodMeshVariants;

        public Material Material;
        public float UniformScale = 1f;

        /// <summary>
        /// Camera distance beyond which far-LOD meshes are drawn. Zero or less disables
        /// the swap entirely (default), preserving pre-LOD behavior.
        /// </summary>
        public float LodSwapDistance;
    }
}
