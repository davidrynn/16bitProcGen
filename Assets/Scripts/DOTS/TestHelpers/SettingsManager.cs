using UnityEngine;
using DOTS.Terrain.Generation;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Simple manager to view and modify terrain generation settings
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        [Header("Current Settings")]
        [SerializeField] private TerrainGenerationSettings currentSettings;
        
        [Header("Settings Preview")]
        [SerializeField] private bool showSettingsPreview = true;
        
        [Header("Quick Settings")]
        [SerializeField] private bool enableDebugLogs = true;
        [SerializeField] private float noiseScale = 0.02f;
        [SerializeField] private float heightMultiplier = 100f;
        
        private void Start()
        {
            LoadCurrentSettings();
        }
        
        [ContextMenu("Load Current Settings")]
        public void LoadCurrentSettings()
        {
            currentSettings = TerrainGenerationSettings.Default;
            if (currentSettings != null)
            {
                DebugSettings.Log("✓ Settings loaded successfully");
                UpdatePreviewValues();
            }
            else
            {
                DebugSettings.LogWarning("⚠ No settings found");
            }
        }
        
        [ContextMenu("Apply Quick Settings")]
        public void ApplyQuickSettings()
        {
            if (currentSettings != null)
            {
                currentSettings.enableDebugLogs = enableDebugLogs;
                currentSettings.noiseScale = noiseScale;
                currentSettings.heightMultiplier = heightMultiplier;
                
                DebugSettings.Log("✓ Quick settings applied");
                DebugSettings.Log($"  - Debug Logs: {currentSettings.enableDebugLogs}");
                DebugSettings.Log($"  - Noise Scale: {currentSettings.noiseScale}");
                DebugSettings.Log($"  - Height Multiplier: {currentSettings.heightMultiplier}");
            }
        }
        
        [ContextMenu("Create Settings Asset")]
        public void CreateSettingsAsset()
        {
            var settings = currentSettings;
            
            // Save to Resources folder
            #if UNITY_EDITOR
            if (!System.IO.Directory.Exists("Assets/Resources"))
            {
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            }
            
            UnityEditor.AssetDatabase.CreateAsset(settings, "Assets/Resources/TerrainGenerationSettings.asset");
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();
            
            DebugSettings.Log("✓ Settings asset created at Assets/Resources/TerrainGenerationSettings.asset");
            #else
            DebugSettings.LogWarning("Settings asset creation only works in Unity Editor");
            #endif
        }
        
        [ContextMenu("Reset to Defaults")]
        public void ResetToDefaults()
        {
            if (currentSettings != null)
            {
                currentSettings.noiseScale = 0.02f;
                currentSettings.heightMultiplier = 100f;
                currentSettings.enableDebugLogs = true;
                currentSettings.enableVerboseLogs = true;
                currentSettings.logHeightValues = true;
                
                DebugSettings.Log("✓ Settings reset to defaults");
            }
        }
        
        private void UpdatePreviewValues()
        {
            if (currentSettings != null)
            {
                enableDebugLogs = currentSettings.enableDebugLogs;
                noiseScale = currentSettings.noiseScale;
                heightMultiplier = currentSettings.heightMultiplier;
            }
        }
        
        private void OnValidate()
        {
            // Auto-apply changes when values are modified in Inspector
            if (Application.isPlaying && currentSettings != null)
            {
                ApplyQuickSettings();
            }
        }
        
        private void OnGUI()
        {
            if (!showSettingsPreview || currentSettings == null) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 300, 20, 280, 200));
            GUILayout.Label("=== TERRAIN SETTINGS ===");
            GUILayout.Label($"Noise Scale: {currentSettings.noiseScale:F3}");
            GUILayout.Label($"Height Multiplier: {currentSettings.heightMultiplier:F1}");
            GUILayout.Label($"Debug Logs: {currentSettings.enableDebugLogs}");
            GUILayout.Label($"Verbose Logs: {currentSettings.enableVerboseLogs}");
            GUILayout.EndArea();
        }
    }
} 