using UnityEngine;
using Unity.Mathematics;

/// <summary>
/// Basic test to verify compute shader pipeline is working
/// </summary>
public class BasicComputeShaderTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool runTestOnStart = true;
    [SerializeField] private int testResolution = 64;
    [SerializeField] private float testValue = 10.0f;
    
    [Header("Test Results")]
    [SerializeField] private string testMessage = "";
    
    private void Start()
    {
        if (runTestOnStart)
        {
            RunBasicTest();
        }
    }
    
    [ContextMenu("Run Basic Compute Shader Test")]
    public void RunBasicTest()
    {
        Debug.Log("=== BASIC COMPUTE SHADER TEST ===");
        
        try
        {
            // Load the basic test shader
            ComputeShader testShader = Resources.Load<ComputeShader>("Shaders/BasicTest");
            if (testShader == null)
            {
                throw new System.Exception("BasicTest compute shader not found!");
            }
            
            Debug.Log("✓ BasicTest compute shader loaded successfully");
            
            // Find the kernel
            int kernelId = testShader.FindKernel("BasicTest");
            if (kernelId == -1)
            {
                throw new System.Exception("BasicTest kernel not found!");
            }
            
            Debug.Log($"✓ BasicTest kernel found with ID: {kernelId}");
            
            // Create output buffer
            int bufferSize = testResolution * testResolution;
            ComputeBuffer outputBuffer = new ComputeBuffer(bufferSize, sizeof(float));
            
            // Set buffer and parameters
            testShader.SetBuffer(kernelId, "output", outputBuffer);
            testShader.SetInt("resolution", testResolution);
            testShader.SetFloat("testValue", testValue);
            
            Debug.Log($"✓ Buffer and parameters set. Buffer size: {bufferSize}");
            
            // Calculate thread groups
            int threadGroupsX = Mathf.CeilToInt(testResolution / 8f);
            int threadGroupsY = Mathf.CeilToInt(testResolution / 8f);
            
            Debug.Log($"✓ Dispatching with thread groups: {threadGroupsX}x{threadGroupsY}");
            
            // Dispatch the compute shader
            testShader.Dispatch(kernelId, threadGroupsX, threadGroupsY, 1);
            
            Debug.Log("✓ Compute shader dispatched successfully");
            
            // Read back results
            float[] results = new float[bufferSize];
            outputBuffer.GetData(results);
            
            Debug.Log("✓ Results read back successfully");
            
            // Verify results
            bool hasExpectedValues = false;
            bool hasNonZeroValues = false;
            
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] != 0)
                {
                    hasNonZeroValues = true;
                }
                
                // Check for our marker values
                if (results[i] == 1000.0f || results[i] == 2000.0f)
                {
                    hasExpectedValues = true;
                    Debug.Log($"✓ Found marker value: {results[i]} at index {i}");
                }
            }
            
            // Log some sample values
            Debug.Log($"Sample values: {results[0]}, {results[1]}, {results[2]}, {results[3]}, {results[4]}");
            Debug.Log($"Last few values: {results[bufferSize-5]}, {results[bufferSize-4]}, {results[bufferSize-3]}, {results[bufferSize-2]}, {results[bufferSize-1]}");
            
            // Check if test passed
            if (!hasNonZeroValues)
            {
                throw new System.Exception("All values are zero - compute shader may not be executing");
            }
            
            if (!hasExpectedValues)
            {
                throw new System.Exception("Marker values not found - compute shader may not be executing correctly");
            }
            
            // Clean up
            outputBuffer.Release();
            
            testMessage = "Basic compute shader test passed! Compute shader pipeline is working.";
            Debug.Log("=== BASIC COMPUTE SHADER TEST PASSED ===");
        }
        catch (System.Exception e)
        {
            testMessage = $"Test failed: {e.Message}";
            Debug.LogError($"=== BASIC COMPUTE SHADER TEST FAILED: {e.Message} ===");
        }
    }
} 