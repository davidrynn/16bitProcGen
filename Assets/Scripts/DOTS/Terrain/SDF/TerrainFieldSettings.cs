using Unity.Entities;

namespace DOTS.Terrain
{
    /// <summary>
    /// Singleton component holding biome terrain-field parameters consumed by SdLayeredGround.
    /// Plains subset only for this MVP — moisture and ruggedness fields are declared but unused.
    /// </summary>
    public struct TerrainFieldSettings : IComponentData
    {
        public float BaseHeight;

        // Elevation layers — all three used for plains
        public float ElevationLowFrequency;
        public float ElevationLowAmplitude;
        public float ElevationMidFrequency;
        public float ElevationMidAmplitude;
        public float ElevationHighFrequency;
        public float ElevationHighAmplitude;

        /// <summary>
        /// Redistribution exponent applied to the combined elevation signal.
        /// Greater than 1 flattens plains and widens valleys.
        /// Less than 1 sharpens peaks for mountains.
        /// 1.0 applies no redistribution.
        /// </summary>
        public float ElevationExponent;

        // Moisture and ruggedness — declared for schema compatibility, unused in plains MVP
        public float MoistureFrequency;
        public float MoistureAmplitude;
        public float RuggednessFrequency;
        public float RuggednessAmplitude;
    }
}
