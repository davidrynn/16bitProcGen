using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Writes CameraEffectState based on player velocity during Ballistic, Gliding,
    /// and ThermalBoost states. Handles the launch FOV punch as a decaying impulse
    /// (fast attack, slow decay) and speed-based FOV/distance scaling.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct CameraSpeedFeedbackSystem : ISystem
    {
        /// <summary>
        /// Tracks the decaying FOV punch from a slingshot launch.
        /// Starts at LaunchFOVPunch on launch frame, decays toward zero.
        /// </summary>
        private float _launchFOVPunchRemaining;
        private PlayerMovementMode _previousMode;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<CameraEffectConfig>();
            state.RequireForUpdate<PlayerMovementState>();
            _launchFOVPunchRemaining = 0f;
            _previousMode = PlayerMovementMode.Grounded;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (effectState, config, movementState) in
                     SystemAPI.Query<RefRW<CameraEffectState>, RefRO<CameraEffectConfig>,
                                     RefRO<PlayerMovementState>>())
            {
                var mode = movementState.ValueRO.Mode;
                var velocity = movementState.ValueRO.Velocity;
                float horizontalSpeed = math.length(new float2(velocity.x, velocity.z));

                // Detect launch frame: transition from SlingshotCharging to Ballistic
                if (mode == PlayerMovementMode.Ballistic && _previousMode == PlayerMovementMode.SlingshotCharging)
                {
                    _launchFOVPunchRemaining = config.ValueRO.LaunchFOVPunch;
                }

                // Decay launch punch
                if (_launchFOVPunchRemaining > 0f)
                {
                    _launchFOVPunchRemaining = math.max(0f,
                        _launchFOVPunchRemaining - config.ValueRO.LaunchFOVDecayRate * dt);
                }

                bool isAirborne = mode == PlayerMovementMode.Ballistic ||
                                  mode == PlayerMovementMode.Gliding ||
                                  mode == PlayerMovementMode.GlideCharging ||
                                  mode == PlayerMovementMode.ThermalBoost;

                if (isAirborne)
                {
                    // Speed-based FOV addition
                    float speedAboveThreshold = math.max(0f, horizontalSpeed - config.ValueRO.SpeedFOVThreshold);
                    float speedFOVAdd = math.min(speedAboveThreshold * config.ValueRO.SpeedFOVScale,
                        config.ValueRO.SpeedFOVMax);

                    effectState.ValueRW.TargetFOV = config.ValueRO.BaseFOV + speedFOVAdd + _launchFOVPunchRemaining;

                    // Speed-based distance addition
                    float speedRatio = math.saturate(horizontalSpeed / (config.ValueRO.SpeedFOVThreshold * 2f));
                    effectState.ValueRW.TargetDistance = config.ValueRO.BaseDistance +
                        config.ValueRO.BallisticDistanceAdd * speedRatio;

                    // State-specific damping
                    effectState.ValueRW.Damping = mode switch
                    {
                        PlayerMovementMode.Ballistic => config.ValueRO.BallisticDamping,
                        PlayerMovementMode.Gliding => config.ValueRO.GlideDamping,
                        PlayerMovementMode.ThermalBoost => config.ValueRO.ThermalDamping,
                        _ => config.ValueRO.BallisticDamping
                    };
                }

                _previousMode = mode;
            }
        }
    }
}
