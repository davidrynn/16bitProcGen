using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using DOTS.Player.Components;
using DOTS.Core;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Applies the slingshot launch impulse when the player releases the charge.
    /// Reads SlingshotChargeState and SlingshotReleased, writes to PhysicsVelocity,
    /// transitions Mode to Ballistic, and removes the charge state.
    /// Chain launches (ChainCount > 0) use an additive velocity formula with a bonus multiplier.
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
                    float impulseStrength = slingshotConfig.ValueRO.MaxForce *
                                            math.pow(charge, slingshotConfig.ValueRO.CurveExponent);

                    bool hasChain = SystemAPI.HasComponent<ChainSlingshotState>(entity);
                    var chainState = hasChain
                        ? SystemAPI.GetComponent<ChainSlingshotState>(entity)
                        : default;

                    if (hasChain && chainState.ChainCount > 0)
                    {
                        // Chained launch: preserve existing velocity and add boosted impulse.
                        // chainBonus caps at ChainMaxCount levels so escalation stays bounded.
                        float chainBonus = 1f + math.min(chainState.ChainCount, slingshotConfig.ValueRO.ChainMaxCount)
                                               * slingshotConfig.ValueRO.ChainImpulseMultiplierStep;
                        velocity.ValueRW.Linear = velocity.ValueRW.Linear * slingshotConfig.ValueRO.ChainVelocityPreservation
                                                 + aimDirection * impulseStrength * chainBonus;
                        DebugSettings.LogPlayer(
                            $"Chain launch x{chainState.ChainCount + 1}: bonus={chainBonus:F2}, " +
                            $"impulse={impulseStrength:F1}, vel={velocity.ValueRW.Linear}");
                    }
                    else
                    {
                        velocity.ValueRW.Linear = aimDirection * impulseStrength;
                        DebugSettings.LogPlayer(
                            $"Slingshot launched: charge={charge:F2}, impulse={impulseStrength:F1}, " +
                            $"dir={aimDirection}, vel={velocity.ValueRW.Linear}");
                    }

                    // Increment chain count. Window opens on landing (ChainWindowSystem reads
                    // LandingImpactEvent and sets WindowRemaining then, not here).
                    if (hasChain)
                    {
                        ecb.SetComponent(entity, new ChainSlingshotState
                        {
                            ChainCount = chainState.ChainCount + 1,
                            WindowRemaining = 0f,
                        });
                    }

                    movementState.ValueRW.Mode = PlayerMovementMode.Ballistic;
                    movementState.ValueRW.IsGrounded = false;
                    // Seed fall time beyond the ground-control grace window so launch
                    // momentum is not immediately damped by grounded smoothing.
                    movementState.ValueRW.FallTime = 0.2f;
                }
                else
                {
                    // Below threshold: cancel, no velocity change. Not a chain launch, window unchanged.
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
