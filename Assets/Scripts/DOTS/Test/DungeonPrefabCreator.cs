using UnityEngine;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Helper script to create different wall prefabs for dungeon visualization
    /// </summary>
    public class DungeonPrefabCreator : MonoBehaviour
    {
        [Header("Prefab Creation")]
        public Material floorMaterial;
        public Material wallMaterial;
        public Material wallMaterialYZ; // Different material for YZ walls if desired
        public Material doorMaterial;
        public Material corridorMaterial;
        public Material cornerMaterial;

        [Header("Settings")]
        public float wallHeight = 1.0f;
        public float wallThickness = 0.1f;

        [ContextMenu("Create Floor Prefab")]
        public void CreateFloorPrefab()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "FloorPrefab";
            floor.transform.localScale = Vector3.one * 0.1f; // 1x1 unit
            
            if (floorMaterial != null)
                floor.GetComponent<Renderer>().material = floorMaterial;
            
            DebugSettings.Log("Floor prefab created. Drag it to your project folder to save as prefab.");
        }

        [ContextMenu("Create Wall Prefab (XY)")]
        public void CreateWallPrefabXY()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallPrefabXY";
            wall.transform.localScale = new Vector3(1f, wallHeight, wallThickness);
            
            if (wallMaterial != null)
                wall.GetComponent<Renderer>().material = wallMaterial;
            
            DebugSettings.Log("Wall prefab (XY) created. Drag it to your project folder to save as prefab.");
        }

        [ContextMenu("Create Wall Prefab (ZY)")]
        public void CreateWallPrefabZY()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "WallPrefabZY";
            wall.transform.localScale = new Vector3(wallThickness, wallHeight, 1f);
            
            if (wallMaterialYZ != null)
                wall.GetComponent<Renderer>().material = wallMaterialYZ;
            else if (wallMaterial != null)
                wall.GetComponent<Renderer>().material = wallMaterial;
            
            DebugSettings.Log("Wall prefab (ZY) created. Drag it to your project folder to save as prefab.");
        }

        [ContextMenu("Create Door Prefab")]
        public void CreateDoorPrefab()
        {
            var door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "DoorPrefab";
            door.transform.localScale = new Vector3(0.8f, wallHeight * 0.8f, wallThickness);
            
            if (doorMaterial != null)
                door.GetComponent<Renderer>().material = doorMaterial;
            
            DebugSettings.Log("Door prefab created. Drag it to your project folder to save as prefab.");
        }

        [ContextMenu("Create Corridor Prefab")]
        public void CreateCorridorPrefab()
        {
            var corridor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            corridor.name = "CorridorPrefab";
            corridor.transform.localScale = Vector3.one * 0.1f;
            
            if (corridorMaterial != null)
                corridor.GetComponent<Renderer>().material = corridorMaterial;
            
            DebugSettings.Log("Corridor prefab created. Drag it to your project folder to save as prefab.");
        }

        [ContextMenu("Create Corner Prefab")]
        public void CreateCornerPrefab()
        {
            var corner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            corner.name = "CornerPrefab";
            corner.transform.localScale = new Vector3(wallThickness, wallHeight, wallThickness);
            
            if (cornerMaterial != null)
                corner.GetComponent<Renderer>().material = cornerMaterial;
            
            DebugSettings.Log("Corner prefab created. Drag it to your project folder to save as prefab.");
        }

        [ContextMenu("Create All Prefabs")]
        public void CreateAllPrefabs()
        {
            CreateFloorPrefab();
            CreateWallPrefabXY();
            CreateWallPrefabZY();
            CreateDoorPrefab();
            CreateCorridorPrefab();
            CreateCornerPrefab();
        }
    }
} 