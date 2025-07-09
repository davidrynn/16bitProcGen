using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.Weather
{
    /// <summary>
    /// Component that stores weather data for a terrain chunk
    /// </summary>
    public struct WeatherComponent : IComponentData
    {
        // Weather type
        public WeatherType weatherType;
        
        // Weather parameters
        public float temperature;
        public float humidity;
        public float windSpeed;
        public float windDirection;
        
        // Effect intensity
        public float intensity;
        
        // Time-based variation
        public float timeOffset;
        
        // Flags
        public bool needsWeatherUpdate;
        public bool isWeatherActive;
    }
    
    /// <summary>
    /// Weather types that can be applied to terrain
    /// </summary>
    public enum WeatherType : byte
    {
        Clear = 0,
        Rain = 1,
        Snow = 2,
        Storm = 3,
        Fog = 4,
        AcidRain = 5,
        Blizzard = 6,
        SandStorm = 7,
        VolcanicAsh = 8
    }
    
    /// <summary>
    /// Component for weather effect buffers
    /// </summary>
    public struct WeatherEffectBuffer : IComponentData
    {
        public Entity bufferEntity;
    }
} 