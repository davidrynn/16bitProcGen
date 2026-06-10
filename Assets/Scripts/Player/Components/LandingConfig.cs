using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants for landing detection and feedback, set once at bootstrap.
    /// </summary>
    public struct LandingConfig : IComponentData
    {
        public float SlideThresholdHorizontalSpeed;   // 8 — above this, landing is a slide
        public float StandardLandingVerticalSpeed;    // 6 — above this, standard recovery plays
        public float HardLandingVerticalSpeed;        // 12 — above this, hard landing effects trigger
        public float DustBurstMinSpeed;               // 5 — minimum speed for dust particles
        public float DustBurstMaxRadius;              // 3.0 — dust radius at terminal velocity
        public float LandingMomentumPreservation;     // 0.94 — velocity.xz *= this on landing
        public float LightLandingRecoveryDuration;    // 0.0 — no recovery lock below standard threshold
        public float StandardLandingRecoveryDuration; // 0.25 — crouch-stand recovery
        public float HardLandingRecoveryDuration;     // 0.5 — stagger/slide recovery (hard and slide tiers share this)
        /// <summary>
        /// When true, all landings fire LandingTrigger regardless of speed tier.
        /// Set false only after the animator controller has dedicated states for
        /// StandardLandingTrigger, HardLandingTrigger, and SlideLandingTrigger.
        /// </summary>
        public bool UseSimpleLandingTrigger;

        public static LandingConfig Default => new LandingConfig
        {
            SlideThresholdHorizontalSpeed = 8f,
            StandardLandingVerticalSpeed = 6f,
            HardLandingVerticalSpeed = 12f,
            DustBurstMinSpeed = 5f,
            DustBurstMaxRadius = 3.0f,
            LandingMomentumPreservation = 0.94f,
            LightLandingRecoveryDuration    = 0.0f,
            StandardLandingRecoveryDuration = 0.25f,
            HardLandingRecoveryDuration     = 0.5f,
            UseSimpleLandingTrigger         = true,
        };
    }
}
