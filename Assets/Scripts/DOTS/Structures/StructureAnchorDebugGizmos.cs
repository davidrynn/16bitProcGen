namespace DOTS.Structures
{
    #if UNITY_EDITOR
    using DOTS.Structures;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;
    
    /// <summary>
    /// Scene View debug overlay for structure anchors. Draws colored spheres at
    /// accepted anchor positions and wireframe boxes for footprint reservations.
    /// Attach to any GameObject in the scene to enable.
    /// </summary>
    [ExecuteAlways]
    public class StructureAnchorDebugGizmos : MonoBehaviour
    {
        [Header("Visualization")]
        public float RelicSphereRadius = 5f;
        public float DungeonSphereRadius = 8f;
        public bool ShowFootprints = true;
        public bool ShowLabels = true;
    
        private static readonly Color RelicColor = new Color(1f, 0.8f, 0f, 0.9f);       // gold
        private static readonly Color DungeonColor = new Color(0.6f, 0.2f, 0.8f, 0.9f);  // purple
        private static readonly Color FootprintColor = new Color(1f, 0.3f, 0.3f, 0.3f);  // transparent red
    
        private void OnDrawGizmos()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
    
            var em = world.EntityManager;
            var query = em.CreateEntityQuery(
                ComponentType.ReadOnly<StructurePlacementSingleton>(),
                ComponentType.ReadOnly<StructureAnchorRecord>());
    
            if (query.IsEmpty)
            {
                query.Dispose();
                return;
            }
    
            using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                var anchors = em.GetBuffer<StructureAnchorRecord>(entity, true);
                foreach (var anchor in anchors)
                {
                    float radius;
                    switch (anchor.Family)
                    {
                        case StructureFamilyId.Relic:
                            Gizmos.color = RelicColor;
                            radius = RelicSphereRadius;
                            break;
                        case StructureFamilyId.Dungeon:
                            Gizmos.color = DungeonColor;
                            radius = DungeonSphereRadius;
                            break;
                        default:
                            Gizmos.color = Color.white;
                            radius = 4f;
                            break;
                    }
    
                    Vector3 pos = anchor.WorldPosition;
                    Gizmos.DrawSphere(pos, radius);
    
                    // Vertical line from ground to above for visibility
                    Gizmos.DrawLine(pos, pos + Vector3.up * radius * 4f);
    
    #if UNITY_EDITOR
                    if (ShowLabels)
                    {
                        UnityEditor.Handles.Label(
                            pos + Vector3.up * (radius * 4f + 2f),
                            $"{anchor.Family} #{anchor.StableAnchorId:X8}");
                    }
    #endif
                }
    
                // Draw footprint reservations
                if (ShowFootprints && em.HasBuffer<StructureFootprintReservation>(entity))
                {
                    var footprints = em.GetBuffer<StructureFootprintReservation>(entity, true);
                    Gizmos.color = FootprintColor;
                    foreach (var fp in footprints)
                    {
                        Vector3 center = fp.Center;
                        Vector3 size = (Vector3)(float3)fp.Extents * 2f;
                        Gizmos.DrawWireCube(center, size);
                    }
                }
            }
            query.Dispose();
        }
    }
    #endif
}
