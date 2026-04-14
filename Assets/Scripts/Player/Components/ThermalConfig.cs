using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants for thermal column mechanics, set once at bootstrap.
    /// </summary>
    public struct ThermalConfig : IComponentData
    {
        public float VerticalBoostAcceleration;       // 15 — m/s² upward
        public float MaxUpwardVelocity;               // 12 — velocity.y clamp
        public float HorizontalVelocityMultiplier;    // 0.97 — slight horizontal reduction

        public static ThermalConfig Default => new ThermalConfig
        {
            VerticalBoostAcceleration = 15f,
            MaxUpwardVelocity = 12f,
            HorizontalVelocityMultiplier = 0.97f
        };
    }
}
