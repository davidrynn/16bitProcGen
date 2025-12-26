using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using DOTS.Terrain;


namespace DOTS.Terrain.Weather
{
    /// <summary>
    /// Hybrid system that coordinates DOTS weather data with Compute Shader effects
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(WeatherSystem))]
    [DisableAutoCreation]
    public partial class HybridWeatherSystem : SystemBase
    {
        // Compute Shader Manager reference
        private ComputeShaderManager computeManager;
        
        // Buffer Manager reference
        private TerrainComputeBufferManager bufferManager;
        
        // Weather effect buffers
        private ComputeBuffer moistureBuffer;
        private ComputeBuffer temperatureBuffer;
        
        // Performance monitoring
        private float lastWeatherUpdateTime;
        private int chunksWithWeatherEffects;
        
        // Debug settings
        private bool enableDebugLogs = false;
        
        protected override void OnCreate()
        {
            DOTS.Terrain.Core.DebugSettings.LogWeather("HybridWeatherSystem: Initializing...");
            lastWeatherUpdateTime = 0f;
            chunksWithWeatherEffects = 0;
            
            // Try to get ComputeShaderManager singleton and debug kernel issues
            try
            {
                computeManager = ComputeShaderManager.Instance;
                DebugLog("Found ComputeShaderManager singleton");
                
                // Debug: Check weather shader and kernel
                if (computeManager.WeatherShader != null)
                {
                    DebugLog("Weather shader loaded successfully");
                    int kernelIndex = computeManager.WeatherKernel;
                    DebugLog($"Weather kernel index: {kernelIndex}");
                    
                    if (kernelIndex == -1)
                    {
                        DebugError("Weather kernel not found during initialization!");
                        // Try manual lookup
                        int manualKernel = computeManager.WeatherShader.FindKernel("ApplyWeatherEffects");
                        DebugError($"Manual FindKernel('ApplyWeatherEffects') returned: {manualKernel}");
                    }
                }
                else
                {
                    DebugError("Weather shader is null!");
                }
            }
            catch (System.Exception e)
            {
                DebugError($"Failed to get ComputeShaderManager: {e.Message}");
            }
            
            DOTS.Terrain.Core.DebugSettings.LogWeather("HybridWeatherSystem: Initialization complete");
        }
        
        protected override void OnUpdate()
        {
            // Try to get ComputeShaderManager singleton
            if (computeManager == null)
            {
                try
                {
                    computeManager = ComputeShaderManager.Instance;
                    DebugLog("Found ComputeShaderManager singleton");
                }
                catch (System.Exception e)
                {
                    DebugWarning($"Failed to get ComputeShaderManager: {e.Message}");
                    return;
                }
            }
            
            if (computeManager == null)
            {
                DebugWarning("Skipping update - no ComputeShaderManager");
                return;
            }
            
            // Try to get TerrainComputeBufferManager
            if (bufferManager == null)
            {
                bufferManager = Object.FindFirstObjectByType<TerrainComputeBufferManager>();
                if (bufferManager != null)
                {
                    DebugLog("Found TerrainComputeBufferManager");
                }
                else
                {
                    // Silently skip weather effects if buffer manager not available
                    return;
                }
            }
            
            // Process weather effects
            ProcessWeatherEffects();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        /// <summary>
        /// Processes weather effects for terrain chunks using Compute Shaders
        /// </summary>
        private void ProcessWeatherEffects()
        {
            chunksWithWeatherEffects = 0;

            var query = GetEntityQuery(ComponentType.ReadWrite<WeatherComponent>(), ComponentType.ReadOnly<TerrainData>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var weather = EntityManager.GetComponentData<WeatherComponent>(entity);
                if (!weather.isWeatherActive)
                {
                    continue;
                }

                var terrain = EntityManager.GetComponentData<TerrainData>(entity);
                if (!terrain.needsGeneration)
                {
                    if (ApplyWeatherEffectsWithComputeShader(ref weather, ref terrain))
                    {
                        chunksWithWeatherEffects++;
                    }
                    EntityManager.SetComponentData(entity, weather);
                }
            }
        }
        
        /// <summary>
        /// Applies weather effects using Compute Shaders
        /// </summary>
        private bool ApplyWeatherEffectsWithComputeShader(ref WeatherComponent weather, ref TerrainData terrain)
        {
            if (enableDebugLogs)
            {
                DOTS.Terrain.Core.DebugSettings.LogWeather($"Applying weather effects: {weather.weatherType} to terrain at {terrain.chunkPosition}");
            }
            
            if (computeManager?.WeatherShader == null)
            {
                DebugError("Weather shader not available");
                return false;
            }
            
            try
            {
                // Initialize buffers if needed
                InitializeWeatherBuffers(terrain.resolution);
                
                // Set weather parameters
                var weatherShader = computeManager.WeatherShader;
                weatherShader.SetInt("resolution", terrain.resolution);
                weatherShader.SetFloat("time", (float)SystemAPI.Time.ElapsedTime);
                weatherShader.SetFloat("deltaTime", (float)SystemAPI.Time.DeltaTime);
                weatherShader.SetFloat("temperature", weather.temperature);
                weatherShader.SetFloat("humidity", weather.humidity);
                weatherShader.SetFloat("windSpeed", weather.windSpeed);
                weatherShader.SetFloat("windDirection", weather.windDirection);
                weatherShader.SetFloat("weatherIntensity", weather.intensity);
                weatherShader.SetInt("weatherType", (int)weather.weatherType);
                
                // Get height buffer from buffer manager
                var heightBuffer = bufferManager.GetHeightBuffer(terrain.chunkPosition, terrain.resolution);
                if (heightBuffer == null)
                {
                    DebugError($"Failed to get height buffer for terrain at {terrain.chunkPosition}");
                    return false;
                }
                
                // Set buffers - use kernel index, not kernel name
                int kernelIndex = computeManager.WeatherKernel;
                
                // Debug: Check if kernel is valid
                if (kernelIndex == -1)
                {
                    DebugError($"Weather kernel not found! WeatherKernel returned {kernelIndex}");
                    DebugError($"Weather shader loaded: {computeManager.WeatherShader != null}");
                    if (computeManager.WeatherShader != null)
                    {
                        DebugError($"Available kernels in WeatherEffects shader:");
                        // Try to find the kernel manually
                        int manualKernel = computeManager.WeatherShader.FindKernel("ApplyWeatherEffects");
                        DebugError($"Manual FindKernel('ApplyWeatherEffects') returned: {manualKernel}");
                    }
                    return false;
                }
                if (enableDebugLogs)
                {
                    DebugLog($"Using weather kernel index: {kernelIndex}", true);
                }
                
                
                weatherShader.SetBuffer(kernelIndex, "heights", heightBuffer);
                weatherShader.SetBuffer(kernelIndex, "moisture", moistureBuffer);
                weatherShader.SetBuffer(kernelIndex, "temperatureBuffer", temperatureBuffer);
                
                // Dispatch compute shader - use kernel index
                int threadGroups = terrain.resolution / 8;
                weatherShader.Dispatch(kernelIndex, threadGroups, threadGroups, 1);
                
                DebugLog($"Applied weather effects to terrain at {terrain.chunkPosition}");
                return true;
            }
            catch (System.Exception e)
            {
                DebugError($"Failed to apply weather effects: {e.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Initializes weather effect buffers
        /// </summary>
        private void InitializeWeatherBuffers(int resolution)
        {
            int bufferSize = resolution * resolution;
            
            // Initialize moisture buffer
            if (moistureBuffer == null || moistureBuffer.count != bufferSize)
            {
                if (moistureBuffer != null)
                {
                    moistureBuffer.Release();
                }
                
                moistureBuffer = new ComputeBuffer(bufferSize, sizeof(float));
                DebugLog($"Initialized moisture buffer with size {bufferSize}");
            }
            
            // Initialize temperature buffer
            if (temperatureBuffer == null || temperatureBuffer.count != bufferSize)
            {
                if (temperatureBuffer != null)
                {
                    temperatureBuffer.Release();
                }
                
                temperatureBuffer = new ComputeBuffer(bufferSize, sizeof(float));
                DebugLog($"Initialized temperature buffer with size {bufferSize}");
            }
        }
        
        /// <summary>
        /// Updates performance metrics
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            lastWeatherUpdateTime = (float)SystemAPI.Time.ElapsedTime;
        }
        
        /// <summary>
        /// Gets weather effect statistics
        /// </summary>
        public (int activeChunks, float lastUpdateTime) GetWeatherEffectStats()
        {
            return (chunksWithWeatherEffects, lastWeatherUpdateTime);
        }
        
        /// <summary>
        /// Forces weather effects update for testing
        /// </summary>
        public void ForceWeatherEffectsUpdate()
        {
            DebugLog("Forcing weather effects update");

            var query = GetEntityQuery(ComponentType.ReadWrite<WeatherComponent>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            foreach (var entity in entities)
            {
                var weather = EntityManager.GetComponentData<WeatherComponent>(entity);
                weather.isWeatherActive = true;
                weather.needsWeatherUpdate = true;
                EntityManager.SetComponentData(entity, weather);
            }
        }
        
        /// <summary>
        /// Cleans up buffers
        /// </summary>
        protected override void OnDestroy()
        {
            if (moistureBuffer != null)
            {
                moistureBuffer.Release();
                moistureBuffer = null;
            }
            
            if (temperatureBuffer != null)
            {
                temperatureBuffer.Release();
                temperatureBuffer = null;
            }
            
            DOTS.Terrain.Core.DebugSettings.LogWeather("HybridWeatherSystem: Cleanup complete");
        }
        
        private void DebugLog(string message, bool verbose = false)
        {
            DOTS.Terrain.Core.DebugSettings.LogWeather($"HybridWeatherSystem: {message}", verbose);
        }
        
        private void DebugError(string message)
        {
            DOTS.Terrain.Core.DebugSettings.LogError($"HybridWeatherSystem: {message}");
        }
        
        private void DebugWarning(string message)
        {
            DOTS.Terrain.Core.DebugSettings.LogWarning($"HybridWeatherSystem: {message}");
        }
    }
} 