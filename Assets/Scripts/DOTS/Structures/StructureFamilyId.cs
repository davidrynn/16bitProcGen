namespace DOTS.Structures
{
    /// <summary>
    /// Identifies a structure family. Each family has its own placement rules
    /// and realization path (e.g. WFC for dungeons, single prefab for relics).
    /// Stored as byte for Burst-safe ECS usage.
    /// </summary>
    public enum StructureFamilyId : byte
    {
        Dungeon = 0,
        Relic = 1,
        // Village and Ruin deferred to post-MVP
    }
}
