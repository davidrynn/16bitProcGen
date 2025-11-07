using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using DOTS.Player.Components;

namespace DOTS.Player.Bootstrap
{
    /// <summary>
    /// Utility to debug and clean up duplicate player entities.
    /// Add this MonoBehaviour to a GameObject in scene to help diagnose entity conflicts.
    /// </summary>
    public class PlayerEntityCleanup : MonoBehaviour
    {
        [Header("Debug")]
        [Tooltip("Log all player entities found in the world")]
        public bool logAllPlayerEntities = true;
        
        [Tooltip("Delete duplicate player entities (keeps the first one)")]
        public bool deleteDuplicates = false;

        private void Start()
        {
            if (logAllPlayerEntities || deleteDuplicates)
            {
                CheckPlayerEntities();
            }
        }

        [ContextMenu("Check Player Entities")]
        public void CheckPlayerEntities()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[PlayerEntityCleanup] No DOTS world found!");
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(PlayerTag));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            Debug.Log($"[PlayerEntityCleanup] Found {entities.Length} player entities:");

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var entityName = entityManager.GetName(entity);
                
                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                    var hasPhysics = entityManager.HasComponent<Unity.Physics.PhysicsVelocity>(entity);
                    var hasInput = entityManager.HasComponent<PlayerInputComponent>(entity);
                    var hasConfig = entityManager.HasComponent<PlayerMovementConfig>(entity);
                    
                    Debug.Log($"  Entity {entity.Index} ({entityName}): " +
                             $"Pos={transform.Position}, " +
                             $"HasPhysics={hasPhysics}, " +
                             $"HasInput={hasInput}, " +
                             $"HasConfig={hasConfig}");
                }
                else
                {
                    Debug.Log($"  Entity {entity.Index} ({entityName}): No LocalTransform");
                }
            }

            if (entities.Length > 1)
            {
                Debug.LogWarning($"[PlayerEntityCleanup] WARNING: Found {entities.Length} player entities! This will cause conflicts.");
                
                if (deleteDuplicates)
                {
                    Debug.Log("[PlayerEntityCleanup] Deleting duplicate entities (keeping first one)...");
                    
                    // Keep the first entity, delete the rest
                    for (int i = 1; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        Debug.Log($"[PlayerEntityCleanup] Deleting duplicate entity {entity.Index}");
                        entityManager.DestroyEntity(entity);
                    }
                    
                    Debug.Log($"[PlayerEntityCleanup] Cleanup complete. {entities.Length - 1} duplicate(s) deleted.");
                }
            }
            else if (entities.Length == 1)
            {
                Debug.Log("[PlayerEntityCleanup] ✓ Exactly one player entity found - this is correct!");
            }
            else
            {
                Debug.LogWarning("[PlayerEntityCleanup] No player entities found! PlayerEntityBootstrap should create one.");
            }

            entities.Dispose();
        }

        [ContextMenu("Check Camera Entities")]
        public void CheckCameraEntities()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogError("[PlayerEntityCleanup] No DOTS world found!");
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(MainCameraTag));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

            Debug.Log($"[PlayerEntityCleanup] Found {entities.Length} camera entities:");

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var entityName = entityManager.GetName(entity);
                
                if (entityManager.HasComponent<LocalTransform>(entity))
                {
                    var transform = entityManager.GetComponentData<LocalTransform>(entity);
                    Debug.Log($"  Camera Entity {entity.Index} ({entityName}): Pos={transform.Position}");
                }
            }

            if (entities.Length > 1)
            {
                Debug.LogWarning($"[PlayerEntityCleanup] Found {entities.Length} camera entities. This may cause conflicts.");
            }

            entities.Dispose();
        }
    }
}

