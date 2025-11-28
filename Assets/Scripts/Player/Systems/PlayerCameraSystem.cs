using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Positions the camera based on player position and view state.
    /// Runs in PresentationSystemGroup after all physics/movement to minimize jitter.
    /// Note: Cannot use [BurstCompile] because we access managed UnityEngine.Camera component.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial struct PlayerCameraSystem : ISystem
    {
        private bool _hasLoggedOnce;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerCameraLink>();
            _hasLoggedOnce = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            // Query for players with camera links
            foreach (var (view, transform, cameraLink, entity) in
                     SystemAPI.Query<RefRO<PlayerViewComponent>, RefRO<LocalTransform>, RefRO<PlayerCameraLink>>().WithEntityAccess())
            {
                if (cameraLink.ValueRO.CameraEntity == Entity.Null)
                {
                    if (!_hasLoggedOnce)
                    {
                        Debug.LogWarning("[PlayerCamera] Camera entity is null!");
                        _hasLoggedOnce = true;
                    }
                    continue;
                }

                var cameraSettings = new PlayerCameraSettings
                {
                    FirstPersonOffset = new float3(0f, 1.6f, 0f),
                    ThirdPersonPivotOffset = new float3(0f, 1.5f, 0f),
                    ThirdPersonDistance = 3.5f,
                    IsThirdPerson = false
                };

                if (SystemAPI.HasComponent<PlayerCameraSettings>(entity))
                {
                    cameraSettings = SystemAPI.GetComponent<PlayerCameraSettings>(entity);
                }

                float3 cameraOffset = cameraSettings.FirstPersonOffset;

                float cameraScale = 1f;
                if (SystemAPI.HasComponent<LocalTransform>(cameraLink.ValueRO.CameraEntity))
                {
                    var existingCameraTransform = SystemAPI.GetComponent<LocalTransform>(cameraLink.ValueRO.CameraEntity);
                    if (math.lengthsq(cameraOffset) < math.EPSILON)
                    {
                        cameraOffset = existingCameraTransform.Position - transform.ValueRO.Position;
                    }
                    cameraScale = existingCameraTransform.Scale;
                }

                float3 cameraPosition;
                if (cameraSettings.IsThirdPerson)
                {
                    // TODO: Implement dedicated third-person placement (orbit distance, collision handling, etc.).
                    cameraPosition = transform.ValueRO.Position + cameraSettings.FirstPersonOffset;
                }
                else
                {
                    if (math.lengthsq(cameraOffset) < math.EPSILON)
                    {
                        cameraOffset = new float3(0f, 1.6f, 0f);
                    }

                    cameraPosition = transform.ValueRO.Position + cameraOffset;
                }
                
                // Combine player yaw rotation with camera pitch rotation
                quaternion playerRotation = quaternion.AxisAngle(math.up(), math.radians(view.ValueRO.YawDegrees));
                quaternion pitchRotation = quaternion.AxisAngle(math.right(), math.radians(view.ValueRO.PitchDegrees));
                quaternion combinedRotation = math.mul(playerRotation, pitchRotation);
                
                // Update entity LocalTransform (for ECS consistency)
                if (SystemAPI.HasComponent<LocalTransform>(cameraLink.ValueRO.CameraEntity))
                {
                    var cameraTransform = SystemAPI.GetComponentRW<LocalTransform>(cameraLink.ValueRO.CameraEntity);
                    cameraTransform.ValueRW = LocalTransform.FromPositionRotationScale(cameraPosition, combinedRotation, cameraScale);
                }

                // CRITICAL: Update the managed Camera GameObject transform directly
                // This is necessary because DOTS doesn't automatically sync entity transforms back to GameObjects
                if (SystemAPI.ManagedAPI.HasComponent<UnityEngine.Camera>(cameraLink.ValueRO.CameraEntity))
                {
                    var camera = SystemAPI.ManagedAPI.GetComponent<UnityEngine.Camera>(cameraLink.ValueRO.CameraEntity);
                    if (camera != null)
                    {
                        camera.transform.SetPositionAndRotation(cameraPosition, combinedRotation);
                        
                        if (!_hasLoggedOnce)
                        {
                            Debug.Log($"[PlayerCamera] Camera updated: pos={cameraPosition}, yaw={view.ValueRO.YawDegrees:F1}, pitch={view.ValueRO.PitchDegrees:F1}");
                            _hasLoggedOnce = true;
                        }
                    }
                }
                else if (!_hasLoggedOnce)
                {
                    Debug.LogWarning($"[PlayerCamera] Camera entity {cameraLink.ValueRO.CameraEntity} doesn't have UnityEngine.Camera component!");
                    _hasLoggedOnce = true;
                }
            }
        }
    }
}
