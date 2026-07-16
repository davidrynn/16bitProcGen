using Unity.Entities;
#if UNITY_ENTITIES_GRAPHICS
using Unity.Rendering;
#endif

namespace DOTS.Structures
{
    /// <summary>
    /// Per-instance spawn fade-in progress for a realized relic (R6 P4,
    /// LANDMARK_DRAW_DISTANCE_SPEC.md §P4). 0 = just spawned (fully dithered
    /// out), 1 = fully visible. Written by <see cref="RelicSpawnFadeSystem"/>;
    /// consumed by RelicLit.shader as the BRG instanced property
    /// <c>_RelicSpawnFade</c>, where it dithers alongside the landmark edge
    /// fade so realization inside the view reads as a ~0.5s dissolve-in
    /// instead of a pop. The shader's material default is 1, so relics
    /// without this component (or non-ECS uses of RelicLit) render solid.
    /// </summary>
#if UNITY_ENTITIES_GRAPHICS
    [MaterialProperty("_RelicSpawnFade")]
#endif
    public struct RelicSpawnFade : IComponentData
    {
        public float Value;
    }
}
