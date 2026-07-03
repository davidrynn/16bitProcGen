using Unity.Collections;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Family-agnostic delta-application mechanics shared by tree/rock placement
    /// (cleanup round 1, plan row C3 — the per-family utilities were identical except
    /// for the "which stage hides the visual" predicate, which stays family-side).
    /// Generic over unmanaged records: constrained struct calls, Burst-compatible.
    /// </summary>
    public static class SurfaceScatterDeltaUtility
    {
        /// <summary>Removes the first record matching the stable candidate-slot id.</summary>
        public static void RemoveByStableLocalId<TRecord>(
            ref NativeList<TRecord> placements,
            ushort stableLocalId)
            where TRecord : unmanaged, IStableLocalIdRecord
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

        /// <summary>
        /// Insertion sort by StableLocalId. RemoveAtSwapBack is order-unstable;
        /// re-sorting restores deterministic record order.
        /// </summary>
        public static void SortByStableLocalId<TRecord>(ref NativeList<TRecord> placements)
            where TRecord : unmanaged, IStableLocalIdRecord
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
