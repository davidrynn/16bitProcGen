using Unity.Entities;

namespace DOTS.Player.Components
{
    public struct PlayerCameraLink : IComponentData
    {
        public Entity CameraEntity;
        public Entity FollowAnchor;  // Entity with Transform to follow
        public Entity LookAnchor;     // Entity with Transform to look at
    }
}
