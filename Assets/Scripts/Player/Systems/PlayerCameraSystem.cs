using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

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
            foreach (var (view, transform, cameraLink) in
                     SystemAPI.Query<RefRO<PlayerViewComponent>, RefRO<LocalTransform>, RefRO<PlayerCameraLink>>())
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

                // Calculate camera world position and rotation
                float3 cameraOffset = new float3(0f, 1.6f, 0f); // Eye height
                float3 cameraPosition = transform.ValueRO.Position + cameraOffset;
                
                // Combine player yaw rotation with camera pitch rotation
                quaternion playerRotation = quaternion.AxisAngle(math.up(), math.radians(view.ValueRO.YawDegrees));
                quaternion pitchRotation = quaternion.AxisAngle(math.right(), math.radians(view.ValueRO.PitchDegrees));
                quaternion combinedRotation = math.mul(playerRotation, pitchRotation);
                
                // Update entity LocalTransform (for ECS consistency)
                if (SystemAPI.HasComponent<LocalTransform>(cameraLink.ValueRO.CameraEntity))
                {
                    var cameraTransform = SystemAPI.GetComponentRW<LocalTransform>(cameraLink.ValueRO.CameraEntity);
                    cameraTransform.ValueRW = LocalTransform.FromPositionRotation(cameraPosition, combinedRotation);
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
