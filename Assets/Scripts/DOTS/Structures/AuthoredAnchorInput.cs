using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace DOTS.Structures
{
    /// <summary>
    /// One developer-authored structure anchor request, consumed by
    /// <see cref="StructureAnchorPlanningSystem"/> as a candidate source that
    /// precedes procedural cell evaluation (STRUCTURE_PLACEMENT_SPEC.md §9.5).
    /// Lives on a singleton buffer created by <see cref="AuthoredAnchorBootstrap"/>.
    ///
    /// Authored anchors bypass hard terrain-fit constraints (guaranteed placement
    /// is the feature) and derive their StableAnchorId from <see cref="AuthorId"/>,
    /// so identity is independent of both world seed and position.
    /// </summary>
    public struct AuthoredAnchorInput : IBufferElementData
    {
        /// <summary>
        /// Stable human-readable identity (e.g. "vista_hero_hand"). Hashed into
        /// StableAnchorId — must be unique across all authored entries.
        /// </summary>
        public FixedString64Bytes AuthorId;

        public StructureFamilyId Family;

        /// <summary>
        /// Explicit template key — must exist in the family's realization registry
        /// (e.g. RelicRenderConfig). Authored anchors skip the planner's
        /// deterministic template assignment.
        /// </summary>
        public FixedString64Bytes TemplateId;

        /// <summary>World-space XZ position. Y comes from terrain sampling unless SnapToTerrain is false.</summary>
        public float2 PositionXZ;

        /// <summary>World-space Y used only when <see cref="SnapToTerrain"/> is false.</summary>
        public float ExplicitY;

        /// <summary>When true (the default authoring path), Y is sampled from the terrain SDF at PositionXZ.</summary>
        public bool SnapToTerrain;

        /// <summary>Yaw around world up, degrees.</summary>
        public float YawDegrees;

        /// <summary>Footprint reservation radius. 0 = inherit the family ruleset's footprint.</summary>
        public float FootprintRadius;
    }
}
