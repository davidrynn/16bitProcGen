using UnityEngine;

namespace DOTS.Structures
{
    /// <summary>
    /// Per-family placement rules for structure anchor planning.
    /// One asset per family (e.g. "RelicRuleset", "DungeonRuleset").
    /// Systems read these at planning time to evaluate candidates.
    /// </summary>
    [CreateAssetMenu(fileName = "StructureFamilyRuleset", menuName = "DOTS/Structures/Family Ruleset")]
    public class StructureFamilyRuleset : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Which structure family these rules apply to")]
        public StructureFamilyId Family;

        [Header("Spacing")]
        [Tooltip("Hard minimum distance between two anchors of this family (world units)")]
        [Min(10f)]
        public float MinSpacing = 200f;

        [Tooltip("Preferred maximum spacing — planner tries to keep anchors under this distance")]
        [Min(10f)]
        public float MaxSpacing = 600f;

        [Tooltip("How many candidate positions to evaluate per planning cell")]
        [Range(1, 8)]
        public int CandidatesPerCell = 2;

        [Header("Terrain Fit")]
        [Tooltip("Maximum terrain slope (dot product with up) below which placement is rejected. 1.0 = flat only, 0.5 = up to 60 degrees")]
        [Range(0f, 1f)]
        public float MinSlopeNormalY = 0.8f;

        [Tooltip("Minimum terrain elevation for placement (world units)")]
        public float MinElevation = 5f;

        [Tooltip("Maximum terrain elevation for placement (world units)")]
        public float MaxElevation = 200f;

        [Header("Footprint")]
        [Tooltip("AABB half-extents of the structure footprint reservation (world units)")]
        public Vector3 FootprintExtents = new Vector3(15f, 10f, 15f);

        [Header("Realization")]
        [Tooltip("Default template ID used when AvailableTemplateIds is empty")]
        public string DefaultTemplateId = "";

        [Tooltip("Template IDs available for this family. Each anchor gets one " +
                 "deterministically from its StableAnchorId. Leave empty to use DefaultTemplateId for all.")]
        public string[] AvailableTemplateIds = new string[0];

        [Tooltip("Scale multiplier applied to realized prefabs")]
        [Min(0.1f)]
        public float RealizationScale = 1f;
    }
}
