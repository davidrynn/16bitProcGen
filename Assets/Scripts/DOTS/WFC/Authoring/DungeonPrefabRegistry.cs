using Unity.Entities;
using UnityEngine;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.WFC.Authoring
{
    /// <summary>
    /// DOTS component storing entity prefab references for dungeon generation
    /// </summary>
    public struct DungeonPrefabRegistry : IComponentData
    {
        public Entity roomFloorPrefab;
        public Entity roomEdgePrefab;
        public Entity doorPrefab;
        public Entity corridorPrefab;
        public Entity cornerPrefab;
    }

    /// <summary>
    /// Authoring component for assigning dungeon prefabs in the Unity Editor
    /// Bakes GameObject prefabs into Entity prefabs for the DOTS runtime
    /// </summary>
    public class DungeonPrefabRegistryAuthoring : MonoBehaviour
    {
        [Header("Required Macro Tile Prefabs")]
        [Tooltip("Prefab for room floor tiles")]
        public GameObject roomFloorPrefab;
        
        [Tooltip("Prefab for room edge/wall tiles")]
        public GameObject roomEdgePrefab;

        [Header("Optional Macro Tile Prefabs")]
        [Tooltip("Prefab for door tiles (optional)")]
        public GameObject doorPrefab;
        
        [Tooltip("Prefab for corridor tiles (optional)")]
        public GameObject corridorPrefab;
        
        [Tooltip("Prefab for corner tiles (optional)")]
        public GameObject cornerPrefab;

        class Baker : Baker<DungeonPrefabRegistryAuthoring>
        {
            public override void Bake(DungeonPrefabRegistryAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                
                AddComponent(entity, new DungeonPrefabRegistry
                {
                    roomFloorPrefab = authoring.roomFloorPrefab != null ? GetEntity(authoring.roomFloorPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    roomEdgePrefab = authoring.roomEdgePrefab != null ? GetEntity(authoring.roomEdgePrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    doorPrefab = authoring.doorPrefab != null ? GetEntity(authoring.doorPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    corridorPrefab = authoring.corridorPrefab != null ? GetEntity(authoring.corridorPrefab, TransformUsageFlags.Dynamic) : Entity.Null,
                    cornerPrefab = authoring.cornerPrefab != null ? GetEntity(authoring.cornerPrefab, TransformUsageFlags.Dynamic) : Entity.Null
                });
                
                // Log warning if required prefabs are missing
                if (authoring.roomFloorPrefab == null || authoring.roomEdgePrefab == null)
                {
                    DebugSettings.LogWarning("DungeonPrefabRegistryAuthoring: roomFloorPrefab and roomEdgePrefab are required but not assigned!");
                }
            }
        }
    }
}

