using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BiomeData))]
public class BiomeDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        BiomeData biome = (BiomeData)target;

        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Suggest Noise Values"))
        {
            SuggestValues(biome);
        }
    }

    private void SuggestValues(BiomeData biome)
    {
        switch (biome.noiseType)
        {
            case NoiseType.Perlin:
                biome.noiseScale = 0.1f;
                biome.heightMultiplier = 10f;
                break;

            case NoiseType.Cellular:
                biome.noiseScale = 0.12f;
                biome.heightMultiplier = 15f;
                break;

            case NoiseType.Voronoi:
                biome.noiseScale = 0.1f;
                biome.heightMultiplier = 20f;
                break;
        }

        EditorUtility.SetDirty(biome);
        Debug.Log($"Suggested values applied for {biome.noiseType} noise.");
    }
}
