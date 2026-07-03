using System;

// Extracted from the deleted PlayerModificationComponent.cs (cleanup round 1, plan row A18):
// still consumed by the live glob systems (TerrainGlobComponent, TerrainGlobPhysicsSystem).
// Kept in the global namespace so existing references resolve unchanged.
[Obsolete("Legacy heightmap-based glob removal; migrate to SDF edit buffers when available.")]
public enum GlobRemovalType
{
    Small = 0,   // 1x1 size
    Medium = 1,  // 2x2 size
    Large = 2    // 3x3 size
}
