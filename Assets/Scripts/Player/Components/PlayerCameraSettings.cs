using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Player.Components
{
    public struct PlayerCameraSettings : IComponentData
    {
        public float3 FirstPersonOffset;
        public float3 ThirdPersonPivotOffset;
        public float ThirdPersonDistance;
        public bool IsThirdPerson;
    }
}
