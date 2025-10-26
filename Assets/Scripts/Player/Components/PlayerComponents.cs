using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player
{
    public enum PlayerMovementMode : byte
    {
        Ground = 0,
        Slingshot = 1,
        Swim = 2,
        ZeroG = 3
    }

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

    public struct PlayerInputComponent : IComponentData
    {
        public float2 Move;
        public float2 Look;
        public bool JumpPressed;
    }

    public struct PlayerMovementState : IComponentData
    {
        public PlayerMovementMode Mode;
        public bool IsGrounded;
        public float FallTime;
    }

    public struct PlayerViewComponent : IComponentData
    {
        public float YawDegrees;
        public float PitchDegrees;
    }

    public struct PlayerCameraLink : IComponentData
    {
        public Entity CameraEntity;
    }

    public struct PlayerCameraSettings : IComponentData
    {
        public float3 Offset;
    }
}
