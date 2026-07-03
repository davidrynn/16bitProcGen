using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Component for WFC constraint data
    /// </summary>
    public struct WFCConstraint : IComponentData
    {
        public int patternId;
        public int direction; // 0=North, 1=East, 2=South, 3=West
        public int neighborCount;
        public float strength;
    }
}
