using DOTS.Terrain.Core;
using Unity.Collections;
using Unity.Entities;

namespace DOTS.Terrain.SurfaceScatter
{
    /// <summary>
    /// Enforces singleton semantics for managed render-config components used by scatter families.
    /// Future families (trees, rocks, bushes) should register config through this utility.
    /// </summary>
    public static class SurfaceScatterRenderConfigBootstrapUtility
    {
        public static void SetOrCreateManagedSingleton<TConfig>(
            EntityManager entityManager,
            TConfig config,
            string configName)
            where TConfig : class, IComponentData
        {
            using var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<TConfig>());
            using var entities = query.ToEntityArray(Allocator.Temp);

            if (entities.Length == 0)
            {
                var entity = entityManager.CreateEntity();
                entityManager.AddComponentObject(entity, config);
                return;
            }

            var primary = entities[0];
            if (entityManager.HasComponent<TConfig>(primary))
            {
                entityManager.RemoveComponent<TConfig>(primary);
            }

            entityManager.AddComponentObject(primary, config);

            if (entities.Length <= 1)
            {
                return;
            }

            for (int i = 1; i < entities.Length; i++)
            {
                var duplicate = entities[i];
                if (entityManager.Exists(duplicate))
                {
                    entityManager.DestroyEntity(duplicate);
                }
            }

            DebugSettings.Log(
                $"Removed duplicate {configName} singleton entities during bootstrap; keeping the first instance.");
        }
    }
}
