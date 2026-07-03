using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    public struct WFCGenerationSettings : IComponentData
    {
        public int maxIterations;
        public float constraintStrength;
        public float entropyThreshold;
        public bool enableBacktracking;
        public int backtrackingLimit;
        public float generationTimeout;
    }
}
