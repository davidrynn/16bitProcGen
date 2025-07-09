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
        Entities
            .WithAll<DOTS.Terrain.TerrainData>()
            .ForEach((Entity entity, in DOTS.Terrain.TerrainData terrain) =>
            {
                if (terrain.resolution <= 0)
                {
                    Debug.LogWarning($"[DOTS] Invalid resolution for entity {entity}");
                }
            }).WithoutBurst().Run();
    }
} 