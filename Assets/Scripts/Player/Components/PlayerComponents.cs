using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
    public enum PlayerMovementMode : byte
    {
        Grounded = 0,
        SlingshotCharging = 1,
        Ballistic = 2,
        GlideCharging = 3,
        Gliding = 4,
        ThermalBoost = 5,
        Grappling = 6        // Layer 2, post-MVP
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
        /// <summary>
        /// True while space is physically held. Used by GlideSystem for charge timing.
        /// Unlike JumpPressed (one-frame event consumed by jump), this persists while held.
        /// </summary>
        public bool JumpHeld;

        // Slingshot input: LMB + RMB both held
        public bool SlingshotHeld;
        // Accumulated mouse delta during slingshot charge
        public float2 SlingshotDrag;
        // One-frame release event when both buttons released during charge
        public bool SlingshotReleased;

        /// <summary>
        /// When true, mouse buttons route to terrain editing (LMB=subtract, RMB=add).
        /// When false, mouse buttons route to traversal (LMB+RMB=slingshot).
        /// Toggled by Tab key.
        /// </summary>
        public bool IsEditMode;
    }

    public struct PlayerMovementState : IComponentData
    {
        public PlayerMovementMode Mode;
        public bool IsGrounded;
        public float FallTime;
        public float3 PreviousPosition;
        /// <summary>
        /// Cached copy of PhysicsVelocity.Linear, written each frame by MovementStateBookkeepingSystem.
        /// Allows camera feedback systems to read speed without requiring PhysicsVelocity access.
        /// </summary>
        public float3 Velocity;
    }

    public struct PlayerViewComponent : IComponentData
    {
        public float YawDegrees;
        public float PitchDegrees;
    }

    public struct PlayerCameraLink : IComponentData
    {
        public Entity CameraEntity;
        public Entity FollowAnchor;  // Entity with Transform to follow
        public Entity LookAnchor;     // Entity with Transform to look at
    }

    public struct PlayerCameraSettings : IComponentData
    {
        public float3 FirstPersonOffset;
        public float3 ThirdPersonPivotOffset;
        public float ThirdPersonDistance;
        public bool IsThirdPerson;
    }

    public struct PlayerStartupReadinessGate : IComponentData
    {
        // Negative means the gate has not started tracking elapsed time yet.
        public double StartTime;
        public float TimeoutSeconds;
        public float ProbeDistance;
        public float ReleasedGravityFactor;
    }
}
