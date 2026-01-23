using UnityEngine;

namespace DOTS.Terrain.Core
{
    /// <summary>
    /// MonoBehaviour to control debug settings from the Unity Inspector
    /// </summary>
    public class DebugController : MonoBehaviour
    {
        [Header("Global Debug Settings")]
        [Tooltip("Enable all debug logging")]
        public bool enableDebugLogging = false;
        
        [Header("System-Specific Debug")]
        [Tooltip("Enable WFC system debug logging")]
        public bool enableWFCDebug = false;
        
        [Tooltip("Enable terrain system debug logging")]
        public bool enableTerrainDebug = false;
        
        [Tooltip("Enable weather system debug logging")]
        public bool enableWeatherDebug = false;
        
        [Tooltip("Enable rendering system debug logging")]
        public bool enableRenderingDebug = false;
        
        [Tooltip("Enable test system debug logging")]
        public bool enableTestDebug = false;
        
        [Tooltip("Enable seam validation debug logging")]
        public bool enableSeamDebug = false;
        
        [Header("Test System Control")]
        [Tooltip("Enable test systems to run (WFCSystemTest, SimpleRenderingTest, etc.)")]
        public bool enableTestSystems = false;
        
        [Header("Quick Presets")]
        [Tooltip("Enable all debug logging")]
        public bool enableAllDebug = false;
        
        [Tooltip("Disable all debug logging")]
        public bool disableAllDebug = false;
        
        void Start()
        {
            ApplySettings();
        }
        
        void Update()
        {
            // Apply settings if they've changed
            if (HasSettingsChanged())
            {
                ApplySettings();
            }
        }
        
        private bool HasSettingsChanged()
        {
            return DebugSettings.EnableDebugLogging != enableDebugLogging ||
                   DebugSettings.EnableWFCDebug != enableWFCDebug ||
                   DebugSettings.EnableTerrainDebug != enableTerrainDebug ||
                   DebugSettings.EnableWeatherDebug != enableWeatherDebug ||
                   DebugSettings.EnableRenderingDebug != enableRenderingDebug ||
                   DebugSettings.EnableTestDebug != enableTestDebug ||
                   DebugSettings.EnableSeamDebug != enableSeamDebug ||
                   DebugSettings.EnableTestSystems != enableTestSystems;
        }
        
        private void ApplySettings()
        {
            // Apply preset overrides first
            if (enableAllDebug)
            {
                enableDebugLogging = true;
                enableWFCDebug = true;
                enableTerrainDebug = true;
                enableWeatherDebug = true;
                enableRenderingDebug = true;
                enableTestDebug = true;
                enableSeamDebug = true;
                enableTestSystems = true;
            }
            
            if (disableAllDebug)
            {
                enableDebugLogging = false;
                enableWFCDebug = false;
                enableTerrainDebug = false;
                enableWeatherDebug = false;
                enableRenderingDebug = false;
                enableTestDebug = false;
                enableSeamDebug = false;
                enableTestSystems = false;
            }
            
            // Apply individual settings
            DebugSettings.EnableDebugLogging = enableDebugLogging;
            DebugSettings.EnableWFCDebug = enableWFCDebug;
            DebugSettings.EnableTerrainDebug = enableTerrainDebug;
            DebugSettings.EnableWeatherDebug = enableWeatherDebug;
            DebugSettings.EnableRenderingDebug = enableRenderingDebug;
            DebugSettings.EnableTestDebug = enableTestDebug;
            DebugSettings.EnableSeamDebug = enableSeamDebug;
            DebugSettings.EnableTestSystems = enableTestSystems;
            
            // Log the current state
            if (enableDebugLogging)
            {
                UnityEngine.Debug.Log($"[DebugController] Debug settings applied: WFC={enableWFCDebug}, Terrain={enableTerrainDebug}, Weather={enableWeatherDebug}, Rendering={enableRenderingDebug}, Test={enableTestDebug}, Seam={enableSeamDebug}, TestSystems={enableTestSystems}");
            }
        }
        
        /// <summary>
        /// Enable all debug logging
        /// </summary>
        [ContextMenu("Enable All Debug")]
        public void EnableAllDebug()
        {
            enableAllDebug = true;
            ApplySettings();
        }
        
        /// <summary>
        /// Disable all debug logging
        /// </summary>
        [ContextMenu("Disable All Debug")]
        public void DisableAllDebug()
        {
            disableAllDebug = true;
            ApplySettings();
        }
        
        /// <summary>
        /// Enable only WFC debug
        /// </summary>
        [ContextMenu("Enable WFC Debug Only")]
        public void EnableWFCDebugOnly()
        {
            enableAllDebug = false;
            disableAllDebug = false;
            enableDebugLogging = true;
            enableWFCDebug = true;
            enableTerrainDebug = false;
            enableWeatherDebug = false;
            enableRenderingDebug = false;
            enableTestDebug = false;
            enableSeamDebug = false;
            enableTestSystems = false;
            ApplySettings();
        }
        
        /// <summary>
        /// Enable only test systems
        /// </summary>
        [ContextMenu("Enable Test Systems Only")]
        public void EnableTestSystemsOnly()
        {
            enableAllDebug = false;
            disableAllDebug = false;
            enableDebugLogging = false;
            enableWFCDebug = false;
            enableTerrainDebug = false;
            enableWeatherDebug = false;
            enableRenderingDebug = false;
            enableTestDebug = true;
            enableSeamDebug = false;
            enableTestSystems = true;
            ApplySettings();
        }
    }
} 