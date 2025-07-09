using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Simple test script to verify Phase 2.1: Compute Shader Setup
/// This can be attached to a GameObject in a test scene
/// </summary>
public class ComputeShaderSetupTest : MonoBehaviour
{
    [Header("Test Settings")]
    [SerializeField] private bool runTestsOnStart = true;
    [SerializeField] private bool logResults = true;
    
    [Header("Test Results")]
    public bool testPassed = false;
    public string testMessage = "";
    
    private ComputeShaderManager computeManager;
    
    private void Start()
    {
        if (runTestsOnStart)
        {
            RunTests();
        }
    }
    
    [ContextMenu("Run Compute Shader Setup Tests")]
    public void RunTests()
    {
        if (logResults)
            Debug.Log("[Phase 2.1] Starting Compute Shader Setup Tests...");
        
        try
        {
            // Get the ComputeShaderManager instance
            computeManager = ComputeShaderManager.Instance;
            
            // Run tests
            TestShaderLoading();
            TestKernelValidation();
            TestThreadGroupCalculation();
            TestPerformanceMetrics();
            TestValidation();
            
            testPassed = true;
            testMessage = "All tests passed successfully!";
            
            if (logResults)
                Debug.Log("[Phase 2.1] Compute Shader Setup Tests Completed Successfully!");
        }
        catch (System.Exception e)
        {
            testPassed = false;
            testMessage = $"Test failed: {e.Message}";
            Debug.LogError($"[Phase 2.1] Test failed: {e.Message}");
        }
    }
    
    private void TestShaderLoading()
    {
        if (logResults)
            Debug.Log("[Test] Testing shader loading...");
        
        // Test all shaders are loaded
        if (computeManager.NoiseShader == null)
            throw new System.Exception("Noise shader not loaded");
        if (computeManager.ErosionShader == null)
            throw new System.Exception("Erosion shader not loaded");
        if (computeManager.WeatherShader == null)
            throw new System.Exception("Weather shader not loaded");
        if (computeManager.ModificationShader == null)
            throw new System.Exception("Modification shader not loaded");
        if (computeManager.WFCShader == null)
            throw new System.Exception("WFC shader not loaded");
        if (computeManager.StructureShader == null)
            throw new System.Exception("Structure shader not loaded");
        
        if (logResults)
            Debug.Log("[Test] ✓ Shader loading test passed");
    }
    
    private void TestKernelValidation()
    {
        if (logResults)
            Debug.Log("[Test] Testing kernel validation...");
        
        // Test all kernels are valid
        if (computeManager.NoiseKernel < 0)
            throw new System.Exception("Noise kernel invalid");
        if (computeManager.BiomeNoiseKernel < 0)
            throw new System.Exception("Biome noise kernel invalid");
        if (computeManager.StructureNoiseKernel < 0)
            throw new System.Exception("Structure noise kernel invalid");
        if (computeManager.ErosionKernel < 0)
            throw new System.Exception("Erosion kernel invalid");
        if (computeManager.WeatherKernel < 0)
            throw new System.Exception("Weather kernel invalid");
        if (computeManager.ModificationKernel < 0)
            throw new System.Exception("Modification kernel invalid");
        if (computeManager.WFCKernel < 0)
            throw new System.Exception("WFC kernel invalid");
        if (computeManager.StructureKernel < 0)
            throw new System.Exception("Structure kernel invalid");
        
        if (logResults)
            Debug.Log("[Test] ✓ Kernel validation test passed");
    }
    
    private void TestThreadGroupCalculation()
    {
        if (logResults)
            Debug.Log("[Test] Testing thread group calculation...");
        
        // Test various resolutions
        int[] resolutions = { 64, 128, 256, 512 };
        int[] expectedThreadGroups = { 8, 16, 32, 64 };
        
        for (int i = 0; i < resolutions.Length; i++)
        {
            int actualThreadGroups = computeManager.CalculateThreadGroups(resolutions[i]);
            if (actualThreadGroups != expectedThreadGroups[i])
            {
                throw new System.Exception($"Thread groups for resolution {resolutions[i]} should be {expectedThreadGroups[i]}, got {actualThreadGroups}");
            }
        }
        
        if (logResults)
            Debug.Log("[Test] ✓ Thread group calculation test passed");
    }
    
    private void TestPerformanceMetrics()
    {
        if (logResults)
            Debug.Log("[Test] Testing performance metrics...");
        
        // Test performance metrics update
        float noiseTime = 0.5f;
        float erosionTime = 0.3f;
        float weatherTime = 0.2f;
        
        computeManager.UpdatePerformanceMetrics(noiseTime, erosionTime, weatherTime);
        var metrics = computeManager.GetPerformanceMetrics();
        
        if (math.abs(metrics.noiseTime - noiseTime) > 0.001f)
            throw new System.Exception($"Noise time not updated correctly. Expected {noiseTime}, got {metrics.noiseTime}");
        if (math.abs(metrics.erosionTime - erosionTime) > 0.001f)
            throw new System.Exception($"Erosion time not updated correctly. Expected {erosionTime}, got {metrics.erosionTime}");
        if (math.abs(metrics.weatherTime - weatherTime) > 0.001f)
            throw new System.Exception($"Weather time not updated correctly. Expected {weatherTime}, got {metrics.weatherTime}");
        
        if (logResults)
            Debug.Log("[Test] ✓ Performance metrics test passed");
    }
    
    private void TestValidation()
    {
        if (logResults)
            Debug.Log("[Test] Testing validation...");
        
        bool isValid = computeManager.ValidateShaders();
        if (!isValid)
            throw new System.Exception("Shader validation failed");
        
        if (logResults)
            Debug.Log("[Test] ✓ Validation test passed");
    }
    
    [ContextMenu("Log Current Status")]
    public void LogCurrentStatus()
    {
        if (computeManager == null)
        {
            Debug.LogWarning("ComputeManager not initialized. Run tests first.");
            return;
        }
        
        Debug.Log("=== Compute Shader Manager Status ===");
        Debug.Log($"Noise Shader: {(computeManager.NoiseShader != null ? "Loaded" : "Missing")}");
        Debug.Log($"Erosion Shader: {(computeManager.ErosionShader != null ? "Loaded" : "Missing")}");
        Debug.Log($"Weather Shader: {(computeManager.WeatherShader != null ? "Loaded" : "Missing")}");
        Debug.Log($"Modification Shader: {(computeManager.ModificationShader != null ? "Loaded" : "Missing")}");
        Debug.Log($"WFC Shader: {(computeManager.WFCShader != null ? "Loaded" : "Missing")}");
        Debug.Log($"Structure Shader: {(computeManager.StructureShader != null ? "Loaded" : "Missing")}");
        Debug.Log($"Thread Group Size: {computeManager.ThreadGroupSize}");
        Debug.Log($"Validation: {(computeManager.ValidateShaders() ? "Passed" : "Failed")}");
    }
    
    void OnGUI()
    {
        // Display test results in scene view
        if (Application.isPlaying)
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 100));
            GUILayout.Label($"ComputeShaderManager Test: {(testPassed ? "PASSED" : "FAILED")}");
            GUILayout.Label(testMessage);
            GUILayout.EndArea();
        }
    }
} 