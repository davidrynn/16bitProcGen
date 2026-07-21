namespace DOTS.Structures
{
    #if UNITY_EDITOR
    using System.Collections.Generic;
    using DOTS.Structures;
    using Unity.Collections;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Scene View debug overlay for structure anchors. Draws colored spheres at
    /// accepted anchor positions and wireframe boxes for footprint reservations.
    /// Attach to any GameObject in the scene to enable.
    /// </summary>
    /// <remarks>
    /// OnDrawGizmos runs on every Scene View repaint — many times a second, in edit
    /// mode, forever. Anything allocated here is allocated continuously, so the query
    /// and the label strings are both cached rather than rebuilt per repaint.
    /// </remarks>
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

        // Cached across repaints. The world is tracked alongside the query because entering
        // or leaving play mode replaces the default world, and a query outlives its world as
        // a dangling handle.
        private EntityQuery _anchorQuery;
        private World _queryWorld;
        private bool _hasQuery;

        // Anchor ids are stable, so a label is built once per anchor instead of once per
        // repaint. Interpolating the enum directly would also box it every time.
        private readonly Dictionary<uint, string> _labelCache = new Dictionary<uint, string>();

        private void OnDisable()
        {
            ReleaseQuery();
            _labelCache.Clear();
        }

        private void ReleaseQuery()
        {
            // Disposing against a torn-down world throws; the world owns the query's memory
            // and has already freed it in that case.
            if (_hasQuery && _queryWorld != null && _queryWorld.IsCreated)
                _anchorQuery.Dispose();
            _hasQuery = false;
            _queryWorld = null;
        }

        private bool TryGetQuery(World world, out EntityQuery query)
        {
            if (!_hasQuery || !ReferenceEquals(world, _queryWorld))
            {
                ReleaseQuery();
                _anchorQuery = world.EntityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<StructurePlacementSingleton>(),
                    ComponentType.ReadOnly<StructureAnchorRecord>());
                _queryWorld = world;
                _hasQuery = true;
            }

            query = _anchorQuery;
            return true;
        }

        private string GetLabel(in StructureAnchorRecord anchor)
        {
            if (!_labelCache.TryGetValue(anchor.StableAnchorId, out var label))
            {
                label = $"{anchor.Family.ToString()} #{anchor.StableAnchorId:X8}";
                _labelCache[anchor.StableAnchorId] = label;
            }

            return label;
        }

        private void OnDrawGizmos()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated)
            {
                ReleaseQuery();
                return;
            }

            var em = world.EntityManager;
            if (!TryGetQuery(world, out var query) || query.IsEmpty)
                return;

            using var entities = query.ToEntityArray(Allocator.Temp);
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

                    if (ShowLabels)
                    {
                        UnityEditor.Handles.Label(
                            pos + Vector3.up * (radius * 4f + 2f),
                            GetLabel(anchor));
                    }
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
        }
    }
    #endif
}
