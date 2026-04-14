using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants for landing detection and feedback, set once at bootstrap.
    /// </summary>
    public struct LandingConfig : IComponentData
    {
        public float SlideThresholdHorizontalSpeed;   // 8 — above this, landing is a slide
        public float HardLandingVerticalSpeed;        // 12 — above this, hard landing effects trigger
        public float DustBurstMinSpeed;               // 5 — minimum speed for dust particles
        public float DustBurstMaxRadius;              // 3.0 — dust radius at terminal velocity
        public float LandingMomentumPreservation;     // 0.94 — velocity.xz *= this on landing

        public static LandingConfig Default => new LandingConfig
        {
            SlideThresholdHorizontalSpeed = 8f,
            HardLandingVerticalSpeed = 12f,
            DustBurstMinSpeed = 5f,
            DustBurstMaxRadius = 3.0f,
            LandingMomentumPreservation = 0.94f
        };
    }
}
