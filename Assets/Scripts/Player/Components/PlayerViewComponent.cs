using Unity.Entities;

namespace DOTS.Player.Components
{
    public struct PlayerViewComponent : IComponentData
    {
        public float YawDegrees;
        public float PitchDegrees;
    }
}
