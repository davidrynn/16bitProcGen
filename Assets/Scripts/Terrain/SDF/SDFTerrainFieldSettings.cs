using Unity.Entities;

namespace DOTS.Terrain.SDF
{
    public struct SDFTerrainFieldSettings : IComponentData
    {
        public float BaseHeight;
        public float Amplitude;
        public float Frequency;
        public float NoiseValue;
    }
}
