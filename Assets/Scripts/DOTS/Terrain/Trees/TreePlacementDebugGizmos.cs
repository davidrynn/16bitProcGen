#if UNITY_EDITOR
using DOTS.Terrain.Trees;
using Unity.Entities;
using UnityEngine;

/// Throw-away Phase B diagnostic. Draws spheres at accepted tree positions.
/// DELETE after Phase B is accepted.
[ExecuteAlways]
public class TreePlacementDebugGizmos : MonoBehaviour
{
    public float SphereRadius = 0.4f;

    private void OnDrawGizmos()
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null || !world.IsCreated) return;

        var em = world.EntityManager;
        var query = em.CreateEntityQuery(
            ComponentType.ReadOnly<DOTS.Terrain.TerrainChunk>(),
            ComponentType.ReadOnly<TreePlacementRecord>());

        Gizmos.color = Color.green;
        using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entities)
        {
            var buffer = em.GetBuffer<TreePlacementRecord>(entity, true);
            foreach (var record in buffer)
                Gizmos.DrawSphere(record.WorldPosition, SphereRadius);
        }
        query.Dispose();
    }
}
#endif
