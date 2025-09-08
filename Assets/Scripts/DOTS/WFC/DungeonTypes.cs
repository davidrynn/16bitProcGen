using Unity.Entities;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Macro tile types for dungeon elements.
    /// </summary>
    public enum DungeonElementType
    {
        RoomFloor = 0,
        RoomEdge = 1,
        Corridor = 2,
        Corner = 3,
        CorridorEndDoorway = 4
    }

    /// <summary>
    /// Component storing the dungeon element domain type.
    /// </summary>
    public struct DungeonElementComponent : IComponentData
    {
        public DungeonElementType elementType;
    }

    /// <summary>
    /// Marker component for spawned dungeon element instances.
    /// </summary>
    public struct DungeonElementInstance : IComponentData { }

    /// <summary>
    /// Container for prefab entity references used by the renderer.
    /// </summary>
    public struct DungeonPrefabs
    {
        public Entity floorPrefab;
        public Entity wallPrefab;
        public Entity doorPrefab;
        public Entity corridorPrefab;
        public Entity cornerPrefab;
    }
}


