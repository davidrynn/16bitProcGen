using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Caches PhysicsVelocity.Linear into PlayerMovementState.Velocity and resets
    /// CameraEffectState to config defaults each frame. Runs first in SimulationSystemGroup
    /// so downstream feedback systems operate on fresh velocity and a clean camera slate.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    public partial struct MovementStateBookkeepingSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerMovementState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (movementState, velocity) in
                     SystemAPI.Query<RefRW<PlayerMovementState>, RefRO<PhysicsVelocity>>())
            {
                movementState.ValueRW.Velocity = velocity.ValueRO.Linear;
            }

            // Reset CameraEffectState to config defaults so each feedback system
            // writes only the fields it owns on top of a clean slate.
            foreach (var (effectState, config) in
                     SystemAPI.Query<RefRW<CameraEffectState>, RefRO<CameraEffectConfig>>())
            {
                effectState.ValueRW = new CameraEffectState
                {
                    TargetFOV = config.ValueRO.BaseFOV,
                    TargetDistance = config.ValueRO.BaseDistance,
                    PositionOffset = float3.zero,
                    ShakeOffset = float3.zero,
                    ShakeDecayRate = 0f,
                    Damping = config.ValueRO.GroundedDamping,
                    RotationDamping = 16f,
                    HorizonStabilize = false,
                    CameraDip = 0f
                };
            }
        }
    }
}
