using UnityEngine;

namespace DOTS.Test
{
    public class SimpleComputeTest : MonoBehaviour
    {
        void Start()
        {
            Debug.Log("=== SIMPLE COMPUTE SHADER TEST ===");
            
            // Test basic loading
            var noiseShader = Resources.Load<ComputeShader>("Shaders/TerrainNoise");
            Debug.Log($"TerrainNoise loaded: {noiseShader != null}");
            
            var erosionShader = Resources.Load<ComputeShader>("Shaders/TerrainErosion");
            Debug.Log($"TerrainErosion loaded: {erosionShader != null}");
            
            var weatherShader = Resources.Load<ComputeShader>("Shaders/WeatherEffects");
            Debug.Log($"WeatherEffects loaded: {weatherShader != null}");
            
            var modificationShader = Resources.Load<ComputeShader>("Shaders/TerrainModification");
            Debug.Log($"TerrainModification loaded: {modificationShader != null}");
            
            var wfcShader = Resources.Load<ComputeShader>("Shaders/WFCGeneration");
            Debug.Log($"WFCGeneration loaded: {wfcShader != null}");
            
            var structureShader = Resources.Load<ComputeShader>("Shaders/StructureGeneration");
            Debug.Log($"StructureGeneration loaded: {structureShader != null}");
            
            Debug.Log("=== SIMPLE TEST COMPLETE ===");
        }
    }
} 