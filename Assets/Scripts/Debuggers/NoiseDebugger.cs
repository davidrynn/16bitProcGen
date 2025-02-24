using UnityEditor;
using UnityEngine;

public class NoiseDebugger : EditorWindow
{
    private Texture2D noisePreview;
    private NoiseType noiseType;
    private float scale = 0.1f;
    private int previewSize = 256;

    [MenuItem("Window/Noise Debugger")]
    public static void ShowWindow()
    {
        GetWindow<NoiseDebugger>("Noise Debugger");
    }

    void OnGUI()
    {
        noiseType = (NoiseType)EditorGUILayout.EnumPopup("Noise Type", noiseType);
        scale = EditorGUILayout.Slider("Noise Scale", scale, 0.01f, 0.5f);

        if (GUILayout.Button("Generate Preview"))
        {
            GenerateNoiseTexture();
        }

        if (noisePreview != null)
        {
            GUILayout.Label(noisePreview, GUILayout.Width(previewSize), GUILayout.Height(previewSize));
        }
    }

    void GenerateNoiseTexture()
    {
        noisePreview = new Texture2D(previewSize, previewSize);

        for (int y = 0; y < previewSize; y++)
        {
            for (int x = 0; x < previewSize; x++)
            {
                float sample = SampleNoise(x, y);
                noisePreview.SetPixel(x, y, new Color(sample, sample, sample));
            }
        }

        noisePreview.Apply();
    }

    float SampleNoise(float x, float y)
    {
        switch (noiseType)
        {
            case NoiseType.Perlin:
                return Mathf.PerlinNoise(x * scale, y * scale);

            case NoiseType.Cellular:
                return new CellularNoise(scale).Generate(x, y);

            case NoiseType.Voronoi:
                return new VoronoiNoise(scale).Generate(x, y);
        }
        return 0;
    }
}
