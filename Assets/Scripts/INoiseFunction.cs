using UnityEngine;
using Unity.Mathematics;
using static Unity.Mathematics.noise;

public interface INoiseFunction
{
    float Generate(float x, float z);
}


public class PerlinNoise : INoiseFunction
{
    private float scale;

    public PerlinNoise(float scale)
    {
        this.scale = scale;
    }

    public float Generate(float x, float z)
    {
        return Mathf.PerlinNoise(x * scale, z * scale);
    }
}

public class CellularNoise : INoiseFunction
{
    private float scale;

    public CellularNoise(float scale)
    {
        this.scale = scale;
    }

    public float Generate(float x, float z)
    {
        float2 position = new float2(x, z) * scale;
        return Cellular(position);
    }

    private float Cellular(float2 position)
    {
        float2 cell = math.floor(position);
        float2 frac = math.frac(position);
        float minDist = float.MaxValue;
        float cellId = 0f;

        // Find nearest cell center
        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float2 neighbor = new float2(x, y);
                float2 randomPoint = math.frac(math.sin(math.dot(cell + neighbor, new float2(127.1f, 311.7f))) * 43758.5453f);
                float2 diff = neighbor + randomPoint - frac;
                float dist = math.length(diff);

                if (dist < minDist)
                {
                    minDist = dist;
                    cellId = math.frac(math.sin(math.dot(cell + neighbor, new float2(269.5f, 183.3f))) * 43758.5453f);
                }
            }
        }

        // Flat-topped cells: Return a constant height per cell based on cellId
        return cellId;
    }
}



public class VoronoiNoise : INoiseFunction
{
    private float scale;

    public VoronoiNoise(float scale)
    {
        this.scale = scale;
    }

    public float Generate(float x, float z)
    {
        float2 pos = new float2(x, z) * scale;
        float cellValue = voronoi(pos).x;

        return math.saturate(cellValue);
    }

    // Simple Voronoi function implementation
    private float2 voronoi(float2 pos)
    {
        float2 i = math.floor(pos);
        float2 f = math.frac(pos);

        float minDist = 1.0f;

        for (int y = -1; y <= 1; y++)
        {
            for (int x = -1; x <= 1; x++)
            {
                float2 neighbor = new float2(x, y);
                float2 randomPoint = math.frac(math.sin(math.dot(i + neighbor, new float2(127.1f, 311.7f))) * 43758.5453f);
                float2 diff = neighbor + randomPoint - f;
                float dist = math.length(diff);

                if (dist < minDist)
                    minDist = dist;
            }
        }

        return new float2(minDist, 0f);
    }
}


