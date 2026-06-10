using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using DOTS.Terrain.WFC;

    public class DungeonVisualizer : MonoBehaviour
    {
        [Header("Dungeon Prefabs")]
        public GameObject floorPrefab;
        public GameObject wallPrefabXY; // Wall oriented along X axis (YZ plane)
        public GameObject wallPrefabZY; // Wall oriented along Z axis (XY plane)
        public GameObject doorPrefab;
        public GameObject corridorPrefab;
        public GameObject cornerPrefab;

        [Header("Settings")]
        public float cellSize = 1.0f;
        public bool showDebugInfo = true;

    void Start()
    {
        // Wait a frame or two for WFC to finish (or trigger this after WFC is done)
        Invoke(nameof(VisualizeDungeon), 1.0f);
    }

    void VisualizeDungeon()
    {
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = entityManager.CreateEntityQuery(typeof(WFCCell));
        var cells = query.ToComponentDataArray<WFCCell>(Allocator.Temp);

        int collapsedCount = 0;
        foreach (var cell in cells)
        {
            if (!cell.collapsed) continue;
            
            collapsedCount++;
            Vector3 pos = new Vector3(cell.position.x * cellSize, 0, cell.position.y * cellSize);

                    // Map dungeon pattern IDs to prefabs
        // 0 = Floor, 1 = Wall, 2 = Door, 3 = Corridor, 4 = Corner
        if (cell.selectedPattern == 0) // Floor
        {
            Instantiate(floorPrefab, pos, Quaternion.identity, transform);
        }
        else if (cell.selectedPattern == 1) // Wall
        {
            // Choose wall orientation based on position for variety
            // Even X coordinates get XY walls, odd X coordinates get ZY walls
            if (cell.position.x % 2 == 0)
            {
                if (wallPrefabXY != null)
                    Instantiate(wallPrefabXY, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
                else
                    Instantiate(wallPrefabZY, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
            }
            else
            {
                if (wallPrefabZY != null)
                    Instantiate(wallPrefabZY, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
                else
                    Instantiate(wallPrefabXY, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
            }
        }
        else if (cell.selectedPattern == 2) // Door
        {
            if (doorPrefab != null)
                Instantiate(doorPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
            else
                Instantiate(wallPrefabXY, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
        }
        else if (cell.selectedPattern == 3) // Corridor
        {
            if (corridorPrefab != null)
                Instantiate(corridorPrefab, pos, Quaternion.identity, transform);
            else
                Instantiate(floorPrefab, pos, Quaternion.identity, transform);
        }
        else if (cell.selectedPattern == 4) // Corner
        {
            if (cornerPrefab != null)
                Instantiate(cornerPrefab, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
            else
                Instantiate(wallPrefabXY, pos + Vector3.up * 0.5f, Quaternion.identity, transform);
        }
        }
        
        if (showDebugInfo)
        {
            Debug.Log($"[DungeonVisualizer] Visualized {collapsedCount} collapsed cells out of {cells.Length} total cells");
        }

        cells.Dispose();
    }
}
