using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Opens the post-landing chain window the first frame IsGrounded becomes true after a
    /// slingshot launch, then ticks the window down and resets ChainCount on expiry.
    /// Tracks IsGrounded directly rather than relying on LandingImpactEvent / Mode == Grounded,
    /// because PlayerGroundingSystem preserves Mode = Ballistic while speed > 2 m/s even after
    /// ground contact — using Mode would delay the window opening by several seconds.
    /// Mode transitions are owned by SlingshotChargeSystem and SlingshotLaunchSystem.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct ChainWindowSystem : ISystem
    {
        private bool _wasGroundedLastFrame;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChainSlingshotState>();
            state.RequireForUpdate<SlingshotConfig>();
            _wasGroundedLastFrame = false;
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;

            foreach (var (chainState, movementState, entity) in
                     SystemAPI.Query<RefRW<ChainSlingshotState>, RefRO<PlayerMovementState>>()
                             .WithAll<SlingshotConfig>()
                             .WithEntityAccess())
            {
                var config = SystemAPI.GetComponentRO<SlingshotConfig>(entity);
                bool isGrounded = movementState.ValueRO.IsGrounded;

                // Detect the first frame of ground contact after a launch.
                bool justLanded = isGrounded && !_wasGroundedLastFrame;
                _wasGroundedLastFrame = isGrounded;

                if (justLanded && chainState.ValueRO.ChainCount > 0 && chainState.ValueRO.WindowRemaining == 0f)
                {
                    chainState.ValueRW.WindowRemaining = config.ValueRO.ChainWindowDuration;
                    continue;
                }

                if (chainState.ValueRO.WindowRemaining <= 0f) continue;

                float remaining = math.max(0f, chainState.ValueRO.WindowRemaining - dt);
                chainState.ValueRW.WindowRemaining = remaining;
                if (remaining == 0f)
                    chainState.ValueRW.ChainCount = 0;
            }
        }
    }
}
