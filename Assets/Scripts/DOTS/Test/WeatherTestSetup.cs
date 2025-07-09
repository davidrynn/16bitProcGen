using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain.Weather;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

namespace DOTS.Test
{
    /// <summary>
    /// Test setup component that adds weather components to terrain entities
    /// </summary>
    public class WeatherTestSetup : MonoBehaviour
    {
        [Header("Weather Test Settings")]
        [SerializeField] private bool runOnStart = true;
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private bool showDebugGUI = true;
        [SerializeField] private bool monitorTerrainChanges = true;
        
        [Header("Weather Configuration")]
        [SerializeField] private WeatherType initialWeather = WeatherType.Clear;
        [SerializeField] private float weatherChangeInterval = 10f;
        [SerializeField] private bool autoChangeWeather = true;
        
        [Header("Weather Parameters")]
        [SerializeField] private float temperature = 20f;
        [SerializeField] private float humidity = 0.5f;
        [SerializeField] private float windSpeed = 5f;
        [SerializeField] private float weatherIntensity = 0.8f;
        
        [Header("Visual Debug")]
        [SerializeField] private Color clearWeatherColor = Color.yellow;
        [SerializeField] private Color rainWeatherColor = Color.blue;
        [SerializeField] private Color snowWeatherColor = Color.white;
        [SerializeField] private Color stormWeatherColor = Color.gray;
        [SerializeField] private Color fogWeatherColor = Color.cyan;
        
        private EntityManager entityManager;
        private EntityQuery terrainQuery;
        private DOTS.Terrain.Weather.WeatherSystem weatherSystem;
        private DOTS.Terrain.Weather.HybridWeatherSystem hybridWeatherSystem;
        
        // Debug data
        private WeatherType currentWeather = WeatherType.Clear;
        private int activeWeatherChunks = 0;
        private float lastTerrainHeight = 0f;
        private float currentTerrainHeight = 0f;
        private float weatherEffectIntensity = 0f;
        private string debugStatus = "Initializing...";
        
        void Start()
        {
            if (runOnStart)
            {
                SetupWeatherTest();
            }
        }
        
        void Update()
        {
            // Test weather changes with number keys
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ForceWeatherChange(WeatherType.Clear);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                ForceWeatherChange(WeatherType.Rain);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                ForceWeatherChange(WeatherType.Snow);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                ForceWeatherChange(WeatherType.Storm);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                ForceWeatherChange(WeatherType.Fog);
            }
            
            // Update debug information
            UpdateDebugInfo();
            
            // Monitor terrain changes
            if (monitorTerrainChanges)
            {
                MonitorTerrainHeightChanges();
            }
        }
        
        /// <summary>
        /// Sets up weather testing by adding weather components to terrain entities
        /// </summary>
        public void SetupWeatherTest()
        {
            Debug.Log("[WeatherTest] Setting up weather test...");
            
            // Get the default world
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[WeatherTest] No default world found!");
                return;
            }
            
            entityManager = world.EntityManager;
            
            // Get weather systems - they might not exist yet, so we'll check for them in Update
            try
            {
                weatherSystem = world.GetOrCreateSystemManaged<DOTS.Terrain.Weather.WeatherSystem>();
                if (weatherSystem != null)
                {
                    Debug.Log("[WeatherTest] Found WeatherSystem");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WeatherTest] WeatherSystem not found: {e.Message}");
            }
            
            try
            {
                hybridWeatherSystem = world.GetOrCreateSystemManaged<DOTS.Terrain.Weather.HybridWeatherSystem>();
                if (hybridWeatherSystem != null)
                {
                    Debug.Log("[WeatherTest] Found HybridWeatherSystem");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[WeatherTest] HybridWeatherSystem not found: {e.Message}");
            }
            
            // Create query for terrain entities
            terrainQuery = entityManager.CreateEntityQuery(typeof(DOTS.Terrain.TerrainData));
            
            // Add weather components to existing terrain entities
            AddWeatherComponentsToTerrain();
            
            // Configure weather system
            ConfigureWeatherSystem();
            
            debugStatus = "Weather test setup complete!";
            Debug.Log("[WeatherTest] Weather test setup complete!");
        }
        
