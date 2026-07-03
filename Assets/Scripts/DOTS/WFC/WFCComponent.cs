using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Component that stores Wave Function Collapse data for structured terrain generation
    /// </summary>
    public struct WFCComponent : IComponentData
    {
        public int2 gridSize;
        public int patternSize;
        public float cellSize;
        public bool isCollapsed;
        public float entropy;
        public int selectedPattern;
        public BlobAssetReference<WFCPatternData> patterns;
        public BlobAssetReference<WFCConstraintData> constraints;
        public bool needsGeneration;
        public bool isGenerating;
        public float generationProgress;
        public float lastUpdateTime;
        public int iterations;
        public int maxIterations;
    }
}
