using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Terrain.WFC
{
    /// <summary>
    /// MonoBehaviour to control dungeon generation
    /// Demonstrates how to request dungeon generation on demand
    /// </summary>
    public class DungeonManager : MonoBehaviour
    {
        [Header("Dungeon Settings")]
        public float3 dungeonPosition = new float3(0, 0, 0);
        public int2 dungeonSize = new int2(16, 16);
        public float cellSize = 1.0f;
        
        [Header("Controls")]
        public KeyCode generateKey = KeyCode.G;
        public bool generateOnStart = false;
        
        private Entity dungeonRequestEntity;
        private World dotsWorld;
        
        void Start()
        {
            if (generateOnStart)
            {
                RequestDungeonGeneration();
            }
        }
        
        void Update()
        {
            if (Input.GetKeyDown(generateKey))
            {
                RequestDungeonGeneration();
            }
        }
        
        /// <summary>
        /// Requests dungeon generation at the specified position
        /// </summary>
        public void RequestDungeonGeneration()
        {
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonManager: Requesting dungeon generation at {dungeonPosition}", true);
            
            // Get DOTS world
            dotsWorld = World.DefaultGameObjectInjectionWorld;
            if (dotsWorld == null)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("DungeonManager: DOTS World not found!");
                return;
            }
            
            // Create or update dungeon request entity
            if (dungeonRequestEntity == Entity.Null)
            {
                dungeonRequestEntity = dotsWorld.EntityManager.CreateEntity();
            }
            
            // Set dungeon generation request
            var request = new DungeonGenerationRequest
            {
                isActive = true,
                position = dungeonPosition,
                size = dungeonSize,
                cellSize = cellSize
            };
            
            dotsWorld.EntityManager.AddComponentData(dungeonRequestEntity, request);
            
            DOTS.Terrain.Core.DebugSettings.LogRendering($"DungeonManager: Dungeon generation request created for {dungeonSize.x}x{dungeonSize.y} dungeon");
        }
        
        /// <summary>
        /// Stops dungeon generation
        /// </summary>
        public void StopDungeonGeneration()
        {
            if (dotsWorld != null && dungeonRequestEntity != Entity.Null)
            {
                var request = dotsWorld.EntityManager.GetComponentData<DungeonGenerationRequest>(dungeonRequestEntity);
                request.isActive = false;
                dotsWorld.EntityManager.SetComponentData(dungeonRequestEntity, request);
                
                DOTS.Terrain.Core.DebugSettings.LogRendering("DungeonManager: Dungeon generation stopped");
            }
        }
        
        /// <summary>
        /// Cleans up the dungeon request entity
        /// </summary>
        void OnDestroy()
        {
            if (dotsWorld != null && dungeonRequestEntity != Entity.Null)
            {
                dotsWorld.EntityManager.DestroyEntity(dungeonRequestEntity);
            }
        }
    }
} 