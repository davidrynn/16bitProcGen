using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
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
}
