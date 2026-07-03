using Unity.Collections;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Domain-specific pattern struct for dungeons
    /// </summary>
    public struct DungeonPattern
    {
        public int id;
        public FixedString32Bytes name;
        public DungeonPatternType type;
        public byte north, east, south, west; // 'F' or 'W'
        public float weight;
    }
}
