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

        public static SlingshotConfig Default => new SlingshotConfig
        {
            MaxForce = 55f,
            CurveExponent = 1.8f,
            MaxDragDistance = 300f,
            MinLaunchThreshold = 0.15f,
            CustomGravity = 22f,
            GroundFriction = 0.94f,
            AirControlBallistic = 0.25f
        };
    }
}
