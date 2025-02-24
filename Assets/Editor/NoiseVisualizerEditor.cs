using UnityEngine;
using UnityEditor;

public class NoiseVisualizerEditor : EditorWindow
{
    private NoiseType noiseType = NoiseType.Perlin;
    private float noiseScale = 0.1f;
    private int resolution = 128;
    private Texture2D noiseTexture;

    [MenuItem("Window/Noise Visualizer")]
    public static void ShowWindow()
    {
        GetWindow<NoiseVisualizerEditor>("Noise Visualizer");
    }

    void OnGUI()
    {
        noiseType = (NoiseType)EditorGUILayout.EnumPopup("Noise Type", noiseType);
        noiseScale = EditorGUILayout.Slider("Noise Scale", noiseScale, 0.01f, 1f);
        resolution = EditorGUILayout.IntSlider("Resolution", resolution, 32, 512);

        if (GUILayout.Button("Generate Noise"))
        {
            GenerateNoiseTexture();
        }

        if (noiseTexture != null)
        {
            GUILayout.Label(noiseTexture, GUILayout.Width(resolution), GUILayout.Height(resolution));
        }
    }

    void GenerateNoiseTexture()
    {
        noiseTexture = new Texture2D(resolution, resolution);

        for (int y = 0; y < resolution; y++)
        {
            for (int x = 0; x < resolution; x++)
            {
                float sample = SampleNoise(x, y);
                Color color = new Color(sample, sample, sample);
                noiseTexture.SetPixel(x, y, color);
            }
        }

        noiseTexture.Apply();
    }

    float SampleNoise(float x, float y)
    {
        float sample = 0f;

        switch (noiseType)
        {
            case NoiseType.Perlin:
                sample = Mathf.PerlinNoise(x * noiseScale, y * noiseScale);
                break;

            case NoiseType.Cellular:
                sample = new CellularNoise(noiseScale).Generate(x, y);
                break;

            case NoiseType.Voronoi:
                sample = new VoronoiNoise(noiseScale).Generate(x, y);
                break;
        }

        return sample;
    }
}
