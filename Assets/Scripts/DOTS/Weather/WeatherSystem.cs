using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using DOTS.Terrain.Generation;

namespace DOTS.Terrain.Weather
{
    /// <summary>
    /// DOTS Weather System that manages weather effects for terrain chunks
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(DOTS.Terrain.Generation.HybridTerrainGenerationSystem))]
    public partial class WeatherSystem : SystemBase
    {
        // Weather settings
        private float weatherChangeInterval = 30f;
        private float lastWeatherChange;
        
        // Performance monitoring
        private int chunksWithWeather;
        private float lastUpdateTime;
        
        // Debug settings
        private bool enableDebugLogs = false;
        
        protected override void OnCreate()
        {
            Debug.Log("[DOTS] WeatherSystem: Initializing...");
            lastWeatherChange = 0f;
            chunksWithWeather = 0;
            lastUpdateTime = 0f;
            Debug.Log("[DOTS] WeatherSystem: Initialization complete");
        }
        
        protected override void OnUpdate()
        {
            float currentTime = (float)SystemAPI.Time.ElapsedTime;
            
            // Update weather periodically
            if (currentTime - lastWeatherChange > weatherChangeInterval)
            {
                UpdateGlobalWeather();
                lastWeatherChange = currentTime;
            }
            
            // Process weather effects for terrain chunks
            ProcessWeatherEffects();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        /// <summary>
        /// Updates global weather conditions
        /// </summary>
        private void UpdateGlobalWeather()
        {
            DebugLog("Updating global weather conditions");
            
            // Simple weather cycle for testing
            WeatherType newWeather = GetRandomWeather();
            
            Entities
                .WithAll<WeatherComponent>()
                .ForEach((Entity entity, ref WeatherComponent weather) =>
                {
                    weather.weatherType = newWeather;
                    weather.needsWeatherUpdate = true;
                    weather.intensity = UnityEngine.Random.Range(0.3f, 1.0f);
                    weather.timeOffset = UnityEngine.Random.Range(0f, 1000f);
                }).WithoutBurst().Run();
                
            DebugLog($"Set global weather to: {newWeather}");
        }
        
        /// <summary>
        /// Processes weather effects for all terrain chunks
        /// </summary>
        private void ProcessWeatherEffects()
        {
            chunksWithWeather = 0;
            
            Entities
                .WithAll<WeatherComponent>()
                .ForEach((Entity entity, ref WeatherComponent weather) =>
                {
                    if (weather.needsWeatherUpdate)
                    {
                        ApplyWeatherToChunk(ref weather);
                        weather.needsWeatherUpdate = false;
                        weather.isWeatherActive = true;
                        chunksWithWeather++;
                    }
                }).WithoutBurst().Run();
        }
        
        /// <summary>
        /// Applies weather effects to a specific chunk
        /// </summary>
        private void ApplyWeatherToChunk(ref WeatherComponent weather)
        {
            // Set weather parameters based on type
            switch (weather.weatherType)
            {
                case WeatherType.Rain:
                    weather.temperature = 15f;
                    weather.humidity = 0.8f;
                    weather.windSpeed = 5f;
                    break;
                    
                case WeatherType.Snow:
                    weather.temperature = -5f;
                    weather.humidity = 0.6f;
                    weather.windSpeed = 3f;
                    break;
                    
                case WeatherType.Storm:
                    weather.temperature = 20f;
                    weather.humidity = 0.9f;
                    weather.windSpeed = 15f;
                    break;
                    
                case WeatherType.Fog:
                    weather.temperature = 10f;
                    weather.humidity = 0.95f;
                    weather.windSpeed = 1f;
                    break;
                    
                case WeatherType.Clear:
                default:
                    weather.temperature = 25f;
                    weather.humidity = 0.3f;
                    weather.windSpeed = 2f;
                    break;
            }
            
            weather.windDirection = UnityEngine.Random.Range(0f, 360f);
        }
        
        /// <summary>
        /// Gets a random weather type for testing
        /// </summary>
        private WeatherType GetRandomWeather()
        {
            float random = UnityEngine.Random.Range(0f, 1f);
            
            if (random < 0.4f) return WeatherType.Clear;
            if (random < 0.6f) return WeatherType.Rain;
            if (random < 0.75f) return WeatherType.Snow;
            if (random < 0.85f) return WeatherType.Storm;
            if (random < 0.95f) return WeatherType.Fog;
            
            return WeatherType.Clear;
        }
        
        /// <summary>
        /// Updates performance metrics
        /// </summary>
        private void UpdatePerformanceMetrics()
        {
            lastUpdateTime = (float)SystemAPI.Time.ElapsedTime;
        }
        
        /// <summary>
        /// Gets current weather statistics
        /// </summary>
        public (int activeChunks, WeatherType currentWeather) GetWeatherStats()
        {
            WeatherType currentWeather = WeatherType.Clear;
            
            // Get the most common weather type
            Entities
                .WithAll<WeatherComponent>()
                .ForEach((in WeatherComponent weather) =>
                {
                    if (weather.isWeatherActive)
                    {
                        currentWeather = weather.weatherType;
                    }
                }).WithoutBurst().Run();
                
            return (chunksWithWeather, currentWeather);
        }
        
        /// <summary>
        /// Forces a weather change for testing
        /// </summary>
        public void ForceWeatherChange(WeatherType newWeather)
        {
            DebugLog($"Forcing weather change to: {newWeather}");
            
            Entities
                .WithAll<WeatherComponent>()
                .ForEach((Entity entity, ref WeatherComponent weather) =>
                {
                    weather.weatherType = newWeather;
                    weather.needsWeatherUpdate = true;
                    weather.intensity = 1.0f;
                }).WithoutBurst().Run();
        }
        
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[DOTS] WeatherSystem: {message}");
            }
        }
    }
} 