using Unity.Entities;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// Simplified DOTS system for terrain validation
/// </summary>
public partial class TerrainSystem : SystemBase
{
    protected override void OnCreate() 
    {
        Debug.Log("[DOTS] TerrainSystem: Initializing...");
        RequireForUpdate<DOTS.Terrain.TerrainData>();
    }
    
    protected override void OnUpdate() 
    {
        // Simple validation only - no complex processing needed
        foreach (var (terrain, entity) in SystemAPI.Query<RefRO<DOTS.Terrain.TerrainData>>().WithEntityAccess())
        {
            if (terrain.ValueRO.resolution <= 0)
            {
                Debug.LogWarning($"[DOTS] Invalid resolution for entity {entity}");
            }
        }
    }
} 