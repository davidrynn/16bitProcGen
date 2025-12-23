using UnityEngine;

[CreateAssetMenu(menuName = "Config/ProjectFeatureConfig")]
public class ProjectFeatureConfig : ScriptableObject
{
    [Header("Feature Toggles")]
    public bool EnablePlayerSystem = true;
    public bool EnableTerrainSystem = true;
    public bool EnableDungeonSystem = true;
    public bool EnableWeatherSystem = true;
    public bool EnableRenderingSystem = true;
    public bool EnableTestSystems = false;
    // Add more toggles as needed for your project
}
