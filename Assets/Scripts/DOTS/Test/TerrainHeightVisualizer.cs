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
        [SerializeField] private float graphWidthPercent = 0.15f; // 15% of screen width
        [SerializeField] private float graphHeightPercent = 0.12f; // 12% of screen height
        [SerializeField] private Color heightColor = Color.green;
        [SerializeField] private Color changeColor = Color.red;
        
        [Header("Weather Overlay")]
        [SerializeField] private Color rainOverlayColor = new Color(0, 0, 1, 0.3f);
        [SerializeField] private Color snowOverlayColor = new Color(1, 1, 1, 0.4f);
        [SerializeField] private Color stormOverlayColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] private Color fogOverlayColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
        
        [Header("Layout Settings")]
        [SerializeField] private float marginPercent = 0.02f; // 2% of screen size
        [SerializeField] private float textHeightPercent = 0.03f; // 3% of screen height
        [SerializeField] private float panelWidthPercent = 0.18f; // 18% of screen width
        [SerializeField] private float panelHeightPercent = 0.15f; // 15% of screen height
        
        [Header("Input Settings")]
        [SerializeField] private KeyCode toggleCursorKey = KeyCode.Tab;
        [SerializeField] private bool enableCursorToggle = true;
        
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
        private bool cursorUnlocked = false;
        
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
            
            HandleCursorToggle();
            
            float currentTime = Time.time;
            if (currentTime - lastUpdateTime > updateInterval)
            {
                UpdateHeightData();
                lastUpdateTime = currentTime;
            }
        }
        
        private void HandleCursorToggle()
        {
            if (!enableCursorToggle) return;
            
            if (Input.GetKeyDown(toggleCursorKey))
            {
                cursorUnlocked = !cursorUnlocked;
                
                if (cursorUnlocked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
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
            
            // Handle mouse wheel input for scroll views
            HandleMouseWheel();
            
            if (showHeightGraph)
            {
                DrawHeightGraph();
            }
            
            if (showWeatherOverlay)
            {
                DrawWeatherOverlay();
            }
            
            DrawDebugInfo();
            
            // Show cursor status
            if (cursorUnlocked)
            {
                DrawCursorStatus();
            }
        }
        
        private void HandleMouseWheel()
        {
            float scrollDelta = Input.GetAxis("Mouse ScrollWheel");
            if (scrollDelta != 0)
            {
                // Apply scroll to the weather info panel
                scrollPosition.y -= scrollDelta * 50f; // Adjust sensitivity
                scrollPosition.y = Mathf.Max(0, scrollPosition.y);
            }
        }
        
        private void DrawCursorStatus()
        {
            // Show a small indicator that cursor is unlocked
            GUI.color = new Color(1, 1, 0, 0.8f);
            GUI.Label(new Rect(10, Screen.height - 30, 200, 20), "Cursor Unlocked - Press Tab to lock");
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws a real-time height graph
        /// </summary>
        private void DrawHeightGraph()
        {
            // Calculate proportional dimensions with constraints
            int graphWidth = Mathf.Clamp(Mathf.RoundToInt(Screen.width * graphWidthPercent), 150, 400);
            int graphHeight = Mathf.Clamp(Mathf.RoundToInt(Screen.height * graphHeightPercent), 80, 200);
            int margin = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(Screen.width, Screen.height) * marginPercent), 10, 50);
            
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
            // Calculate proportional dimensions with constraints
            int margin = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(Screen.width, Screen.height) * marginPercent), 10, 50);
            int panelWidth = Mathf.Clamp(Mathf.RoundToInt(Screen.width * panelWidthPercent), 200, 400);
            int panelHeight = Mathf.Clamp(Mathf.RoundToInt(Screen.height * panelHeightPercent), 100, 200);
            int textHeight = Mathf.Clamp(Mathf.RoundToInt(Screen.height * textHeightPercent), 20, 40);
            
            // Weather info panel (top-left)
            Rect weatherPanel = new Rect(margin, margin, panelWidth, panelHeight);
            
            // Background
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Box(weatherPanel, "");
            
            // Create scroll view for weather info
            Rect scrollViewRect = new Rect(weatherPanel.x + 5, weatherPanel.y + 5, weatherPanel.width - 10, weatherPanel.height - 10);
            Rect contentRect = new Rect(0, 0, scrollViewRect.width - 20, textHeight * 8); // Extra height for content
            
            scrollPosition = GUI.BeginScrollView(scrollViewRect, scrollPosition, contentRect);
            
            // Title
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 0, contentRect.width, textHeight), $"Weather: {currentWeather}");
            
            // Weather details
            GUI.Label(new Rect(0, textHeight, contentRect.width, textHeight), $"Active Chunks: {activeChunks}");
            GUI.Label(new Rect(0, textHeight * 2, contentRect.width, textHeight), $"Intensity: {weatherIntensity:F2}");
            
            // Height info
            GUI.Label(new Rect(0, textHeight * 3, contentRect.width, textHeight), $"Height: {lastHeight:F4}");
            GUI.Label(new Rect(0, textHeight * 4, contentRect.width, textHeight), $"Max Change: {maxHeightChange:F4}");
            
            // Additional debug info that might be useful
            GUI.Label(new Rect(0, textHeight * 5, contentRect.width, textHeight), $"Time: {Time.time:F1}s");
            GUI.Label(new Rect(0, textHeight * 6, contentRect.width, textHeight), $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}");
            
            GUI.EndScrollView();
            GUI.color = Color.white;
        }
        
        /// <summary>
        /// Draws debug information
        /// </summary>
        private void DrawDebugInfo()
        {
            // Calculate proportional dimensions with constraints
            int margin = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(Screen.width, Screen.height) * marginPercent), 10, 50);
            int panelWidth = Mathf.Clamp(Mathf.RoundToInt(Screen.width * panelWidthPercent), 200, 400);
            int panelHeight = Mathf.Clamp(Mathf.RoundToInt(Screen.height * panelHeightPercent), 100, 200);
            int textHeight = Mathf.Clamp(Mathf.RoundToInt(Screen.height * textHeightPercent), 20, 40);
            
            // Debug info panel (top-right)
            Rect debugPanel = new Rect(Screen.width - panelWidth - margin, margin, panelWidth, panelHeight);
            
            GUI.color = new Color(0, 0, 0, 0.8f);
            GUI.Box(debugPanel, "");
            
            // Create scroll view for debug info
            Rect scrollViewRect = new Rect(debugPanel.x + 5, debugPanel.y + 5, debugPanel.width - 10, debugPanel.height - 10);
            Rect contentRect = new Rect(0, 0, scrollViewRect.width - 20, textHeight * 10); // Extra height for content
            
            Vector2 debugScrollPosition = GUI.BeginScrollView(scrollViewRect, Vector2.zero, contentRect);
            
            GUI.color = Color.white;
            GUI.Label(new Rect(0, 0, contentRect.width, textHeight), "Debug Info");
            GUI.Label(new Rect(0, textHeight, contentRect.width, textHeight), 
                     $"FPS: {Mathf.RoundToInt(1f / Time.deltaTime)}");
            GUI.Label(new Rect(0, textHeight * 2, contentRect.width, textHeight), 
                     $"Time: {Time.time:F1}s");
            GUI.Label(new Rect(0, textHeight * 3, contentRect.width, textHeight), 
                     $"Entities: {terrainQuery.CalculateEntityCount()}");
            
            // Additional debug info
            GUI.Label(new Rect(0, textHeight * 4, contentRect.width, textHeight), 
                     $"Screen: {Screen.width}x{Screen.height}");
            GUI.Label(new Rect(0, textHeight * 5, contentRect.width, textHeight), 
                     $"Memory: {System.GC.GetTotalMemory(false) / 1024 / 1024}MB");
            
            // Instructions
            GUI.Label(new Rect(0, textHeight * 6, contentRect.width, textHeight), 
                     "=== CONTROLS ===");
            GUI.Label(new Rect(0, textHeight * 7, contentRect.width, textHeight), 
                     $"Press {toggleCursorKey} to toggle cursor");
            GUI.Label(new Rect(0, textHeight * 8, contentRect.width, textHeight), 
                     "Mouse wheel to scroll panels");
            GUI.Label(new Rect(0, textHeight * 9, contentRect.width, textHeight), 
                     "Click and drag to scroll");
            
            GUI.EndScrollView();
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