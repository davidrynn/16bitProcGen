using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Core WFC pattern struct (used by ECS system, supports all domains)
    /// </summary>
    public struct WFCPattern : IComponentData
    {
        public int patternId;         // Unique pattern ID
        public float weight;          // Pattern selection weight
        public PatternDomain domain;  // Domain (dungeon, tree, etc.)
        public int type;              // Domain-specific type (e.g., DungeonPatternType)
        public byte north, east, south, west; // Edge types (always present)
    }
}
