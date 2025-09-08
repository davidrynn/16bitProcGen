#if UNITY_EDITOR
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using DOTS.Terrain.WFC;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Simple test to verify the rendering pipeline works without WFC complexity
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SimpleRenderingTest : SystemBase
    {
        private bool testInitialized = false;
        private bool testCompleted = false;
        private int updateCounter = 0;
        private static bool enableTest = false; // Static flag to control if test runs
        
        // Public method to enable/disable the test
        public static void SetTestEnabled(bool enabled)
        {
            enableTest = enabled;
        }
        
        protected override void OnCreate()
        {
            // Check if test systems are enabled before doing anything
            if (!DOTS.Terrain.Core.DebugSettings.EnableTestSystems)
            {
                return;
            }
            
            DOTS.Terrain.Core.DebugSettings.LogTest("SimpleRenderingTest: OnCreate called", true);
            // No RequireForUpdate needed - this system should run automatically
        }
        
        protected override void OnUpdate()
        {
            // Only run if test systems are enabled AND this test is explicitly enabled
            if (!DOTS.Terrain.Core.DebugSettings.EnableTestSystems || !enableTest)
            {
                return;
            }
            
            updateCounter++;
            
            if (!testInitialized)
            {
                InitializeTest();
                return;
            }
            
            if (testCompleted)
            {
                // Don't log every frame - just return silently
                return;
            }
            
            // Check if we've been running too long
            if (updateCounter > 1000)
            {
                DOTS.Terrain.Core.DebugSettings.LogError("SimpleRenderingTest: Test running too long - forcing completion");
                testCompleted = true;
                return;
            }
            
            // Just log occasionally to show we're still running
            if (updateCounter % 100 == 0)
            {
                DOTS.Terrain.Core.DebugSettings.LogTest($"SimpleRenderingTest: Still running (update {updateCounter})");
            }
        }
        
        private void InitializeTest()
        {
            DOTS.Terrain.Core.DebugSettings.LogTest("SimpleRenderingTest: Initializing simple rendering test...");
            
            // Create a DungeonGenerationRequest to enable visualization
            var requestEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<DungeonGenerationRequest>(requestEntity);
            EntityManager.SetComponentData(requestEntity, new DungeonGenerationRequest
            {
                isActive = true,
                position = float3.zero,
                size = new int2(16, 16),
                cellSize = 1f
            });
            DOTS.Terrain.Core.DebugSettings.LogTest("SimpleRenderingTest: Created DungeonGenerationRequest");
            
            // Create a few simple entities with different types
            CreateTestEntity(DungeonElementType.RoomFloor, new float3(0, 0, 0));
            CreateTestEntity(DungeonElementType.RoomEdge, new float3(1, 0, 0));
            CreateTestEntity(DungeonElementType.CorridorEndDoorway, new float3(2, 0, 0));
            CreateTestEntity(DungeonElementType.Corridor, new float3(0, 0, 1));
            CreateTestEntity(DungeonElementType.Corner, new float3(1, 0, 1));
            
            // Create a few more to make a small pattern
            CreateTestEntity(DungeonElementType.RoomFloor, new float3(3, 0, 0));
            CreateTestEntity(DungeonElementType.RoomFloor, new float3(4, 0, 0));
            CreateTestEntity(DungeonElementType.RoomEdge, new float3(0, 0, 2));
            CreateTestEntity(DungeonElementType.RoomEdge, new float3(1, 0, 2));
            
            testInitialized = true;
            testCompleted = true;
            
            DOTS.Terrain.Core.DebugSettings.LogTest("SimpleRenderingTest: Test initialization complete - created 9 test entities");
        }
        
        private void CreateTestEntity(DungeonElementType elementType, float3 position)
        {
            var entity = EntityManager.CreateEntity();
            
            // Add required components
            EntityManager.AddComponent<DungeonElementComponent>(entity);
            EntityManager.AddComponent<LocalTransform>(entity);
            EntityManager.AddComponent<DungeonElementInstance>(entity);
            
            // Set component data
            EntityManager.SetComponentData(entity, new DungeonElementComponent
            {
                elementType = elementType
            });
            
            EntityManager.SetComponentData(entity, new LocalTransform
            {
                Position = position,
                Rotation = quaternion.identity,
                Scale = 1f
            });
            
            DOTS.Terrain.Core.DebugSettings.LogTest($"SimpleRenderingTest: Created {elementType} entity at {position}");
        }
        
        protected override void OnDestroy()
        {
            DOTS.Terrain.Core.DebugSettings.LogTest("SimpleRenderingTest: Destroyed");
        }
    }
}
#endif 