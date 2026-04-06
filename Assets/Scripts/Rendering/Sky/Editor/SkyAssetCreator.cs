
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using DOTS.Rendering.Sky;

namespace DOTS.Rendering.Sky.Editor
{
    public static class SkyAssetCreator
    {
        [MenuItem("Tools/Sky/Create Default Sky Preset")]
        public static void CreateDefaultSkyPreset()
        {
            var preset = ScriptableObject.CreateInstance<SkyPreset>();
            const string dir = "Assets/Resources/Sky";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Sky");
            }
            AssetDatabase.CreateAsset(preset, dir + "/DefaultSkyPreset.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[SkyAssetCreator] Created DefaultSkyPreset at " + dir);
        }

        [MenuItem("Tools/Sky/Create Default Biome Sky Mapping")]
        public static void CreateDefaultBiomeSkyMapping()
        {
            var mapping = ScriptableObject.CreateInstance<BiomeSkyMapping>();
            const string dir = "Assets/Resources/Sky";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "Sky");
            }
            AssetDatabase.CreateAsset(mapping, dir + "/DefaultBiomeSkyMapping.asset");
            AssetDatabase.SaveAssets();
            Debug.Log("[SkyAssetCreator] Created DefaultBiomeSkyMapping at " + dir);
        }
    }
}
#endif
