using UnityEngine;

namespace DOTS.Terrain.Settings
{
    /// <summary>
    /// Per-biome grass appearance configuration.
    /// One asset per biome type; referenced by index from <see cref="GrassSystemSettings.Biomes"/>.
    ///
    /// Usage: Create via Assets > Create > DOTS Terrain > Grass Biome Settings.
    /// Place under Assets/Resources/Biomes/ to allow runtime loading if needed.
    /// </summary>
    [CreateAssetMenu(menuName = "DOTS Terrain/Grass Biome Settings", fileName = "GrassBiome_Default")]
    public class GrassBiomeSettings : ScriptableObject
    {
        [Header("Colour")]
        [Tooltip("Base blade colour. Per-blade noise adds variation around this value.")]
        public Color BaseColor = new Color(0.35f, 0.65f, 0.20f, 1f);

        [Tooltip("±Amount of random colour variation added to each blade channel.")]
        [Range(0f, 0.5f)]
        public float ColorNoiseScale = 0.12f;

        [Header("Density")]
        [Tooltip("Multiplied with the chunk's Density value. 1 = normal, 0.2 = sparse rocky.")]
        [Range(0f, 2f)]
        public float DensityMultiplier = 1f;

        [Header("Blade Height")]
        [Tooltip("Minimum blade height in world units.")]
        [Min(0.01f)]
        public float MinBladeHeight = 0.15f;

        [Tooltip("Maximum blade height in world units.")]
        [Min(0.01f)]
        public float MaxBladeHeight = 0.45f;

        [Header("Wind")]
        [Tooltip("Per-biome wind strength override written to the shader via MaterialPropertyBlock.")]
        [Range(0f, 1f)]
        public float WindStrength = 0.25f;
    }
}
