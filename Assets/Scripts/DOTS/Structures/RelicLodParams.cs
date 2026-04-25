using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Structures
{
    /// <summary>
    /// Per-entity LOD parameters set once at spawn time by
    /// <see cref="RelicRealizationSystem"/>. Decouples
    /// <see cref="RelicLodSelectionSystem"/> from the template registry so
    /// the LOD system reads only per-entity data — no lookups needed.
    /// </summary>
    public struct RelicLodParams : IComponentData
    {
        /// <summary>LocalTransform.Scale when at LOD 0 (full mesh).</summary>
        public float FullScale;

        /// <summary>LocalTransform.Scale when at LOD 1 (impostor).</summary>
        public float ImpostorScale;

        /// <summary>RenderBounds.Value when at LOD 0.</summary>
        public AABB FullBoundsLocal;

        /// <summary>RenderBounds.Value when at LOD 1.</summary>
        public AABB ImpostorBoundsLocal;
    }
}
