using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// Enum for dungeon element types
    /// </summary>
    public enum DungeonElementType
    {
        Floor,
        Wall,
        Door,
        Corridor,
        Corner
    }
    
    /// <summary>
    /// Component to store dungeon element data
    /// </summary>
    public struct DungeonElementComponent : IComponentData
    {
        public DungeonElementType elementType;
    }
    
    /// <summary>
    /// Component to identify spawned dungeon element instances
    /// </summary>
    public struct DungeonElementInstance : IComponentData
    {
    }
    
    /// <summary>
    /// Pure code-based dungeon prefab creator
    /// Creates entity prefabs programmatically without needing authoring assets
    /// </summary>
    public static class DungeonPrefabCreator
    {
        /// <summary>
        /// Creates all dungeon element prefabs and returns them in a struct
        /// </summary>
        public static DungeonPrefabs CreatePrefabs(EntityManager entityManager)
        {
            return new DungeonPrefabs
            {
                floorPrefab = CreateFloorPrefab(entityManager),
                wallPrefab = CreateWallPrefab(entityManager),
                doorPrefab = CreateDoorPrefab(entityManager),
                corridorPrefab = CreateCorridorPrefab(entityManager),
                cornerPrefab = CreateCornerPrefab(entityManager)
            };
        }
        
        private static Entity CreateFloorPrefab(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity();
            
            // Add dungeon element component
            entityManager.AddComponent<DungeonElementComponent>(entity);
            entityManager.SetComponentData(entity, new DungeonElementComponent
            {
                elementType = DungeonElementType.Floor
            });
            
            // Add transform
            entityManager.AddComponent<LocalTransform>(entity);
            entityManager.SetComponentData(entity, new LocalTransform
            {
                Scale = 1f,
                Position = float3.zero,
                Rotation = quaternion.identity
            });
            
            // Add a marker component for rendering (will be handled by a separate rendering system)
            entityManager.AddComponent<DungeonElementInstance>(entity);
            
            return entity;
        }
        
        private static Entity CreateWallPrefab(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity();
            
            // Add dungeon element component
            entityManager.AddComponent<DungeonElementComponent>(entity);
            entityManager.SetComponentData(entity, new DungeonElementComponent
            {
                elementType = DungeonElementType.Wall
            });
            
            // Add transform (wall is thin and tall)
            entityManager.AddComponent<LocalTransform>(entity);
            entityManager.SetComponentData(entity, new LocalTransform
            {
                Scale = 1f, // Use uniform scale, adjust mesh instead
                Position = float3.zero,
                Rotation = quaternion.identity
            });
            
            // Add a marker component for rendering (will be handled by a separate rendering system)
            entityManager.AddComponent<DungeonElementInstance>(entity);
            
            return entity;
        }
        
        private static Entity CreateDoorPrefab(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity();
            
            // Add dungeon element component
            entityManager.AddComponent<DungeonElementComponent>(entity);
            entityManager.SetComponentData(entity, new DungeonElementComponent
            {
                elementType = DungeonElementType.Door
            });
            
            // Add transform
            entityManager.AddComponent<LocalTransform>(entity);
            entityManager.SetComponentData(entity, new LocalTransform
            {
                Scale = 1f, // Use uniform scale, adjust mesh instead
                Position = float3.zero,
                Rotation = quaternion.identity
            });
            
            // Add a marker component for rendering (will be handled by a separate rendering system)
            entityManager.AddComponent<DungeonElementInstance>(entity);
            
            return entity;
        }
        
        private static Entity CreateCorridorPrefab(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity();
            
            // Add dungeon element component
            entityManager.AddComponent<DungeonElementComponent>(entity);
            entityManager.SetComponentData(entity, new DungeonElementComponent
            {
                elementType = DungeonElementType.Corridor
            });
            
            // Add transform
            entityManager.AddComponent<LocalTransform>(entity);
            entityManager.SetComponentData(entity, new LocalTransform
            {
                Scale = 1f,
                Position = float3.zero,
                Rotation = quaternion.identity
            });
            
            // Add a marker component for rendering (will be handled by a separate rendering system)
            entityManager.AddComponent<DungeonElementInstance>(entity);
            
            return entity;
        }
        
        private static Entity CreateCornerPrefab(EntityManager entityManager)
        {
            var entity = entityManager.CreateEntity();
            
            // Add dungeon element component
            entityManager.AddComponent<DungeonElementComponent>(entity);
            entityManager.SetComponentData(entity, new DungeonElementComponent
            {
                elementType = DungeonElementType.Corner
            });
            
            // Add transform
            entityManager.AddComponent<LocalTransform>(entity);
            entityManager.SetComponentData(entity, new LocalTransform
            {
                Scale = 1f, // Use uniform scale, adjust mesh instead
                Position = float3.zero,
                Rotation = quaternion.identity
            });
            
            // Add a marker component for rendering (will be handled by a separate rendering system)
            entityManager.AddComponent<DungeonElementInstance>(entity);
            
            return entity;
        }
        

    }
    
    /// <summary>
    /// Container for all dungeon element prefabs
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