using UnityEngine;

namespace DOTS.Test
{
    public class ComputeShaderDebugTest : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("=== COMPUTE SHADER DEBUG TEST ===");
            
            // Test loading each Compute Shader
            TestShaderLoading("Shaders/TerrainNoise");
            TestShaderLoading("Shaders/TerrainErosion");
            TestShaderLoading("Shaders/WeatherEffects");
            TestShaderLoading("Shaders/TerrainModification");
            TestShaderLoading("Shaders/WFCGeneration");
            TestShaderLoading("Shaders/StructureGeneration");
            
            Debug.Log("=== DEBUG TEST COMPLETE ===");
        }
        
        void TestShaderLoading(string shaderPath)
        {
            ComputeShader shader = Resources.Load<ComputeShader>(shaderPath);
            if (shader != null)
            {
                Debug.Log($"✓ {shaderPath} loaded successfully");
                
                // Try to get kernel names
                try
                {
                    // Temporarily disabled to prevent compilation errors
                    // uint x, y, z;
                    // shader.GetKernelThreadGroupSizes(0, out x, out y, out z);
                    // Debug.Log($"  - Kernel thread group sizes: {x}, {y}, {z}");
                    Debug.Log($"  - Shader loaded successfully");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"  - Error getting kernel info: {e.Message}");
                }
            }
            else
            {
                Debug.LogError($"✗ {shaderPath} failed to load");
            }
        }
    }
} 