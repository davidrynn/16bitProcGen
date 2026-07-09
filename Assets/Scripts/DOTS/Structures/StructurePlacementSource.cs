namespace DOTS.Structures
{
    /// <summary>
    /// How a structure anchor was created. Shared with persistence layer
    /// (PERSISTENCE_SPEC.md Layer 2). Determines save/load behavior:
    /// SeededAnchor regenerates from seed, PlayerBuilt always persists.
    /// </summary>
    public enum StructurePlacementSource : byte
    {
        SeededAnchor = 0,
        WFC = 1,
        PlayerBuilt = 2,

        /// <summary>
        /// Explicit developer-authored placement (STRUCTURE_PLACEMENT_SPEC.md §9.5).
        /// Regenerates from authoring data like SeededAnchor; identity is
        /// seed-independent (hash of the authored string id).
        /// </summary>
        Authored = 3,
    }
}
