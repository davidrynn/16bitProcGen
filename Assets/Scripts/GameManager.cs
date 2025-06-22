using UnityEngine;

public class GameManager : MonoBehaviour
{
    private TerrainManager terrainManager;
    private LODTerrainManager lodTerrainManager;
    private BiomeManager biomeManager;
    void Start()
    {
        biomeManager = FindAnyObjectByType<BiomeManager>(); 
        terrainManager = FindAnyObjectByType<TerrainManager>(); // Find existing TerrainManager
        lodTerrainManager = FindAnyObjectByType<LODTerrainManager>(); // Find existing LODTerrainManager
        if (terrainManager != null)
        {
            terrainManager.Initialize(biomeManager); // Pass biome manager
        } else if (lodTerrainManager != null)
        {
            lodTerrainManager.Initialize(biomeManager); // Pass biome manager
        }
        else
        {
            Debug.LogError("TerrainManager not found in the scene!");
        }
    }
}
