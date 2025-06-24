using Unity.Mathematics;
using UnityEngine;

// Test to verify Mathematics package is working
public class MathematicsTest : MonoBehaviour
{
    void Start()
    {
        Debug.Log("Mathematics package test: Starting...");
        
        // Test basic math operations
        TestBasicMath();
        
        // Test SIMD operations
        TestSIMDOperations();
        
        // Test noise functions
        TestNoiseFunctions();
        
        // Test vector operations
        TestVectorOperations();
    }
    
    private void TestBasicMath()
    {
        Debug.Log("Testing basic math operations...");
        
        // Test math constants
        float pi = math.PI;
        float e = math.E;
        
        Debug.Log($"✅ Math constants: PI = {pi}, E = {e}");
        
        // Test trigonometric functions
        float sin45 = math.sin(math.radians(45f));
        float cos45 = math.cos(math.radians(45f));
        
        Debug.Log($"✅ Trig functions: sin(45°) = {sin45:F4}, cos(45°) = {cos45:F4}");
        
        // Test power and root functions
        float power = math.pow(2f, 8f);
        float sqrt = math.sqrt(16f);
        
        Debug.Log($"✅ Power functions: 2^8 = {power}, √16 = {sqrt}");
    }
    
    private void TestSIMDOperations()
    {
        Debug.Log("Testing SIMD operations...");
        
        // Test float4 operations (SIMD)
        float4 vector1 = new float4(1f, 2f, 3f, 4f);
        float4 vector2 = new float4(5f, 6f, 7f, 8f);
        
        // SIMD addition
        float4 sum = vector1 + vector2;
        Debug.Log($"✅ SIMD addition: {vector1} + {vector2} = {sum}");
        
        // SIMD multiplication
        float4 product = vector1 * vector2;
        Debug.Log($"✅ SIMD multiplication: {vector1} * {vector2} = {product}");
        
        // SIMD dot product
        float dotProduct = math.dot(vector1, vector2);
        Debug.Log($"✅ SIMD dot product: {dotProduct}");
        
        // SIMD length
        float length = math.length(vector1);
        Debug.Log($"✅ SIMD length: |{vector1}| = {length:F4}");
    }
    
    private void TestNoiseFunctions()
    {
        Debug.Log("Testing noise functions...");
        
        // Test Perlin noise
        float perlin1 = noise.cnoise(new float2(0.5f, 0.5f));
        float perlin2 = noise.cnoise(new float2(1.0f, 1.0f));
        
        Debug.Log($"✅ Perlin noise: cnoise(0.5,0.5) = {perlin1:F4}, cnoise(1.0,1.0) = {perlin2:F4}");
        
        // Test cellular noise
        float cellular = noise.cellular(new float2(0.5f, 0.5f)).x;
        Debug.Log($"✅ Cellular noise: cellular(0.5,0.5) = {cellular:F4}");
        
        // Test simplex noise
        float simplex = noise.snoise(new float2(0.5f, 0.5f));
        Debug.Log($"✅ Simplex noise: snoise(0.5,0.5) = {simplex:F4}");
    }
    
    private void TestVectorOperations()
    {
        Debug.Log("Testing vector operations...");
        
        // Test float3 operations
        float3 point1 = new float3(1f, 2f, 3f);
        float3 point2 = new float3(4f, 5f, 6f);
        
        // Distance between points
        float distance = math.distance(point1, point2);
        Debug.Log($"✅ Distance: {distance:F4}");
        
        // Normalize vector
        float3 normalized = math.normalize(point1);
        Debug.Log($"✅ Normalized: {normalized}");
        
        // Cross product
        float3 cross = math.cross(point1, point2);
        Debug.Log($"✅ Cross product: {cross}");
        
        // Lerp (linear interpolation)
        float3 lerped = math.lerp(point1, point2, 0.5f);
        Debug.Log($"✅ Lerp (50%): {lerped}");
    }
}
