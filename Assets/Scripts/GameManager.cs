using UnityEngine;

public class GameManager : MonoBehaviour
{
    private TerrainManager terrainManager;
    private BiomeManager biomeManager;
    void Start()
    {
        biomeManager = FindAnyObjectByType<BiomeManager>(); 
        terrainManager = FindAnyObjectByType<TerrainManager>(); // Find existing TerrainManager

        if (terrainManager != null)
        {
            terrainManager.Initialize(biomeManager); // Pass biome manager
        }
        else
        {
            Debug.LogError("TerrainManager not found in the scene!");
        }
    }
}
