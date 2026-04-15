using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using DOTS.Player.Components;
using DOTS.Terrain.Core;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Applies the slingshot launch impulse when the player releases the charge.
    /// Reads SlingshotChargeState and SlingshotReleased, writes to PhysicsVelocity,
    /// transitions Mode to Ballistic, and removes the charge state.
    /// </summary>
    /// <remarks>
    /// Not Burst-compiled because it uses DebugSettings string logging on the launch frame.
    /// Launch fires infrequently so managed overhead is negligible.
    /// </remarks>
    [DisableAutoCreation]
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(SlingshotChargeSystem))]
    [UpdateBefore(typeof(PlayerMovementSystem))]
    public partial struct SlingshotLaunchSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SlingshotConfig>();
            state.RequireForUpdate<SlingshotChargeState>();
        }

        /// <remarks>WithoutBurst: uses DebugSettings string logging on the infrequent launch frame.</remarks>
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (chargeState, movementState, velocity, entity) in
                     SystemAPI.Query<RefRO<SlingshotChargeState>,
                                     RefRW<PlayerMovementState>,
                                     RefRW<PhysicsVelocity>>()
                             .WithAll<PlayerInputComponent, SlingshotConfig>()
                             .WithEntityAccess())
            {
                var input = SystemAPI.GetComponentRO<PlayerInputComponent>(entity);
                var slingshotConfig = SystemAPI.GetComponentRO<SlingshotConfig>(entity);

                if (!input.ValueRO.SlingshotReleased)
                    continue;

                float charge = chargeState.ValueRO.ChargeNormalized;
                float3 aimDirection = chargeState.ValueRO.AimDirection;

                if (charge >= slingshotConfig.ValueRO.MinLaunchThreshold)
                {
                    // Apply launch impulse
                    float impulseStrength = slingshotConfig.ValueRO.MaxForce *
                                            math.pow(charge, slingshotConfig.ValueRO.CurveExponent);
                    float3 impulse = aimDirection * impulseStrength;

                    velocity.ValueRW.Linear = impulse;
                    movementState.ValueRW.Mode = PlayerMovementMode.Ballistic;
                    movementState.ValueRW.IsGrounded = false;

                    DebugSettings.LogPlayer(
                        $"Slingshot launched: charge={charge}, impulse={impulseStrength}, " +
                        $"dir={aimDirection}, vel={impulse}");
                }
                else
                {
                    // Below threshold: cancel, no velocity change
                    movementState.ValueRW.Mode = PlayerMovementMode.Grounded;
                }

                // Always remove charge state on release
                ecb.RemoveComponent<SlingshotChargeState>(entity);
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
