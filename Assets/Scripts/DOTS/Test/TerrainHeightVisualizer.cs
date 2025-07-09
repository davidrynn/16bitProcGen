using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Terrain.Weather;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

namespace DOTS.Test
{
    /// <summary>
    /// Visualizes terrain height changes to show weather effects
    /// </summary>
    public class TerrainHeightVisualizer : MonoBehaviour
    {
        [Header("Visualization Settings")]
        [SerializeField] private bool enableVisualization = true;
        [SerializeField] private bool showHeightGraph = true;
        [SerializeField] private bool showWeatherOverlay = true;
        [SerializeField] private float updateInterval = 0.5f;
        
        [Header("Height Graph")]
        [SerializeField] private int graphWidth = 200;
        [SerializeField] private int graphHeight = 100;
        [SerializeField] private Color heightColor = Color.green;
        [SerializeField] private Color changeColor = Color.red;
        
        [Header("Weather Overlay")]
        [SerializeField] private Color rainOverlayColor = new Color(0, 0, 1, 0.3f);
        [SerializeField] private Color snowOverlayColor = new Color(1, 1, 1, 0.4f);
        [SerializeField] private Color stormOverlayColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color fogOverlayColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
        
        [Header("Layout Settings")]
        [SerializeField] private int margin = 20;
        [SerializeField] private int textHeight = 25;
        [SerializeField] private int panelWidth = 250;
        [SerializeField] private int panelHeight = 120;
        
        private EntityManager entityManager;
        private EntityQuery terrainQuery;
        private DOTS.Terrain.Weather.WeatherSystem weatherSystem;
        
        // Height tracking
        private float[] heightHistory = new float[100];
        private int historyIndex = 0;
        private float lastHeight = 0f;
        private float maxHeightChange = 0f;
        private float lastUpdateTime = 0f;
        
        // Weather tracking
        private WeatherType currentWeather = WeatherType.Clear;
        private float weatherIntensity = 0f;
        private int activeChunks = 0;
        
        private Vector2 scrollPosition = Vector2.zero;
        
        void Start()
        {
            // Get DOTS world
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[TerrainHeightVisualizer] No DOTS world found!");
                return;
            }
            
            entityManager = world.EntityManager;
            terrainQuery = entityManager.CreateEntityQuery(typeof(DOTS.Terrain.TerrainData));
            
            // Try to get weather system
            try
            {
                weatherSystem = world.GetOrCreateSystemManaged<DOTS.Terrain.Weather.WeatherSystem>();
                if (weatherSystem != null)
                {
                    Debug.Log("[TerrainHeightVisualizer] Found WeatherSystem");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[TerrainHeightVisualizer] WeatherSystem not found: {e.Message}");
            }
            
            // Initialize height history
            for (int i = 0; i < heightHistory.Length; i++)
            {
                heightHistory[i] = 0f;
            }
        }
        
        void Update()
        {
            if (!enableVisualization) return;
            
            float currentTime = Time.time;
            if (currentTime - lastUpdateTime > updateInterval)
            {
                UpdateHeightData();
                lastUpdateTime = currentTime;
            }
        }
        
