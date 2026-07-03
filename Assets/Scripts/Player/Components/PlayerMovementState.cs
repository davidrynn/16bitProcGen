using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
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
        /// <summary>
        /// Counts down from LandingRecoveryDuration to 0 after a landing. While > 0, jump and
        /// slingshot charge are suppressed, and the animator plays the landing recovery clip.
        /// </summary>
        public float LandingRecoveryTime;
        /// <summary>
        /// Original duration for the current landing tier. Stored alongside LandingRecoveryTime
        /// so LandingRecoveryNormalized = LandingRecoveryTime / LandingRecoveryDuration can be
        /// computed without ambiguity.
        /// </summary>
        public float LandingRecoveryDuration;
        /// <summary>
        /// True when the current recovery is a momentum slide (high horizontal speed at impact).
        /// Used by PlayerAnimatorBridge to restrict the velocity-facing yaw override to slide
        /// recoveries only — hard landings face the camera direction instead.
        /// </summary>
        public bool LandingIsSlide;
    }
}
