using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Processes mouse look input and updates player yaw/pitch.
    /// Runs in SimulationSystemGroup BEFORE movement so the rotation is ready for movement calculations.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct PlayerLookSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerViewComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (view, input, config, transform) in
                     SystemAPI.Query<RefRW<PlayerViewComponent>, RefRW<PlayerInputComponent>, RefRO<PlayerMovementConfig>, RefRW<LocalTransform>>())
            {
                // Update yaw and pitch from look input
                float2 lookDelta = input.ValueRO.Look * config.ValueRO.MouseSensitivity;
                view.ValueRW.YawDegrees += lookDelta.x;
                view.ValueRW.PitchDegrees = math.clamp(
                    view.ValueRO.PitchDegrees - lookDelta.y, 
                    -config.ValueRO.MaxPitchDegrees, 
                    config.ValueRO.MaxPitchDegrees
                );

                // Update player rotation (yaw only - horizontal turning)
                // This rotation is used by movement system to determine forward/right directions
                quaternion yawRotation = quaternion.AxisAngle(math.up(), math.radians(view.ValueRO.YawDegrees));
                transform.ValueRW.Rotation = yawRotation;

                // Clear look input after processing
                input.ValueRW.Look = float2.zero;
            }
        }
    }
}

