using Unity.Collections;

namespace DOTS.Terrain.Rocks
{
    /// <summary>
    /// Applies sparse rock-state deltas to regenerated placement records.
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

                RemoveByStableLocalId(ref placements, deltas[i].StableLocalId);
            }

            // RemoveAtSwapBack is order-unstable; re-sort for deterministic record order.
            SortByStableLocalId(ref placements);
        }

        public static bool HidesFullRockVisual(RockStateStage stage)
        {
            return stage == RockStateStage.Depleted;
        }

        private static void RemoveByStableLocalId(
            ref NativeList<RockPlacementRecord> placements,
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

        private static void SortByStableLocalId(ref NativeList<RockPlacementRecord> placements)
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
