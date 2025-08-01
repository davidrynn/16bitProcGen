using UnityEngine;

namespace DOTS.Terrain.Core
{
    /// <summary>
    /// Centralized debug settings for controlling debug output across all DOTS systems
    /// </summary>
    public static class DebugSettings
    {
        // Global debug flags
        public static bool EnableDebugLogging = false;
        public static bool EnableWFCDebug = false;
        public static bool EnableTerrainDebug = false;
        public static bool EnableWeatherDebug = false;
        public static bool EnableRenderingDebug = false;
        public static bool EnableTestDebug = false;
        
        // Test system control
        public static bool EnableTestSystems = false;
        
        /// <summary>
        /// Logs a debug message only if debug logging is enabled
        /// </summary>
        public static void Log(string message, bool forceLog = false)
        {
            if (EnableDebugLogging || forceLog)
            {
                Debug.Log($"[DOTS] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if WFC debug is enabled
        /// </summary>
        public static void LogWFC(string message, bool forceLog = false)
        {
            if (EnableWFCDebug || forceLog)
            {
                Debug.Log($"[DOTS-WFC] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if terrain debug is enabled
        /// </summary>
        public static void LogTerrain(string message, bool forceLog = false)
        {
            if (EnableTerrainDebug || forceLog)
            {
                Debug.Log($"[DOTS-Terrain] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if weather debug is enabled
        /// </summary>
        public static void LogWeather(string message, bool forceLog = false)
        {
            if (EnableWeatherDebug || forceLog)
            {
                Debug.Log($"[DOTS-Weather] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if rendering debug is enabled
        /// </summary>
        public static void LogRendering(string message, bool forceLog = false)
        {
            if (EnableRenderingDebug || forceLog)
            {
                Debug.Log($"[DOTS-Rendering] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if test debug is enabled
        /// </summary>
        public static void LogTest(string message, bool forceLog = false)
        {
            if (EnableTestDebug || forceLog)
            {
                Debug.Log($"[DOTS-Test] {message}");
            }
        }
        
        /// <summary>
        /// Logs a warning message (always shown)
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning($"[DOTS] {message}");
        }
        
        /// <summary>
        /// Logs an error message (always shown)
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError($"[DOTS] {message}");
        }
    }
} 