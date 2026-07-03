using Unity.Entities;

namespace DOTS.Player.Components
{
    public struct PlayerMovementConfig : IComponentData
    {
        public float GroundSpeed;
        public float JumpImpulse;
        public float AirControl;
        public float SlingshotImpulse;
        public float SwimSpeed;
        public float ZeroGDamping;
        public float MouseSensitivity;
        public float MaxPitchDegrees;
        public float GroundProbeDistance;
    }
}
