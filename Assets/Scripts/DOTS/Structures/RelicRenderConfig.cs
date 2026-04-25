using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace DOTS.Structures
{
    /// <summary>
    /// Per-template visual data for a relic type. Each entry maps a
    /// <see cref="TemplateId"/> to a mesh/material/scale/impostor set.
    /// </summary>
    [System.Serializable]
    public class RelicTemplateEntry
    {
        public string TemplateId;
        public Mesh Mesh;
        public Material Material;
        public float UniformScale = 15f;
        public float YOffset;
        public Mesh ImpostorMesh;
        public Material ImpostorMaterial;
        public float ImpostorScale = 30f;
    }

    /// <summary>
    /// Singleton managed component holding the template registry and shared
    /// LOD settings for relic rendering. Replaces the former single-mesh config
    /// to support multiple relic types (giant head, ruined tower, bone pile, etc.)
    /// within the same placement family.
    /// </summary>
    public class RelicRenderConfig : IComponentData
    {
        /// <summary>
        /// All registered relic templates. Keyed by <see cref="RelicTemplateEntry.TemplateId"/>.
        /// Populated by <see cref="RelicVisualBootstrap"/> at startup.
        /// </summary>
        public List<RelicTemplateEntry> Templates = new();

        // ── Shared LOD settings (same for all templates) ──

        /// <summary>
        /// Center of the LOD swap band, in world units from camera to relic.
        /// Auto-derived from the camera far clip and mesh extents at bootstrap
        /// when the inspector value is 0.
        /// </summary>
        public float LodSwapDistance;

        /// <summary>± buffer around <see cref="LodSwapDistance"/> to prevent per-frame flicker.</summary>
        public float LodHysteresis;

        /// <summary>
        /// Finds the template entry matching <paramref name="templateId"/>.
        /// Falls back to the first entry if no match is found (or null if empty).
        /// </summary>
        public RelicTemplateEntry GetTemplate(FixedString64Bytes templateId)
        {
            var idStr = templateId.ToString();
            for (int i = 0; i < Templates.Count; i++)
            {
                if (Templates[i].TemplateId == idStr)
                    return Templates[i];
            }
            return Templates.Count > 0 ? Templates[0] : null;
        }
    }
}
