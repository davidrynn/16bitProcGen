using Unity.Collections;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Applies sparse rock-state deltas to regenerated placement records.
    /// Remove/sort mechanics live in <see cref="SurfaceScatterDeltaUtility"/> (plan C3);
    /// only the rock-specific "which stage hides the full visual" predicate stays here.
    /// </summary>
    public static class RockPlacementDeltaUtility
    {
        public static void ApplyStateDeltas(
            ref NativeList<RockPlacementRecord> placements,
            NativeArray<RockStateDelta> deltas)
        {
            if (!deltas.IsCreated || deltas.Length == 0 || placements.Length == 0)
            {
                return;
            }

            for (int i = 0; i < deltas.Length; i++)
            {
                if (!HidesFullRockVisual(deltas[i].Stage))
                {
                    continue;
                }

                SurfaceScatterDeltaUtility.RemoveByStableLocalId(ref placements, deltas[i].StableLocalId);
            }

            // RemoveAtSwapBack is order-unstable; re-sort for deterministic record order.
            SurfaceScatterDeltaUtility.SortByStableLocalId(ref placements);
        }

        public static bool HidesFullRockVisual(RockStateStage stage)
        {
            return stage == RockStateStage.Depleted;
        }
    }
}
