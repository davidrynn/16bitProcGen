using Unity.Entities;

namespace DOTS.Impostors
{
    /// <summary>
    /// Marker tag identifying the single ground-plane impostor entity.
    /// Used by <see cref="GroundPlaneImpostorSystem"/> to locate the entity
    /// each frame without an archetype search.
    /// </summary>
    public struct GroundPlaneImpostorTag : IComponentData { }
}
