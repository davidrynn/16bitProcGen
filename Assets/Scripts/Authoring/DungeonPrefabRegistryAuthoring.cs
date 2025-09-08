using Unity.Entities;
using UnityEngine;

namespace DOTS.Terrain.WFC.Authoring
{
    /// <summary>
    /// Authoring-time registry that holds references to floor and wall prefabs (GameObjects).
    /// Its baker converts them to a singleton ECS component with baked Entity prefab references.
    /// </summary>
    public class DungeonPrefabRegistryAuthoring : MonoBehaviour
    {
        public GameObject corridorPrefab;
        public GameObject cornerPrefab;
        public GameObject roomEdgePrefab;
        public GameObject roomFloorPrefab;
        public GameObject doorPrefab;
    }

    /// <summary>
    /// Singleton ECS component that stores baked entity references for dungeon prefabs.
    /// </summary>
    public struct DungeonPrefabRegistry : IComponentData
    {
        public Entity corridorPrefab;
        public Entity cornerPrefab;
        public Entity roomEdgePrefab;
        public Entity roomFloorPrefab;
        public Entity doorPrefab;
    }

    public class DungeonPrefabRegistryBaker : Baker<DungeonPrefabRegistryAuthoring>
    {
        public override void Bake(DungeonPrefabRegistryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            Entity corridorEntity = Entity.Null;
            Entity cornerEntity = Entity.Null;
            Entity roomEdgeEntity = Entity.Null;
            Entity roomFloorEntity = Entity.Null;
            Entity doorEntity = Entity.Null;

            if (authoring.corridorPrefab != null)
            {
                corridorEntity = GetEntity(authoring.corridorPrefab, TransformUsageFlags.Renderable);
            }

            if (authoring.cornerPrefab != null)
            {
                cornerEntity = GetEntity(authoring.cornerPrefab, TransformUsageFlags.Renderable);
            }

            if (authoring.roomEdgePrefab != null)
            {
                roomEdgeEntity = GetEntity(authoring.roomEdgePrefab, TransformUsageFlags.Renderable);
            }

            if (authoring.roomFloorPrefab != null)
            {
                roomFloorEntity = GetEntity(authoring.roomFloorPrefab, TransformUsageFlags.Renderable);
            }

            if (authoring.doorPrefab != null)
            {
                doorEntity = GetEntity(authoring.doorPrefab, TransformUsageFlags.Renderable);
            }

            AddComponent(entity, new DungeonPrefabRegistry
            {
                corridorPrefab = corridorEntity,
                cornerPrefab = cornerEntity,
                roomEdgePrefab = roomEdgeEntity,
                roomFloorPrefab = roomFloorEntity,
                doorPrefab = doorEntity
            });
        }
    }
}