        /// <summary>
        /// Updates height data from terrain entities
        /// </summary>
        private void UpdateHeightData()
        {
            var entities = terrainQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            if (entities.Length > 0)
            {
                // Get average height from all terrain chunks
                float totalHeight = 0f;
                int chunkCount = 0;
                
                foreach (var entity in entities)
                {
                    if (entityManager.HasComponent<DOTS.Terrain.TerrainData>(entity))
                    {
                        var terrainData = entityManager.GetComponentData<DOTS.Terrain.TerrainData>(entity);
                        totalHeight += terrainData.averageHeight;
                        chunkCount++;
                    }
                }
                
                if (chunkCount > 0)
                {
                    float currentHeight = totalHeight / chunkCount;
                    
                    // Update height history
                    heightHistory[historyIndex] = currentHeight;
                    historyIndex = (historyIndex + 1) % heightHistory.Length;
                    
                    // Calculate height change
                    if (lastHeight != 0f)
                    {
                        float heightChange = Mathf.Abs(currentHeight - lastHeight);
                        maxHeightChange = Mathf.Max(maxHeightChange, heightChange);
                        
                        if (heightChange > 0.001f)
                        {
                            Debug.Log($"[TerrainHeightVisualizer] Height changed: {lastHeight:F4} -> {currentHeight:F4} (Δ: {heightChange:F4})");
                        }
                    }
                    
                    lastHeight = currentHeight;
                }
            }
            
            entities.Dispose();
            
            // Update weather info
            if (weatherSystem != null)
            {
                try
                {
                    var stats = weatherSystem.GetWeatherStats();
                    activeChunks = stats.Item1;
                    currentWeather = stats.Item2;
                    
                    // Calculate weather intensity based on active chunks
                    weatherIntensity = activeChunks > 0 ? 0.8f : 0f;
                    
                    // Debug weather info
                    if (activeChunks > 0)
                    {
                        Debug.Log($"[TerrainHeightVisualizer] Weather: {currentWeather}, Active Chunks: {activeChunks}, Intensity: {weatherIntensity}");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[TerrainHeightVisualizer] Error getting weather stats: {e.Message}");
                }
            }
        }
        
        void OnGUI()
        {
            if (!enableVisualization) return;
            
            if (showHeightGraph)
            {
                DrawHeightGraph();
            }
            
            if (showWeatherOverlay)
            {
                DrawWeatherOverlay();
            }
            
            DrawDebugInfo();
        }
        
        /// <summary>
        /// Draws a real-time height graph
        /// </summary>
        private void DrawHeightGraph()
        {
            // Height graph (bottom-right)
            Rect graphRect = new Rect(Screen.width - graphWidth - margin, Screen.height - graphHeight - margin - 50, 
                                    graphWidth, graphHeight);
            
            // Background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Box(graphRect, "");
            
            // Title
            GUI.color = Color.white;
            GUI.Label(new Rect(graphRect.x + 5, graphRect.y + 5, graphRect.width - 10, 20), "Height Over Time");
            
            // Draw height line
            if (heightHistory.Length > 1)
            {
                GUI.color = heightColor;
                
                for (int i = 1; i < heightHistory.Length; i++)
                {
                    int prevIndex = (historyIndex - i + heightHistory.Length) % heightHistory.Length;
                    int currIndex = (historyIndex - i + 1 + heightHistory.Length) % heightHistory.Length;
                    
                    float prevHeight = heightHistory[prevIndex];
                    float currHeight = heightHistory[currIndex];
                    
                    if (prevHeight > 0f && currHeight > 0f)
                    {
                        float x1 = graphRect.x + 10 + (i - 1) * (graphRect.width - 20) / (heightHistory.Length - 1);
                        float x2 = graphRect.x + 10 + i * (graphRect.width - 20) / (heightHistory.Length - 1);
                        
                        float y1 = graphRect.y + graphRect.height - 10 - (prevHeight * (graphRect.height - 30));
                        float y2 = graphRect.y + graphRect.height - 10 - (currHeight * (graphRect.height - 30));
                        
                        // Clamp to graph bounds
                        y1 = Mathf.Clamp(y1, graphRect.y + 25, graphRect.y + graphRect.height - 10);
                        y2 = Mathf.Clamp(y2, graphRect.y + 25, graphRect.y + graphRect.height - 10);
                        
                        DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), 2f);
                    }
                }
            }
            
            // Draw change indicator
            if (maxHeightChange > 0.001f)
            {
                GUI.color = changeColor;
                GUI.Label(new Rect(graphRect.x + 5, graphRect.y + graphRect.height - 20, graphRect.width - 10, 20), 
                         $"Max Change: {maxHeightChange:F4}");
            }
            
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws weather overlay
        /// </summary>
        private void DrawWeatherOverlay()
        {
            // Show overlay even with low intensity for testing
            if (weatherIntensity > 0.01f || activeChunks > 0)
            {
                Color overlayColor = GetWeatherOverlayColor(currentWeather);
                
                // Make overlay more visible for testing
                overlayColor.a = Mathf.Max(0.2f, overlayColor.a * weatherIntensity);
                
                GUI.color = overlayColor;
                GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "");
                GUI.color = Color.white;
                
                // Add weather text overlay with proper positioning
                DrawWeatherInfo();
            }
        }

