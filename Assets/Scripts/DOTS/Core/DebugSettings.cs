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
        public static bool EnableSeamDebug = false;
        
        // Test system control
        public static bool EnableTestSystems = false;
        
        // WFC Random Seed Control
        public static bool UseFixedWFCSeed = true;
        public static int FixedWFCSeed = 12345;
        
        /// <summary>
        /// Logs a debug message only if debug logging is enabled
        /// </summary>
        public static void Log(string message, bool forceLog = false)
        {
            if (EnableDebugLogging || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if WFC debug is enabled
        /// </summary>
        public static void LogWFC(string message, bool forceLog = false)
        {
            if (EnableWFCDebug || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS-WFC] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if terrain debug is enabled
        /// </summary>
        public static void LogTerrain(string message, bool forceLog = false)
        {
            if (EnableTerrainDebug || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS-Terrain] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if weather debug is enabled
        /// </summary>
        public static void LogWeather(string message, bool forceLog = false)
        {
            if (EnableWeatherDebug || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS-Weather] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if rendering debug is enabled
        /// </summary>
        public static void LogRendering(string message, bool forceLog = false)
        {
            if (EnableRenderingDebug || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS-Rendering] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if test debug is enabled
        /// </summary>
        public static void LogTest(string message, bool forceLog = false)
        {
            if (EnableTestDebug || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS-Test] {message}");
            }
        }
        
        /// <summary>
        /// Logs a debug message only if seam debug is enabled
        /// </summary>
        public static void LogSeam(string message, bool forceLog = false)
        {
            if (EnableSeamDebug || forceLog)
            {
                UnityEngine.Debug.Log($"[DOTS-Seam] {message}");
            }
        }
        
        /// <summary>
        /// Logs a seam warning message only if seam debug is enabled
        /// </summary>
        public static void LogSeamWarning(string message, bool forceLog = false)
        {
            if (EnableSeamDebug || forceLog)
            {
                UnityEngine.Debug.LogWarning($"[DOTS-Seam] {message}");
            }
        }
        
        /// <summary>
        /// Logs a warning message (always shown)
        /// </summary>
        public static void LogWarning(string message)
        {
            UnityEngine.Debug.LogWarning($"[DOTS] {message}");
        }
        
        /// <summary>
        /// Logs an error message (always shown)
        /// </summary>
        public static void LogError(string message)
        {
            UnityEngine.Debug.LogError($"[DOTS] {message}");
        }
    }
} 