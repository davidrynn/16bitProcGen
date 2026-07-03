using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    public struct WFCPerformanceData : IComponentData
    {
        public float generationTime;
        public int cellsProcessed;
        public int constraintChecks;
        public float averageEntropy;
        public int successfulGenerations;
        public int failedGenerations;
    }
}
