using Unity.Entities;
using DOTS.Terrain.Core;

namespace DOTS.Terrain
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct TerrainColliderSettingsBootstrapSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton<TerrainColliderSettings>(out _))
            {
                var entity = state.EntityManager.CreateEntity(typeof(TerrainColliderSettings));
                state.EntityManager.SetComponentData(entity, new TerrainColliderSettings
                {
                    Enabled = true,
                    MaxCollidersPerFrame = 4,
                    EnableDetailedStaticMeshCollision = true
                });
                DebugSettings.LogTerrain("Created default TerrainColliderSettings singleton (Enabled=true).");
            }
            else if (SystemAPI.TryGetSingleton<TerrainColliderSettings>(out var settings))
            {
                var updated = settings;
                var changed = false;

                if (updated.MaxCollidersPerFrame <= 0)
                {
                    updated.MaxCollidersPerFrame = 4;
                    changed = true;
                }

                if (changed)
                {
                    SystemAPI.SetSingleton(updated);
                    DebugSettings.LogTerrain("Updated TerrainColliderSettings defaults (MaxCollidersPerFrame=4, EnableDetailedStaticMeshCollision=true).");
                }
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
