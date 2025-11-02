using Unity.Cinemachine;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Player.Test
{
    /// <summary>
    /// Ensures that CinemachineCamera components in subscenes properly follow their DOTS entity targets.
    /// Updates the Follow target of virtual cameras to point to the companion GameObject of the entity.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(CameraOscillationCompanionSyncSystem))]
    public partial struct VirtualCameraFollowSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var entityManager = state.EntityManager;

            foreach (var (cameraLink, targetEntity) in
                     SystemAPI.Query<RefRO<VirtualCameraLink>>()
                              .WithEntityAccess()
                              .WithAll<CameraOscillationTag>())
            {
                if (cameraLink.ValueRO.CameraEntity == Entity.Null)
                    continue;

                // Get the companion Transform of the target entity (the oscillating anchor)
                if (!entityManager.HasComponent<Transform>(targetEntity))
                    continue;
                    
                var targetTransform = entityManager.GetComponentObject<Transform>(targetEntity);
                if (targetTransform == null)
                    continue;

                // Get the CinemachineCamera component from the camera entity
                if (!entityManager.HasComponent<CinemachineCamera>(cameraLink.ValueRO.CameraEntity))
                    continue;
                    
                var virtualCamera = entityManager.GetComponentObject<CinemachineCamera>(cameraLink.ValueRO.CameraEntity);
                if (virtualCamera == null)
                    continue;

                // Update the follow target if it's not already set correctly
                if (virtualCamera.Follow != targetTransform)
                {
                    virtualCamera.Follow = targetTransform;
                    virtualCamera.LookAt = targetTransform;
                    Debug.Log($"VirtualCameraFollowSystem: Set {virtualCamera.name} to follow {targetTransform.name}");
                }
            }
        }
    }
}




