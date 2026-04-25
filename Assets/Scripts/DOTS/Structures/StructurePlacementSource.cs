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
    }
}
