using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Player.Systems
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateAfter(typeof(PlayerInputSystem))]
    public partial struct PlayerCameraSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerViewComponent>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach (var (view, input, config, transform, cameraLink) in
                     SystemAPI.Query<RefRW<PlayerViewComponent>, RefRW<PlayerInputComponent>, RefRO<PlayerMovementConfig>, RefRW<LocalTransform>, RefRO<PlayerCameraLink>>())
            {
                float2 lookDelta = input.ValueRO.Look * config.ValueRO.MouseSensitivity;
                view.ValueRW.YawDegrees += lookDelta.x;
                view.ValueRW.PitchDegrees = math.clamp(view.ValueRO.PitchDegrees - lookDelta.y, -config.ValueRO.MaxPitchDegrees, config.ValueRO.MaxPitchDegrees);

                transform.ValueRW = transform.ValueRO.WithRotation(quaternion.AxisAngle(math.up(), math.radians(view.ValueRO.YawDegrees)));

                if (cameraLink.ValueRO.CameraEntity != Entity.Null && SystemAPI.HasComponent<LocalTransform>(cameraLink.ValueRO.CameraEntity))
                {
                    var cameraTransform = SystemAPI.GetComponentRW<LocalTransform>(cameraLink.ValueRO.CameraEntity);
                    cameraTransform.ValueRW = cameraTransform.ValueRO.WithRotation(quaternion.AxisAngle(math.right(), math.radians(view.ValueRO.PitchDegrees)));
                }

                input.ValueRW.Look = float2.zero;
            }
        }
    }
}
