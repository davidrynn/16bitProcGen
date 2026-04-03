using UnityEngine;

namespace DOTS.Terrain.Settings
{
    /// <summary>
    /// Singleton ScriptableObject with global grass rendering configuration.
    ///
    /// Location: Assets/Resources/GrassSystemSettings.asset (required for Resources.Load).
    /// Create via Assets > Create > DOTS Terrain > Grass System Settings.
    ///
    /// Systems that need this call <see cref="Load"/> once and cache the result.
    /// If the asset is missing, <see cref="Load"/> returns null and systems fall back to
    /// hardcoded defaults defined in <see cref="GrassDefaults"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "DOTS Terrain/Grass System Settings", fileName = "GrassSystemSettings")]
    public class GrassSystemSettings : ScriptableObject
    {
        private const string ResourcePath = "GrassSystemSettings";

        [Header("Blade Mesh & Material")]
        [Tooltip("Simple cross or quad mesh for one blade. Created by DOTS Terrain > Build Grass Blade Mesh.")]
        public Mesh BladeMesh;

        [Tooltip("Material using the GrassBlades shader.")]
        public Material BaseMaterial;

        [Header("Density")]
        [Tooltip("Hard cap on blades per chunk regardless of surface area.")]
        [Min(1)]
        public int MaxBladesPerChunk = 4096;

        [Tooltip("Target blade count per square world unit at Density=1 with DensityMultiplier=1.")]
        [Min(0.1f)]
        public float BladesPerSqMeter = 6f;

        [Header("LOD Fade")]
        [Tooltip("Distance (world units) at which blades begin to fade out.")]
        public float FadeStartDistance = 60f;

        [Tooltip("Distance beyond which no blades are drawn.")]
        public float FadeEndDistance = 120f;

        [Header("Biomes")]
        [Tooltip("Index 0 is the default biome. TerrainChunkGrassSurface.BiomeTypeId selects the entry.")]
        public GrassBiomeSettings[] Biomes = System.Array.Empty<GrassBiomeSettings>();

        /// <summary>
        /// Loads the singleton asset from Resources/GrassSystemSettings.
        /// Returns null if the asset has not been created yet.
        /// </summary>
        public static GrassSystemSettings Load() =>
            Resources.Load<GrassSystemSettings>(ResourcePath);

        /// <summary>
        /// Returns the biome at <paramref name="biomeTypeId"/>, or the first biome if the
        /// index is out of range, or null if no biomes are configured.
        /// </summary>
        public GrassBiomeSettings GetBiome(int biomeTypeId)
        {
            if (Biomes == null || Biomes.Length == 0) return null;
            int index = biomeTypeId >= 0 && biomeTypeId < Biomes.Length ? biomeTypeId : 0;
            return Biomes[index];
        }
    }

    /// <summary>Fallback constants used when GrassSystemSettings asset is absent.</summary>
    public static class GrassDefaults
    {
        public const int   MaxBladesPerChunk  = 2048;
        public const float BladesPerSqMeter   = 6f;
        public const float FadeStartDistance  = 60f;
        public const float FadeEndDistance    = 120f;
        public const float MinBladeHeight     = 0.15f;
        public const float MaxBladeHeight     = 0.40f;
        public const float WindStrength       = 0.25f;
    }
}
