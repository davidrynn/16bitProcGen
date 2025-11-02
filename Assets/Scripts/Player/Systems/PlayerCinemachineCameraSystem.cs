using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Binds authored Cinemachine virtual cameras to the DOTS player anchors so follow/aim targets stay in sync.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct PlayerCinemachineCameraSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCameraLink>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;
            foreach (var (cameraLink, entity) in SystemAPI.Query<RefRO<PlayerCameraLink>>().WithEntityAccess())
            {
                var link = cameraLink.ValueRO;
                if (link.CameraEntity == Entity.Null)
                {
                    continue;
                }

                if (!entityManager.HasComponent<CinemachineCamera>(link.CameraEntity))
                {
                    continue;
                }

                var cinemachineCamera = entityManager.GetComponentObject<CinemachineCamera>(link.CameraEntity);
                if (cinemachineCamera == null)
                {
                    continue;
                }

                Transform followTransform = null;
                if (link.FollowAnchor != Entity.Null && entityManager.HasComponent<Transform>(link.FollowAnchor))
                {
                    followTransform = entityManager.GetComponentObject<Transform>(link.FollowAnchor);
                }

                Transform lookTransform = null;
                if (link.LookAnchor != Entity.Null && entityManager.HasComponent<Transform>(link.LookAnchor))
                {
                    lookTransform = entityManager.GetComponentObject<Transform>(link.LookAnchor);
                }

                if (cinemachineCamera.Follow != followTransform)
                {
                    cinemachineCamera.Follow = followTransform;
                }

                if (cinemachineCamera.LookAt != (lookTransform != null ? lookTransform : followTransform))
                {
                    cinemachineCamera.LookAt = lookTransform != null ? lookTransform : followTransform;
                }
            }
        }
    }
}
