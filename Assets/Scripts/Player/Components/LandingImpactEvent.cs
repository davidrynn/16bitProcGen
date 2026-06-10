using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Fires on the frame the player transitions from airborne to grounded.
    /// Consumed by camera and VFX systems, then disabled.
    /// Uses IEnableableComponent so it can be toggled without structural changes.
    /// </summary>
    public struct LandingImpactEvent : IComponentData, IEnableableComponent
    {
        public float VerticalSpeed;     // abs(velocity.y) at impact
        public float HorizontalSpeed;   // horizontal speed at impact
        public float GroundContactY;    // world-Y of entity position on the contact frame; used by PlayerVisualSync floor clamp
    }
}