        /// <summary>
        /// Adds weather components to all terrain entities
        /// </summary>
        private void AddWeatherComponentsToTerrain()
        {
            var entities = terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            Debug.Log($"[WeatherTest] Found {entities.Length} terrain entities");
            
            foreach (var entity in entities)
            {
                if (!entityManager.HasComponent<WeatherComponent>(entity))
                {
                    // Add weather component
                    var weatherComponent = new WeatherComponent
                    {
                        weatherType = initialWeather,
                        temperature = temperature,
                        humidity = humidity,
                        windSpeed = windSpeed,
                        windDirection = UnityEngine.Random.Range(0f, 360f),
                        intensity = weatherIntensity,
                        timeOffset = UnityEngine.Random.Range(0f, 1000f),
                        needsWeatherUpdate = true,
                        isWeatherActive = true
                    };
                    
                    entityManager.AddComponentData(entity, weatherComponent);
                    
                    if (enableDebugLogs)
                    {
                        Debug.Log($"[WeatherTest] Added weather component to entity {entity.Index}");
                    }
                }
            }
            
            entities.Dispose();
        }
        
        /// <summary>
        /// Configures the weather system for testing
        /// </summary>
        private void ConfigureWeatherSystem()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            Debug.Log("[WeatherTest] Weather systems will be auto-registered by DOTS");
        }
        
        /// <summary>
        /// Forces a weather change for testing
        /// </summary>
        public void ForceWeatherChange(WeatherType newWeather)
        {
            Debug.Log($"[WeatherTest] Forcing weather change to: {newWeather}");
            currentWeather = newWeather;
            
            // Update all terrain entities with new weather directly
            var entities = terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<WeatherComponent>(entity))
                {
                    var weather = entityManager.GetComponentData<WeatherComponent>(entity);
                    weather.weatherType = newWeather;
                    weather.needsWeatherUpdate = true;
                    weather.intensity = weatherIntensity;
                    entityManager.SetComponentData(entity, weather);
                }
            }
            
            entities.Dispose();
            
            // Force weather system update
            if (weatherSystem != null)
            {
                try
                {
                    weatherSystem.ForceWeatherChange(newWeather);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[WeatherTest] Error forcing weather change: {e.Message}");
                }
            }
            
            debugStatus = $"Weather changed to: {newWeather}";
        }
        
        /// <summary>
        /// Updates debug information from weather systems
        /// </summary>
        private void UpdateDebugInfo()
        {
            // Try to get weather systems if they don't exist yet
            if (weatherSystem == null)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null)
                {
                    try
                    {
                        weatherSystem = world.GetOrCreateSystemManaged<DOTS.Terrain.Weather.WeatherSystem>();
                    }
                    catch { }
                }
            }
            
            if (hybridWeatherSystem == null)
            {
                var world = World.DefaultGameObjectInjectionWorld;
                if (world != null)
                {
                    try
                    {
                        hybridWeatherSystem = world.GetOrCreateSystemManaged<DOTS.Terrain.Weather.HybridWeatherSystem>();
                    }
                    catch { }
                }
            }
            
            // Update stats if systems exist
            if (weatherSystem != null)
            {
                try
                {
                    var stats = weatherSystem.GetWeatherStats();
                    activeWeatherChunks = stats.activeChunks;
                    
                    if (currentWeather != stats.currentWeather)
                    {
                        currentWeather = stats.currentWeather;
                        debugStatus = $"Weather auto-changed to: {currentWeather}";
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[WeatherTest] Error getting weather stats: {e.Message}");
                }
            }
            
            if (hybridWeatherSystem != null)
            {
                try
                {
                    var hybridStats = hybridWeatherSystem.GetWeatherEffectStats();
                    weatherEffectIntensity = hybridStats.activeChunks > 0 ? 1f : 0f;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[WeatherTest] Error getting hybrid weather stats: {e.Message}");
                }
            }
        }
        
        /// <summary>
        /// Monitors terrain height changes to show weather effects
        /// </summary>
        private void MonitorTerrainHeightChanges()
        {
            var entities = terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                var terrainData = entityManager.GetComponentData<DOTS.Terrain.TerrainData>(entities[0]);
                currentTerrainHeight = terrainData.averageHeight;
                
                if (lastTerrainHeight != 0f && Mathf.Abs(currentTerrainHeight - lastTerrainHeight) > 0.01f)
                {
                    debugStatus = $"Terrain height changed: {lastTerrainHeight:F2} -> {currentTerrainHeight:F2} (Weather: {currentWeather})";
                }
                
                lastTerrainHeight = currentTerrainHeight;
            }
            
            entities.Dispose();
        }
        
        /// <summary>
        /// Gets current weather statistics
        /// </summary>
        public void LogWeatherStats()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            
            Debug.Log($"[WeatherTest] Current Weather: {currentWeather}");
            Debug.Log($"[WeatherTest] Active Weather Chunks: {activeWeatherChunks}");
            Debug.Log($"[WeatherTest] Weather Effect Intensity: {weatherEffectIntensity}");
            Debug.Log($"[WeatherTest] Current Terrain Height: {currentTerrainHeight:F2}");
        }
        
        void OnGUI()
        {
            if (!showDebugGUI) return;
            
            // Weather status panel
            GUILayout.BeginArea(new Rect(20, 20, 400, 300));
            GUILayout.BeginVertical("box");
            
            // Title
            GUILayout.Label("=== WEATHER SYSTEM DEBUG ===", GUI.skin.box);
            
            // Current weather with color indicator
            Color originalColor = GUI.color;
            GUI.color = GetWeatherColor(currentWeather);
            GUILayout.Label($"Current Weather: {currentWeather}", GUI.skin.box);
            GUI.color = originalColor;
            
            // Status information
            GUILayout.Label($"Status: {debugStatus}");
            GUILayout.Label($"Active Weather Chunks: {activeWeatherChunks}");
            GUILayout.Label($"Weather Effect Intensity: {weatherEffectIntensity:F2}");
            GUILayout.Label($"Current Terrain Height: {currentTerrainHeight:F2}");
            GUILayout.Label($"Temperature: {temperature:F1}°C");
            GUILayout.Label($"Humidity: {humidity:F2}");
            GUILayout.Label($"Wind Speed: {windSpeed:F1} m/s");
            
            // System status
            GUILayout.Space(10);
            GUILayout.Label("=== SYSTEM STATUS ===", GUI.skin.box);
            GUILayout.Label($"WeatherSystem: {(weatherSystem != null ? "✓ Active" : "✗ Missing")}");
            GUILayout.Label($"HybridWeatherSystem: {(hybridWeatherSystem != null ? "✓ Active" : "✗ Missing")}");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            // Controls panel
            GUILayout.BeginArea(new Rect(20, 340, 400, 200));
            GUILayout.BeginVertical("box");
            
            GUILayout.Label("=== WEATHER CONTROLS ===", GUI.skin.box);
            GUILayout.Label("1: Clear Weather");
            GUILayout.Label("2: Rain");
            GUILayout.Label("3: Snow");
            GUILayout.Label("4: Storm");
            GUILayout.Label("5: Fog");
            GUILayout.Label("Space: Log Weather Stats");
            
            GUILayout.Space(10);
            GUILayout.Label("=== WEATHER EFFECTS ===", GUI.skin.box);
            GUILayout.Label("• Rain: Erodes terrain, increases moisture");
            GUILayout.Label("• Snow: Accumulates on high areas");
            GUILayout.Label("• Storm: Strong erosion, wind effects");
            GUILayout.Label("• Fog: Minimal terrain impact");
            GUILayout.Label("• Clear: No weather effects");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
            
            // Visual weather indicator
            DrawWeatherIndicator();
        }
        
        /// <summary>
        /// Gets the color for a weather type
        /// </summary>
        private Color GetWeatherColor(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear: return clearWeatherColor;
                case WeatherType.Rain: return rainWeatherColor;
                case WeatherType.Snow: return snowWeatherColor;
                case WeatherType.Storm: return stormWeatherColor;
                case WeatherType.Fog: return fogWeatherColor;
                default: return Color.white;
            }
        }
        
        /// <summary>
        /// Draws a visual weather indicator
        /// </summary>
        private void DrawWeatherIndicator()
        {
            // Draw weather indicator in top-right corner
            Rect indicatorRect = new Rect(Screen.width - 120, 20, 100, 100);
            
            // Background
            GUI.color = new Color(0, 0, 0, 0.7f);
            GUI.Box(indicatorRect, "");
            
            // Weather icon/indicator
            GUI.color = GetWeatherColor(currentWeather);
            GUI.Box(new Rect(indicatorRect.x + 10, indicatorRect.y + 10, 80, 80), GetWeatherSymbol(currentWeather));
            
            // Intensity indicator
            GUI.color = Color.white;
            float intensityBarWidth = 80f * weatherEffectIntensity;
            GUI.Box(new Rect(indicatorRect.x + 10, indicatorRect.y + 95, intensityBarWidth, 5), "");
            
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Gets a symbol for the weather type
        /// </summary>
        private string GetWeatherSymbol(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Clear: return "☀";
                case WeatherType.Rain: return "🌧";
                case WeatherType.Snow: return "❄";
                case WeatherType.Storm: return "⛈";
                case WeatherType.Fog: return "🌫";
                default: return "?";
            }
        }
    }
} 