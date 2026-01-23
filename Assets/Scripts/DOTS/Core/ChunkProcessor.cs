using Unity.Entities;
using UnityEngine;
using DOTS.Terrain;
using TerrainData = DOTS.Terrain.TerrainData;

/// <summary>
/// DOTS System for managing terrain chunks
/// </summary>
[DisableAutoCreation]
public partial struct ChunkProcessor : ISystem
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
                UnityEngine.Debug.LogWarning($"Invalid resolution for entity {entity}");
            }
        }
    }

    public void OnDestroy(ref SystemState state)
    {
    }
} 
