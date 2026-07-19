using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Core;

namespace DOTS.Terrain
{
    /// <summary>
    /// Scene-side authoring for the <c>H</c> flatten mask (WORLD_STRUCTURE_SPEC.md §4.1, ticket H3) —
    /// the <see cref="AuthoredAnchorBootstrap"/>-style pattern (serialized inspector entries, no
    /// ScriptableObject editing, standing owner preference). On <c>Start</c> it pushes its corridors
    /// to <see cref="WorldStructureBroadcast.PushMask"/>, overriding the default vista corridor the
    /// broadcast already seeds.
    ///
    /// <para><b>Optional for MVP.</b> The broadcast seeds <see cref="WorldStructureMask.DefaultVistaCorridor"/>
    /// by default, so the sightline is protected even without this component in the scene. Add it only
    /// to author extra corridors or re-tune the vista one in the inspector.</para>
    /// </summary>
    public class WorldStructureMaskBootstrap : MonoBehaviour
    {
        [System.Serializable]
        public class CorridorEntry
        {
            [Tooltip("Segment start, world XZ. Spawn is the origin; +Z is the spawn view direction.")]
            public Vector2 fromXZ = Vector2.zero;

            [Tooltip("Segment end, world XZ. Default reaches past the hero hand at (0, 900).")]
            public Vector2 toXZ = new Vector2(0f, 1000f);

            [Tooltip("Flat-core half-width: H is fully flattened within this distance of the segment.")]
            [Min(0f)]
            public float radius = 110f;

            [Tooltip("Ramp width from the flat core back to full macro relief.")]
            [Min(0f)]
            public float feather = 220f;
        }

        [Tooltip("Flatten corridors. Defaults to the MVP vista corridor (spawn → past the hero). At " +
                 "most WorldStructureMask.MaxRegions are uploaded.")]
        [SerializeField]
        private List<CorridorEntry> corridors = new() { new CorridorEntry() };

        private void Start()
        {
            int n = Mathf.Min(corridors.Count, WorldStructureMask.MaxRegions);
            var regions = new WorldStructureMaskRegion[n];
            for (int i = 0; i < n; i++)
            {
                var e = corridors[i];
                regions[i] = new WorldStructureMaskRegion
                {
                    A = new float2(e.fromXZ.x, e.fromXZ.y),
                    B = new float2(e.toXZ.x, e.toXZ.y),
                    Radius = e.radius,
                    Feather = e.feather,
                };
            }

            WorldStructureBroadcast.PushMask(regions);
            DebugSettings.LogTerrain(
                $"WorldStructureMaskBootstrap: pushed {regions.Length} flatten corridor(s).", forceLog: true);
        }
    }
}
