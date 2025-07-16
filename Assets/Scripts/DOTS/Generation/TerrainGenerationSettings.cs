using UnityEngine;

namespace DOTS.Terrain.Generation
{
    /// <summary>
    /// ScriptableObject configuration for terrain generation settings
    /// Allows runtime configuration without code changes
    /// </summary>
    [CreateAssetMenu(fileName = "TerrainGenerationSettings", menuName = "DOTS/Terrain/Generation Settings")]
    public class TerrainGenerationSettings : ScriptableObject
    {
        [Header("Performance Settings")]
        [Tooltip("Maximum chunks processed per frame to prevent frame drops")]
        public int maxChunksPerFrame = 4;
        
        [Tooltip("Default buffer size for GPU computation (in floats)")]
        public int defaultBufferSize = 1024 * 1024; // 1M floats
        
        [Header("Noise Generation Settings")]
        [Tooltip("Scale of the noise function - smaller values create larger terrain features")]
        [Range(0.001f, 1.0f)]
        public float noiseScale = 0.02f;
        
        [Tooltip("Multiplier for terrain height - controls actual world height")]
        [Range(1f, 1000f)]
        public float heightMultiplier = 100.0f;
        
        [Tooltip("Scale factor for biome influence")]
        [Range(0.1f, 10f)]
        public float biomeScale = 1.0f;
        
        [Tooltip("Fixed seed for consistent terrain generation")]
        public Vector2 noiseOffset = new Vector2(123.456f, 789.012f);
        
        [Header("Debug Settings")]
        [Tooltip("Enable debug logging for terrain generation")]
        public bool enableDebugLogs = false;
        
        [Tooltip("Enable verbose logging for detailed debugging")]
        public bool enableVerboseLogs = false;
        
        [Tooltip("Enable height value logging")]
        public bool logHeightValues = false;
        
        [Header("Terrain Type Thresholds")]
        [Tooltip("Height threshold for water terrain type")]
        public float waterThreshold = 10f;
        
        [Tooltip("Height threshold for sand terrain type")]
        public float sandThreshold = 20f;
        
        [Tooltip("Height threshold for grass terrain type")]
        public float grassThreshold = 35f;
        
        [Tooltip("Height threshold for flora terrain type")]
        public float floraThreshold = 45f;
        
        // Default instance for easy access
        private static TerrainGenerationSettings _defaultInstance;
        public static TerrainGenerationSettings Default
        {
            get
            {
                if (_defaultInstance == null)
                {
                    _defaultInstance = Resources.Load<TerrainGenerationSettings>("TerrainGenerationSettings");
                    if (_defaultInstance == null)
                    {
                        Debug.LogWarning("TerrainGenerationSettings not found in Resources folder. Creating default settings.");
                        _defaultInstance = CreateInstance<TerrainGenerationSettings>();
                    }
                }
                return _defaultInstance;
            }
        }
        
        /// <summary>
        /// Gets terrain type based on height value using configured thresholds
        /// </summary>
        public TerrainType GetTerrainTypeFromHeight(float height)
        {
            if (height < waterThreshold) return TerrainType.Water;
            if (height < sandThreshold) return TerrainType.Sand;
            if (height < grassThreshold) return TerrainType.Grass;
            if (height < floraThreshold) return TerrainType.Flora;
            return TerrainType.Rock;
        }
        
        /// <summary>
        /// Validates settings and logs warnings for invalid values
        /// </summary>
        public void ValidateSettings()
        {
            if (maxChunksPerFrame <= 0)
            {
                Debug.LogWarning("TerrainGenerationSettings: maxChunksPerFrame should be > 0");
            }
            
            if (noiseScale <= 0)
            {
                Debug.LogWarning("TerrainGenerationSettings: noiseScale should be > 0");
            }
            
            if (heightMultiplier <= 0)
            {
                Debug.LogWarning("TerrainGenerationSettings: heightMultiplier should be > 0");
            }
            
            if (waterThreshold >= sandThreshold || sandThreshold >= grassThreshold || grassThreshold >= floraThreshold)
            {
                Debug.LogWarning("TerrainGenerationSettings: Terrain type thresholds should be in ascending order");
            }
        }
    }
} 