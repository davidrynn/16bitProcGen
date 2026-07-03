using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Component for WFC cell data
    /// Each cell tracks its possible patterns as a bitmask (up to 32 patterns)
    /// </summary>
    public struct WFCCell : IComponentData
    {
        public int2 position;
        public bool collapsed;
        public float entropy;
        public int selectedPattern;
        public int patternCount;
        public bool needsUpdate;
        public bool visualized; // Flag to track if this cell has been visualized
        public uint possiblePatternsMask; // Each bit represents a possible pattern (up to 32)
    }
}
