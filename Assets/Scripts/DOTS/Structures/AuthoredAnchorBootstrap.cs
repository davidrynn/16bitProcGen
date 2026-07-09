using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using DOTS.Core;

namespace DOTS.Structures
{
    /// <summary>
    /// Scene-side bootstrap for authored structure anchors
    /// (STRUCTURE_PLACEMENT_SPEC.md §9.5). Converts inspector-authored entries
    /// into the singleton <see cref="AuthoredAnchorInput"/> buffer that
    /// <see cref="StructureAnchorPlanningSystem"/> merges ahead of procedural
    /// candidates — same inspector-authoring pattern as RelicVisualBootstrap.
    ///
    /// This is product data (guaranteed vista hero, quest layouts), not a dev
    /// pin; only the separate debug-layout list is gated by
    /// <see cref="DebugSettings.EnableAuthoredDebugAnchors"/>.
    /// </summary>
    public class AuthoredAnchorBootstrap : MonoBehaviour
    {
        [System.Serializable]
        public class AuthoredAnchorEntry
        {
            [Tooltip("Stable unique identity, e.g. \"vista_hero_hand\". Hashed into StableAnchorId — " +
                     "renaming it re-rolls the anchor's identity; moving the position does not.")]
            public string authorId;

            public StructureFamilyId family = StructureFamilyId.Relic;

            [Tooltip("Explicit template key — must exist in the family's realization registry (e.g. RelicVisualBootstrap templates).")]
            public string templateId;

            [Tooltip("World-space XZ position. Spawn is at the origin; +Z is the spawn view direction.")]
            public Vector2 positionXZ;

            [Tooltip("Sample Y from the terrain SDF at positionXZ (recommended — survives terrain retuning).")]
            public bool snapToTerrain = true;

            [Tooltip("World-space Y, used only when snapToTerrain is off.")]
            public float explicitY;

            [Tooltip("Yaw around world up, degrees.")]
            public float yawDegrees;

            [Tooltip("Footprint reservation radius. 0 = inherit the family ruleset's footprint.")]
            [Min(0f)]
            public float footprintRadius;
        }

        [Tooltip("Always-active authored anchors — product world layout (vista hero, quest placements).")]
        [SerializeField]
        private List<AuthoredAnchorEntry> worldAnchors = new()
        {
            // The guaranteed vista hero hand (ticket V12): dead ahead of spawn
            // (+Z), inside R6's 2000u landmark band. Distance/yaw are eyeball
            // knobs — the scene's serialized values are the source of truth
            // once this component exists in the scene.
            new AuthoredAnchorEntry
            {
                authorId = "vista_hero_hand",
                family = StructureFamilyId.Relic,
                templateId = "relic_hand_hero",
                positionXZ = new Vector2(0f, 900f),
                snapToTerrain = true,
                yawDegrees = 180f,
            },
        };

        [Tooltip("Dev-only layout, active only when DebugSettings.EnableAuthoredDebugAnchors is true.")]
        [SerializeField]
        private List<AuthoredAnchorEntry> debugAnchors = new();

        private void Start()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var em = world.EntityManager;

            // Guard against a second bootstrap in the scene: two buffer entities
            // would make the planner's singleton query throw every frame. Reuse
            // the existing buffer instead (the planner id-dedups duplicate entries).
            Entity entity;
            using (var existing = em.CreateEntityQuery(typeof(AuthoredAnchorInput))
                       .ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                if (existing.Length > 0)
                {
                    DebugSettings.LogWarning(
                        "AuthoredAnchorBootstrap: authored anchor buffer already exists — " +
                        "is this bootstrap duplicated in the scene? Appending to the existing buffer.");
                    entity = existing[0];
                }
                else
                {
                    entity = em.CreateEntity();
                    em.SetName(entity, "AuthoredAnchorInputSingleton");
                    em.AddBuffer<AuthoredAnchorInput>(entity);
                }
            }
            var buffer = em.GetBuffer<AuthoredAnchorInput>(entity);

            var seenIds = new HashSet<string>();
            AppendEntries(worldAnchors, buffer, seenIds);
            if (DebugSettings.EnableAuthoredDebugAnchors)
                AppendEntries(debugAnchors, buffer, seenIds);

            DebugSettings.LogTerrain(
                $"AuthoredAnchorBootstrap: {buffer.Length} authored anchor(s) registered " +
                $"(debug layout {(DebugSettings.EnableAuthoredDebugAnchors ? "on" : "off")}).",
                forceLog: true);
        }

        private static void AppendEntries(
            List<AuthoredAnchorEntry> entries,
            DynamicBuffer<AuthoredAnchorInput> buffer,
            HashSet<string> seenIds)
        {
            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.authorId))
                {
                    DebugSettings.LogWarning("AuthoredAnchorBootstrap: entry with empty authorId — skipping.");
                    continue;
                }
                if (!seenIds.Add(e.authorId))
                {
                    DebugSettings.LogWarning(
                        $"AuthoredAnchorBootstrap: duplicate authorId '{e.authorId}' — skipping duplicate.");
                    continue;
                }
                // FixedString64Bytes holds at most 61 UTF-8 bytes; oversize ids
                // would throw during implicit conversion.
                if (System.Text.Encoding.UTF8.GetByteCount(e.authorId) > FixedString64Bytes.UTF8MaxLengthInBytes)
                {
                    DebugSettings.LogWarning(
                        $"AuthoredAnchorBootstrap: authorId '{e.authorId}' too long for FixedString64Bytes — skipping.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(e.templateId))
                {
                    DebugSettings.LogWarning(
                        $"AuthoredAnchorBootstrap: '{e.authorId}' has empty templateId — realization will skip it.");
                }
                else if (System.Text.Encoding.UTF8.GetByteCount(e.templateId) > FixedString64Bytes.UTF8MaxLengthInBytes)
                {
                    DebugSettings.LogWarning(
                        $"AuthoredAnchorBootstrap: '{e.authorId}' templateId too long for FixedString64Bytes — skipping.");
                    continue;
                }

                buffer.Add(new AuthoredAnchorInput
                {
                    AuthorId = new FixedString64Bytes(e.authorId),
                    Family = e.family,
                    TemplateId = new FixedString64Bytes(e.templateId ?? ""),
                    PositionXZ = new float2(e.positionXZ.x, e.positionXZ.y),
                    ExplicitY = e.explicitY,
                    SnapToTerrain = e.snapToTerrain,
                    YawDegrees = e.yawDegrees,
                    FootprintRadius = e.footprintRadius,
                });
            }
        }
    }
}
