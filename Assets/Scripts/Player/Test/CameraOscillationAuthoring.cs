using Unity.Entities;
using Unity.Cinemachine;
using UnityEngine;

namespace DOTS.Player.Test
{
    public class CameraOscillationAuthoring : MonoBehaviour
    {
        [Range(0f, 5f)] public float amplitude = 2f;
        [Range(0f, 5f)] public float frequency = 0.5f;
        
        [Tooltip("Optional: CinemachineCamera on another GameObject in the subscene that should follow this")]
        public CinemachineCamera virtualCamera;

        private class Baker : Baker<CameraOscillationAuthoring>
        {
            public override void Bake(CameraOscillationAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<CameraOscillationTag>(entity);
                AddComponent(entity, new CameraOscillationSettings
                {
                    Amplitude = authoring.amplitude,
                    Frequency = authoring.frequency
                });
                
                // If a virtual camera is assigned, link it
                if (authoring.virtualCamera != null)
                {
                    var cameraEntity = GetEntity(authoring.virtualCamera, TransformUsageFlags.Dynamic);
                    AddComponent(entity, new VirtualCameraLink
                    {
                        CameraEntity = cameraEntity
                    });
                }
            }
        }
    }
}