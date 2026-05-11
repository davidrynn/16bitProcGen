using Unity.Entities;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tunable constants for slingshot mechanics, set once at bootstrap.
    /// </summary>
    public struct SlingshotConfig : IComponentData
    {
        public float MaxForce;              // 55
        public float CurveExponent;         // 1.8
        public float MaxDragDistance;       // 300 pixels
        public float MinLaunchThreshold;   // 0.15
        public float CustomGravity;        // 22
        public float GroundFriction;       // 0.94
        public float AirControlBallistic;  // 0.25

        // ── Chain slingshot ──
        /// <summary>Seconds the chain window stays open after each launch.</summary>
        public float ChainWindowDuration;
        /// <summary>Fraction of existing velocity preserved when a chained launch fires.
        /// velocity_out = velocity_existing * this + newImpulse.</summary>
        public float ChainVelocityPreservation;
        /// <summary>Additional impulse multiplier added per chain level.</summary>
        public float ChainImpulseMultiplierStep;
        /// <summary>Max chain levels that grant a bonus (chains beyond this still work but gain no extra multiplier).</summary>
        public int ChainMaxCount;

        public static SlingshotConfig Default => new SlingshotConfig
        {
            MaxForce = 55f,
            CurveExponent = 1.8f,
            MaxDragDistance = 300f,
            MinLaunchThreshold = 0.15f,
            CustomGravity = 22f,
            GroundFriction = 0.94f,
            AirControlBallistic = 0.25f,
            ChainWindowDuration = 2.0f,
            ChainVelocityPreservation = 0.85f,
            ChainImpulseMultiplierStep = 0.25f,
            ChainMaxCount = 3,
        };
    }
}
