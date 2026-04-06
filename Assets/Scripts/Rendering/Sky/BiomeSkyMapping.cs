using System;
using UnityEngine;

namespace DOTS.Rendering.Sky
{
    [CreateAssetMenu(menuName = "Rendering/Biome Sky Mapping", fileName = "BiomeSkyMapping")]
    public class BiomeSkyMapping : ScriptableObject
    {
        [Serializable]
        public struct BiomeSkyEntry
        {
            public BiomeType biomeType;
            public SkyPreset skyPreset;
            public CloudSettings cloudOverride;
            [Tooltip("Use cloud override instead of default CloudSettings.")]
            public bool overrideClouds;
        }

        [SerializeField] private SkyPreset fallbackPreset;
        [SerializeField] private BiomeSkyEntry[] entries = Array.Empty<BiomeSkyEntry>();

        public SkyPreset FallbackPreset => fallbackPreset;

        public bool TryGetPreset(BiomeType biome, out SkyPreset preset, out CloudSettings? clouds)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].biomeType == biome)
                {
                    preset = entries[i].skyPreset != null ? entries[i].skyPreset : fallbackPreset;
                    clouds = entries[i].overrideClouds ? entries[i].cloudOverride : null;
                    return true;
                }
            }

            preset = fallbackPreset;
            clouds = null;
            return false;
        }
    }
}
