using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Structures
{
    /// <summary>
    /// Advances every realized relic's <see cref="RelicSpawnFade"/> from 0 to 1
    /// after spawn (R6 P4, LANDMARK_DRAW_DISTANCE_SPEC.md §P4) so relics
    /// realizing inside the view dither in over ~<see cref="SpawnFadeSeconds"/>
    /// instead of popping. Fades ALL realizations, not just in-frustum ones —
    /// an off-screen fade completes unseen, which is visually identical to the
    /// spec's in-frustum wording and needs no camera test.
    ///
    /// [DisableAutoCreation]: created by DotsSystemBootstrap under the same
    /// EnableRelicRealizationSystem flag as the system that spawns the
    /// component — one flag, one feature.
    /// </summary>
    [DisableAutoCreation]
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(RelicRealizationSystem))]
    public partial struct RelicSpawnFadeSystem : ISystem
    {
        /// <summary>
        /// Fade duration. A behavior constant from the spec (~0.5s), not scene
        /// tuning — promote to RelicRenderConfig only if per-scene variation is
        /// ever wanted.
        /// </summary>
        public const float SpawnFadeSeconds = 0.5f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<RelicSpawnFade>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var fade in SystemAPI.Query<RefRW<RelicSpawnFade>>())
            {
                // Finished fades stay at 1 with a cheap read-only skip — with a
                // handful of relics in range, removing the component (a
                // structural change) costs more than it saves.
                if (fade.ValueRO.Value >= 1f)
                    continue;

                fade.ValueRW.Value = Advance(fade.ValueRO.Value, deltaTime);
            }
        }

        /// <summary>
        /// Pure fade step, static so the progression is unit-testable without a
        /// World (same pattern as RelicRealizationSystem.TemplateParticipatesInLod).
        /// </summary>
        public static float Advance(float current, float deltaTime)
        {
            return math.min(1f, current + deltaTime / SpawnFadeSeconds);
        }
    }
}