        private void DrawWeatherInfo()
        {
            // Weather info panel (top-left)
            Rect weatherPanel = new Rect(margin, margin, panelWidth, panelHeight);
            
            // Background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Box(weatherPanel, "");
            
            // Title
            GUI.color = Color.white;
            GUI.Label(new Rect(weatherPanel.x + 10, weatherPanel.y + 5, weatherPanel.width - 20, textHeight), 
                     $"Weather: {currentWeather}");
            
            // Weather details
            GUI.Label(new Rect(weatherPanel.x + 10, weatherPanel.y + 30, weatherPanel.width - 20, textHeight), 
                     $"Active Chunks: {activeChunks}");
            GUI.Label(new Rect(weatherPanel.x + 10, weatherPanel.y + 55, weatherPanel.width - 20, textHeight), 
                     $"Intensity: {weatherIntensity:F2}");
            
            // Height info
            GUI.Label(new Rect(weatherPanel.x + 10, weatherPanel.y + 80, weatherPanel.width - 20, textHeight), 
                     $"Height: {lastHeight:F4}");
            GUI.Label(new Rect(weatherPanel.x + 10, weatherPanel.y + 105, weatherPanel.width - 20, textHeight), 
                     $"Max Change: {maxHeightChange:F4}");
            
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws debug information
        /// </summary>
        private void DrawDebugInfo()
        {
            // Debug info panel (top-right)
            Rect debugPanel = new Rect(Screen.width - panelWidth - margin, margin, panelWidth, panelHeight);
            
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Box(debugPanel, "");
            
            GUI.color = Color.white;
            GUI.Label(new Rect(debugPanel.x + 10, debugPanel.y + 5, debugPanel.width - 20, textHeight), "Debug Info");
            GUI.Label(new Rect(debugPanel.x + 10, debugPanel.y + 30, debugPanel.width - 20, textHeight), 
                     $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}");
            GUI.Label(new Rect(debugPanel.x + 10, debugPanel.y + 55, debugPanel.width - 20, textHeight), 
                     $"Time: {Time.time:F1}s");
            GUI.Label(new Rect(debugPanel.x + 10, debugPanel.y + 80, debugPanel.width - 20, textHeight), 
                     $"Entities: {terrainQuery.CalculateEntityCount()}");
            
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Gets overlay color for weather type
        /// </summary>
        private Color GetWeatherOverlayColor(WeatherType weather)
        {
            switch (weather)
            {
                case WeatherType.Rain: 
                    return new Color(0, 0, 1, 0.4f); // More visible blue
                case WeatherType.Snow: 
                    return new Color(1, 1, 1, 0.5f); // More visible white
                case WeatherType.Storm: 
                    return new Color(0.3f, 0.3f, 0.3f, 0.6f); // More visible dark gray
                case WeatherType.Fog: 
                    return new Color(0.8f, 0.8f, 0.8f, 0.4f); // More visible light gray
                default: 
                    return new Color(0, 1, 0, 0.2f); // Slight green tint for clear weather
            }
        }
        
        /// <summary>
        /// Draws a line between two points
        /// </summary>
        private void DrawLine(Vector2 start, Vector2 end, float thickness)
        {
            // Simple line drawing using GUI.Box
            Vector2 direction = end - start;
            float distance = direction.magnitude;
            direction.Normalize();
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            
            GUI.matrix = Matrix4x4.TRS(start, Quaternion.Euler(0, 0, angle), Vector3.one);
            GUI.Box(new Rect(0, -thickness / 2, distance, thickness), "");
            GUI.matrix = Matrix4x4.identity;
        }
        
        /// <summary>
        /// Resets the height change tracking
        /// </summary>
        public void ResetHeightTracking()
        {
            maxHeightChange = 0f;
            for (int i = 0; i < heightHistory.Length; i++)
            {
                heightHistory[i] = 0f;
            }
            historyIndex = 0;
            lastHeight = 0f;
        }
        
        /// <summary>
        /// Gets current height statistics
        /// </summary>
        public (float currentHeight, float maxChange, WeatherType weather) GetHeightStats()
        {
            return (lastHeight, maxHeightChange, currentWeather);
        }
    }
} 