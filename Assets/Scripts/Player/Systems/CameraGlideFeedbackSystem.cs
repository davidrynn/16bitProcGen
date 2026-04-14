using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Writes CameraEffectState during glide: calm FOV, tight damping, horizon stabilization.
    /// The resolver uses HorizonStabilize to gradually blend camera pitch toward horizon.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct CameraGlideFeedbackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraEffectConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (effectState, config, movementState, glideState) in
                     SystemAPI.Query<RefRW<CameraEffectState>, RefRO<CameraEffectConfig>,
                                     RefRO<PlayerMovementState>, RefRO<GlideState>>())
            {
                if (movementState.ValueRO.Mode != PlayerMovementMode.Gliding)
                    continue;

                effectState.ValueRW.TargetFOV = config.ValueRO.BaseFOV + config.ValueRO.GlideFOVAdd;
                effectState.ValueRW.TargetDistance = config.ValueRO.BaseDistance + config.ValueRO.GlideDistanceAdd;
                effectState.ValueRW.Damping = config.ValueRO.GlideDamping;
                effectState.ValueRW.HorizonStabilize = true;
            }
        }
    }
}
