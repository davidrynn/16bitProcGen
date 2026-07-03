using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Blob payload holding the pattern set consumed by WFCComponent
    /// </summary>
    public struct WFCPatternData
    {
        public BlobArray<WFCPattern> patterns;
        public int patternCount;
    }
}
