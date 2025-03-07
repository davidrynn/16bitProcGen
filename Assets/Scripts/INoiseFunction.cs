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

public class TestNoise : INoiseFunction
{
    private float scale;
    public TestNoise(float scale)
    {
        this.scale = scale;
    }
    public float Generate(float x, float z)
    {
        // Get local position within the chunk (0 to 1)
        float localX = (x % 16) / 16f;
        float localZ = (z % 16) / 16f;

        // Fixed corner heights for every chunk (ensuring every chunk is identical)
        float h00 = 0.5f;   // Bottom-left corner
        float h10 = 1.0f;   // Bottom-right corner
        float h01 = 1.5f;   // Top-left corner
        float h11 = 2.0f;  // Top-right corner

        // Bilinear interpolation to smoothly blend heights inside the chunk
        float height =
            h00 * (1 - localX) * (1 - localZ) +  // Influence from bottom-left
            h10 * localX * (1 - localZ) +        // Influence from bottom-right
            h01 * (1 - localX) * localZ +        // Influence from top-left
            h11 * localX * localZ;               // Influence from top-right

        return height; // Every chunk will have the exact same height distribution
    }






}


