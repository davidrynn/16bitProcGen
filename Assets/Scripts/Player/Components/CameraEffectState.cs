using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
    /// <summary>
    /// Single source of truth for all camera effect parameters.
    /// Written by multiple movement-feedback systems, consumed by CameraEffectResolverSystem.
    /// Reset to defaults each frame by MovementStateBookkeepingSystem.
    /// </summary>
    public struct CameraEffectState : IComponentData
    {
        public float TargetFOV;
        public float TargetDistance;       // third-person orbit distance
        public float3 PositionOffset;     // dolly / pull-back offset
        public float3 ShakeOffset;        // additive screen shake
        public float ShakeDecayRate;      // how fast shake returns to zero
        public float Damping;             // position smoothing rate
        public float RotationDamping;     // rotation smoothing rate
        public bool HorizonStabilize;     // pitch drifts toward horizon
        public float CameraDip;           // vertical dip (landing impact)
    }
}
