using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.WFC;
using DOTS.Terrain.Core;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Debug script to see what DOTS entities exist in the scene
    /// </summary>
    public class EntityDebugger : MonoBehaviour
    {
        [Header("Debug Controls")]
        [Space(10)]
        public bool showDebugInfo = true;
        
        [ContextMenu("Debug DOTS Entities")]
        public void DebugDOTSEntities()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                DebugSettings.LogError("DOTS World not found! Make sure you're in Play mode.");
                return;
            }
            
            var entityManager = world.EntityManager;
                      if (entityManager == null)
            {
                DebugSettings.LogError("DOTS EntityManager not found! Make sure you're in Play mode.");
                return;
            }
            
            
            // Check for DungeonGenerationRequest entities
            var requestQuery = entityManager.CreateEntityQuery(typeof(DungeonGenerationRequest));
            var requestEntities = requestQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            DebugSettings.Log($"=== DOTS ENTITY DEBUG ===");
            DebugSettings.Log($"DungeonGenerationRequest entities: {requestEntities.Length}");
            
            for (int i = 0; i < requestEntities.Length; i++)
            {
                var request = entityManager.GetComponentData<DungeonGenerationRequest>(requestEntities[i]);
                DebugSettings.Log($"  Entity {i}: isActive={request.isActive}, size={request.size}, position={request.position}");
            }
            
            // Check for WFCComponent entities
            var wfcQuery = entityManager.CreateEntityQuery(typeof(WFCComponent));
            var wfcEntities = wfcQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            DebugSettings.Log($"WFCComponent entities: {wfcEntities.Length}");
            
            // Check for WFCCell entities
            var cellQuery = entityManager.CreateEntityQuery(typeof(WFCCell));
            var cellEntities = cellQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            DebugSettings.Log($"WFCCell entities: {cellEntities.Length}");
            
            // Check for DungeonPrefabRegistry
            if (entityManager.HasComponent<DOTS.Terrain.WFC.Authoring.DungeonPrefabRegistry>(Entity.Null))
            {
                DebugSettings.Log("DungeonPrefabRegistry singleton exists");
            }
            else
            {
                DebugSettings.Log("DungeonPrefabRegistry singleton does NOT exist");
            }
            
            requestEntities.Dispose();
            wfcEntities.Dispose();
            cellEntities.Dispose();
            requestQuery.Dispose();
            wfcQuery.Dispose();
            cellQuery.Dispose();
            
            DebugSettings.Log("=== END DOTS ENTITY DEBUG ===");
            
            // Additional info about what's causing the infinite errors
            if (requestEntities.Length == 0 && wfcEntities.Length == 0 && cellEntities.Length == 0)
            {
                DebugSettings.Log("✅ GOOD NEWS: No DOTS entities found - this means no WFC generation is happening");
                DebugSettings.Log("❌ BAD NEWS: DungeonVisualizationSystem is running infinitely because it can't find DungeonPrefabRegistryAuthoring");
                DebugSettings.Log("💡 SOLUTION: The infinite errors are from DungeonVisualizationSystem, not your model alignment test");
            }
        }
        
        [ContextMenu("Clear All DOTS Entities")]
        public void ClearAllDOTSEntities()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                DebugSettings.LogError("DOTS World not found! Make sure you're in Play mode.");
                return;
            }
            
            var entityManager = world.EntityManager;
            
            // Clear DungeonGenerationRequest entities
            var requestQuery = entityManager.CreateEntityQuery(typeof(DungeonGenerationRequest));
            entityManager.DestroyEntity(requestQuery);
            requestQuery.Dispose();
            
            // Clear WFCComponent entities
            var wfcQuery = entityManager.CreateEntityQuery(typeof(WFCComponent));
            entityManager.DestroyEntity(wfcQuery);
            wfcQuery.Dispose();
            
            // Clear WFCCell entities
            var cellQuery = entityManager.CreateEntityQuery(typeof(WFCCell));
            entityManager.DestroyEntity(cellQuery);
            cellQuery.Dispose();
            
            DebugSettings.Log("Cleared all DOTS entities");
        }
        
        [ContextMenu("Disable DungeonVisualizationSystem")]
        public void DisableDungeonVisualizationSystem()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                DebugSettings.LogError("DOTS World not found! Make sure you're in Play mode.");
                return;
            }
            
            // Find and disable the DungeonVisualizationSystem
            var systems = world.Systems;
            foreach (var system in systems)
            {
                if (system.GetType().Name == "DungeonVisualizationSystem")
                {
                    system.Enabled = false;
                    DebugSettings.Log("✅ Disabled DungeonVisualizationSystem - this should stop the infinite error spam");
                    return;
                }
            }
            
            DebugSettings.LogWarning("DungeonVisualizationSystem not found in the world");
        }
        
        [ContextMenu("Re-enable DungeonVisualizationSystem")]
        public void ReEnableDungeonVisualizationSystem()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                DebugSettings.LogError("DOTS World not found! Make sure you're in Play mode.");
                return;
            }
            
            // Find and enable the DungeonVisualizationSystem
            var systems = world.Systems;
            foreach (var system in systems)
            {
                if (system.GetType().Name == "DungeonVisualizationSystem")
                {
                    system.Enabled = true;
                    DebugSettings.Log("✅ Re-enabled DungeonVisualizationSystem");
                    return;
                }
            }
            
            DebugSettings.LogWarning("DungeonVisualizationSystem not found in the world");
        }
    }
}