using Unity.Entities;
using UnityEngine;
using DOTS.Terrain;

/// <summary>
/// [LEGACY] Simplified DOTS system for terrain validation of the legacy TerrainData component.
/// 
/// ⚠️ LEGACY SYSTEM: This system operates on DOTS.Terrain.TerrainData component.
/// The current active terrain system uses SDF (Signed Distance Fields) with systems in DOTS.Terrain namespace:
/// - TerrainChunkDensitySamplingSystem, TerrainChunkMeshBuildSystem, etc.
/// 
/// This system is maintained for backward compatibility with existing tests and legacy code.
/// </summary>
[DisableAutoCreation]
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