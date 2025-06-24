// Scripts/DOTS/Core/BurstMathFunctions.cs
using Unity.Burst;
using Unity.Mathematics;

// Collection of Burst-compiled math functions
public static class BurstMathFunctions
{
    // Burst-compiled function for complex calculations
    [BurstCompile]
    public static float ComplexCalculation(float input)
    {
        // Complex math operations
        float result = 0f;
        for (int i = 0; i < 1000; i++)
        {
            result += math.sin(input + i) * math.cos(input - i);
        }
        return result;
    }
    
    // Additional Burst-compiled math functions can go here
    [BurstCompile]
    public static float NoiseCalculation(float x, float y, float scale)
    {
        return math.sin(x * scale) * math.cos(y * scale);
    }
    
    [BurstCompile]
    public static float2 RotateVector(float2 vector, float angle)
    {
        float cos = math.cos(angle);
        float sin = math.sin(angle);
        return new float2(
            vector.x * cos - vector.y * sin,
            vector.x * sin + vector.y * cos
        );
    }
}