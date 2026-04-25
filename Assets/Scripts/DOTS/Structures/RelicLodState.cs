using Unity.Entities;

namespace DOTS.Structures
{
    /// <summary>
    /// Per-relic LOD state tracked by <see cref="RelicLodSelectionSystem"/>.
    /// <c>0</c> = full mesh (near), <c>1</c> = impostor (far).
    /// Stored as a byte so it is cheap to add to every realized relic entity.
    /// Kept as a separate component (rather than, say, repurposing a tag) so
    /// selection can read/write without forcing a structural change.
    /// </summary>
    public struct RelicLodState : IComponentData
    {
        public byte CurrentLod;
    }
}
