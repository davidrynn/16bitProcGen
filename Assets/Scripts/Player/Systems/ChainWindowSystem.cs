using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using DOTS.Player.Components;

namespace DOTS.Player.Systems
{
    /// <summary>
    /// Ticks ChainSlingshotState.WindowRemaining down each frame and resets ChainCount on expiry.
    /// Intentionally does NOT close the window on landing — the grounded transition is transparent
    /// to the chain window so post-landing chains within the remaining window time are allowed.
    /// Mode transitions are owned by SlingshotChargeSystem and SlingshotLaunchSystem.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(MovementStateBookkeepingSystem))]
    public partial struct ChainWindowSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ChainSlingshotState>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            foreach (var chainState in SystemAPI.Query<RefRW<ChainSlingshotState>>())
            {
                if (chainState.ValueRO.WindowRemaining <= 0f) continue;

                float remaining = math.max(0f, chainState.ValueRO.WindowRemaining - dt);
                chainState.ValueRW.WindowRemaining = remaining;
                if (remaining == 0f)
                    chainState.ValueRW.ChainCount = 0;
            }
        }
    }
}
