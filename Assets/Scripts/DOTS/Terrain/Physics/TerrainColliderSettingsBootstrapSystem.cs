using Unity.Entities;

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
                state.EntityManager.SetComponentData(entity, new TerrainColliderSettings { Enabled = true });
                UnityEngine.Debug.Log("[DOTS Terrain] Created default TerrainColliderSettings singleton (Enabled=true).");
            }

            state.Enabled = false;
        }

        public void OnUpdate(ref SystemState state)
        {
        }
    }
}
