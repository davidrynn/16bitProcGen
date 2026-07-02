using Unity.Entities;
using DOTS.Terrain;
using DOTS.Terrain.Core;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// [LEGACY] Validates legacy heightmap <see cref="TerrainData"/> entities (warns on
/// non-positive resolution). Survivor of the ChunkProcessor/TerrainSystem merge
/// (cleanup round 1, plan row C5) — the two were duplicate validators.
/// The active SDF pipeline does not use this component.
/// </summary>
[DisableAutoCreation]
public partial struct TerrainDataValidationSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<TerrainData>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (terrain, entity) in SystemAPI.Query<RefRO<TerrainData>>().WithEntityAccess())
        {
            if (terrain.ValueRO.resolution <= 0)
            {
                DebugSettings.LogWarning($"[TerrainDataValidationSystem] Invalid resolution for entity {entity}");
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
    }
}
