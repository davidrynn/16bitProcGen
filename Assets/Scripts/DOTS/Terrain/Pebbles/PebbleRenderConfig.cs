using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.Pebbles
{
    /// <summary>
    /// Managed singleton holding meshes/material used to render pebble-cluster instances.
    /// Mirrors RockRenderConfig's contract (incl. the SURFACE_SCATTER_LOD_SPEC far-LOD
    /// fields), though per TICKETS B2 the pebble family ships without far meshes —
    /// clusters cull at distance via chunk LOD instead of swapping.
    /// </summary>
    public class PebbleRenderConfig : IComponentData
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
        /// never swaps.
        /// </summary>
        public Mesh[] LodMeshVariants;

        public Material Material;

        public float UniformScale = 1f;

        /// <summary>
        /// Camera distance beyond which far-LOD meshes are drawn. Zero or less disables the swap.
        /// </summary>
        public float LodSwapDistance;
    }
}
