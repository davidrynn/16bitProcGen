// Scripts/DOTS/Compute/ComputeShaderManager.cs
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using Unity.Burst;

/// <summary>
/// Compute Shader Manager for the terrain system
/// Handles loading, caching, and coordination of GPU computation
/// </summary>
public class ComputeShaderManager
{
    // Compute Shader references
    private ComputeShader noiseShader;
    private ComputeShader erosionShader;
    private ComputeShader weatherShader;
    private ComputeShader modificationShader;
    private ComputeShader wfcShader;
    private ComputeShader structureShader;
    
    // Shader kernel IDs
    private int noiseKernel = -1;
    private int biomeNoiseKernel = -1;
    private int structureNoiseKernel = -1;
    private int erosionKernel = -1;
    private int weatherKernel = -1;
    private int modificationKernel = -1;
    private int wfcKernel = -1;
    private int structureKernel = -1;
    
    // Settings
    private int threadGroupSize = 8;
    private bool enableDebugLogging = true;
    
    // Performance monitoring
    private float lastNoiseGenerationTime;
    private float lastErosionTime;
    private float lastWeatherTime;
    
    // Singleton instance
    private static ComputeShaderManager instance;
    public static ComputeShaderManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = new ComputeShaderManager();
                instance.InitializeComputeShaders();
            }
            return instance;
        }
    }
    
    private ComputeShaderManager()
    {
        // Private constructor for singleton
    }
    
    /// <summary>
    /// Initializes all Compute Shaders and their kernels
    /// </summary>
    private void InitializeComputeShaders()
    {
        if (enableDebugLogging)
            Debug.Log("[DOTS] Initializing Compute Shaders...");
        
        // Load Compute Shaders from Resources
        noiseShader = Resources.Load<ComputeShader>("Shaders/TerrainNoise");
        erosionShader = Resources.Load<ComputeShader>("Shaders/TerrainErosion");
        weatherShader = Resources.Load<ComputeShader>("Shaders/WeatherEffects");
        modificationShader = Resources.Load<ComputeShader>("Shaders/TerrainModification");
        wfcShader = Resources.Load<ComputeShader>("Shaders/WFCGeneration");
        structureShader = Resources.Load<ComputeShader>("Shaders/StructureGeneration");
        
        // Log loading results
        if (enableDebugLogging)
        {
            Debug.Log($"[DOTS] TerrainNoise shader loaded: {noiseShader != null}");
            Debug.Log($"[DOTS] TerrainErosion shader loaded: {erosionShader != null}");
            Debug.Log($"[DOTS] WeatherEffects shader loaded: {weatherShader != null}");
            Debug.Log($"[DOTS] TerrainModification shader loaded: {modificationShader != null}");
            Debug.Log($"[DOTS] WFCGeneration shader loaded: {wfcShader != null}");
            Debug.Log($"[DOTS] StructureGeneration shader loaded: {structureShader != null}");
        }
        
        // Get kernel IDs
        InitializeKernels();
        
        if (enableDebugLogging)
            Debug.Log("[DOTS] Compute Shader initialization complete");
    }
    
    /// <summary>
    /// Initializes all kernel IDs for the Compute Shaders
    /// </summary>
    private void InitializeKernels()
    {
        if (noiseShader != null)
        {
            try
            {
                noiseKernel = noiseShader.FindKernel("GenerateNoise");
                biomeNoiseKernel = noiseShader.FindKernel("GenerateBiomeNoise");
                structureNoiseKernel = noiseShader.FindKernel("GenerateStructureNoise");
                
                if (enableDebugLogging)
                    Debug.Log($"[DOTS] Noise shader kernels: {noiseKernel}, {biomeNoiseKernel}, {structureNoiseKernel}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS] Error finding noise kernels: {e.Message}");
            }
        }
        
        if (erosionShader != null)
        {
            try
            {
                erosionKernel = erosionShader.FindKernel("ApplyErosion");
                if (enableDebugLogging)
                    Debug.Log($"[DOTS] Erosion shader kernel: {erosionKernel}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS] Error finding erosion kernel: {e.Message}");
            }
        }
        
        if (weatherShader != null)
        {
            try
            {
                weatherKernel = weatherShader.FindKernel("ApplyWeatherEffects");
                if (enableDebugLogging)
                    Debug.Log($"[DOTS] Weather shader kernel: {weatherKernel}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS] Error finding weather kernel: {e.Message}");
            }
        }
        
        if (modificationShader != null)
        {
            try
            {
                modificationKernel = modificationShader.FindKernel("ApplyModification");
                if (enableDebugLogging)
                    Debug.Log($"[DOTS] Modification shader kernel: {modificationKernel}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS] Error finding modification kernel: {e.Message}");
            }
        }
        
        if (wfcShader != null)
        {
            try
            {
                wfcKernel = wfcShader.FindKernel("PropagateConstraints");
                if (enableDebugLogging)
                    Debug.Log($"[DOTS] WFC shader kernel: {wfcKernel}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS] Error finding WFC kernel: {e.Message}");
            }
        }
        
        if (structureShader != null)
        {
            try
            {
                structureKernel = structureShader.FindKernel("GenerateStructure");
                if (enableDebugLogging)
                    Debug.Log($"[DOTS] Structure shader kernel: {structureKernel}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[DOTS] Error finding structure kernel: {e.Message}");
            }
        }
    }
    
    /// <summary>
    /// Gets the noise Compute Shader
    /// </summary>
    public ComputeShader NoiseShader => noiseShader;
    
    /// <summary>
    /// Gets the erosion Compute Shader
    /// </summary>
    public ComputeShader ErosionShader => erosionShader;
    
    /// <summary>
    /// Gets the weather Compute Shader
    /// </summary>
    public ComputeShader WeatherShader => weatherShader;
    
    /// <summary>
    /// Gets the modification Compute Shader
    /// </summary>
    public ComputeShader ModificationShader => modificationShader;
    
    /// <summary>
    /// Gets the WFC Compute Shader
    /// </summary>
    public ComputeShader WFCShader => wfcShader;
    
    /// <summary>
    /// Gets the structure Compute Shader
    /// </summary>
    public ComputeShader StructureShader => structureShader;
    
    /// <summary>
    /// Gets the thread group size
    /// </summary>
    public int ThreadGroupSize => threadGroupSize;
    
    /// <summary>
    /// Calculates the number of thread groups needed for a given resolution
    /// </summary>
    /// <param name="resolution">Resolution of the terrain grid</param>
    /// <returns>Number of thread groups</returns>
    public int CalculateThreadGroups(int resolution)
    {
        return (int)math.ceil((float)resolution / threadGroupSize);
    }
    
    /// <summary>
    /// Gets performance metrics
    /// </summary>
    /// <returns>Performance data</returns>
    public (float noiseTime, float erosionTime, float weatherTime) GetPerformanceMetrics()
    {
        return (lastNoiseGenerationTime, lastErosionTime, lastWeatherTime);
    }
    
    /// <summary>
    /// Validates that all required Compute Shaders are loaded
    /// </summary>
    /// <returns>True if all shaders are available</returns>
    public bool ValidateShaders()
    {
        bool allValid = true;
        
        if (noiseShader == null)
        {
            Debug.LogError("[DOTS] Noise Compute Shader is missing!");
            allValid = false;
        }
        
        if (erosionShader == null)
        {
            Debug.LogError("[DOTS] Erosion Compute Shader is missing!");
            allValid = false;
        }
        
        if (weatherShader == null)
        {
            Debug.LogError("[DOTS] Weather Compute Shader is missing!");
            allValid = false;
        }
        
        if (modificationShader == null)
        {
            Debug.LogError("[DOTS] Modification Compute Shader is missing!");
            allValid = false;
        }
        
        if (wfcShader == null)
        {
            Debug.LogError("[DOTS] WFC Compute Shader is missing!");
            allValid = false;
        }
        
        if (structureShader == null)
        {
            Debug.LogError("[DOTS] Structure Compute Shader is missing!");
            allValid = false;
        }
        
        return allValid;
    }
    
    /// <summary>
    /// Gets kernel ID for noise generation
    /// </summary>
    public int NoiseKernel => noiseKernel;
    
    /// <summary>
    /// Gets kernel ID for biome noise generation
    /// </summary>
    public int BiomeNoiseKernel => biomeNoiseKernel;
    
    /// <summary>
    /// Gets kernel ID for structure noise generation
    /// </summary>
    public int StructureNoiseKernel => structureNoiseKernel;
    
    /// <summary>
    /// Gets kernel ID for erosion
    /// </summary>
    public int ErosionKernel => erosionKernel;
    
    /// <summary>
    /// Gets kernel ID for weather effects
    /// </summary>
    public int WeatherKernel => weatherKernel;
    
    /// <summary>
    /// Gets kernel ID for terrain modification
    /// </summary>
    public int ModificationKernel => modificationKernel;
    
    /// <summary>
    /// Gets kernel ID for WFC
    /// </summary>
    public int WFCKernel => wfcKernel;
    
    /// <summary>
    /// Gets kernel ID for structure generation
    /// </summary>
    public int StructureKernel => structureKernel;
    
    /// <summary>
    /// Updates performance metrics
    /// </summary>
    /// <param name="noiseTime">Noise generation time</param>
    /// <param name="erosionTime">Erosion time</param>
    /// <param name="weatherTime">Weather time</param>
    public void UpdatePerformanceMetrics(float noiseTime, float erosionTime, float weatherTime)
    {
        lastNoiseGenerationTime = noiseTime;
        lastErosionTime = erosionTime;
        lastWeatherTime = weatherTime;
    }
}
