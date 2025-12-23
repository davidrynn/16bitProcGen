using Unity.Entities;
using UnityEngine;

public class DotsSystemBootstrap : MonoBehaviour
{
    public ProjectFeatureConfig config;

    void Awake()
    {
        var world = World.DefaultGameObjectInjectionWorld;

        if (config == null)
        {
            Debug.LogWarning("[DOTS Bootstrap] ProjectFeatureConfig not assigned. No systems will be enabled.");
            return;
        }

        if (config.EnableTerrainSystem)
        {
            world.CreateSystem<TerrainGenerationSystem>();
            Debug.Log("[DOTS Bootstrap] TerrainGenerationSystem enabled via config.");
        }
        if (config.EnablePlayerSystem)
        {
            world.CreateSystem<PlayerEntityBootstrap>();
            Debug.Log("[DOTS Bootstrap] PlayerEntityBootstrap enabled via config.");
        }
        if (config.EnableDungeonSystem)
        {
            world.CreateSystem<DungeonVisualizationSystem>();
            Debug.Log("[DOTS Bootstrap] DungeonVisualizationSystem enabled via config.");
        }
        // Add more systems as needed
    }
}
