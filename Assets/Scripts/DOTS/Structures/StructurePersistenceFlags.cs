using System;

namespace DOTS.Structures
{
    /// <summary>
    /// Tracks divergence from seeded defaults. Any flag set means
    /// the anchor is locked against silent re-roll during regeneration.
    /// </summary>
    [Flags]
    public enum StructurePersistenceFlags : byte
    {
        None = 0,
        Locked = 1 << 0,
        Modified = 1 << 1,
        Destroyed = 1 << 2,
        Discovered = 1 << 3,
    }
}
