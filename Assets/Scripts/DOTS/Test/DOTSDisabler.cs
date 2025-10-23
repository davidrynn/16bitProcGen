using UnityEngine;
using Unity.Entities;
using DOTS.Terrain.WFC;

namespace DOTS.Terrain.Test
{
    /// <summary>
    /// Utility script to disable DOTS dungeon generation systems
    /// Use this to prevent interference with model alignment tests
    /// </summary>
    public class DOTSDisabler : MonoBehaviour
    {
        [Header("DOTS Control")]
        public bool disableDOTSOnStart = true;
        public bool reEnableOnDestroy = true;
        
        private bool dotsWasActive = false;
        private Entity requestEntity = Entity.Null;
        
        void Start()
        {
            if (disableDOTSOnStart)
            {
                DisableDOTSSystem();
            }
        }
        
        void OnDestroy()
        {
            if (reEnableOnDestroy && dotsWasActive)
            {
                ReEnableDOTSSystem();
            }
        }
        
        [ContextMenu("Disable DOTS System")]
        public void DisableDOTSSystem()
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            var query = entityManager.CreateEntityQuery(typeof(DungeonGenerationRequest));
            var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            
            foreach (var entity in entities)
            {
                if (entityManager.HasComponent<DungeonGenerationRequest>(entity))
                {
                    var request = entityManager.GetComponentData<DungeonGenerationRequest>(entity);
                    if (request.isActive)
                    {
                        request.isActive = false;
                        entityManager.SetComponentData(entity, request);
                        requestEntity = entity;
                        dotsWasActive = true;
                        Debug.Log("DOTSDisabler: Disabled DungeonGenerationRequest to prevent DOTS interference");
                    }
                }
            }
            
            entities.Dispose();
            query.Dispose();
        }
        
        [ContextMenu("Re-enable DOTS System")]
        public void ReEnableDOTSSystem()
        {
            if (requestEntity != Entity.Null)
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                if (entityManager.HasComponent<DungeonGenerationRequest>(requestEntity))
                {
                    var request = entityManager.GetComponentData<DungeonGenerationRequest>(requestEntity);
                    request.isActive = true;
                    entityManager.SetComponentData(requestEntity, request);
                    Debug.Log("DOTSDisabler: Re-enabled DungeonGenerationRequest");
                }
            }
        }
    }
}
