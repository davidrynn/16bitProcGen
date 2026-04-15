using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;
using DOTS.Terrain.Core;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// The sole system that writes to the camera. Reads CameraEffectState (written by
    /// feedback systems) and applies smoothed third-person orbit positioning, FOV,
    /// shake, camera dip, and horizon stabilization to both the ECS entity and the
    /// managed Camera GameObject.
    /// </summary>
    /// <remarks>
    /// Replaces PlayerCameraSystem as the primary camera driver. Runs last in
    /// PresentationSystemGroup to consume all feedback system outputs.
    /// Cannot use [BurstCompile] because it accesses managed Camera component.
    /// </remarks>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PresentationSystemGroup), OrderLast = true)]
    public partial struct CameraEffectResolverSystem : ISystem
    {
        private float _currentFOV;
        private float _currentDistance;
        private float _currentDip;
        private float3 _currentShake;
        private float _currentPitchOffset; // for horizon stabilization
        private bool _wasHorizonStabilizing; // tracks whether stabilization was active last frame
        private bool _initialized;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraEffectState>();
            state.RequireForUpdate<PlayerViewComponent>();
            _initialized = false;
        }

        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            if (dt <= 0f) return;

            foreach (var (effectState, effectConfig, view, playerTransform, cameraLink, cameraSettings, entity) in
                     SystemAPI.Query<RefRO<CameraEffectState>, RefRO<CameraEffectConfig>,
                                     RefRO<PlayerViewComponent>, RefRO<LocalTransform>,
                                     RefRO<PlayerCameraLink>, RefRO<PlayerCameraSettings>>()
                             .WithEntityAccess())
            {
                var cameraEntity = cameraLink.ValueRO.CameraEntity;
                if (cameraEntity == Entity.Null) continue;
                if (!SystemAPI.HasComponent<LocalTransform>(cameraEntity)) continue;

                var es = effectState.ValueRO;
                var cfg = effectConfig.ValueRO;

                // Initialize smoothed values on first frame
                if (!_initialized)
                {
                    _currentFOV = es.TargetFOV;
                    _currentDistance = es.TargetDistance;
                    _currentDip = 0f;
                    _currentShake = float3.zero;
                    _currentPitchOffset = 0f;
                    _initialized = true;
                }

                // Smooth FOV toward target using exponential smoothing
                float fovSmooth = math.saturate(8f * dt);
                _currentFOV = math.lerp(_currentFOV, es.TargetFOV, fovSmooth);

                // Smooth distance
                float distSmooth = math.saturate(es.Damping * dt);
                _currentDistance = math.lerp(_currentDistance, es.TargetDistance, distSmooth);

                // Decay shake
                if (es.ShakeDecayRate > 0f)
                {
                    float shakeDecay = math.saturate(es.ShakeDecayRate * dt);
                    _currentShake = math.lerp(_currentShake, es.ShakeOffset, shakeDecay);
                }
                else
                {
                    _currentShake = es.ShakeOffset;
                }

                // Decay camera dip
                float dipDecay = math.saturate(5f * dt);
                _currentDip = math.lerp(_currentDip, es.CameraDip, 0.5f);
                _currentDip *= (1f - dipDecay); // fade toward zero

                // Horizon stabilization: gradually blend effective pitch toward horizon (0°).
                // On first frame of stabilization, seed the offset from the player's current
                // pitch so the lerp-to-zero actually has something to blend away.
                if (es.HorizonStabilize)
                {
                    if (!_wasHorizonStabilizing)
                    {
                        // Entering stabilization: offset cancels current pitch
                        _currentPitchOffset = -view.ValueRO.PitchDegrees;
                        _wasHorizonStabilizing = true;
                    }
                    float horizonRate = math.saturate(0.8f * dt);
                    _currentPitchOffset = math.lerp(_currentPitchOffset, 0f, horizonRate);
                }
                else
                {
                    _currentPitchOffset = 0f;
                    _wasHorizonStabilizing = false;
                }

                float3 playerPos = playerTransform.ValueRO.Position;
                float yaw = math.radians(view.ValueRO.YawDegrees);
                float pitch = math.radians(view.ValueRO.PitchDegrees + _currentPitchOffset);

                float3 cameraPos;
                quaternion cameraRotation;

                if (cameraSettings.ValueRO.IsThirdPerson)
                {
                    // Third-person orbit: camera behind and above player
                    float3 pivotPos = playerPos + cfg.BasePivotOffset;
                    float3 orbitOffset = new float3(
                        -math.sin(yaw) * math.cos(pitch),
                        math.sin(pitch),
                        -math.cos(yaw) * math.cos(pitch)
                    ) * _currentDistance;

                    cameraPos = pivotPos + orbitOffset + es.PositionOffset;
                    cameraPos += _currentShake;
                    cameraPos.y -= _currentDip;

                    float3 lookDir = math.normalizesafe(pivotPos - cameraPos);
                    cameraRotation = lookDir.Equals(float3.zero)
                        ? quaternion.identity
                        : quaternion.LookRotation(lookDir, math.up());
                }
                else
                {
                    // First-person: camera at player head, looking where player looks
                    cameraPos = playerPos + cameraSettings.ValueRO.FirstPersonOffset + es.PositionOffset;
                    cameraPos += _currentShake;
                    cameraPos.y -= _currentDip;

                    float3 forward = new float3(
                        math.sin(yaw) * math.cos(pitch),
                        -math.sin(pitch),
                        math.cos(yaw) * math.cos(pitch)
                    );
                    cameraRotation = quaternion.LookRotation(math.normalizesafe(forward), math.up());
                }

                // Write to ECS camera entity
                var camTransformRW = SystemAPI.GetComponentRW<LocalTransform>(cameraEntity);
                camTransformRW.ValueRW = LocalTransform.FromPositionRotationScale(
                    cameraPos, cameraRotation, camTransformRW.ValueRO.Scale);

                // Write to managed Camera GameObject (for rendering and FOV)
                if (SystemAPI.ManagedAPI.HasComponent<Camera>(cameraEntity))
                {
                    var camera = SystemAPI.ManagedAPI.GetComponent<Camera>(cameraEntity);
                    if (camera != null)
                    {
                        camera.transform.SetPositionAndRotation(cameraPos, cameraRotation);
                        camera.fieldOfView = _currentFOV;
                    }
                }
            }
        }
    }
}
