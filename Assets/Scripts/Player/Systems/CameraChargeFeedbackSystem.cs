using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Writes CameraEffectState during slingshot charge: camera pull-back, FOV reduction,
    /// and shake scaled proportional to ChargeNormalized.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct CameraChargeFeedbackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraEffectConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (effectState, config, chargeState) in
                     SystemAPI.Query<RefRW<CameraEffectState>, RefRO<CameraEffectConfig>,
                                     RefRO<SlingshotChargeState>>())
            {
                float charge = chargeState.ValueRO.ChargeNormalized;

                // Camera pulls back proportional to charge
                effectState.ValueRW.TargetDistance = config.ValueRO.BaseDistance +
                    config.ValueRO.ChargeDistanceAdd * charge;

                // FOV narrows during charge to create tunnel tension
                effectState.ValueRW.TargetFOV = config.ValueRO.BaseFOV -
                    config.ValueRO.ChargeFOVReduce * charge;

                // Shake ramps with charge using noise for organic feel
                float shakeAmount = math.lerp(config.ValueRO.ChargeShakeMin,
                    config.ValueRO.ChargeShakeMax, charge);
                // Simple pseudo-random shake using sine waves at different frequencies
                float3 shake = new float3(
                    math.sin(time * 47.3f) * shakeAmount,
                    math.sin(time * 61.7f) * shakeAmount,
                    math.sin(time * 53.1f) * shakeAmount * 0.5f
                );
                effectState.ValueRW.ShakeOffset = shake;

                effectState.ValueRW.Damping = config.ValueRO.ChargeDamping;
            }
        }
    }
}
