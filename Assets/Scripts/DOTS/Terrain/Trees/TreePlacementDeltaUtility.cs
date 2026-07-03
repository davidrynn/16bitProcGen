using Unity.Collections;
using DOTS.Terrain.SurfaceScatter;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Applies sparse tree-state deltas to regenerated placement records.
    /// Remove/sort mechanics live in <see cref="SurfaceScatterDeltaUtility"/> (plan C3);
    /// only the tree-specific "which stage hides the full visual" predicate stays here.
    /// </summary>
    public static class TreePlacementDeltaUtility
    {
        public static void ApplyStateDeltas(
            ref NativeList<TreePlacementRecord> placements,
            NativeArray<TreeStateDelta> deltas)
        {
            if (!deltas.IsCreated || deltas.Length == 0 || placements.Length == 0)
            {
                return;
            }

            for (int i = 0; i < deltas.Length; i++)
            {
                if (!HidesFullTreeVisual(deltas[i].Stage))
                {
                    continue;
                }

                SurfaceScatterDeltaUtility.RemoveByStableLocalId(ref placements, deltas[i].StableLocalId);
            }

            // RemoveAtSwapBack is order-unstable; re-sort for deterministic record order.
            SurfaceScatterDeltaUtility.SortByStableLocalId(ref placements);
        }

        public static bool HidesFullTreeVisual(TreeStateStage stage)
        {
            return stage == TreeStateStage.Stump
                || stage == TreeStateStage.Sapling
                || stage == TreeStateStage.Growing;
        }
    }
}