using UnityEngine;

public class LODTestScript : MonoBehaviour
{
    public TerrainManagerLegacy terrainManager;
    public Transform player;
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L))
        {
            // Force LOD update
            if (terrainManager != null)
            {
                terrainManager.showLODDebug = !terrainManager.showLODDebug;
                Debug.Log($"LOD Debug: {terrainManager.showLODDebug}");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.I))
        {
            // Print chunk info
            if (terrainManager != null && terrainManager.chunks != null)
            {
                foreach (var chunk in terrainManager.chunks.Values)
                {
                    if (chunk != null)
                    {
                        Debug.Log($"Chunk {chunk.chunkPosition}: {chunk.GetLODInfo()}");
                    }
                }
            }
        }
    }
} 