using Unity.Collections;

namespace DOTS.Terrain.Trees
{
    /// <summary>
    /// Applies sparse tree-state deltas to regenerated placement records.
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

                RemoveByStableLocalId(ref placements, deltas[i].StableLocalId);
            }

            SortByStableLocalId(ref placements);
        }

        public static bool HidesFullTreeVisual(TreeStateStage stage)
        {
            return stage == TreeStateStage.Stump
                || stage == TreeStateStage.Sapling
                || stage == TreeStateStage.Growing;
        }

        private static void RemoveByStableLocalId(
            ref NativeList<TreePlacementRecord> placements,
            ushort stableLocalId)
        {
            for (int i = 0; i < placements.Length; i++)
            {
                if (placements[i].StableLocalId != stableLocalId)
                {
                    continue;
                }

                placements.RemoveAtSwapBack(i);
                return;
            }
        }

        private static void SortByStableLocalId(ref NativeList<TreePlacementRecord> placements)
        {
            for (int i = 1; i < placements.Length; i++)
            {
                var current = placements[i];
                var insertIndex = i - 1;

                while (insertIndex >= 0 && placements[insertIndex].StableLocalId > current.StableLocalId)
                {
                    placements[insertIndex + 1] = placements[insertIndex];
                    insertIndex--;
                }

                placements[insertIndex + 1] = current;
            }
        }
    }
}