using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Tracks active slingshot charge. Added when charge begins, removed on release or cancel.
    /// </summary>
    public struct SlingshotChargeState : IComponentData
    {
        public float ChargeNormalized;   // 0..1
        public float2 DragDelta;         // accumulated mouse drag in pixels
        public float3 AimDirection;      // world-space launch direction (opposite of drag)
        public float ChargeStartTime;    // elapsed time at charge start
    }
}
