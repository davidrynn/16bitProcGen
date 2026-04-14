using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Writes CameraEffectState on landing: shake impulse and FOV dip proportional
    /// to impact speed. Reads the one-frame LandingImpactEvent.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(LandingDetectionSystem))]
    public partial struct CameraLandingFeedbackSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraEffectConfig>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float time = (float)SystemAPI.Time.ElapsedTime;

            foreach (var (effectState, config, landingEvent) in
                     SystemAPI.Query<RefRW<CameraEffectState>, RefRO<CameraEffectConfig>,
                                     RefRO<LandingImpactEvent>>()
                             .WithAll<LandingImpactEvent>())
            {
                float verticalSpeed = landingEvent.ValueRO.VerticalSpeed;

                // Shake proportional to vertical impact speed, clamped
                float shakeMagnitude = math.min(verticalSpeed * config.ValueRO.LandingShakeScale,
                    config.ValueRO.LandingShakeMax);

                // Additive shake on top of any existing shake
                effectState.ValueRW.ShakeOffset += new float3(
                    math.sin(time * 83.7f) * shakeMagnitude,
                    math.sin(time * 97.3f) * shakeMagnitude * 1.5f, // stronger vertical shake
                    math.sin(time * 71.1f) * shakeMagnitude * 0.5f
                );
                effectState.ValueRW.ShakeDecayRate = 5f; // fast decay for impact shake

                // FOV dip on landing
                effectState.ValueRW.TargetFOV -= config.ValueRO.LandingFOVDip;

                // Camera dip for hard landings
                float cameraDip = math.min(
                    verticalSpeed * config.ValueRO.LandingShakeScale * 8f,
                    config.ValueRO.LandingCameraDipMax);
                effectState.ValueRW.CameraDip = cameraDip;
            }
        }
    }
}
