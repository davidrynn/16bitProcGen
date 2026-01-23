using UnityEngine;

namespace DOTS.Terrain.Core
{
    /// <summary>
    /// Simple test controller to verify debug control is working
    /// </summary>
    public class DebugTestController : MonoBehaviour
    {
        [Header("Test Controls")]
        [Tooltip("Press this key to enable test systems")]
        public KeyCode enableTestSystemsKey = KeyCode.T;
        
        [Tooltip("Press this key to enable all debug")]
        public KeyCode enableAllDebugKey = KeyCode.D;
        
        [Tooltip("Press this key to disable all debug")]
        public KeyCode disableAllDebugKey = KeyCode.X;
        
        void Update()
        {
            if (Input.GetKeyDown(enableTestSystemsKey))
            {
                DebugSettings.EnableTestSystems = true;
                DebugSettings.EnableTestDebug = true;
                UnityEngine.Debug.Log("[DebugTestController] Test systems ENABLED - WFCSystemTest should now run");
            }
            
            if (Input.GetKeyDown(enableAllDebugKey))
            {
                DebugSettings.EnableDebugLogging = true;
                DebugSettings.EnableWFCDebug = true;
                DebugSettings.EnableTerrainDebug = true;
                DebugSettings.EnableWeatherDebug = true;
                DebugSettings.EnableRenderingDebug = true;
                DebugSettings.EnableTestDebug = true;
                DebugSettings.EnableTestSystems = true;
                UnityEngine.Debug.Log("[DebugTestController] ALL DEBUG ENABLED");
            }
            
            if (Input.GetKeyDown(disableAllDebugKey))
            {
                DebugSettings.EnableDebugLogging = false;
                DebugSettings.EnableWFCDebug = false;
                DebugSettings.EnableTerrainDebug = false;
                DebugSettings.EnableWeatherDebug = false;
                DebugSettings.EnableRenderingDebug = false;
                DebugSettings.EnableTestDebug = false;
                DebugSettings.EnableTestSystems = false;
                UnityEngine.Debug.Log("[DebugTestController] ALL DEBUG DISABLED");
            }
        }
        
        void OnGUI()
        {
            // Display current debug state
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("Debug Control Test", GUI.skin.box);
            GUILayout.Label($"Test Systems: {DebugSettings.EnableTestSystems}");
            GUILayout.Label($"Debug Logging: {DebugSettings.EnableDebugLogging}");
            GUILayout.Label($"WFC Debug: {DebugSettings.EnableWFCDebug}");
            GUILayout.Label($"Rendering Debug: {DebugSettings.EnableRenderingDebug}");
            GUILayout.Label($"Test Debug: {DebugSettings.EnableTestDebug}");
            GUILayout.Space(10);
            GUILayout.Label("Controls:");
            GUILayout.Label($"T - Enable Test Systems");
            GUILayout.Label($"D - Enable All Debug");
            GUILayout.Label($"X - Disable All Debug");
            GUILayout.EndArea();
        }
    }
} 